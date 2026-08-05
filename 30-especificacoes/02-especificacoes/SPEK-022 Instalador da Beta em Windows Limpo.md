---
title: SPEK-022 Instalador da Beta em Windows Limpo
aliases: [Instalador da Beta, Validacao em Windows Limpo]
tags: [especificacao, beta, instalador, windows, ci]
type: spec
created: 2026-08-05
updated: 2026-08-05
status: completed
summary: Gera um instalador EXE por usuario e valida instalacao, execucao e desinstalacao em Windows descartavel.
related: ["[[SPEK-011 Empacotamento e Validacao Manual]]", "[[ADR-008 Instalador por Usuario com Inno Setup]]"]
---

# SPEK-022 | Instalador da beta em Windows limpo

## Objetivo

Distribuir Tray e Worker em um instalador Windows x64 reproduzivel e comprovar o ciclo de instalacao em uma maquina limpa.

## Fora de escopo

- Assinatura digital e reputacao SmartScreen.
- Atualizacao automatica.
- Instalar OBS, Docker, FFmpeg, modelos Whisper ou CLIs autenticadas.
- Hospedar o Worker como Servico do Windows.

## Regras

- A versao inicial do pacote e `0.1.0-beta.1`.
- A publicacao continua `Release`, `win-x64` e autocontida.
- O instalador usa Inno Setup 6.7.3 fixado e verificado por hash no CI.
- A instalacao padrao ocorre por usuario em `%LocalAppData%\Programs\Anamnesis` sem solicitar elevacao.
- O Menu Iniciar oferece atalhos separados para Tray e Worker.
- A desinstalacao remove somente binarios e atalhos; banco, configuracao e arquivos em `%LocalAppData%\Anamnesis` sao preservados.
- O smoke instala silenciosamente em diretorio isolado, inicia Worker e Tray publicados, desinstala e preserva logs.
- O GitHub Actions usa uma VM Windows nova, executa testes, build, instalacao, smoke e desinstalacao.

## Criterios de aceite

- [x] O teste de empacotamento falha quando o instalador ou os payloads obrigatorios nao existem.
- [x] O build produz um unico `Anamnesis-0.1.0-beta.1-win-x64-setup.exe`.
- [x] A instalacao silenciosa cria Tray, Worker e desinstalador no destino isolado.
- [x] Worker retorna sucesso com fila vazia e Tray permanece ativo no smoke sem chamar OBS.
- [x] A desinstalacao remove o diretorio do programa e preserva dados do usuario.
- [x] O workflow conclui em uma VM Windows nova e publica instalador e logs como artefatos.

## Evidencias esperadas

- `installer\Anamnesis.iss`
- `scripts\Build-Installer.ps1`
- `scripts\Test-Installer.ps1`
- `.github\workflows\beta-installer.yml`
- `artifacts\installer-e2e\`

## Execucao local

- Red: `Test-Installer.ps1` recusou o caminho sem instalador.
- Red: Inno Setup recusou a versao de arquivo com sufixo de prerelease.
- Green: build produziu o EXE unico e o smoke local terminou com codigos zero.
- Evidencia: `artifacts\installer-e2e\20260805-local-final\resultado.md`.
- Testes: 49 testes Release aprovados.

## Execucao em Windows limpo

- Workflow: `https://github.com/michel-az-de/anamnesis/actions/runs/31017790651`.
- Runner: Windows `10.0.26100`, cultura base `en-US`.
- Duracao: 2 minutos e 45 segundos.
- Instalacao, Worker, Tray e desinstalacao terminaram com codigo zero.
- SHA-256 remoto: `fb47d5818cb2736d145fe2c4bc248010c6b83b267a3c6f6762ccbb2860fe2375`.
- Artefato: `anamnesis-0.1.0-beta.1-win-x64`, retido por 14 dias no GitHub Actions.
- Evidencia baixada: `artifacts\github-actions\31017790651\`.

## Decisoes pendentes

- Certificado de assinatura de codigo e canal de atualizacao ficam para a proxima SPEK de distribuicao.
