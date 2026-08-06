---
title: ADR-015 Journal SQLite Isolado
aliases: [Journal Operacional Separado, Banco de Observabilidade]
tags: [adr, observabilidade, sqlite, privacidade, resiliencia]
type: adr
created: 2026-08-05
updated: 2026-08-06
status: accepted
summary: Eventos operacionais ficam em um SQLite separado para que logs, limpeza ou corrupcao nunca afetem reunioes e jobs.
related: ["[[SPEK-031 Observabilidade Operacional Real]]", "[[SPEK-048 Inicializacao Concorrente do Journal SQLite]]", "[[ADR-013 Engine SQLite Embarcada]]"]
---

# ADR-015 | Journal SQLite isolado

## Contexto

Tray e Worker precisam registrar eventos locais no mesmo journal. Esses eventos sao best-effort: ajudam a diagnosticar, mas nao sao fonte de verdade e nunca podem alterar gravacao, processamento ou retencao.

O banco principal ja usa WAL, que separa leitores de escritores, mas continua permitindo apenas um escritor por vez. Colocar eventos e limpeza no mesmo arquivo faria observabilidade competir com reunioes e jobs e ampliaria o impacto de corrupcao do journal.

## Decisao

Persistir o journal em um SQLite separado, usando a mesma engine embarcada, `Pooling=false` e WAL. O caminho e derivado de `CaminhoBanco`: `anamnesis.db` produz `anamnesis.journal.db`.

Tray e Worker podem escrever. Somente o Tray consulta e remove eventos expirados. Nao existe `ATTACH`, chave estrangeira ou transacao entre os dois arquivos. Contencao usa timeout de 1 s, menor granularidade efetiva exposta por `Microsoft.Data.Sqlite`; depois disso o evento pode ser descartado sem propagar falha ao caso de uso. Um teste cronometrado protege a primeira escrita, incluindo WAL e criacao de schema, contra o timeout padrao de 30 s do driver.

A primeira preparacao do journal e serializada entre processos por um arquivo adjacente `anamnesis.journal.db.init.lock`, aberto com `FileShare.None` e espera maxima de um segundo. A trava cobre apenas WAL e schema. Depois de adquiri-la, cada nova instancia consulta de forma read-only se WAL, as treze colunas e os quatro indices ja existem; quando o schema esta completo, ela pula toda DDL antes de iniciar seu `INSERT`. O arquivo de trava nao e apagado, pois recria-lo enquanto outro processo ainda mantem o handle antigo abriria uma segunda secao critica. Queda do processo fecha o handle pelo sistema operacional.

O arquivo e recriavel. Remover ou corromper o journal nao remove reunioes, jobs ou artefatos e nao impede o produto de continuar funcionando.

## Alternativas consideradas

| Alternativa | Probabilidade de adequacao | Motivo |
| --- | ---: | --- |
| SQLite separado e caminho derivado | 92% | Isola falha e contencao sem nova configuracao, dependencia ou servico. |
| Tabela no banco principal | 60% | E mais simples em quantidade de arquivos, mas limpeza e escrita de logs competem com o estado de negocio. |
| Arquivo de texto rotativo | 45% | Facil de gravar, porem pior para filtros, correlacao, concorrencia Tray/Worker e expiracao testavel. |
| Servico ou telemetria remota | 5% | Viola a proposta local, privada e sem infraestrutura externa. |

## Consequencias

- Observabilidade pode perder um evento sob queda ou contencao; isso e aceito porque o dominio permanece a fonte de verdade.
- O diretorio local passa a conter dois bancos com papeis distintos e nomes deterministicos.
- O diretorio tambem contem um arquivo de trava vazio e recriavel ao lado do journal.
- Metricas que combinam eventos e fila fazem duas consultas independentes, sem fingir consistencia transacional.
- A retencao remove somente linhas do journal e nunca executa `VACUUM` no caminho operacional.
- Backup do banco principal nao depende do journal para restaurar o produto.
- Um programa externo que ignore a trava ainda pode disputar o SQLite; o timeout nativo de um segundo e a defesa residual e o evento continua best-effort.
