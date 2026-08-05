---
title: SPEK-025 Prontidao Automatica do OBS
aliases: [Inicializacao Automatica do OBS, OBS Pronto para Gravar]
tags: [especificacao, beta, obs, tray, windows]
type: spec
created: 2026-08-05
updated: 2026-08-05
status: completed
summary: Garante que o OBS esteja executando e com o websocket disponivel antes de iniciar uma gravacao pelo Tray.
related: ["[[SPEK-007 Gravacao de Teste com OBS]]", "[[SPEK-024 Captura Universal de Audio pelo OBS]]", "[[ADR-010 Inicializacao do OBS sob Demanda]]"]
---

# SPEK-025 | Prontidao automatica do OBS

## Objetivo

Permitir que o usuario inicie uma gravacao pelo Tray mesmo quando o OBS estiver fechado, abrindo o OBS minimizado e aguardando o websocket ficar disponivel.

## Fora de escopo

- Instalar ou atualizar o OBS.
- Alterar senha ou ativar o websocket pela interface grafica.
- Iniciar Docker Desktop.
- Detectar automaticamente reunioes.

## Regras

- `IObsPreflight` e a fronteira substituivel chamada antes de `IGravador.IniciarAsync`.
- Se o endpoint OBS ja estiver acessivel, nenhum processo e iniciado.
- Se estiver inacessivel, o adaptador localiza `obs64.exe` pela configuracao ou pelo caminho padrao do Windows.
- O OBS e iniciado com `--minimize-to-tray` e diretorio de trabalho igual ao diretorio do executavel.
- O preflight aguarda a porta do websocket por no maximo 30 segundos.
- Ausencia do executavel ou timeout produz erro claro e nao inicia a gravacao.
- Testes automatizados nao iniciam OBS real nem fazem rede externa.

## Criterios de aceite

- [x] Teste falha antes da implementacao ao exigir o preflight antes do gravador.
- [x] OBS acessivel nao inicia outro processo.
- [x] OBS fechado inicia o executavel configurado e aguarda o endpoint.
- [x] Caminho ausente e timeout retornam mensagens acionaveis.
- [x] Falha do preflight persiste a reuniao como falha e nao chama `StartRecord`.
- [x] Suite Release continua verde: 62 testes.
- [x] Ensaio real com OBS inicialmente fechado inicia, grava e encerra pelo Tray.

## Evidencias esperadas

- `src\Anamnesis.Application\Contracts\IObsPreflight.cs`
- `src\Anamnesis.Infrastructure\Obs\ObsProcessPreflight.cs`
- testes de Application e Infrastructure.
- `artifacts\obs-preflight-e2e\`.

## Resultado

- E2E de preflight: `artifacts\obs-preflight-e2e\20260805-real-04\resultado.md`.
- O OBS estava fechado e foi iniciado pelo Tray como PID `23676`.
- O fluxo concluiu com Tray `0`, Worker `0`, MP4 com audio AAC, Whisper local, ata e arquivamento.
- E2E de voz complementar: `artifacts\obs-audio-e2e\20260805-real-06\resultado.md`.
- A frase sintetizada foi reconhecida como `A nao-nesis esta gravando o audio desta reuniao com o sucesso.`.
- Interrupcoes forcadas do OBS podem abrir o dialogo oficial de recuperacao; o timeout informa que o websocket precisa ser ativado ou liberado pelo usuario.

## Decisoes pendentes

- Docker Desktop sob demanda sera tratado na SPEK-026.
