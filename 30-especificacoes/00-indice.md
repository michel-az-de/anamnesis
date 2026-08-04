---
title: Indice de SPEKs
aliases: [Índice de Especificações, Indice de SPEKs]
tags: [especificacao, indice]
type: moc
created: 2026-08-04
updated: 2026-08-04
status: evergreen
summary: Fonte de verdade para a ordem de leitura e estado das especificações.
related: ["[[Especificacoes MOC]]", "[[Protocolo de Agentes]]"]
---

# Índice de SPEKs

| ID | Título | Estado | Código relacionado |
| --- | --- | --- | --- |
| SPEK-001 | [[SPEK-001 Ciclo de Reuniao]] | aprovado | `Domain`, `Application` |
| SPEK-002 | [[SPEK-002 Geracao de Ata]] | rascunho | `Application`, `Infrastructure` |
| SPEK-003 | [[SPEK-003 Retencao de Gravacao]] | rascunho | `Domain`, `Worker` |
| SPEK-004 | [[SPEK-004 Fila Local de Jobs]] | aprovado | `Application`, `Infrastructure`, `Worker` |

## Fluxo obrigatório

```text
SPEK aprovada → teste falhando → implementação mínima → teste verde → atualização da SPEK
```
