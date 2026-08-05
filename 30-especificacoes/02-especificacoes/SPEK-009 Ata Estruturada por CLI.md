---
title: SPEK-009 Ata Estruturada por CLI
aliases: [Ata Estruturada por CLI]
tags: [especificacao, llm, cli, ata]
type: spec
created: 2026-08-04
updated: 2026-08-04
status: completed
summary: Adaptador de CLI autenticada que retorna JSON validado para a ata.
related: ["[[SPEK-002 Geracao de Ata]]", "[[ADR-002 Providers por CLI]]"]
---

# SPEK-009 — Ata estruturada por CLI

## Objetivo

Executar uma CLI de LLM já autenticada, enviar o contexto da reunião por entrada padrão e converter exclusivamente JSON válido em `AtaGerada`.

## Fora de escopo

- Automatizar interfaces web de provedores.
- Escolher ou instalar uma CLI de LLM.
- Seleção entre múltiplos provedores, divisão de transcrição ou fallback local.

## Regras

- `CliAtaRunner` implementa `IAtaRunner`; a configuração declara executável, argumentos e nome do provider.
- A CLI recebe JSON pela entrada padrão e deve emitir no stdout apenas o JSON da ata.
- O JSON deve conter `resumoExecutivo`, `decisoes` e `tarefas`; JSON malformado ou campos ausentes falham antes de alterar a reunião.
- A CLI não recebe autoridade para decidir estado de reunião, arquivamento ou exclusão de arquivos.
- Testes unitários validam o parser e não executam CLI real.

## Critérios de aceite

- [x] JSON de uma CLI válida é convertido para `AtaGerada` com decisões e tarefas.
- [x] Saída inválida ou incompleta produz erro explícito.
- [x] A execução usa redirecionamento de stdin/stdout sem shell.
- [x] Nenhum teste automatizado chama uma CLI autenticada real.

## Testes associados

- `AtaEstruturadaJsonTests.DeveConverterJsonValidoEmAtaGerada`
- `AtaEstruturadaJsonTests.DeveRejeitarJsonInvalido`
- `AtaEstruturadaJsonTests.DeveRejeitarResumoAusente`

## Decisões pendentes

- Política de priorização e fallback entre CLIs permanece na SPEK-002.
