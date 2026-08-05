---
title: ADR-007 Fallback Docker para Whisper
aliases: [Whisper em container local]
tags: [adr, whisper, docker, seguranca]
type: adr
created: 2026-08-05
updated: 2026-08-05
status: accepted
summary: Usar imagem oficial do whisper.cpp como fallback local quando o binário Windows é bloqueado.
related: ["[[SPEK-018 Whisper Local em Docker]]"]
---

# ADR-007 | Fallback Docker para Whisper

## Decisão

Suportar execução do whisper.cpp em Docker Desktop local, além do executável nativo já existente.

## Contexto

O Microsoft Defender colocou `whisper-cli.exe` oficial v1.9.2 em quarentena como `Trojan:Win32/Wacatac.H!ml`. Desabilitar proteção ou criar exclusão não é aceitável. Docker Desktop já está instalado e a imagem oficial é documentada pelo projeto.

## Consequências

- A alpha nesta máquina depende de Docker Desktop para transcrição.
- Modelo, áudio e saída são montados com escopo mínimo.
- O modo nativo continua disponível em máquinas onde o binário é aceito.
- Uma distribuição futura deve preferir binário nativo assinado ou compilado em pipeline confiável.
