---
title: ADR-004 Integração com OBS
aliases: [Integração com OBS]
tags: [adr, obs, gravacao]
type: adr
created: 2026-08-04
updated: 2026-08-04
status: accepted
summary: Controlar a gravação por obs-websocket v5 sem pacote adicional.
related: ["[[SPEK-007 Gravacao de Teste com OBS]]"]
---

# ADR-004 — Integração com OBS

## Decisão

Usar o obs-websocket v5 incluído no OBS Studio 28 ou posterior, acessado com `ClientWebSocket` da BCL. O endereço padrão será `ws://127.0.0.1:4455` e a senha será fornecida pela configuração local, nunca registrada em logs.

## Contexto

O Anamnesis precisa iniciar e encerrar uma gravação de teste local. O protocolo oficial do obs-websocket fornece `StartRecord` e `StopRecord`; a resposta de parada contém o caminho do arquivo gerado. Não é necessário adicionar um pacote de terceiros para essa única superfície do protocolo.

## Consequências

- Exige OBS Studio 28+ com o servidor WebSocket ativado.
- O adaptador implementa autenticação de desafio quando o servidor a exigir.
- Testes automatizados usam `IGravador`; não abrem conexão WebSocket.
