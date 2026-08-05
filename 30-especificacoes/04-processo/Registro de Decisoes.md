---
title: Registro de Decisoes
aliases: [Decisões de Arquitetura]
tags: [processo, adr]
type: note
created: 2026-08-04
updated: 2026-08-05
status: evergreen
summary: Convenção para registrar decisões que mudam limites, dependências ou políticas do produto.
related: ["[[ADR-001 Arquitetura Modular]]", "[[ADR-002 Providers por CLI]]", "[[Roadmap de Produto]]"]
---

# Registro de decisões

Crie um ADR quando uma mudança afetar mais de um módulo, introduzir dependência, alterar privacidade/retenção ou tornar uma decisão difícil de reverter.

Formato: contexto, decisão, consequências e links para SPEKs afetadas.

## Aceitos na robustez pós-alpha

| ADR | Decisão | SPEKs |
| --- | --- | --- |
| [[ADR-012 Instancia Unica do Worker]] | Mutex nomeado por banco garante um Worker por fila; prazo de reserva recusado por piorar a recuperação de queda | 040 |
| [[ADR-013 Engine SQLite Embarcada]] | `bundle_e_sqlite3` 3.0.5 no lugar da engine do Windows, para não depender do build do sistema | 043 |
| [[ADR-014 Protecao de Segredos Locais]] | DPAPI por usuário com prefixo e migração transparente; mecanismo a ser reutilizado pelas integrações | 044 |
| [[ADR-015 Journal SQLite Isolado]] | Journal best-effort em arquivo separado para isolar falhas, contenção e limpeza do estado de negócio | 031 |

## Fila pós-alpha

| Gate | SPEKs | Estado |
| --- | --- | --- |
| APIs Windows para detecção local e política contra falsos positivos | 032 | proposto |
| OAuth desktop, credenciais protegidas e sincronização de agenda | 033, 034, 035 | proposto |
| Publicação Markdown segura em vault Obsidian | 036 | proposto |
| Aprovação humana, idempotência e propriedade de campos externos | 037, 038, 039 | proposto |

Nenhuma dessas SPEKs de integração pode passar de rascunho para aprovada antes do ADR correspondente ser aceito.
