---
title: ADR-002 Providers por CLI
aliases: [Providers por CLI]
tags: [adr, ia, integracao]
type: adr
created: 2026-08-04
updated: 2026-08-04
status: accepted
summary: Usar CLIs oficiais autenticados por assinatura atrás de IAtaRunner.
related: ["[[SPEK-002 Geracao de Ata]]"]
---

# ADR-002 — Providers por CLI

## Decisão

Usar adapters de CLI oficialmente autenticados por assinatura, atrás de `IAtaRunner`, em vez de automatizar interfaces web ou acoplar a regra de negócio a um provider.

## Providers iniciais

- Codex CLI
- Claude Code
- Kimi Code
- GLM/ZCode quando configurado pelo usuário
- Ollama como fallback local

## Consequências

- Cada adapter precisa de health check, timeout e parser de saída estruturada.
- Falha de quota faz o job avançar ao próximo adapter configurado.
- Nenhuma credencial de provider é armazenada no repositório.
