---
title: SPEK-021 UTF-8 nas CLIs Locais
aliases: [Codificacao UTF-8 da Ata]
tags: [especificacao, cli, utf8, ata]
type: spec
created: 2026-08-05
updated: 2026-08-05
status: validating
summary: Preserva caracteres PT-BR nas CLIs autenticadas e nos executaveis locais.
related: ["[[SPEK-009 Ata Estruturada por CLI]]", "[[SPEK-019 Ata Markdown Estruturada]]", "[[SPEK-022 Instalador da Beta em Windows Limpo]]"]
---

# SPEK-021 | UTF-8 nas CLIs locais

## Objetivo

Garantir que o texto enviado e recebido por `CliAtaRunner` preserve caracteres Unicode independentemente da página de código do Windows.

## Regras

- stdin, stdout e stderr do processo são configurados explicitamente como UTF-8 sem BOM.
- O JSON continua trafegando sem shell e a CLI real permanece fora dos testes automatizados.
- Tray e Worker configuram a saída de console explicitamente como UTF-8 antes de registrar mensagens.

## Critérios de aceite

- [x] Uma CLI fake em UTF-8 preserva acentos no resumo, decisões e tarefas.
- [x] O ensaio real com Codex gera `ata.md` legível em PT-BR.
- [ ] Os testes black box de Tray e Worker preservam acentos no runner Windows `en-US`.

## Testes e evidências

- `CliAtaRunnerTests.DevePreservarCaracteresUtf8DaCliFake`.
- `artifacts\real-e2e\20260805-final-8\ata.md`.
- GitHub Actions da SPEK-022 em runner `windows-2025`.

## Decisões pendentes

- Nenhuma para a alpha.
