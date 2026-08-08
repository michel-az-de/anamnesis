---
title: SPEK-056 Busca Local no Conteudo das Reunioes
aliases: [SPEK-056, Busca Local Completa, Memoria Pesquisavel]
tags: [especificacao, busca, sqlite, desktop, pos-alpha]
type: spek
created: 2026-08-07
updated: 2026-08-07
status: completed
summary: Pesquisa localmente titulo, resumo, decisoes, tarefas e transcricao e abre a secao correspondente.
related: ["[[SPEK-030 Desktop com Dados Reais]]", "[[Roadmap de Produto]]"]
---

# SPEK-056 | Busca local no conteudo das reunioes

## Objetivo

Transformar o historico em memoria recuperavel, permitindo localizar uma reuniao pelo conteudo sem enviar texto para rede ou modelo externo.

## Regras

- A busca consulta titulo, resumo executivo, decisoes, tarefas e transcricao no SQLite local.
- Resultado contem somente resumo da reuniao, secao correspondente e um trecho curto; o conteudo completo continua carregado apenas ao abrir o detalhe.
- A ordem permanece da reuniao mais recente para a mais antiga.
- Filtros de estado e periodo sao combinaveis com o texto.
- Uma correspondencia abre a aba mais relevante: resumo, transcricao, decisoes ou tarefas.
- Campo vazio preserva a listagem normal e nao carrega texto extenso.
- A interface aplica pequena espera cancelavel antes da consulta para nao pesquisar a cada tecla.
- Nenhum texto pesquisado entra no journal operacional.
- O primeiro corte usa SQLite existente, sem modelo semantico, rede ou dependencia nova.

## Criterios de aceite

- [x] Termo presente apenas na transcricao encontra a reuniao e sugere a aba Transcricao.
- [x] Termo presente apenas no resumo, decisao ou tarefa encontra a reuniao e sugere a secao correta.
- [x] Titulo continua pesquisavel.
- [x] Estado e periodo restringem os resultados.
- [x] Resultado exibe trecho curto sem retornar a transcricao completa no read model.
- [x] Limite de cem resultados e ordenacao decrescente permanecem.
- [x] Limpar o campo restaura a lista normal.
- [x] Testes nao acessam rede, arquivos de audio ou modelos.

## Sequencia TDD

1. Red: consulta por conteudo e contexto falham no SQLite.
2. Green: ampliar o read model e a query sem dependencia nova.
3. Red: Desktop nao pesquisa o banco nem abre a aba correspondente.
4. Green: integrar busca cancelavel, filtros e navegacao contextual.
5. Refactor: manter a consulta e a representacao visual pequenas.

## Entrega

- Red: os testes falharam pela ausencia de secao, trecho, periodo e consulta de conteudo.
- Green: `SqliteReuniaoQuery` pesquisa cinco fontes locais com filtro literal, estado, periodo, limite e ordem preservados.
- Desktop: espera cancelavel de 280 ms, trecho destacado e abertura direta da aba correspondente.
- Evidencia visual: `artifacts/evidencias/SPEK-056/busca-conteudo.png`.
- Validacao final: 327 testes Release verdes no conjunto do produto.
