---
title: SPEK-033 Agenda Conectada
aliases: [SPEK-033, Calendarios Conectados]
tags: [especificacao, agenda, oauth, sqlite, privacidade, pos-alpha]
type: spek
created: 2026-08-05
updated: 2026-08-05
status: draft
summary: Define contas, eventos e sincronizacao incremental somente leitura para adapters de calendario.
related: ["[[SPEK-032 Captura Instantanea e Deteccao Local]]", "[[SPEK-034 Google Calendar Adapter]]", "[[SPEK-035 Microsoft Graph Calendar Adapter]]", "[[Roadmap de Produto]]"]
---

# SPEK-033 Agenda conectada

## Objetivo

Criar o contrato comum e o cache local que permitem conectar uma ou mais contas de calendario, mostrar proximas reunioes e fornecer contexto ao detector sem acoplar o produto a Google ou Microsoft.

## Fora de escopo

- Implementar um provider especifico.
- Criar, alterar, aceitar ou excluir eventos.
- Armazenar corpo do convite, anexos ou lista completa de participantes.
- Hospedar endpoint publico ou relay de webhook.
- Iniciar gravacao apenas porque existe um evento.

## Modelo local minimo

| Campo | Regra |
| --- | --- |
| `ContaAgendaId` | Identificador local, sem usar e-mail como chave |
| `Provider` | Google ou Microsoft |
| `EventoExternoId` | Unico dentro da conta |
| `Titulo` | Persistido para exibicao, tratado como dado sensivel |
| `Inicio` e `Fim` | UTC com fuso original preservado quando disponivel |
| `UrlReuniao` | URL oficial de conferencia, criptografada para o usuario Windows |
| `Status` | Confirmado, tentativo ou cancelado |
| `CursorSync` | Valor opaco por conta e provider, nunca analisado ou registrado |
| `JanelaSyncInicio` e `JanelaSyncFim` | Vinculam o cursor ao intervalo que o produziu |
| `AtualizadoEm` | Usado para reconciliacao e diagnostico |

## Regras

- Toda conta e conectada por OAuth delegado e consentimento explicito no navegador do sistema.
- O primeiro corte solicita apenas leitura basica de eventos.
- Access e refresh tokens usam armazenamento protegido pelo usuario Windows e nunca entram no SQLite comum ou no journal.
- O cache SQLite contem somente os campos necessarios ao produto.
- Sincronizacao inicial cobre uma janela limitada de passado e futuro; sincronizacoes seguintes usam cursor incremental do provider.
- O scheduler atualiza ao abrir o app e adapta o intervalo por conta: mais frequente perto de reuniao, mais economico fora da janela, em bateria ou sem rede. Sempre usa jitter, backoff e `Retry-After`.
- Perda ou invalidacao do cursor causa ressincronizacao completa somente daquela conta.
- Cursores sao validos apenas para a consulta e a janela que os produziram. O avanco da janela temporal dispara periodicamente uma nova carga completa.
- Cada pagina e aplicada em staging. Cache e cursor final so avancam juntos depois de todas as paginas concluidas.
- A nova janela inclui sobreposicao temporal, e montada em cache temporario e substitui a anterior de forma atomica somente depois do sucesso; uma falha preserva o cache utilizavel.
- Depois da troca, eventos fora da janela sao removidos. Titulo e URL seguem o mesmo prazo de retencao do cache da conta.
- Desconectar revoga quando suportado, apaga credenciais locais e remove o cache daquela conta sem tocar reunioes gravadas.
- Eventos cancelados nao iniciam sugestao; mudancas de horario atualizam o cache.
- A agenda enriquece titulo e horario, mas nao altera estados de dominio por conta propria.
- Webhooks ficam adiados porque exigem endpoint HTTPS publico e renovacao de assinaturas.

## Critérios de aceite

- [ ] Multiplas contas podem coexistir sem compartilhar tokens ou cursores.
- [ ] A tela mostra estado conectado, sincronizando, requer atencao ou desconectado.
- [ ] Eventos recorrentes aparecem como ocorrencias na janela consultada.
- [ ] Atualizacao incremental nao duplica eventos.
- [ ] Cursor invalido reconstrui somente o cache afetado.
- [ ] O deslocamento da janela executa rebase atomico e nao deixa lacunas nem apaga o ultimo cache valido em caso de falha.
- [ ] Crash entre paginas preserva cursor e cache anteriores e permite retomar sem perder evento.
- [ ] URL de entrada nao aparece em texto puro no banco, configuracao ou journal.
- [ ] Cancelamento, reconexao, backoff e desconexao possuem testes deterministas.
- [ ] O detector consome um read model local e nao chama APIs de calendario durante a decisao.
- [ ] Nenhum teste unitario abre navegador ou acessa rede real.

## Testes associados

- Contrato compartilhado de adapter com provider fake.
- SQLite temporario para contas, cache, unicidade, cursor e limpeza por conta.
- Scheduler com `TimeProvider` e respostas simuladas de throttling.
- Testes de seguranca garantindo ausencia de tokens em banco, configuracao e journal.
- Teste arquitetural garantindo que adapters de agenda nao recebem interface de retencao nem caminho da gravacao.

## Decisoes pendentes

- [x] Aprovar ADR de OAuth desktop, cache protegido e polling antes do código. ([[ADR-019 OAuth Desktop e Cache Protegido para Agendas]])
- Escolher a fronteira minima entre `AgendaAdapter`, `AgendaSyncService` e cache local.
- Definir a janela inicial depois de medir o uso, com proposta de 7 dias passados e 30 futuros.
