---
title: SPEK-034 Google Calendar Adapter
aliases: [SPEK-034, Google Calendar]
tags: [especificacao, google, calendar, oauth, pos-alpha]
type: spek
created: 2026-08-05
updated: 2026-08-05
status: draft
summary: Le o calendario principal Google por OAuth desktop e sincronizacao incremental local.
related: ["[[SPEK-033 Agenda Conectada]]", "[[SPEK-032 Captura Instantanea e Deteccao Local]]", "[[Roadmap de Produto]]"]
---

# SPEK-034 Google Calendar Adapter

## Objetivo

Implementar o provider Google da SPEK-033 para listar eventos do calendario principal, reconhecer links Google Meet e manter o cache local atualizado com permissao minima.

## Fora de escopo

- Criar ou alterar eventos.
- Ler todos os calendarios na primeira versao.
- Ler corpo, anexos, convidados ou e-mails alem do necessario ao login.
- Usar service account, automacao web ou segredo de cliente embutido como protecao.
- Usar `events.watch` ou qualquer endpoint publico.

## Regras

- Autenticacao usa OAuth 2.0 para aplicativo instalado, navegador do sistema, `state` aleatorio, redirect em `127.0.0.1` com porta aleatoria, callback unico com timeout e PKCE S256.
- Solicitar acesso offline e preferir `calendar.events.owned.readonly` para o calendario principal. `calendar.events.readonly` exige POC e justificativa se o escopo menor nao atender.
- `calendar.calendarlist.readonly` so entra em SPEK futura para selecao de varios calendarios.
- A carga inicial usa `events.list` no calendario `primary`, `singleEvents=true`, intervalo limitado e `orderBy=startTime`.
- O adapter persiste `nextSyncToken` no estado de sincronizacao da conta.
- Requisicao incremental usa somente parametros compativeis com `syncToken`; ordenacao da interface ocorre no cache local.
- Quando a janela da SPEK-033 avanca, o adapter cria um snapshot completo paralelo e troca o cache e o novo token atomicamente.
- Resposta HTTP 410 invalida o cursor e dispara carga completa daquela conta.
- Link Meet prioriza `conferenceData` com tipo `hangoutsMeet` e seu entry point; `hangoutLink` serve como fallback.
- Respostas 429 e erros de cota respeitam backoff e nunca bloqueiam o Tray.
- Refresh token fica no cofre protegido definido pelo ADR da SPEK-033.
- Reconexao e revogacao sao apresentadas como acoes explicitas.
- Paginas ficam em staging; `nextSyncToken` e cache so sao trocados atomicamente no fim, e o token e tratado como valor opaco.

## Critérios de aceite

- [ ] Login conecta uma conta e nao grava token em SQLite, JSON ou journal.
- [ ] Callback com `state` incorreto, repetido ou fora do timeout e rejeitado.
- [ ] Carga inicial normaliza eventos unicos e recorrentes no cache comum.
- [ ] Sincronizacao com `nextSyncToken` aplica criacoes, mudancas e cancelamentos sem duplicar.
- [ ] Rebase de janela preserva o snapshot anterior se qualquer pagina da nova carga falhar.
- [ ] HTTP 410 reconstrui apenas a conta Google afetada.
- [ ] Um evento Meet fornece URL de entrada valida ao detector e ao Desktop.
- [ ] Evento sem conferencia continua visivel sem inventar URL.
- [ ] Throttling, token revogado e falha de rede resultam em estado recuperavel.
- [ ] Testes usam HTTP fake e navegador OAuth fake, sem acessar Google real.
- [ ] Ensaio manual usa uma conta de teste e registra somente metadados redigidos.
- [ ] Distribuicao publica permanece bloqueada ate consent screen, verificacao e escopo minimo serem validados.

## Referencias oficiais

- [Events.list](https://developers.google.com/workspace/calendar/api/v3/reference/events/list)
- [Modelo Event e conferenceData](https://developers.google.com/workspace/calendar/api/v3/reference/events)
- [Escopos do Google Calendar](https://developers.google.com/workspace/calendar/api/auth)
- [OAuth para aplicativo instalado](https://developers.google.com/identity/protocols/oauth2/native-app)
- [Sincronizacao incremental](https://developers.google.com/workspace/calendar/api/guides/sync)
- [Boas praticas de OAuth](https://developers.google.com/identity/protocols/oauth2/resources/best-practices)

## Decisoes pendentes

- [x] Aprovar o ADR da SPEK-033 e registrar qualquer biblioteca OAuth nova antes do codigo. ([[ADR-019 OAuth Desktop e Cache Protegido para Agendas]])
- Validar exigencias de verificacao OAuth para distribuicao publica do aplicativo.
- Comparar os escopos `calendar.events.owned.readonly` e `calendar.events.readonly` com convites recebidos no calendario principal.
- Revalidar cotas e politicas oficiais no inicio da implementacao.
