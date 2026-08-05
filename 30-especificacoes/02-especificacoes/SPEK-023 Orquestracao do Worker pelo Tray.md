---
title: SPEK-023 Orquestracao do Worker pelo Tray
aliases: [Worker Automatico pelo Tray, Processamento apos Gravacao]
tags: [especificacao, beta, tray, worker, processo]
type: spec
created: 2026-08-05
updated: 2026-08-05
status: completed
summary: Inicia o Worker automaticamente depois da gravacao e ao abrir o Tray, preservando jobs quando o processo falha.
related: ["[[SPEK-006 Worker e Retomada de Processamento]]", "[[SPEK-010 Tray Configuracao e Diagnosticos]]", "[[SPEK-022 Instalador da Beta em Windows Limpo]]"]
---

# SPEK-023 | Orquestracao do Worker pelo Tray

## Objetivo

Permitir que o usuario use somente o Tray: ao abrir o aplicativo ou finalizar uma gravacao, o Worker local e iniciado para consumir jobs pendentes.

## Fora de escopo

- Detectar automaticamente se Teams, Meet, Zoom ou outro aplicativo entrou em reuniao.
- Configurar fontes de audio do OBS sem participacao do usuario.
- Transformar o Worker em Servico do Windows.
- Executar dois processamentos pesados em paralelo.

## Regras

- `IWorkerLauncher` e a fronteira substituivel para iniciar o processo local.
- A reuniao e o job sao persistidos antes de solicitar a inicializacao do Worker.
- Falha ao iniciar o Worker nao remove o job nem altera a reuniao para `Falha`.
- O Tray tenta iniciar o Worker ao abrir, retomando jobs de uma sessao anterior.
- O Tray inicia o Worker novamente depois de finalizar e enfileirar uma gravacao.
- O processo usa `UseShellExecute=false`, janela oculta e herda o caminho da configuracao por `ANAMNESIS_CONFIGURACAO`.
- O caminho padrao usa o layout instalado `tray\..\worker\Anamnesis.Worker.exe`.
- `ANAMNESIS_WORKER_EXECUTAVEL` permite substituir o caminho para desenvolvimento e testes.
- O modo `--gravar-teste-segundos` so inicia o Worker quando recebe tambem `--iniciar-worker`.
- Testes automatizados usam OBS, Whisper e CLI fakes, sem rede ou provedores reais.

## Criterios de aceite

- [x] Teste de aplicacao comprova que o Worker e solicitado somente depois do job ser enfileirado.
- [x] Teste de regressao comprova que uma falha do launcher preserva reuniao e job pendentes.
- [x] Testes validam o caminho instalado, a configuracao herdada e a opcao `--iniciar-worker`.
- [x] Black box comprova Tray, OBS fake, SQLite, processo Worker, transcricao fake, ata fake e arquivamento.
- [x] O smoke do instalador continua verde com Tray e Worker no layout publicado.
- [x] Os 49 testes anteriores continuam verdes, alem dos novos testes da SPEK.

## Evidencias esperadas

- `src\Anamnesis.Application\Contracts\IWorkerLauncher.cs`
- `src\Anamnesis.Infrastructure\Processos\WorkerProcessLauncher.cs`
- `tests\Anamnesis.Application.Tests\ControlarGravacaoHandlerTests.cs`
- `tests\Anamnesis.Infrastructure.Tests\TrayBlackBoxE2ETests.cs`
- `artifacts\tray-worker-e2e\`

## Execucao local

- Red: solução não compilou sem `IWorkerLauncher`, `WorkerProcessLauncher` e `IniciarWorker`.
- Green: 53 testes Release aprovados.
- Black box: `artifacts\tray-worker-e2e\20260805-final\resultado.md`.
- Estado final da reunião: `Arquivada`.
- Instalador: `artifacts\installer-e2e\20260805-spek023\resultado.md`.
- SHA-256 local: `fb4c3fe0341986cf3db880f7a8ace295505e4263f313132efce13585ab59e453`.

## Execucao em Windows limpo

- Workflow: `https://github.com/michel-az-de/anamnesis/actions/runs/31020179379`.
- Resultado: 53 testes, build, instalacao, smoke e desinstalacao aprovados.
- Duracao: 2 minutos e 52 segundos.
- Artefato: `8936332244`.

## Decisoes pendentes

- A deteccao de reuniao e a configuracao assistida de audio serao tratadas em SPEKs separadas depois deste fluxo manual confiavel.
