---
title: SPEK-008 Transcricao Local com Whisper
aliases: [Transcrição Local com Whisper]
tags: [especificacao, whisper, transcricao]
type: spec
created: 2026-08-04
updated: 2026-08-04
status: completed
summary: Adaptador para transcrever localmente uma gravação com whisper.cpp.
related: ["[[SPEK-002 Geracao de Ata]]", "[[SPEK-007 Gravacao de Teste com OBS]]"]
---

# SPEK-008 — Transcrição local com Whisper

## Objetivo

Transformar a gravação local em texto por meio do executável `whisper-cli` e do modelo configurado na própria máquina.

## Fora de escopo

- Baixar modelos ou executáveis automaticamente.
- Usar rede, serviços de transcrição ou APIs pagas.
- Revisar ou resumir a transcrição.

## Regras

- `WhisperTranscritor` implementa `ITranscritor` e executa exclusivamente um binário local configurado.
- O comando recebe modelo, arquivo de áudio, idioma e uma base de saída temporária; não é montado por shell.
- A transcrição é lida do arquivo `.txt` produzido pelo `whisper-cli`.
- Executável ausente, modelo ausente, saída vazia ou código de saída diferente de zero causam erro diagnóstico sem alterar a gravação.
- Testes unitários verificam a composição do comando e não executam processo real.

## Critérios de aceite

- [x] O comando do Whisper separa argumentos sem depender de interpretação de shell.
- [x] A execução local lê o texto gerado e retorna o idioma configurado.
- [x] Falhas do executável trazem diagnóstico sem expor caminho de gravação em logs adicionais.
- [x] Nenhum teste automatizado executa Whisper real.

## Testes associados

- `WhisperComandoTests.DeveComporArgumentosDoWhisperSemShell`
- `WhisperComandoTests.DeveUsarIdiomaConfigurado`

## Decisões pendentes

- O local persistente de executável e modelos será configurado pelo Tray.
