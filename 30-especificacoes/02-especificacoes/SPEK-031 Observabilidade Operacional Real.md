---
title: SPEK-031 Observabilidade Operacional Real
aliases: [SPEK-031, Jornal Operacional]
tags: [especificacao, observabilidade, sqlite, diagnostico, pos-alpha]
type: spek
created: 2026-08-05
updated: 2026-08-05
status: completed
summary: Substitui eventos simulados por um jornal local persistente, seguro e correlacionado ao fluxo real.
related: ["[[SPEK-028 Console Local de Observabilidade]]", "[[SPEK-030 Desktop com Dados Reais]]", "[[ADR-015 Journal SQLite Isolado]]", "[[Roadmap de Produto]]"]
---

# SPEK-031 Observabilidade operacional real

## Objetivo

Alimentar o console do Desktop com eventos e metricas do fluxo real, preservando diagnosticos entre reinicializacoes sem enviar telemetria para fora da maquina.

## Fora de escopo

- Telemetria remota, analytics ou monitoramento em nuvem.
- Armazenar transcricao, prompts, tokens ou conteudo integral da ata em logs.
- Usar o jornal como fonte de verdade do estado da reuniao.
- Criar alertas externos ou pacote de suporte.
- Detectar reunioes ou controlar gravacoes.

## Regras

- Eventos possuem UTC, nivel, codigo estavel, componente, mensagem segura e correlacao opcional com `ReuniaoId` e `JobId`.
- Metadados seguem lista permitida. Segredos, texto transcrito, corpo de ata, titulo de janela e argumentos de CLI nao sao registrados.
- Mensagens de excecao passam por redacao antes da persistencia; stack trace completo fica fora do console padrao.
- O jornal usa o SQLite separado `anamnesis.journal.db`, derivado do caminho do banco principal conforme ADR-015, e fronteiras substituiveis de escrita e leitura.
- Tray e Worker escrevem no mesmo journal; somente o Tray consulta e remove eventos expirados.
- Nao existe transacao, `ATTACH` ou chave estrangeira entre journal e banco principal.
- Contencao do journal usa timeout de 1 s, menor granularidade efetiva do `Microsoft.Data.Sqlite`; depois disso o evento pode ser descartado sem afetar o fluxo observado.
- Falha ao registrar observabilidade nunca muda estado de dominio nem interrompe gravacao, processamento ou retencao.
- Eventos permanecem por 14 dias por padrao, com configuracao entre 1 e 90 dias.
- A limpeza remove somente eventos operacionais vencidos e possui teste separado da retencao de gravacoes.
- O console filtra por nivel, componente, codigo, correlacao e intervalo de tempo.
- Consultas retornam no maximo 500 eventos, ordenados por UTC decrescente e identificador.
- O console carrega a janela dos 500 eventos globais mais recentes; texto, nivel, componente e intervalo filtram essa janela localmente e a interface deixa esse limite explicito.
- Metricas de saude sao derivadas dos eventos e do banco real, nunca inventadas ou persistidas como estado de negocio.
- Nao existe rede, SDK de analytics ou dependencia externa.
- `ReuniaoId` correlaciona todo o fluxo. `JobId` passa a ser obrigatorio de `job.enfileirado` ate `job.concluido`; antes da criacao do job e na retencao ele pode ser ausente.
- Mensagens possuem template seguro, uma linha e no maximo 512 caracteres. Excecoes passam pelo redator e nunca sao persistidas diretamente.
- Metadados permitidos sao fechados e tipados: `operacao`, `tentativa`, `resultado`, `motivo_codigo` e `duracao_ms`.

## Catalogo minimo

| Codigo | Componente | Marco |
| --- | --- | --- |
| `gravacao.iniciada` | OBS | OBS confirmou o inicio |
| `gravacao.finalizada` | OBS | Caminho da gravacao foi persistido |
| `job.enfileirado` | Fila | Job duravel foi criado |
| `job.reservado` | Worker | Reserva exclusiva foi confirmada |
| `transcricao.iniciada` | Whisper | Etapa de transcricao iniciou |
| `transcricao.concluida` | Whisper | Transcritor retornou com sucesso |
| `ata.gerada` | Ata | Estrutura foi validada |
| `reuniao.arquivada` | Arquivo | Manifesto e estado foram persistidos |
| `job.concluido` | Worker | Fila confirmou a conclusao |
| `retencao.avaliada` | Retencao | Simulacao ou avaliacao terminou |
| `retencao.aplicada` | Retencao | Lixeira e estado foram confirmados |
| `operacao.falhou` | Componente da etapa | Falha segura e correlacionada |

Adicionar codigo e compativel. Renomear ou mudar seu significado exige revisar esta SPEK.

## Critérios de aceite

- [x] Reiniciar o Tray preserva e recarrega os eventos dentro da retencao configurada.
- [x] Iniciar, finalizar, enfileirar, reservar, transcrever, gerar ata, arquivar, reter e falhar produzem codigos estaveis.
- [x] Um fluxo pode ser seguido por `ReuniaoId` e `JobId` do inicio ao fim.
- [x] Filtros e metricas usam apenas dados reais.
- [x] Conteudo sensivel conhecido e removido antes da gravacao do evento.
- [x] Falha do journal sink nao altera o resultado do caso de uso observado.
- [x] Eventos expirados sao removidos sem tocar reunioes, jobs ou artefatos.
- [x] Testes reiniciam o armazenamento temporario e comprovam persistencia e redacao.
- [x] Duas instancias independentes, representando Tray e Worker, escrevem no mesmo journal sem compartilhar objetos em memoria.
- [x] O banco principal nao recebe tabela de eventos e permanece utilizavel durante a limpeza do journal.
- [x] Consulta limita o resultado aos 500 eventos mais recentes.

## Testes associados

- Testes de Application para catalogo de codigos, correlacao e falha tolerada do sink.
- Testes de Infrastructure com SQLite temporario para consulta, filtro, reinicio e expiracao.
- Testes do Tray para console e metricas ligados ao read model real.
- Teste canario procura senha, token, caminho de usuario, argumentos de CLI, transcricao e ata no banco do journal.
- Nenhum teste unitario chama OBS, rede ou CLI real.

## Sequencia TDD

1. Red: catalogo, redator e sink tolerante ainda nao existem.
2. Green: criar modelos e fronteiras minimas em Application.
3. Red: persistencia, reinicio, filtros, concorrencia e expiracao falham sem o journal SQLite.
4. Green: implementar o banco separado e a consulta limitada.
5. Red: fluxo real e Desktop nao produzem nem exibem os eventos.
6. Green: instrumentar casos de uso, Worker e console real.
7. Refactor: remover estado simulado do modo real sem alterar a demonstracao.

## Decisoes pendentes

- Nenhuma. Catalogo, correlacao, armazenamento e isolamento foram aprovados no ADR-015.

## Entrega

- Red confirmou ausencia de contratos, persistencia, instrumentacao, console real, isolamento de falhas e timeout inicial do SQLite.
- Green entregou journal separado, 12 codigos, correlacao, redacao segura, retencao configuravel, filtros, metricas e composicao Tray/Worker.
- E2E hermetico percorreu OBS, Worker, Whisper, CLI, arquivo e retencao, comprovou os 12 codigos e passou no canario de senha, token, caminho, transcricao e ata.
- Black-box executou Tray e Worker em processos separados e confirmou nove marcos correlacionados no mesmo journal.
- Validacao final: `dotnet test Anamnesis.sln --configuration Release --no-restore --verbosity minimal`, 177 de 177 testes verdes em Release.
- Publicacao autocontida: `artifacts/publish/SPEK-031`, com Tray e Worker `win-x64`.
- Evidencias preservadas: `artifacts/evidencias/SPEK-031`, incluindo logs, bancos, eventos JSON, ata, transcricao e captura do console real.

