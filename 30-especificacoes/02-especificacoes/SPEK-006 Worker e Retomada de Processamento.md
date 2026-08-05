---
title: SPEK-006 Worker e Retomada de Processamento
aliases: [Worker e Retomada]
tags: [especificacao, worker, processamento]
type: spec
created: 2026-08-04
updated: 2026-08-05
status: completed
summary: Consumo local de jobs com liberação de reservas após reinicialização.
related: ["[[SPEK-004 Fila Local de Jobs]]", "[[SPEK-005 Persistencia de Reuniao em SQLite]]"]
---

# SPEK-006 — Worker e retomada de processamento

## Objetivo

Consumir um job local de cada vez e recuperar jobs que ficaram reservados quando o Worker foi interrompido.

## Fora de escopo

- Hospedar o Worker como Serviço do Windows.
- Definir limite, atraso ou escalonamento de tentativas.
- Integrar OBS, Whisper ou uma CLI de LLM real.

## Regras

- Ao iniciar, o Worker libera reservas ativas não concluídas para que voltem a ser elegíveis.
- O consumidor reserva no máximo um job por execução e invoca `ProcessarReuniaoHandler` para sua reunião.
- Após sucesso, o job é concluído.
- Se o processamento falhar ou for cancelado, o job é liberado e a exceção é propagada.
- Na tentativa seguinte, uma reunião em `Falha` volta a `AguardandoProcessamento`, preserva a gravação original e descarta somente resultados parciais de transcrição e ata.
- A fila e o Worker não chamam rede, OBS ou CLIs em seus testes; esses colaboradores são dublês.

## Critérios de aceite

- [x] Uma reserva deixada no SQLite torna-se reservável depois da retomada.
- [x] Um job processado com sucesso é concluído e não volta à fila ativa.
- [x] Uma fila vazia não chama o processamento.
- [x] Uma falha libera o job antes de propagar a exceção.
- [x] Uma nova tentativa após `Falha` reinicia o processamento desde a gravação original.

## Testes associados

- `SqliteJobQueueTests.DeveLiberarReservasAtivasParaRetomada`
- `ReuniaoConsumerTests.DeveConcluirJobDepoisDeProcessarReuniao`
- `ReuniaoConsumerTests.NaoDeveProcessarQuandoNaoHaJob`
- `ReuniaoConsumerTests.DeveLiberarJobQuandoOProcessamentoFalha`
- `ReuniaoTests.DeveReiniciarProcessamentoAposFalhaPreservandoGravacao`
- `ProcessarReuniaoHandlerTests.DeveReprocessarReuniaoEmFalha`

## Decisões pendentes

- Limite, atraso e classificação de falhas transitórias continuam fora da alpha.
