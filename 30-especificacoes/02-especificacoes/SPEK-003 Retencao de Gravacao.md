---
title: SPEK-003 Retencao de Gravacao
aliases: [Retenção de Gravação]
tags: [especificacao, privacidade, retencao]
type: spec
created: 2026-08-04
updated: 2026-08-04
status: draft
summary: Regras de retenção e exclusão recuperável de arquivos de gravação.
related: ["[[SPEK-001 Ciclo de Reuniao]]"]
---

# SPEK-003 — Retenção de gravação

## Objetivo

Liberar espaço sem expor o usuário ao risco de perder material ainda não processado.

## Regras

- A política padrão move a gravação à Lixeira após sete dias em `Arquivada`.
- Exclusão definitiva é uma etapa separada e configurável.
- Falhas, reuniões privadas ou itens sem ata validada não são removidos automaticamente.
- Toda transição de retenção fica no histórico do job.

## Critérios de aceite

- A exclusão é bloqueada para qualquer estado anterior a `PendenteExclusao`.
- A simulação de retenção informa o que será movido sem alterar arquivos.
- A implementação tem testes para sucesso, arquivo ausente e erro de I/O.
