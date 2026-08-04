---
title: Arquitetura Tecnica
aliases: [arquitetura]
tags: [arquitetura, projeto/anamnesis]
type: note
created: 2026-08-04
updated: 2026-08-04
status: evergreen
summary: Visão técnica resumida dos módulos e responsabilidades do Anamnesis.
related: ["[[ADR-001 Arquitetura Modular]]", "[[ADR-002 Providers por CLI]]"]
---

# Arquitetura

```text
Tray (sessão do usuário) -> SQLite/job -> Worker -> arquivo/ata
                                    |          |
                                    |          +-> Transcritor
                                    |          +-> AtaRunner
                                    +-> ReuniaoRepository
```

## Responsabilidades

- `Anamnesis.Tray`: controla OBS e detecta gatilhos; nunca processa transcrição longa.
- `Anamnesis.Worker`: consome jobs pendentes e chama os casos de uso.
- `Anamnesis.Application`: define os contratos e orquestra o fluxo.
- `Anamnesis.Domain`: protege estados e regras de negócio; não conhece OBS, SQLite ou modelos.
- `Anamnesis.Infrastructure`: implementa integrações substituíveis.

## Decisão de previsibilidade

O modelo de IA só retorna dados estruturados. O sistema monta `ata.md` a partir desses dados. O código, e não o modelo, decide persistência, retentativas, retenção e exclusão.
