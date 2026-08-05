---
title: SPEK-007 Gravacao de Teste com OBS
aliases: [Gravação de Teste com OBS]
tags: [especificacao, obs, gravacao]
type: spec
created: 2026-08-04
updated: 2026-08-04
status: completed
summary: Início e encerramento de uma gravação de teste via OBS com persistência e job local.
related: ["[[SPEK-001 Ciclo de Reuniao]]", "[[SPEK-004 Fila Local de Jobs]]", "[[ADR-004 Integracao com OBS]]"]
---

# SPEK-007 — Gravação de teste com OBS

## Objetivo

Iniciar e encerrar uma gravação controlada pelo OBS, persistindo a reunião e criando um job somente quando o caminho da gravação estiver disponível.

## Fora de escopo

- Selecionar cenas, fontes ou perfil do OBS.
- Transcrever ou gerar ata.
- Excluir, mover ou arquivar a gravação.

## Regras

- `IGravador` é a fronteira de captura; `ObsGravador` usa obs-websocket v5 conforme ADR-004.
- Antes de solicitar o início ao OBS, a reunião é persistida como `Gravando`.
- Ao parar, o caminho retornado pelo OBS finaliza a gravação, é persistido e gera um job na fila local.
- Em falha ao iniciar, a reunião é marcada como `Falha` e persistida; nenhum job é criado.
- A senha do OBS não aparece em exceções ou logs produzidos pelo adaptador.

## Critérios de aceite

- [x] Iniciar uma gravação persiste uma reunião em `Gravando` e chama o gravador.
- [x] Encerrar uma gravação persiste o caminho, muda para `AguardandoProcessamento` e enfileira um job.
- [x] Falha ao iniciar persiste `Falha` e não enfileira job.
- [x] O adaptador OBS envia `StartRecord` e `StopRecord` pelo protocolo v5, com autenticação de desafio quando necessária.

## Testes associados

- `ControlarGravacaoHandlerTests.DevePersistirReuniaoEIniciarGravacao`
- `ControlarGravacaoHandlerTests.DeveFinalizarGravacaoEPersistirJob`
- `ControlarGravacaoHandlerTests.DeveRegistrarFalhaSemEnfileirarJob`

## Decisões pendentes

- A tela para configurar endereço e senha do OBS será especificada junto do Tray.
