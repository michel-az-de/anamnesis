---
title: ADR-009 Cena OBS Gerenciada pelo Anamnesis
aliases: [Cena Anamnesis no OBS, Captura Universal de Audio]
tags: [adr, obs, audio, windows, privacidade]
type: adr
created: 2026-08-05
updated: 2026-08-05
status: accepted
summary: Usa uma cena OBS dedicada com audio padrao do sistema e microfone, restaurando a cena anterior depois da gravacao.
related: ["[[SPEK-024 Captura Universal de Audio pelo OBS]]", "[[ADR-004 Integracao com OBS]]"]
---

# ADR-009 | Cena OBS gerenciada pelo Anamnesis

## Contexto

O controle atual envia apenas `StartRecord` e `StopRecord`. Nesta maquina, a colecao OBS nao possui fontes configuradas, portanto uma gravacao pode existir sem conter a reuniao. Configuracoes por aplicativo tambem variam entre Teams, Meet, Zoom e navegadores.

## Decisao

Criar e reutilizar uma cena dedicada `Anamnesis` pelo obs-websocket v5. A cena recebe uma fonte de saida de audio WASAPI com dispositivo padrao e uma fonte de entrada WASAPI com microfone padrao.

Antes de gravar, o adaptador guarda a cena atual, seleciona `Anamnesis` e valida as fontes. Ao encerrar, restaura a cena anterior mesmo quando o encerramento falha.

## Alternativas consideradas

| Alternativa | Probabilidade de adequacao | Motivo |
| --- | ---: | --- |
| Cena dedicada com audio global | 90% | Independe da plataforma da reuniao e nao mistura fontes com cenas pessoais. |
| Configuracao manual do OBS | 20% | Simples no codigo, mas permite gravacoes silenciosas e aumenta erros de uso. |
| Uma fonte por aplicativo | 10% | Isola audio, mas exige conhecer executaveis e janelas de cada plataforma. |

## Consequencias

- Todo som reproduzido no dispositivo padrao durante a gravacao pode ser capturado.
- A gravacao continua manual e visivel pelo estado do Tray.
- O usuario pode trocar os dispositivos padrao no Windows sem reconfigurar cada plataforma.
- A cena e as fontes sao idempotentes e nao sao removidas automaticamente.
- Uma selecao explicita de dispositivos fica para uma tela posterior.
