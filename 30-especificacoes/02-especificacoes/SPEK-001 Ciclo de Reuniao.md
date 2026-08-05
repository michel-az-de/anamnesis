---
title: SPEK-001 Ciclo de Reuniao
aliases: [Ciclo de Reunião]
tags: [especificacao, reuniao, dominio]
type: spec
created: 2026-08-04
updated: 2026-08-05
status: completed
summary: Estados válidos de uma reunião, da gravação ao arquivamento seguro.
related: ["[[SPEK-003 Retencao de Gravacao]]", "[[ADR-001 Arquitetura Modular]]"]
---

# SPEK-001 — Ciclo de reunião

## Objetivo

Controlar o ciclo de vida de uma reunião sem permitir perda de gravações antes que seus artefatos tenham sido arquivados.

## Estados

```text
Agendada → Gravando → AguardandoProcessamento → EmTranscricao
→ EmAnalise → AguardandoArquivamento → Arquivada
→ PendenteExclusao → Excluida
```

`Falha` pode ser alcançado a partir das etapas de processamento e mantém os artefatos para diagnóstico.

## Critérios de aceite

- Não é possível finalizar uma gravação que não foi iniciada.
- Não é possível processar uma reunião sem caminho de gravação.
- Não é possível mover uma gravação para retenção antes do estado `Arquivada`.
- Não é possível excluir uma gravação antes de `PendenteExclusao`.

## Testes associados

- `ReuniaoTests.DevePrepararProcessamentoDepoisDeFinalizarGravacao`
- `ReuniaoTests.NaoDevePermitirExcluirAntesDeArquivar`
- `ReuniaoTests.DeveReiniciarProcessamentoAposFalhaPreservandoGravacao`
