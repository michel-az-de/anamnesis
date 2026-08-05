---
title: ADR-005 Lixeira do Windows
aliases: [Lixeira do Windows]
tags: [adr, retencao, windows]
type: adr
created: 2026-08-05
updated: 2026-08-05
status: accepted
summary: Usar a API Shell do Windows para remoção recuperável das gravações.
related: ["[[SPEK-003 Retencao de Gravacao]]"]
---

# ADR-005 — Lixeira do Windows

## Decisão

Mover gravações para a Lixeira com `SHFileOperationW` e as opções de desfazer e sem interação. O adaptador é a única fronteira que chama a API Shell.

## Consequências

- A operação é específica para Windows 10/11.
- Falhas da API são propagadas e a reunião volta de `PendenteExclusao` para `Arquivada`.
- Testes substituem o adaptador de Lixeira e não chamam a API Shell.
