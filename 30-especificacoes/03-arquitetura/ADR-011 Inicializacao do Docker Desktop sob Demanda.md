---
title: ADR-011 Inicializacao do Docker Desktop sob Demanda
aliases: [Docker Automatico pelo Worker]
tags: [adr, docker, whisper, worker, windows]
type: adr
created: 2026-08-05
updated: 2026-08-05
status: accepted
summary: O Worker inicia Docker Desktop somente quando um job realmente precisa do Whisper em container.
related: ["[[SPEK-026 Prontidao Automatica do Docker]]", "[[ADR-007 Fallback Docker para Whisper]]"]
---

# ADR-011 | Inicializacao do Docker Desktop sob demanda

## Contexto

O modo Docker protege a maquina do binario Whisper bloqueado pelo antivírus, mas o Worker falha quando o Docker Desktop esta instalado e o engine ainda nao foi iniciado.

## Decisao

Executar um preflight dentro do adaptador de transcricao Docker. Ele usa `docker info` para testar o engine, inicia Docker Desktop oculto quando necessario e repete a verificacao por tempo limitado. O modo nativo nao passa por esse caminho.

## Alternativas consideradas

| Alternativa | Probabilidade de adequacao | Motivo |
| --- | ---: | --- |
| Iniciar sob demanda no primeiro job | 90% | Evita falha e nao inicia Docker quando a fila esta vazia. |
| Iniciar Docker junto com o Tray | 55% | Anteciparia a espera, mas consome recursos mesmo sem transcricao. |
| Exigir inicio manual | 30% | Tem menos codigo, mas perde jobs no uso diario sem terminal. |

## Consequencias

- A primeira transcricao pode aguardar ate 120 segundos pelo engine.
- Docker Desktop permanece aberto depois do processamento.
- Nenhuma dependencia de pacote ou servico remoto e adicionada.
- Falhas de licenca ou inicializacao continuam exigindo acao do usuario e aparecem em mensagem clara.
