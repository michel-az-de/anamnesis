---
title: ADR-006 Preparacao de Audio com FFmpeg
aliases: [FFmpeg para Whisper]
tags: [adr, audio, ffmpeg]
type: adr
created: 2026-08-05
updated: 2026-08-05
status: accepted
summary: Usar FFmpeg local para normalizar gravações do OBS antes do whisper.cpp.
related: ["[[SPEK-016 Preparacao de Audio com FFmpeg]]"]
---

# ADR-006 | Preparação de áudio com FFmpeg

## Decisão

Executar um FFmpeg local configurado para converter a gravação em WAV PCM 16-bit, mono e 16 kHz antes de chamar `whisper-cli`.

## Contexto

O `whisper-cli` do whisper.cpp aceita WAV PCM 16-bit. O OBS produz contêineres como MKV, portanto o caminho atual não funciona com os binários reais.

## Consequências

- A máquina precisa de FFmpeg local e seu caminho entra na configuração e nos diagnósticos.
- A gravação original nunca é alterada ou removida pela conversão.
- O arquivo derivado é temporário e pode ser removido após a transcrição.
- Não é adicionada biblioteca NuGet nem serviço remoto.
