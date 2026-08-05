---
title: SPEK-014 Ensaio E2E com Evidencias
aliases: [Ensaio E2E auditavel]
tags: [especificacao, e2e, evidencias]
type: spec
created: 2026-08-05
updated: 2026-08-05
status: completed
summary: Executa o fluxo E2E hermético e preserva evidências auditáveis de cada etapa.
related: ["[[SPEK-013 E2E Hermetico da Alpha]]", "[[Roteiro de Validacao Alpha]]"]
---

# SPEK-014 — Ensaio E2E com evidências

## Objetivo

Permitir executar o fluxo E2E hermético da alpha como um ensaio operacional que preserva evidências verificáveis fora do diretório temporário.

## Regras

- A execução precisa registrar, em arquivo de log, o início e o resultado de cada etapa do fluxo.
- As evidências preservadas devem incluir o banco SQLite, a gravação de entrada, a transcrição, a ata, o JSON enviado à CLI e um resumo legível do resultado.
- O ensaio continua hermético: sem rede externa, OBS instalado, Whisper real ou CLI autenticada real.
- O diretório de evidências é informado explicitamente pelo executor e não pode ser apagado pelo teste.

## Critérios de aceite

- [x] Um comando reproduzível executa o ensaio e retorna código zero.
- [x] `e2e.log` identifica as etapas de gravação, retomada, transcrição, ata, arquivamento e retenção.
- [x] O diretório final contém banco SQLite, gravação, ata, transcrição e entrada da CLI.
- [x] `resultado.md` mostra os identificadores, o estado final e os caminhos dos artefatos.

## Execução

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Executar-EnsaioE2E.ps1
```

O comando cria um diretório novo em `artifacts\e2e\` e preserva as evidências mesmo se o ensaio falhar.

## Decisões pendentes

- A evidência continua demonstrando o contrato dos adaptadores locais; a certificação com binários reais pertence à SPEK-011.
