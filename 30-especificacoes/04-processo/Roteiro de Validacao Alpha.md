---
title: Roteiro de Validacao Alpha
aliases: [Validação Manual Alpha]
tags: [processo, alpha, validacao]
type: runbook
created: 2026-08-05
updated: 2026-08-05
status: active
related: ["[[SPEK-011 Empacotamento e Validacao Manual]]"]
---

# Roteiro de validação manual da alpha

## Pré-requisitos

- [x] OBS Studio 32.2.1 aberto, servidor WebSocket ativo e credenciais configuradas.
- [x] whisper.cpp local em Docker, modelo `ggml-base.bin` e FFmpeg 9.0 disponíveis.
- [x] Codex CLI 0.146.0 autenticado, com JSON estruturado no stdout.
- [x] `%LocalAppData%\Anamnesis\config.json` preenchido com caminhos e diretórios locais.

## Execução

1. Execute `scripts\Publish-Alpha.ps1` e abra `artifacts\alpha\win-x64\tray\Anamnesis.Tray.exe`.
2. No ícone de bandeja, abra **Diagnósticos** e confirme os quatro itens disponíveis.
3. Inicie e encerre uma gravação de teste; confirme no SQLite que existe uma reunião em `AguardandoProcessamento` e um job ativo.
4. Inicie o Worker, interrompa-o durante um job reservado e inicie-o novamente; confirme que a reserva foi liberada e retomada.
5. Confirme que a transcrição local, `ata.md` e `transcricao.md` foram criadas no diretório de arquivo.
6. Confirme a simulação de retenção; avance o relógio de teste ou use uma reunião arquivada há trinta dias e confirme a ida à Lixeira somente após arquivamento.

## Resultado

Registre data, versões de OBS/Whisper/CLI, caminho do artefato publicado e o resultado de cada passo. Qualquer pré-requisito ausente bloqueia a aprovação ponta a ponta.

## Execução aprovada em 2026-08-05

- Pacote: `artifacts\alpha\win-x64-final-8`.
- Reunião: `2ac017d2c8dc440083bf7da045c6c5b8`.
- Resultado: gravação, persistência, transcrição, ata estruturada, arquivamento, simulação e Lixeira concluídos.
- Evidências: `artifacts\real-e2e\20260805-final-8\resultado.md` e logs adjacentes.
