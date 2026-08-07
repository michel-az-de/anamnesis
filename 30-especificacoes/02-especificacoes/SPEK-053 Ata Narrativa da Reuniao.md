---
title: SPEK-053 Ata Narrativa da Reuniao
aliases: [Resumo Narrativo, Ata de Escrivao]
tags: [especificacao, ata, llm, narrativa]
type: spec
created: 2026-08-07
updated: 2026-08-07
status: completed
summary: Gera um relato factual da reuniao com contexto, desenvolvimento, encerramento, duracao e definicoes.
related: ["[[SPEK-009 Ata Estruturada por CLI]]", "[[SPEK-019 Ata Markdown Estruturada]]"]
---

# SPEK-053 | Ata narrativa da reuniao

## Objetivo

Fazer o resumo executivo funcionar como relato de escrivao, situando a reuniao e narrando o que foi discutido e definido.

## Regras

- O relato informa titulo, data, duracao, quantidade estimada de locutores quando disponivel, assuntos, desenvolvimento, conclusao e definicoes.
- O texto e factual, em terceira pessoa, sem inventar nomes, presencas, consenso ou falas ausentes.
- Quando um dado nao estiver disponivel, ele e omitido ou marcado como nao identificado.
- Decisoes e tarefas continuam estruturadas no JSON e renderizadas deterministicamente por C#.
- O resumo narrativo tem entre um e quatro paragrafos curtos.

## Criterios de aceite

- [x] Prompt pede relato factual em terceira pessoa e ordem cronologica.
- [x] Prompt inclui titulo, data e duracao como contexto verificavel.
- [x] Contrato proibe inventar participantes e atribuicoes.
- [x] Teste confirma o contrato narrativo enviado a CLI.
- [x] Suite Release permanece verde.

## Evidencias

- `CliAtaRunnerTests.DeveSolicitarAtaNarrativaComContextoVerificavelSemInventarParticipantes` registrou Red pela ausencia de duracao e passou apos o novo contrato.
- Suite Release: 297 testes verdes.
- Versao instalada `0.2.0-beta.8-local.1`; Worker real retomou um job pendente, concluiu o processamento e encerrou com fila vazia e codigo 0.
