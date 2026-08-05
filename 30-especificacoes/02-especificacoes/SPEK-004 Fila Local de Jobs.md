---
title: SPEK-004 Fila Local de Jobs
aliases: [Fila Local de Jobs]
tags: [especificacao, persistencia, worker]
type: spec
created: 2026-08-04
updated: 2026-08-05
status: completed
summary: Fila SQLite durável para desacoplar a captura de reunião do processamento.
related: ["[[SPEK-001 Ciclo de Reuniao]]", "[[ADR-001 Arquitetura Modular]]"]
---

# SPEK-004 — Fila local de jobs

## Objetivo

Persistir pedidos de processamento localmente para que uma reunião capturada não seja perdida se o Worker for reiniciado.

## Regras

- Um job identifica uma única reunião por `ReuniaoId`.
- Um job pendente pode ser reservado por apenas um consumidor.
- Liberar um job reservado torna-o elegível novamente e incrementa sua contagem de tentativas na próxima reserva.
- Concluir um job o remove da fila ativa.
- A fila usa SQLite e não depende de rede ou serviço externo.

## Critérios de aceite

- Um job enfileirado pode ser reservado uma única vez enquanto estiver reservado.
- Um job liberado pode ser reservado novamente com tentativa incrementada.
- Um job concluído não volta a ser reservado.
- Os testes criam um banco temporário e não acessam o banco do usuário.

## Testes associados

- `SqliteJobQueueTests.DeveReservarUmJobUmaUnicaVezEnquantoEstiverReservado`
- `SqliteJobQueueTests.DevePermitirReservarNovamenteDepoisDeLiberar`
- `SqliteJobQueueTests.NaoDeveReservarJobConcluido`
- `SqliteJobQueueTests.DeveManterUmUnicoJobAtivoPorReuniao`
