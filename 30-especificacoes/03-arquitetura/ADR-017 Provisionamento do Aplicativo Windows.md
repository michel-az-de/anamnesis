---
title: ADR-017 Provisionamento do Aplicativo Windows
aliases: [ADR Primeiro Uso Windows, ADR Dependencias do Instalador]
tags: [adr, windows, instalador, provisionamento]
type: adr
created: 2026-08-05
updated: 2026-08-05
status: accepted
summary: Mantem o instalador do Anamnesis autocontido e separa a descoberta local do provisionamento consentido de dependencias externas.
related: ["[[SPEK-045 Experiencia Windows Instalada e Primeiro Uso]]", "[[ADR-008 Instalador por Usuario com Inno Setup]]"]
---

# ADR-017 | Provisionamento do aplicativo Windows

## Contexto

O instalador atual distribui Tray e Worker, mas OBS, FFmpeg, Docker, modelo Whisper e CLI autenticada possuem licencas, tamanhos, privilegios e ciclos de atualizacao independentes. Tratar todos como payload silencioso tornaria a instalacao opaca e fragil, e uma CLI autenticada exige participacao humana.

## Decisao

O instalador continua per-user, offline e autocontido apenas para o Anamnesis. No primeiro uso, o aplicativo descobre caminhos locais padrao que realmente existem, preserva qualquer configuracao anterior e apresenta diagnostico acionavel para o restante.

Dependencias externas nao sao baixadas nem instaladas silenciosamente. Um futuro bootstrapper consentido devera possuir SPEK e ADR proprias, licencas e hashes fixados, origem oficial, rollback e confirmacao explicita. Autenticacao de CLI nunca sera automatizada.

## Alternativas consideradas

| Alternativa | Probabilidade de adequacao | Motivo |
| --- | ---: | --- |
| Aplicativo autocontido com descoberta local | 85% | Mantem instalacao pequena, previsivel e segura; fica pronta imediatamente onde os pre-requisitos ja existem. |
| Bootstrapper consentido separado | 70% futuro | Pode reduzir friccao, mas exige rede, elevacao eventual, licencas, hashes e rollback. |
| Embutir todas as dependencias no EXE | 10% | Pacote muito grande, atualizacoes acopladas e autenticacao ainda nao resolvida. |

## Consequencias

- O shell do Anamnesis sempre abre, mesmo quando uma dependencia externa falta.
- Em uma maquina preparada, instalar e abrir e suficiente para operar o produto.
- Em Windows limpo, o diagnostico diferencia aplicativo instalado de pipeline ainda nao preparado.
- O produto nao promete instalar software de terceiros ou autenticar contas sem consentimento.
- Assinatura e atualizacao automatica continuam independentes desta decisao.
