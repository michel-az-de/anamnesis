---
title: SPEK-017 Modo de Validacao do Tray
aliases: [Gravacao automatizada de validacao]
tags: [especificacao, tray, e2e, obs]
type: spec
created: 2026-08-05
updated: 2026-08-05
status: completed
summary: Executa uma gravação temporizada pelo Tray para validação ponta a ponta repetível.
related: ["[[SPEK-007 Gravacao de Teste com OBS]]", "[[SPEK-011 Empacotamento e Validacao Manual]]"]
---

# SPEK-017 | Modo de validação do Tray

## Objetivo

Permitir que o executável Tray realize uma gravação de teste temporizada pelo OBS, persista reunião e job e encerre com código verificável.

## Regras

- O modo é ativado somente por `--gravar-teste-segundos N`.
- A configuração pode ser isolada por `ANAMNESIS_CONFIGURACAO`, como no Worker.
- O modo usa o mesmo `ControlarGravacaoHandler` da interface de bandeja.
- Sucesso retorna código zero e registra início, reunião e conclusão no stdout; falha retorna código um no stderr.
- Sem o argumento, o comportamento visual do Tray permanece inalterado.

## Critérios de aceite

- [x] Argumento ausente mantém o modo bandeja.
- [x] Duração inválida é rejeitada sem iniciar gravação.
- [x] Teste black box inicia o Tray em processo separado contra OBS local temporário e confirma reunião/job SQLite.
- [x] Execução real controla OBS instalado e produz gravação com caminho persistido.

## Evidência real

- Reunião `bda72419-0e85-44b7-80be-cb76c3ecbd89` persistida como `AguardandoProcessamento`.
- Gravação OBS real: `C:\Users\felip\Videos\2026-08-05 10-22-45.mp4`, 11,9 segundos, H.264 e áudio AAC.
- Logs: `artifacts\real-e2e\20260805-01\tray.stdout.log`.

## Decisões pendentes

- O modo permanece disponível na alpha como ferramenta de diagnóstico local.
