---
title: SPEK-002 Geracao de Ata
aliases: [Geração de Ata]
tags: [especificacao, ia, ata]
type: spec
created: 2026-08-04
updated: 2026-08-04
status: draft
summary: Geração previsível de ata com LLMs autenticadas por assinatura ou fallback local.
related: ["[[ADR-002 Providers por CLI]]", "[[SPEK-001 Ciclo de Reuniao]]", "[[SPEK-037 Mapeamento de Tarefas Externas]]"]
---

# SPEK-002 | Geração de ata

## Objetivo

Transformar uma transcrição em dados estruturados e renderizar a ata por template determinístico.

## Regras

- A LLM retorna JSON válido; ela não escreve diretamente o arquivo final.
- Cada decisão, tarefa e prazo deve carregar pelo menos uma evidência com timestamp.
- Evidências possuem identificador no JSON e cada tarefa referencia explicitamente uma ou mais delas.
- Depois da validação, o Anamnesis atribui `TarefaLocalId` persistido. A LLM nunca escolhe identidade local ou remota.
- A transcrição longa é dividida em trechos antes da consolidação.
- Providers são tentados na ordem configurada e uma falha de quota não é repetida no mesmo provider.
- O fallback local é chamado quando nenhum provider de assinatura conclui a tarefa.

## Saída mínima

```json
{
  "resumoExecutivo": "string",
  "decisoes": ["string"],
  "tarefas": [{ "descricao": "string", "responsavel": "string|null", "prazo": "YYYY-MM-DD|null", "evidenciaIds": ["e1"] }],
  "evidencias": [{ "id": "e1", "inicio": "HH:MM:SS", "fim": "HH:MM:SS", "trecho": "string" }]
}
```

## Critérios de aceite

- JSON inválido não pode ser arquivado como ata.
- A ata Markdown é gerada somente a partir do JSON validado.
- Tarefa sem evidência válida é rejeitada antes de receber identidade local.
- Reprocessar uma ata não altera silenciosamente a identidade de tarefa já publicada.
- Testes automatizados usam um `AtaRunnerFake`, nunca um CLI real.
