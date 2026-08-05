---
title: SPEK-042 Ciclo de Vida e Recuperacao da Reuniao
aliases: [SPEK-042, Recuperacao de Gravacao Presa]
tags: [especificacao, gravacao, tray, retencao, robustez, pos-alpha]
type: spek
created: 2026-08-05
updated: 2026-08-05
status: completed
summary: Uma falha ao encerrar a gravacao nao pode deixar o Anamnesis sem gravar ate reiniciar o Tray.
related: ["[[SPEK-001 Ciclo de Reuniao]]", "[[SPEK-003 Retencao de Gravacao]]", "[[SPEK-030 Desktop com Dados Reais]]", "[[SPEK-040 Concorrencia de Worker e Fila]]"]
---

# SPEK-042 Ciclo de vida e recuperacao da reuniao

## Objetivo

Garantir que nenhum caminho de falha deixe uma reuniao presa em `Gravando`, porque o indice unico de gravacao ativa transforma esse estado em bloqueio de todo o produto.

## Contexto do defeito

`ControlarGravacaoHandler.FinalizarAsync` chamava o gravador sem tratamento. Com o OBS fechado ou a conexao caida, a excecao subia e a reuniao permanecia `Gravando`. Como existe o indice unico parcial `ux_reunioes_gravando`, toda nova gravacao passava a falhar com `GravacaoJaAtivaException`.

A reconciliacao que resolveria isso rodava uma unica vez por sessao, entao a unica saida era reiniciar o Tray.

Alem disso, `AtualizarAsync` escrevia o estado da sessao fora do semaforo que protege os comandos, e era chamada tanto pelo timer de polling quanto de dentro dos proprios comandos.

## Fora de escopo

- Reiniciar a gravacao automaticamente apos a falha; a decisao continua sendo do usuario.
- Recuperar uma reuniao presa em `EmTranscricao` apos queda do Worker, que exige mudanca na maquina de estados e SPEK propria.
- Persistir a cena anterior do OBS entre reinicios do Tray, tratada na SPEK-041.
- Alterar a politica de retencao de sete dias.

## Regras

- Falha ao encerrar a gravacao registra `Falha` na reuniao e persiste antes de propagar a excecao.
- Cancelamento durante `FinalizarAsync` segue a mesma compensacao com `CancellationToken.None` e reprojeta a `OperationCanceledException` original.
- O job so e enfileirado depois de a gravacao ser finalizada e persistida com sucesso.
- Registrada a falha, o indice de gravacao ativa libera e uma nova gravacao pode comecar sem reiniciar o Tray.
- A sessao do Desktop reconcilia qualquer gravacao ativa que nao tenha sido iniciada por ela propria, em qualquer atualizacao, e nao apenas na primeira.
- Uma gravacao iniciada pela propria sessao nao e reconciliada, para nao consultar o OBS a cada ciclo de polling.
- Todo estado compartilhado da sessao e lido e escrito sob o mesmo semaforo dos comandos.
- O motivo de uma avaliacao de retencao e um valor tipado; o texto e apresentacao e nunca decide fluxo.
- Nenhuma dependencia nova e necessaria, portanto esta SPEK nao exige ADR.

## Critérios de aceite

- [x] Falha do gravador ao encerrar deixa a reuniao em `Falha`, com o motivo preservado, e nao enfileira job.
- [x] Cancelamento durante o `StopRecord` deixa a reuniao em `Falha`, persiste mesmo com o token cancelado, nao enfileira job e preserva a excecao original.
- [x] Depois dessa falha, uma nova gravacao pode ser iniciada no mesmo processo, contra banco SQLite real.
- [x] Uma gravacao orfa que aparece depois da primeira atualizacao e reconciliada.
- [x] O comportamento de reconciliacao na primeira atualizacao permanece.
- [x] Encerramentos concorrentes continuam enviando um unico stop ao OBS.
- [x] A retencao decide o tipo de excecao pelo motivo tipado, e nao por comparacao de texto.
- [x] A suite existente permanece verde.

## Testes associados

- `ControlarGravacaoHandlerTests.DeveRegistrarFalhaQuandoGravadorNaoEncerra`.
- `ControlarGravacaoHandlerTests.DeveCompensarCancelamentoAoEncerrarEPreservarExcecaoOriginal`.
- `SqliteReuniaoRepositoryTests.DevePermitirNovaGravacaoAposFalhaAoEncerrar`, contra banco temporario real, que e o teste que prova o desbloqueio.
- `SqliteReuniaoRepositoryTests.DevePermitirNovaGravacaoAposCancelamentoAoEncerrar`, contra banco temporario real.
- `DesktopRealSessionTests.DeveReconciliarGravacaoOrfaEmAtualizacaoPosterior`.
- `RetencaoGravacaoHandlerTests.DeveRelatarMotivoTipadoSemDependerDoTexto`.
- Nenhum teste unitario chama OBS, rede ou CLI real.

## Execucao local

- Red Application: a regressao retornou `Expected: Falha`, `Actual: Gravando`.
- Red SQLite: a reuniao persistida continuou `Gravando` depois do cancelamento.
- Green Application: 17 de 17 testes verdes em Release.
- Green SQLite: regressao direcionada verde contra banco temporario real.
- Na entrega isolada, a suite completa aguardou o Red paralelo da SPEK-031; a consolidacao posterior confirmou 177 de 177 testes verdes em Release.

## Decisoes pendentes

- Nenhuma para este incremento.
