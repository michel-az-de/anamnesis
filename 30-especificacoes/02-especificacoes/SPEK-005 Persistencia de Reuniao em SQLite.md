---
title: SPEK-005 Persistencia de Reuniao em SQLite
aliases: [Persistência de Reunião em SQLite]
tags: [especificacao, persistencia, reuniao]
type: spec
created: 2026-08-04
updated: 2026-08-04
status: completed
summary: Persistência local do agregado Reunião para retomada segura do processamento.
related: ["[[SPEK-001 Ciclo de Reuniao]]", "[[SPEK-004 Fila Local de Jobs]]", "[[ADR-003 SQLite do Windows]]"]
---

# SPEK-005 — Persistência de reunião em SQLite

## Objetivo

Salvar e recuperar uma reunião localmente, preservando o estado e os artefatos necessários para que o Worker possa retomar o processamento após reinicialização.

## Fora de escopo

- Criar ou consumir jobs pelo Worker.
- Aplicar política de retenção ou excluir arquivos.
- Migrar bancos de versões anteriores.

## Regras

- `SqliteReuniaoRepository` implementa `IReuniaoRepository` e usa apenas SQLite local, conforme ADR-003.
- Salvar a mesma reunião novamente atualiza seu registro; não cria duplicata.
- A recuperação restaura identidade, título, data de criação, status, motivo de falha, gravação, transcrição e ata, incluindo decisões e tarefas.
- Datas são persistidas em formato round-trip; listas da ata são persistidas como JSON no banco local.
- A reconstrução do agregado é explícita no domínio e não expõe setters públicos.
- Consultar uma reunião inexistente retorna `null`.

## Critérios de aceite

- [x] Uma reunião aguardando processamento recuperada preserva o caminho e os horários da gravação.
- [x] Uma reunião arquivada recuperada preserva transcrição, ata, decisões e tarefas.
- [x] Salvar duas vezes a mesma reunião mantém um único registro e reflete o estado mais recente.
- [x] Consultar uma reunião ausente retorna `null`.
- [x] Os testes usam banco temporário e não acessam o banco do usuário.

## Testes associados

- `SqliteReuniaoRepositoryTests.DeveRecuperarGravacaoEEstadoDaReuniao`
- `SqliteReuniaoRepositoryTests.DeveRecuperarArtefatosDeUmaReuniaoArquivada`
- `SqliteReuniaoRepositoryTests.DeveAtualizarUmaReuniaoJaPersistida`
- `SqliteReuniaoRepositoryTests.DeveRetornarNuloParaReuniaoInexistente`
- `SqliteReuniaoRepositoryTests.DeveRecuperarMotivoDeFalha`

## Decisões pendentes

- Nenhuma.
