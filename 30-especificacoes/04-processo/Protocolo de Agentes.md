---
title: Protocolo de Agentes
aliases: [Protocolo Multi-LLM]
tags: [processo, agentes]
type: protocol
created: 2026-08-04
updated: 2026-08-04
status: evergreen
summary: Processo compartilhado para Codex, Claude, Kimi e outros agentes contribuírem sem divergência.
related: ["[[Indice de SPEKs]]", "[[Guia TDD]]", "[[Template de Entrega]]", "[[Status Alpha]]"]
---

# Protocolo de agentes

## Fechamento de entrega

Todo agente encerra a resposta com o formato de [[Template de Entrega]]. O objetivo é tornar o andamento legível sem que alguém precise interpretar commits ou perguntar pelo estado atual.

- **Feito:** uma frase objetiva.
- **Evolução estimada:** percentual ponderado da alpha e o que esse número mede.
- **Falta:** itens concretos que impedem a próxima meta testável.
- **Próximo passo sugerido:** apenas um incremento acionável, normalmente uma SPEK.

Quando a entrega mudar o caminho para a alpha, atualize [[Status Alpha]]. Percentuais representam valor entregue para a alpha; não contam quantidade de arquivos, linhas ou commits.

## Entrada obrigatória

Todo agente começa por `AGENTS.md`, `30-especificacoes/00-indice.md` e a SPEK alvo.

## Papéis possíveis

| Papel | Responsabilidade | Saída |
| --- | --- | --- |
| Especificador | esclarecer requisito e critérios | SPEK ou atualização |
| Implementador | alterar código de uma SPEK | código e testes verdes |
| Revisor | procurar falhas e lacunas | comentários com SPEK e evidência |
| Integrador | organizar commits e conflitos | histórico limpo |

## Regras de coordenação

- Um agente é dono de uma SPEK durante a implementação.
- Revisores não alteram os arquivos do implementador sem handoff explícito.
- Divergências de arquitetura viram ADR, não instruções ocultas em prompt.
- Cada entrega inclui SPEK atendida, arquivos alterados, testes e pendências.
