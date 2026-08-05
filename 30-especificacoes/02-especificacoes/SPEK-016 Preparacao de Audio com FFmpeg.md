---
title: SPEK-016 Preparacao de Audio com FFmpeg
aliases: [Conversao de gravacao para Whisper]
tags: [especificacao, audio, ffmpeg, whisper]
type: spec
created: 2026-08-05
updated: 2026-08-05
status: completed
summary: Converte gravações do OBS em WAV PCM 16-bit mono 16 kHz antes da transcrição local.
related: ["[[SPEK-008 Transcricao Local com Whisper]]", "[[ADR-006 Preparacao de Audio com FFmpeg]]"]
---

# SPEK-016 | Preparação de áudio com FFmpeg

## Objetivo

Preparar a gravação produzida pelo OBS no formato aceito pelo `whisper-cli`, sem alterar ou excluir o arquivo original.

## Regras

- O FFmpeg é um executável local configurado, sem download automático pela aplicação.
- A conversão produz WAV PCM 16-bit, mono e 16 kHz em diretório temporário.
- O `WhisperTranscritor` usa o WAV convertido e remove somente o derivado temporário ao finalizar.
- Falha, ausência do executável ou ausência da saída interrompem a transcrição com diagnóstico claro.
- Testes automatizados usam processo local temporário e nunca executam FFmpeg ou Whisper reais.

## Critérios de aceite

- [x] O comando FFmpeg recebe entrada, saída e parâmetros sem shell.
- [x] Uma gravação `.mkv` é convertida antes de chamar o Whisper.
- [x] A gravação original permanece intacta após sucesso ou falha.
- [x] Configuração, diagnósticos e Worker incluem o caminho do FFmpeg.

## Testes associados

- `FfmpegComandoTests.DeveComporConversaoParaWavPcm16Mono16KhzSemShell`
- `WhisperTranscritorTests.DeveConverterMkvParaWavSemAlterarGravacaoOriginal`

## Evidência real

- `artifacts\real-smoke\whisper-20260805-01`: MKV convertido e transcrito pelo whisper.cpp 1.9.2 com modelo `base` multilíngue.

## Decisões pendentes

- Nenhuma para a alpha.
