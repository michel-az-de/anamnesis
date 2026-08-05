---
title: SPEK-010 Tray Configuracao e Diagnosticos
aliases: [Tray e Diagnósticos]
tags: [especificacao, tray, configuracao, diagnostico]
type: spec
created: 2026-08-05
updated: 2026-08-05
status: completed
summary: Tray mínimo para disparar gravação de teste e indicar pré-requisitos locais.
related: ["[[SPEK-007 Gravacao de Teste com OBS]]", "[[SPEK-008 Transcricao Local com Whisper]]", "[[SPEK-009 Ata Estruturada por CLI]]"]
---

# SPEK-010 — Tray, configuração e diagnósticos

## Objetivo

Disponibilizar uma bandeja do Windows que inicia ou encerra uma gravação de teste e apresenta o diagnóstico dos pré-requisitos locais.

## Fora de escopo

- Editor gráfico completo de configurações.
- Instalar OBS, Whisper, modelos ou CLI de LLM.
- Executar a transcrição, a ata ou a retenção pelo Tray.

## Regras

- A configuração fica em JSON local editável em `%LocalAppData%\Anamnesis\config.json`; a senha do OBS não é exibida pelo Tray.
- Na primeira abertura, o arquivo é criado com valores padrão e caminhos vazios para Whisper e CLI.
- O diagnóstico valida endereço OBS configurado e existência local de executável/modelo Whisper e CLI, sem invocar esses programas.
- O menu oferece Diagnósticos, Abrir configuração, Iniciar gravação de teste, Encerrar gravação e Sair.
- Iniciar e encerrar usam `ControlarGravacaoHandler`; os erros são mostrados ao usuário sem encerrar o Tray.

## Critérios de aceite

- [x] A configuração padrão é persistida e pode ser carregada novamente.
- [x] O diagnóstico informa cada pré-requisito ausente sem executar processo, rede ou OBS.
- [x] O Tray compõe OBS, SQLite e o fluxo de gravação a partir da configuração local.
- [x] O Tray permite iniciar e encerrar uma gravação de teste pelo menu.

## Testes associados

- `ArquivoConfiguracaoTests.DeveCriarECarregarConfiguracaoPadrao`
- `DiagnosticosLocaisTests.DeveIndicarDependenciasAusentesSemExecutaLas`

## Decisões pendentes

- A edição assistida dos campos e proteção adicional da senha do OBS ficam para a beta.
