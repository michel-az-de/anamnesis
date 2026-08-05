---
title: SPEK-024 Captura Universal de Audio pelo OBS
aliases: [Audio Universal no OBS, Sistema e Microfone]
tags: [especificacao, beta, obs, audio, windows]
type: spec
created: 2026-08-05
updated: 2026-08-05
status: completed
summary: Prepara uma cena OBS idempotente que grava o audio do sistema e o microfone padrao em qualquer plataforma de reuniao.
related: ["[[SPEK-007 Gravacao de Teste com OBS]]", "[[SPEK-023 Orquestracao do Worker pelo Tray]]", "[[ADR-009 Cena OBS Gerenciada pelo Anamnesis]]"]
---

# SPEK-024 | Captura universal de audio pelo OBS

## Objetivo

Garantir que uma gravacao iniciada pelo Tray contenha o audio reproduzido pelo Windows e o microfone padrao, sem configuracao especifica para Teams, Meet, Zoom ou navegador.

## Fora de escopo

- Detectar automaticamente o inicio de uma reuniao.
- Selecionar dispositivos diferentes dos padroes do Windows.
- Capturar somente um aplicativo e excluir os demais sons do sistema.
- Alterar configuracoes de streaming ou credenciais do OBS.

## Regras

- O adaptador usa somente obs-websocket v5 e a cena `Anamnesis` definida no ADR-009.
- Antes de `StartRecord`, consulta cenas e entradas existentes.
- Cria a cena somente quando ausente.
- Cria `Anamnesis | Audio do sistema` com `wasapi_output_capture` e `device_id=default` somente quando ausente.
- Cria `Anamnesis | Microfone` com `wasapi_input_capture` e `device_id=default` somente quando ausente.
- Guarda a cena atual, seleciona `Anamnesis`, inicia a gravacao e restaura a cena anterior depois de `StopRecord`.
- Falhas de preparacao impedem `StartRecord` e sao registradas no ciclo da reuniao.
- Nenhum teste automatizado chama OBS real, rede externa ou CLI real.

## Criterios de aceite

- [x] Teste falha antes da implementacao ao esperar a sequencia de preparacao antes de `StartRecord`.
- [x] Cena e fontes ausentes sao criadas com nomes e tipos definidos.
- [x] Uma segunda gravacao reutiliza cena e fontes sem duplicar.
- [x] A cena anterior e restaurada depois de encerrar ou falhar.
- [x] Eventos assincronos do OBS sao ignorados ate a resposta correlacionada pelo `requestId`.
- [x] Testes anteriores continuam verdes: 57 testes em Release.
- [x] Ensaio real grava uma frase reproduzida no audio do Windows e gera transcricao local nao vazia.

## Evidencias esperadas

- `src\Anamnesis.Infrastructure\Obs\ObsGravador.cs`
- `tests\Anamnesis.Infrastructure.Tests\ObsGravadorTests.cs`
- `artifacts\obs-audio-e2e\`

## Resultado

- E2E real: `artifacts\obs-audio-e2e\20260805-real-04\resultado.md`.
- Gravacao: MP4 com 12.260.229 bytes e audio AAC, 48 kHz, 2 canais.
- Whisper local: reconheceu `Amanhas esta gravando o audio desta reuniao com sucesso.`.
- Codex CLI: gerou `ata.md` estruturada e o Worker arquivou a reuniao.
- OBS real: cena anterior `Cena` restaurada e fontes gerenciadas reutilizadas sem duplicacao.
- Regressao real corrigida: eventos `InputCreated` podem chegar antes da resposta `CreateInput`.

## Decisoes pendentes

- A escolha manual de dispositivos e a deteccao automatica de reunioes ficam para incrementos posteriores.
