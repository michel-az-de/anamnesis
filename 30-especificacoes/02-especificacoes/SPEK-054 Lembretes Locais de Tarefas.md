---
title: SPEK-054 Lembretes Locais de Tarefas
aliases: [Lembretes de Tarefas, Agenda Local de Pendencias]
tags: [especificacao, tarefas, lembrete, windows]
type: spec
created: 2026-08-07
updated: 2026-08-07
status: completed
summary: Permite criar lembretes locais e confirmados a partir das tarefas geradas na ata.
related: ["[[SPEK-019 Ata Markdown Estruturada]]", "[[SPEK-037 Mapeamento de Tarefas Externas]]"]
---

# SPEK-054 | Lembretes locais de tarefas

## Objetivo

Transformar tarefas da ata em lembretes acionaveis sem publicar dados em servicos externos.

## Regras

- Cada tarefa oferece `Criar lembrete` e abre confirmacao de data e hora.
- A pessoa confirma a descricao, data e hora antes de salvar.
- O lembrete fica em SQLite local e a notificacao e emitida pelo Tray.
- Padrao inicial: proximo dia as 09:00, sempre editavel antes da confirmacao.
- O primeiro disparo marca o lembrete como notificado e impede duplicacao indefinida.
- Integracoes com Outlook, Google, Trello ou Azure DevOps continuam fora deste incremento.

## Criterios de aceite

- [x] Criacao exige confirmacao humana.
- [x] Reinicio do Tray nao perde lembretes.
- [x] Notificacao vencida nao e duplicada indefinidamente.
- [x] Testes usam relogio falso e nao exibem notificacao real.

## Evidencias

- Red de aplicacao: tipos de lembrete e fronteiras ainda inexistentes.
- Red de interface: `DesktopPocForm` ainda nao aceitava o caso de uso nem oferecia `Criar lembrete`.
- Green: criacao, disparo unico, persistencia SQLite apos reinicio e interface confirmada.
- Suite Release: 301 testes verdes.
- Pacote local `0.2.0-beta.9-local` publicado com 490 arquivos; instalacao adiada porque havia uma gravacao real ativa.

## Fora de escopo

- Adiar, concluir e cancelar pela interface ficam para um incremento posterior.
- Outlook, Google, Trello e Azure DevOps.
