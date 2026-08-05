---
title: SPEK-012 Host Local do Worker
aliases: [Host do Worker]
tags: [especificacao, worker, composicao]
type: spec
created: 2026-08-05
updated: 2026-08-05
status: completed
summary: Executável Worker que compõe os adaptadores locais e esvazia a fila pendente.
related: ["[[SPEK-006 Worker e Retomada de Processamento]]", "[[SPEK-010 Tray Configuracao e Diagnosticos]]"]
---

# SPEK-012 — Host local do Worker

## Objetivo

Compor repositório, fila, Whisper, CLI e arquivador a partir da configuração local para processar jobs pendentes em uma execução do Worker.

## Fora de escopo

- Serviço permanente do Windows, agendador ou paralelismo.
- Política de repetição para reunião em `Falha`.
- Alterar ou instalar dependências externas.

## Regras

- O Worker usa `%LocalAppData%\Anamnesis\config.json`, a mesma fonte do Tray.
- Na inicialização, reservas ativas são liberadas; em seguida, jobs são consumidos sequencialmente até a fila esvaziar.
- Falhas de um job são exibidas no stderr e fazem o processo retornar código diferente de zero.
- Executar o Worker sem jobs retorna sucesso sem chamar Whisper ou CLI.

## Critérios de aceite

- [x] O executável compõe os adaptadores locais a partir de `ConfiguracaoAnamnesis`.
- [x] Uma execução libera reservas e esvazia a fila de jobs processáveis.
- [x] Uma falha de processamento resulta em código de saída não nulo.

## Decisões pendentes

- Serviço do Windows e operação contínua ficam para a beta.

## Evidência de execução

- `Anamnesis.Worker.exe` publicado executou sem jobs e retornou código `0`.
