---
title: SPEK-031 Observabilidade Operacional Real
aliases: [SPEK-031, Jornal Operacional]
tags: [especificacao, observabilidade, sqlite, diagnostico, pos-alpha]
type: spek
created: 2026-08-05
updated: 2026-08-05
status: draft
summary: Substitui eventos simulados por um jornal local persistente, seguro e correlacionado ao fluxo real.
related: ["[[SPEK-028 Console Local de Observabilidade]]", "[[SPEK-030 Desktop com Dados Reais]]", "[[Roadmap de Produto]]"]
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
- O jornal usa armazenamento SQLite local e uma fronteira de escrita e leitura substituivel.
- Falha ao registrar observabilidade nunca muda estado de dominio nem interrompe gravacao, processamento ou retencao.
- Eventos permanecem por 14 dias por padrao, com configuracao entre 1 e 90 dias.
- A limpeza remove somente eventos operacionais vencidos e possui teste separado da retencao de gravacoes.
- O console filtra por nivel, componente, codigo, correlacao e intervalo de tempo.
- Metricas de saude sao derivadas dos eventos e do banco real, nunca inventadas ou persistidas como estado de negocio.
- Nao existe rede, SDK de analytics ou dependencia externa.

## Critérios de aceite

- [ ] Reiniciar o Tray preserva e recarrega os eventos dentro da retencao configurada.
- [ ] Iniciar, finalizar, enfileirar, reservar, transcrever, gerar ata, arquivar, reter e falhar produzem codigos estaveis.
- [ ] Um fluxo pode ser seguido por `ReuniaoId` e `JobId` do inicio ao fim.
- [ ] Filtros e metricas usam apenas dados reais.
- [ ] Conteudo sensivel conhecido e removido antes da gravacao do evento.
- [ ] Falha do journal sink nao altera o resultado do caso de uso observado.
- [ ] Eventos expirados sao removidos sem tocar reunioes, jobs ou artefatos.
- [ ] Testes reiniciam o armazenamento temporario e comprovam persistencia e redacao.

## Testes associados

- Testes de Application para catalogo de codigos, correlacao e falha tolerada do sink.
- Testes de Infrastructure com SQLite temporario para consulta, filtro, reinicio e expiracao.
- Testes do Tray para console e metricas ligados ao read model real.
- Nenhum teste unitario chama OBS, rede ou CLI real.

## Decisoes pendentes

- Definir os codigos minimos de evento sem transformar cada linha de log em contrato publico.
- Validar se o journal compartilha o banco principal ou usa arquivo SQLite separado para reduzir contencao.

