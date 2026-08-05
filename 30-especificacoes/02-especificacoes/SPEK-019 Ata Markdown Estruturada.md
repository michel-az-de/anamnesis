---
title: SPEK-019 Ata Markdown Estruturada
aliases: [Renderizacao Estruturada da Ata]
tags: [especificacao, ata, markdown, arquivamento]
type: spec
created: 2026-08-05
updated: 2026-08-05
status: completed
summary: Renderiza resumo, decisoes e tarefas validadas em ata.md deterministica.
related: ["[[SPEK-002 Geracao de Ata]]", "[[SPEK-009 Ata Estruturada por CLI]]"]
---

# SPEK-019 | Ata Markdown estruturada

## Objetivo

Garantir que o arquivo `ata.md` arquivado represente todos os dados estruturados validados no domínio.

## Regras

- O Markdown é produzido deterministicamente em C# a partir de `Ata`, nunca diretamente pela LLM.
- A saída contém título, resumo executivo, decisões e tarefas.
- Cada tarefa registra descrição, responsável e prazo quando disponíveis.
- Listas vazias permanecem explícitas no documento.

## Critérios de aceite

- [x] `ata.md` preserva resumo, decisões e tarefas recebidas da CLI.
- [x] Responsável e prazo são renderizados sem depender da LLM para formatação.
- [x] O ensaio E2E hermético verifica o conteúdo estruturado do arquivo.

## Testes associados

- `DiscoArquivadorTests.DeveArquivarAtaMarkdownComResumoDecisoesETarefas`
- `FluxoAlphaE2ETests.DeveProcessarFluxoCompletoComInfraestruturaLocal`

## Decisões pendentes

- Evidências temporais por item continuam no escopo futuro da SPEK-002.
