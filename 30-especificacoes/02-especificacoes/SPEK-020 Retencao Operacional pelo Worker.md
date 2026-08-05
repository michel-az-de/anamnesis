---
title: SPEK-020 Retencao Operacional pelo Worker
aliases: [Retencao Manual da Alpha]
tags: [especificacao, worker, retencao, lixeira]
type: spec
created: 2026-08-05
updated: 2026-08-05
status: completed
summary: Expoe simulacao e aplicacao explicita da retencao segura no Worker publicado.
related: ["[[SPEK-003 Retencao de Gravacao]]", "[[SPEK-011 Empacotamento e Validacao Manual]]"]
---

# SPEK-020 | Retenção operacional pelo Worker

## Objetivo

Permitir validar e executar a política de retenção da alpha por uma operação local, explícita e auditável.

## Regras

- `--retencao-simular` avalia uma reunião sem alterar estado ou arquivo.
- `--retencao-aplicar` exige também `--confirmar-lixeira` para mover a gravação.
- `--reuniao` identifica exatamente um agregado e `--agora` aceita um instante ISO 8601 para ensaio controlado.
- A operação usa `RetencaoGravacaoHandler`, `SqliteReuniaoRepository` e `LixeiraWindows`; o Worker não decide elegibilidade.
- Saída e código de processo registram resultado suficiente para evidência.

## Critérios de aceite

- [x] Argumentos inválidos ou aplicação sem confirmação são rejeitados.
- [x] A simulação informa elegibilidade e caminho sem mover a gravação.
- [x] A aplicação elegível move a gravação para a Lixeira e persiste `Excluida`.
- [x] O pacote publicado executa simulação e aplicação reais com logs preservados.

## Evidência real

- Pacote `artifacts\alpha\win-x64-final-8`.
- Reunião `2ac017d2c8dc440083bf7da045c6c5b8` simulada e aplicada com código de saída zero.
- Logs em `artifacts\real-e2e\20260805-final-8`.

## Decisões pendentes

- Agendamento automático e processamento em lote ficam para depois da alpha.
