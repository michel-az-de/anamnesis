---
title: ADR-010 Inicializacao do OBS sob Demanda
aliases: [OBS Automatico pelo Tray]
tags: [adr, obs, tray, windows, processos]
type: adr
created: 2026-08-05
updated: 2026-08-05
status: accepted
summary: O Tray inicia o OBS local minimizado somente quando o websocket ainda nao esta disponivel.
related: ["[[SPEK-025 Prontidao Automatica do OBS]]", "[[ADR-004 Integracao com OBS]]"]
---

# ADR-010 | Inicializacao do OBS sob demanda

## Contexto

O fluxo atual funciona quando o OBS ja esta aberto. Para uso diario, exigir que o usuario prepare manualmente o OBS antes de cada reuniao gera falhas evitaveis.

## Decisao

Adicionar um preflight na fronteira OBS. Ele primeiro testa a porta configurada. Quando indisponivel, inicia o executavel local com `--minimize-to-tray` e aguarda o websocket por tempo limitado. O caminho pode ser configurado e possui descoberta pelo local padrao do instalador no Windows.

## Alternativas consideradas

| Alternativa | Probabilidade de adequacao | Motivo |
| --- | ---: | --- |
| Iniciar sob demanda antes de gravar | 90% | Reduz passos manuais e nao mantem o OBS aberto sem necessidade. |
| Exigir OBS aberto pelo usuario | 35% | Tem menos codigo, mas falha no momento mais critico da reuniao. |
| Iniciar OBS junto com o Windows | 60% | E simples para o usuario, mas consome recursos mesmo sem reuniao. |

## Consequencias

- O Tray passa a iniciar um processo local conhecido, sem nova dependencia de pacote.
- O OBS permanece aberto depois da gravacao para evitar encerramento inseguro.
- Falhas de instalacao ou websocket aparecem antes de tentar `StartRecord`.
- O processo nao e encerrado automaticamente pelo Anamnesis.
