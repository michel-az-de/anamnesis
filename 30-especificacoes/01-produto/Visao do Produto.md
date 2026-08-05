---
title: Visao do Produto
aliases: [Visão do Produto]
tags: [produto, visao]
type: note
created: 2026-08-04
updated: 2026-08-05
status: evergreen
summary: Propósito, limites e princípios de privacidade do Anamnesis.
related: ["[[SPEK-001 Ciclo de Reuniao]]", "[[SPEK-002 Geracao de Ata]]", "[[Roadmap de Produto]]"]
---

# Visão do produto

O Anamnesis é um aplicativo Windows local que transforma reuniões em memória organizada: gravação, transcrição, ata estruturada, tarefas e retenção segura.

## Princípios de produto

- Áudio e transcrição são locais por padrão.
- Modelos externos recebem somente texto e apenas quando permitidos pela política da reunião.
- Toda decisão ou tarefa extraída pela IA precisa ter evidência na transcrição.
- A gravação só entra em retenção após ata e transcrição arquivadas com sucesso.
- O usuário pode trocar de provider sem alterar a regra de negócio.
- Integrações externas são opcionais e não podem impedir o fluxo local.
- Calendários começam somente leitura; tarefas externas exigem revisão e confirmação humana.
- Credenciais externas ficam protegidas pelo usuário Windows e nunca aparecem em logs ou no SQLite comum.
- O Anamnesis nunca exclui ou conclui conteúdo remoto automaticamente.

## Evolução planejada

O [[Roadmap de Produto]] separa a evolução em Desktop real, observabilidade, captura instantânea, contexto de agenda e publicação aprovada em ferramentas de conhecimento e tarefas.
