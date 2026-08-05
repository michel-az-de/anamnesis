---
title: SPEK-013 E2E Hermetico da Alpha
aliases: [E2E Hermético]
tags: [especificacao, e2e, teste]
type: spec
created: 2026-08-05
updated: 2026-08-05
status: completed
summary: Fluxo integrado completo com SQLite e arquivos reais, substituindo apenas pré-requisitos externos indisponíveis.
related: ["[[SPEK-007 Gravacao de Teste com OBS]]", "[[SPEK-008 Transcricao Local com Whisper]]", "[[SPEK-009 Ata Estruturada por CLI]]", "[[SPEK-003 Retencao de Gravacao]]"]
---

# SPEK-013 — E2E hermético da alpha

## Objetivo

Executar em um único teste o fluxo de captura, fila, retomada, transcrição, ata, arquivamento e retenção com infraestrutura local real.

## Regras

- SQLite, fila, Worker, repositório, arquivador e adaptadores OBS/Whisper/CLI são exercitados no teste.
- Um WebSocket OBS local e scripts `.cmd` temporários substituem somente OBS, Whisper e a CLI autenticada, que não estão instalados nesta máquina.
- A retenção usa uma Lixeira fake no teste; a API Shell do Windows permanece coberta pelos testes da SPEK-003.
- Nenhuma rede externa, OBS real, modelo real ou CLI autenticada real é chamada.

## Critérios de aceite

- [x] A gravação via `ObsGravador` produz reunião e job SQLite.
- [x] O Worker consome o job com `WhisperTranscritor` e `CliAtaRunner` reais contra processos locais temporários.
- [x] `ata.md` e `transcricao.md` são arquivadas e a retenção conclui a transição do agregado.
- [x] O teste verifica que o OBS recebeu `StartRecord` e `StopRecord` e que a CLI recebeu o JSON de entrada.

## Testes associados

- `FluxoAlphaE2ETests.DeveProcessarFluxoCompletoComInfraestruturaLocal`

## Decisões pendentes

- A validação com binários reais continua sendo o único passo para liberar a SPEK-011.
