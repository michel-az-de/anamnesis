---
title: SPEK-055 Diarizacao Local de Locutores
aliases: [Pessoa 1 Pessoa 2, Identidade de Locutores]
tags: [especificacao, audio, diarizacao, locutores]
type: spec
created: 2026-08-07
updated: 2026-08-07
status: draft
summary: Segmenta a transcricao por locutor com rotulos neutros e permite nomeacao humana posterior.
related: ["[[SPEK-008 Transcricao Local com Whisper]]", "[[SPEK-051 Confiabilidade do Primeiro Uso Real]]"]
---

# SPEK-055 | Diarizacao local de locutores

## Objetivo

Exibir quem falou cada trecho como `Pessoa 1`, `Pessoa 2` e `Pessoa 3`, sem atribuir nomes sem evidencia.

## Regras propostas

- Diarizacao e executada localmente sobre o audio, antes da geracao da ata.
- Rotulos sao estaveis dentro da reuniao, mas nao identificam a pessoa real.
- A pessoa pode renomear `Pessoa 1` para um nome confirmado; a LLM nunca faz essa associacao sozinha.
- Segmentos de baixa confianca usam `Locutor incerto`.
- Transcricao preserva timestamp inicial, timestamp final, locutor e texto.
- Nova dependencia ou modelo exige ADR com custo de disco, memoria, tempo e licenca.

## Criterios de aceite propostos

- [ ] Dois locutores sinteticos recebem rotulos diferentes e estaveis.
- [ ] Segmentos incertos nao recebem nome inventado.
- [ ] Renomeacao humana atualiza visualizacao e ata sem alterar o audio.
- [ ] Pipeline continua totalmente local e testavel sem rede.

## Decisoes pendentes

- Comparar modelo `whisper.cpp` com suporte a turnos e uma ferramenta dedicada de diarizacao local.
- Definir limite aceitavel de CPU, memoria, download e tempo adicional por hora gravada.

