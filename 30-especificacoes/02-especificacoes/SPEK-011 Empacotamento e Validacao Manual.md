---
title: SPEK-011 Empacotamento e Validacao Manual
aliases: [Empacotamento da Alpha]
tags: [especificacao, empacotamento, alpha]
type: spec
created: 2026-08-05
updated: 2026-08-05
status: completed
summary: Publicação autocontida win-x64 e roteiro de validação manual ponta a ponta.
related: ["[[SPEK-010 Tray Configuracao e Diagnosticos]]"]
---

# SPEK-011 — Empacotamento e validação manual da alpha

## Objetivo

Gerar os executáveis autocontidos do Tray e Worker para Windows x64 e fornecer um roteiro repetível para validar o fluxo completo.

## Fora de escopo

- Instalador MSI, assinatura de código ou atualização automática.
- Instalar OBS, Whisper, modelos ou uma CLI autenticada.
- Declarar sucesso manual sem os pré-requisitos reais configurados.

## Regras

- A publicação usa `Release`, `win-x64` e `SelfContained=true` para Tray e Worker.
- O script não exclui artefatos existentes.
- O roteiro exige configurar `%LocalAppData%\Anamnesis\config.json` e registrar cada etapa observada.
- A validação ponta a ponta só é aprovada após capturar, reiniciar o Worker, transcrever, gerar/arquivar ata e simular retenção na mesma máquina.

## Critérios de aceite

- [x] O script produz diretórios independentes para Tray e Worker em `artifacts\alpha\win-x64`.
- [x] O roteiro identifica configurações e pré-requisitos antes de iniciar a gravação.
- [x] Uma indisponibilidade de OBS, Whisper ou CLI impede a aprovação manual e é registrada como bloqueio.

## Evidências

- `scripts\Publish-Alpha.ps1`
- `30-especificacoes\04-processo\Roteiro de Validacao Alpha.md`

## Decisões pendentes

- Instalador e assinatura de código ficam para a beta.

## Execução nesta máquina

- Publicação final validada: `artifacts\alpha\win-x64-final-8`.
- OBS Studio 32.2.1 gravou por controle real do Tray via WebSocket.
- FFmpeg 9.0 preparou o áudio e whisper.cpp executou localmente pela imagem Docker oficial fixada por digest.
- Codex CLI 0.146.0 autenticado por assinatura retornou JSON estruturado em UTF-8.
- Worker concluiu o job, arquivou ata e transcrição, simulou retenção e moveu a gravação para a Lixeira.
- Evidência canônica: `artifacts\real-e2e\20260805-final-8\resultado.md`.
