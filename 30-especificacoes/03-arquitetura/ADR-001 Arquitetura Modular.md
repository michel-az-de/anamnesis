---
title: ADR-001 Arquitetura Modular
aliases: [Arquitetura Modular]
tags: [adr, arquitetura]
type: adr
created: 2026-08-04
updated: 2026-08-04
status: accepted
summary: Separar domínio, aplicação, infraestrutura, agente de bandeja e worker.
related: ["[[SPEK-001 Ciclo de Reuniao]]"]
---

# ADR-001 — Arquitetura modular

## Decisão

Usar uma solução .NET modular com `Domain`, `Application`, `Infrastructure`, `Tray` e `Worker`.

## Contexto

O controle de OBS e áudio depende da sessão interativa do usuário; transcrição e processamento devem continuar desacoplados e reiniciáveis.

## Consequências

- `Tray` controla gatilhos e OBS.
- `Worker` consome jobs persistidos.
- `Domain` não conhece SDKs, arquivos ou providers.
- SQLite é a fila local inicial; não há broker externo.
