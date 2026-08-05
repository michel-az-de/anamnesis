---
title: SPEK-026 Prontidao Automatica do Docker
aliases: [Docker Desktop sob Demanda, Docker Pronto para Whisper]
tags: [especificacao, beta, docker, whisper, worker, windows]
type: spec
created: 2026-08-05
updated: 2026-08-05
status: completed
summary: Inicia Docker Desktop sob demanda e aguarda o engine antes de executar Whisper em container.
related: ["[[SPEK-018 Whisper Local em Docker]]", "[[SPEK-025 Prontidao Automatica do OBS]]", "[[ADR-011 Inicializacao do Docker Desktop sob Demanda]]"]
---

# SPEK-026 | Prontidao automatica do Docker

## Objetivo

Permitir que um job de transcricao em modo Docker seja processado mesmo quando Docker Desktop estiver fechado no inicio do Worker.

## Fora de escopo

- Instalar ou atualizar Docker Desktop.
- Aceitar licenca, autenticar ou alterar configuracoes da interface grafica.
- Modificar o modo nativo do Whisper.
- Manter Docker aberto quando nao existem jobs.

## Regras

- O preflight e executado pelo `WhisperTranscritor` somente quando `ImagemDocker` estiver configurada e houver job.
- `docker info` verifica se o engine ja esta disponivel.
- Se indisponivel, o adaptador inicia Docker Desktop em segundo plano pelo caminho configurado ou padrao.
- O adaptador aguarda o engine por no maximo 120 segundos.
- Caminho ausente, falha de processo e timeout produzem mensagens acionaveis.
- O Anamnesis nao encerra Docker Desktop depois da transcricao.
- Testes automatizados nao iniciam Docker real nem fazem rede externa.

## Criterios de aceite

- [x] Teste falha antes da implementacao ao exigir preflight no modo Docker.
- [x] Modo nativo nao executa preflight.
- [x] Engine disponivel nao inicia Docker Desktop novamente.
- [x] Engine indisponivel inicia Docker Desktop oculto e aguarda prontidao.
- [x] Caminho ausente e timeout retornam mensagens acionaveis.
- [x] Suite Release continua verde: 66 testes.
- [x] E2E real processa um job com Docker inicialmente parado.

## Evidencias esperadas

- `src\Anamnesis.Infrastructure\Whisper\DockerProcessPreflight.cs`.
- testes do preflight e do transcritor.
- `artifacts\docker-preflight-e2e\`.

## Resultado

- E2E real: `artifacts\docker-preflight-e2e\20260805-real-02\resultado.md`.
- Docker Desktop foi parado pelo comando oficial antes do Worker.
- O Worker iniciou Docker Desktop, aguardou o engine e executou Whisper local.
- A frase foi reconhecida como `a nome desses esta gravando o audio desta reuniao com sucesso`.
- Tray `0`, Worker `0`, Docker voltou a `running`, ata e transcricao foram arquivadas.
- A corrida de limpeza do SQLite no teste black box da CI recebeu espera limitada e passou cinco rodadas locais consecutivas.

## Decisoes pendentes

- Binario nativo assinado continua preferivel para uma distribuicao futura sem Docker.
