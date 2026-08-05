---
title: SPEK-018 Whisper Local em Docker
aliases: [Fallback seguro do whisper.cpp]
tags: [especificacao, whisper, docker, seguranca]
type: spec
created: 2026-08-05
updated: 2026-08-05
status: completed
summary: Executa whisper.cpp na imagem oficial local quando o binário Windows é bloqueado pelo antivírus.
related: ["[[SPEK-008 Transcricao Local com Whisper]]", "[[ADR-007 Fallback Docker para Whisper]]"]
---

# SPEK-018 | Whisper local em Docker

## Objetivo

Permitir transcrição local pela imagem oficial do whisper.cpp sem restaurar binário em quarentena ou criar exclusão no antivírus.

## Regras

- Usar `ghcr.io/ggml-org/whisper.cpp:main`, preferencialmente fixada pelo digest instalado.
- O modelo é montado somente para leitura.
- Áudio e saída usam apenas diretórios temporários do Anamnesis.
- O adaptador aceita modo nativo ou Docker por configuração explícita.
- Testes automatizados verificam argumentos e não iniciam Docker real.

## Critérios de aceite

- [x] O comando Docker monta modelo, áudio e saída nos caminhos mínimos necessários.
- [x] O `WhisperTranscritor` usa Docker quando uma imagem está configurada e mantém modo nativo compatível.
- [x] Configuração, diagnósticos e Worker suportam o modo Docker.
- [x] A gravação real pendente é transcrita e arquivada sem reduzir a proteção do Defender.

## Evidência real

- Imagem oficial fixada por digest: `ghcr.io/ggml-org/whisper.cpp@sha256:de6861ca4d509482d7e7c31e4480ee888a76a48903dad1f448faafa6c915d53c`.
- Reunião `bda724190e8544b780becb76c3ecbd89` transcrita pelo Worker publicado e arquivada em 2026-08-05.
- Logs preservados em `artifacts\real-e2e\20260805-01\worker-retry.stdout.log` e `worker-retry.stderr.log`.

## Decisões pendentes

- Avaliar build nativo assinado para versões futuras.
