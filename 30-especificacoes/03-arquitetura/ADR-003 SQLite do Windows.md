---
title: ADR-003 SQLite do Windows
aliases: [SQLite do Windows]
tags: [adr, persistencia, seguranca]
type: adr
created: 2026-08-04
updated: 2026-08-04
status: accepted
summary: Usar a biblioteca SQLite fornecida pelo Windows em vez de binário SQLite distribuído pelo pacote padrão.
related: ["[[SPEK-004 Fila Local de Jobs]]"]
---

# ADR-003 — SQLite do Windows

## Decisão

Usar `Microsoft.Data.Sqlite.Core` com `SQLitePCLRaw.bundle_winsqlite3`, que usa a `winsqlite3.dll` do Windows 10/11.

## Contexto

O pacote padrão `Microsoft.Data.Sqlite` distribui uma biblioteca SQLite nativa e a restauração acusou vulnerabilidade de alta gravidade em sua dependência transitiva.

## Consequências

- O Anamnesis é explicitamente um aplicativo Windows 10/11.
- O binário SQLite não é empacotado pelo projeto.
- Atualizações de segurança da biblioteca nativa acompanham o Windows Update.
