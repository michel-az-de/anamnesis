---
title: SPEK-035 Microsoft Graph Calendar Adapter
aliases: [SPEK-035, Microsoft Calendar, Teams Calendar]
tags: [especificacao, microsoft-graph, calendar, teams, oauth, pos-alpha]
type: spek
created: 2026-08-05
updated: 2026-08-05
status: draft
summary: Le calendarios Microsoft pessoais ou corporativos via Graph, MSAL e permissao delegada minima.
related: ["[[SPEK-033 Agenda Conectada]]", "[[SPEK-032 Captura Instantanea e Deteccao Local]]", "[[Roadmap de Produto]]"]
---

# SPEK-035 Microsoft Graph Calendar Adapter

## Objetivo

Implementar o provider Microsoft da SPEK-033 para listar ocorrencias do calendario, reconhecer links do Teams e sincronizar mudancas de contas pessoais, corporativas ou escolares suportadas.

## Fora de escopo

- Criar ou alterar eventos e reunioes Teams.
- Gravar chamadas pela API do Teams.
- Ler anexos, corpo ou participantes no primeiro corte.
- Suportar Azure DevOps Server ou Exchange local.
- Usar client secret, PAT, OAuth legado do Azure DevOps ou automacao web.
- Hospedar webhook de change notification.

## Regras

- Aplicativo desktop e cliente publico, autenticado por Microsoft Entra ID com MSAL.NET e preferencia por WAM no Windows.
- Solicitar permissao delegada `Calendars.ReadBasic` no primeiro consentimento.
- `Calendars.Read` so pode ser solicitado depois, com consentimento incremental, se convites nao estruturados exigirem leitura de corpo.
- A carga inicial usa `/me/calendar/calendarView` com intervalo definido pela SPEK-033.
- Mudancas seguintes usam delta de eventos e persistem `@odata.deltaLink` por calendario, conta e janela fixa.
- Quando a janela temporal avanca, uma nova consulta delta inicial constroi snapshot paralelo e substitui cache e cursor de forma atomica somente depois do sucesso.
- Link Teams exige `isOnlineMeeting`, provider `teamsForBusiness` e `onlineMeeting.joinUrl`.
- O adapter nao depende de `onlineMeetingUrl`, sinalizado pela Microsoft para descontinuacao futura.
- O cache MSAL persistido e criptografado e isolado ao usuario Windows.
- Respostas 429 respeitam `Retry-After`; consentimento ou token invalido produzem estado de reconexao.
- Cada calendario possui cursor proprio para evitar misturar janelas e contas.
- Paginas ficam em staging; cache e `@odata.deltaLink` so avancam atomicamente no fim, e o cursor e tratado como valor opaco.

## Critérios de aceite

- [ ] Login por conta Microsoft suportada conclui sem client secret local.
- [ ] `calendarView` normaliza ocorrencias e excecoes recorrentes no cache comum.
- [ ] Delta aplica inclusoes, mudancas e remocoes sem duplicar.
- [ ] Rebase de janela nao deixa lacunas e preserva o snapshot anterior se a nova carga falhar.
- [ ] Evento Teams usa `onlineMeeting.joinUrl`; evento comum nao inventa link.
- [ ] O primeiro consentimento pede somente `Calendars.ReadBasic`.
- [ ] Uma POC confirma `onlineMeeting.joinUrl` com `Calendars.ReadBasic` em conta pessoal e corporativa antes de aprovar a SPEK.
- [ ] Cache de token, 429, expiracao e reconexao possuem testes sem dados sensiveis.
- [ ] Testes usam Graph fake e MSAL fake, sem rede ou janela real.
- [ ] Ensaio manual cobre uma conta de teste e registra somente evidencias redigidas.

## Referencias oficiais

- [List calendarView](https://learn.microsoft.com/en-us/graph/api/user-list-calendarview?view=graph-rest-1.0)
- [Recurso event](https://learn.microsoft.com/en-us/graph/api/resources/event?view=graph-rest-1.0)
- [OnlineMeetingInfo](https://learn.microsoft.com/en-us/graph/api/resources/onlinemeetinginfo?view=graph-rest-1.0)
- [Delta de eventos](https://learn.microsoft.com/en-us/graph/delta-query-events)
- [Configuracao de aplicativo desktop](https://learn.microsoft.com/en-us/entra/identity-platform/scenario-desktop-app-configuration)
- [Serializacao do cache MSAL](https://learn.microsoft.com/en-gb/entra/msal/dotnet/how-to/token-cache-serialization)
- [Throttling no Microsoft Graph](https://learn.microsoft.com/en-us/graph/throttling)

## Decisoes pendentes

- Aprovar o ADR da SPEK-033 e a dependencia MSAL.NET antes do codigo.
- Validar WAM em Windows 10 e 11 e definir fallback para navegador do sistema.
- Confirmar em conta pessoal e corporativa quais dados chegam com `Calendars.ReadBasic`.
- Definir tipos de conta, tenant suportado e fallback WAM ou navegador a partir da POC de autenticacao.
