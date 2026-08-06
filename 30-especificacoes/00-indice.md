---
title: Indice de SPEKs
aliases: [Índice de Especificações, Indice de SPEKs]
tags: [especificacao, indice]
type: moc
created: 2026-08-04
updated: 2026-08-06
status: evergreen
summary: Fonte de verdade para a ordem de leitura e estado das especificações.
related: ["[[Especificacoes MOC]]", "[[Protocolo de Agentes]]", "[[Roadmap de Produto]]"]
---

# Índice de SPEKs

| ID | Título | Estado | Código relacionado |
| --- | --- | --- | --- |
| SPEK-001 | [[SPEK-001 Ciclo de Reuniao]] | concluído | `Domain`, `Application` |
| SPEK-002 | [[SPEK-002 Geracao de Ata]] | rascunho | `Application`, `Infrastructure` |
| SPEK-003 | [[SPEK-003 Retencao de Gravacao]] | concluído | `Domain`, `Application`, `Infrastructure` |
| SPEK-004 | [[SPEK-004 Fila Local de Jobs]] | concluído | `Application`, `Infrastructure`, `Worker` |
| SPEK-005 | [[SPEK-005 Persistencia de Reuniao em SQLite]] | concluído | `Domain`, `Application`, `Infrastructure` |
| SPEK-006 | [[SPEK-006 Worker e Retomada de Processamento]] | concluído | `Application`, `Infrastructure`, `Worker` |
| SPEK-007 | [[SPEK-007 Gravacao de Teste com OBS]] | concluído | `Domain`, `Application`, `Infrastructure` |
| SPEK-008 | [[SPEK-008 Transcricao Local com Whisper]] | concluído | `Application`, `Infrastructure` |
| SPEK-009 | [[SPEK-009 Ata Estruturada por CLI]] | concluído | `Application`, `Infrastructure` |
| SPEK-010 | [[SPEK-010 Tray Configuracao e Diagnosticos]] | concluído | `Tray`, `Infrastructure` |
| SPEK-011 | [[SPEK-011 Empacotamento e Validacao Manual]] | concluído | `Tray`, `Worker` |
| SPEK-012 | [[SPEK-012 Host Local do Worker]] | concluído | `Worker`, `Infrastructure` |
| SPEK-013 | [[SPEK-013 E2E Hermetico da Alpha]] | concluído | `Application`, `Infrastructure`, `Worker` |
| SPEK-014 | [[SPEK-014 Ensaio E2E com Evidencias]] | concluído | `Infrastructure`, `Worker` |
| SPEK-015 | [[SPEK-015 Worker Black Box com Evidencias]] | concluído | `Infrastructure`, `Worker` |
| SPEK-016 | [[SPEK-016 Preparacao de Audio com FFmpeg]] | concluído | `Infrastructure`, `Worker`, `Tray` |
| SPEK-017 | [[SPEK-017 Modo de Validacao do Tray]] | concluído | `Tray`, `Application`, `Infrastructure` |
| SPEK-018 | [[SPEK-018 Whisper Local em Docker]] | concluído | `Infrastructure`, `Worker`, `Tray` |
| SPEK-019 | [[SPEK-019 Ata Markdown Estruturada]] | concluído | `Infrastructure` |
| SPEK-020 | [[SPEK-020 Retencao Operacional pelo Worker]] | concluído | `Worker`, `Application`, `Infrastructure` |
| SPEK-021 | [[SPEK-021 UTF-8 nas CLIs Locais]] | concluído | `Infrastructure`, `Tray`, `Worker` |
| SPEK-022 | [[SPEK-022 Instalador da Beta em Windows Limpo]] | concluído | `Installer`, `Scripts`, `CI` |
| SPEK-023 | [[SPEK-023 Orquestracao do Worker pelo Tray]] | concluído | `Application`, `Infrastructure`, `Tray`, `Worker` |
| SPEK-024 | [[SPEK-024 Captura Universal de Audio pelo OBS]] | concluído | `Infrastructure`, `Tray` |
| SPEK-025 | [[SPEK-025 Prontidao Automatica do OBS]] | concluído | `Application`, `Infrastructure`, `Tray` |
| SPEK-026 | [[SPEK-026 Prontidao Automatica do Docker]] | concluído | `Infrastructure`, `Worker` |
| SPEK-027 | [[SPEK-027 Desktop Windows para Estado Visivel]] | concluído | `Tray` |
| SPEK-028 | [[SPEK-028 Console Local de Observabilidade]] | concluído | `Tray` |
| SPEK-029 | [[SPEK-029 Polimento Visual e Motion Desktop]] | concluído | `Tray` |
| SPEK-030 | [[SPEK-030 Desktop com Dados Reais]] | concluído | `Application`, `Infrastructure`, `Tray` |
| SPEK-031 | [[SPEK-031 Observabilidade Operacional Real]] | concluído | `Application`, `Infrastructure`, `Tray`, `Worker` |
| SPEK-032 | [[SPEK-032 Captura Instantanea e Deteccao Local]] | em validação | `Application`, `Infrastructure`, `Tray` |
| SPEK-033 | [[SPEK-033 Agenda Conectada]] | rascunho | `Application`, `Infrastructure`, `Tray` |
| SPEK-034 | [[SPEK-034 Google Calendar Adapter]] | rascunho | `Infrastructure`, `Tray` |
| SPEK-035 | [[SPEK-035 Microsoft Graph Calendar Adapter]] | rascunho | `Infrastructure`, `Tray` |
| SPEK-036 | [[SPEK-036 Publicacao Local no Obsidian]] | rascunho | `Application`, `Infrastructure`, `Tray` |
| SPEK-037 | [[SPEK-037 Mapeamento de Tarefas Externas]] | rascunho | `Domain`, `Application`, `Infrastructure`, `Tray` |
| SPEK-038 | [[SPEK-038 Trello Adapter]] | rascunho | `Infrastructure`, `Tray` |
| SPEK-039 | [[SPEK-039 Azure DevOps Work Items Adapter]] | rascunho | `Infrastructure`, `Tray` |
| SPEK-040 | [[SPEK-040 Concorrencia de Worker e Fila]] | concluído | `Infrastructure`, `Worker` |
| SPEK-041 | [[SPEK-041 Resiliencia de Processos Externos]] | concluído | `Infrastructure` |
| SPEK-042 | [[SPEK-042 Ciclo de Vida e Recuperacao da Reuniao]] | concluído | `Application`, `Tray` |
| SPEK-043 | [[SPEK-043 Persistencia Local Deterministica]] | concluído | `Infrastructure` |
| SPEK-044 | [[SPEK-044 Configuracao Local Protegida]] | concluído | `Infrastructure` |
| SPEK-045 | [[SPEK-045 Experiencia Windows Instalada e Primeiro Uso]] | concluido | `Tray`, `Infrastructure`, `Installer`, `Scripts`, `CI` |
| SPEK-046 | [[SPEK-046 Limite da Espera por Exclusividade]] | concluido | `Application`, `Infrastructure`, `Worker` |
| SPEK-047 | [[SPEK-047 Instalador Resiliente com Atualizacao e Reparo]] | concluído | `Tray`, `Installer`, `Scripts`, `CI` |
| SPEK-048 | [[SPEK-048 Inicializacao Concorrente do Journal SQLite]] | concluido | `Infrastructure` |
| SPEK-049 | [[SPEK-049 Release Canonico do Instalador Windows]] | em validacao | `Installer`, `Scripts`, `CI`, `Docs` |

## Fluxo obrigatório

```text
SPEK aprovada → teste falhando → implementação mínima → teste verde → atualização da SPEK
```
