---
title: ADR-019 OAuth Desktop e Cache Protegido para Agendas
aliases: [ADR-019, OAuth Agenda, Token Cache]
tags: [adr, oauth, seguranca, agenda, windows, dpapi, msal]
type: adr
created: 2026-08-07
updated: 2026-08-07
status: accepted
summary: Tokens OAuth de agenda usam armazenamento protegido pelo usuario Windows, cache de eventos fica em SQLite separado, e sincronizacao e incremental com cursor opaco.
related: ["[[SPEK-033 Agenda Conectada]]", "[[SPEK-034 Google Calendar Adapter]]", "[[SPEK-035 Microsoft Graph Calendar Adapter]]", "[[ADR-014 Protecao de Segredos Locais]]", "[[ADR-013 Engine SQLite Embarcada]]"]
---

# ADR-019 | OAuth desktop e cache protegido para agendas

## Contexto

As SPEKs 033 a 035 precisam conectar contas Google Calendar e Microsoft Calendar para fornecer contexto ao detector local. Isso exige:

1. Autenticacao OAuth 2.0 delegada no desktop do usuario.
2. Armazenamento seguro de access e refresh tokens.
3. Cache local de eventos com cursor de sincronizacao incremental.
4. Isolamento entre contas, de modo que uma nao acesse dados da outra.

O ADR-014 ja define `SegredoLocal` com DPAPI para a senha do OBS. Tokens OAuth sao diferentes: sao temporarios, renovaveis, multiplos por conta, e precisam de formato que suporte expiry, scopes e refresh. DPAPI puro para cada campo seria possivel, mas dificulta serializacao, backup e migracao. A abordagem deve reutilizar o principio (protecao por usuario Windows) sem forcar o mecanismo exato do ADR-014 onde ele nao se ajusta.

## Decisao

### 1. Tokens OAuth: Windows Credential Manager (WinCred) como padrao

Access e refresh tokens de cada conta sao armazenados no Windows Credential Manager, sob uma chave unica por conta e provider (`anamnesis:agenda:<provider>:<contaId>`). O valor contem o token completo serializado como JSON protegido por DPAPI `CurrentUser`.

**Por que nao DPAPI direto no arquivo de configuracao:**
- Tokens OAuth possuem estrutura rica (expiry, scopes, token_type) que serializa melhor em JSON.
- Multiplas contas exigem multiplas chaves; WinCred ja e um key-value store com namespace.
- O ADR-014 continua valido para segredos simples (senha do OBS, chaves de API); tokens OAuth usam WinCred como extensao natural do mesmo principio.
- O Credential Manager e o destino nativo de MSAL.NET, que sera usado para Microsoft Graph.

**Fallback para DPAPI em arquivo:** se WinCred falhar (politica corporativa restrictiva), o sistema cai para DPAPI em arquivo com o mesmo JSON, prefixado por `dpapi-agenda:`. O fallback e registrado no journal como `agenda.armazenamento_fallback`.

### 2. Cache de eventos: SQLite separado, nao o banco principal

Eventos de agenda ficam em um SQLite proprio (`agenda-cache.db`), nao no `anamnesis.db`. Isso isola o cache de eventos do estado da reuniao, permite limpeza independente, e evita que o journal do dominio receba operacoes de cache.

O esquema minimo segue a SPEK-033:

```sql
CREATE TABLE ContaAgenda (
    ContaAgendaId TEXT PRIMARY KEY,
    Provider TEXT NOT NULL CHECK (Provider IN ('Google', 'Microsoft')),
    Estado TEXT NOT NULL CHECK (Estado IN ('Conectada', 'Sincronizando', 'RequerAtencao', 'Desconectada')),
    CursorSync TEXT,
    JanelaSyncInicio TEXT,
    JanelaSyncFim TEXT,
    AtualizadoEm TEXT
);

CREATE TABLE EventoAgenda (
    EventoAgendaId TEXT PRIMARY KEY,
    ContaAgendaId TEXT NOT NULL REFERENCES ContaAgenda(ContaAgendaId) ON DELETE CASCADE,
    EventoExternoId TEXT NOT NULL,
    Titulo TEXT,
    Inicio TEXT NOT NULL,
    Fim TEXT NOT NULL,
    FusoOriginal TEXT,
    UrlReuniao TEXT,
    Status TEXT CHECK (Status IN ('Confirmado', 'Tentativo', 'Cancelado')),
    AtualizadoEm TEXT,
    UNIQUE (ContaAgendaId, EventoExternoId)
);

CREATE INDEX IX_EventoAgenda_Conta ON EventoAgenda(ContaAgendaId);
CREATE INDEX IX_EventoAgenda_Inicio ON EventoAgenda(Inicio);
```

**URL de reuniao criptografada:** o campo `UrlReuniao` usa DPAPI `CurrentUser` no mesmo esquema do ADR-014, com prefixo `dpapi:`. O restante da linha e texto claro (titulo, horario) porque sao dados de exibicao, nao segredos.

### 3. Sincronizacao incremental com cursor opaco

Cada conta mantem um cursor (`nextSyncToken` para Google, `@odata.deltaLink` para Microsoft) e uma janela temporal (`JanelaSyncInicio`/`JanelaSyncFim`). O cursor e opaco: nunca analisado, apenas persistido e reenviado.

**Rebase atomico de janela:** quando a janela avanca, a nova carga e construida em tabelas temporarias (`_staging`). So apos todas as paginas serem consumidas com sucesso, ocorre `BEGIN; DROP TABLE EventoAgenda; ALTER TABLE EventoAgenda_staging RENAME TO EventoAgenda; COMMIT;`. Falha em qualquer pagina preserva o cache anterior.

**Remocao por expiracao:** eventos fora da nova janela sao removidos. Titulos de eventos removidos seguem a mesma retencao do cache da conta (configuravel, padrao 30 dias apos desconexao).

### 4. Adapter de agenda: contrato unico, implementacoes separadas

O dominio define `IAgendaAdapter`:

```csharp
public interface IAgendaAdapter : IDisposable
{
    string Provider { get; }
    Task<ResultadoAutenticacao> IniciarAutenticacaoAsync(CancellationToken ct = default);
    Task<ResultadoSincronizacao> SincronizarAsync(ContaAgenda conta, CancellationToken ct = default);
    Task RevogarAsync(ContaAgenda conta, CancellationToken ct = default);
}
```

Implementacoes:
- `GoogleCalendarAdapter`: OAuth 2.0 com PKCE S256, redirect `127.0.0.1` porta aleatoria, `Google.Apis.Calendar.v3`.
- `MicrosoftGraphAdapter`: MSAL.NET com WAM preferencial, fallback para navegador do sistema.

Ambos usam `IAgendaTokenStore` (abstracao sobre WinCred/DPAPI) e `IAgendaCache` (abstracao sobre SQLite de agenda). O detector consome `IAgendaCache`, nunca `IAgendaAdapter`.

### 5. Scheduler adaptativo

A frequencia de sincronizacao varia por estado:

| Estado | Intervalo | Gatilho |
| --- | --- | --- |
| Desktop aberto | 15 min | Timer com jitter |
| Proximo a reuniao (< 30 min) | 2 min | Timer + evento de abertura |
| Sem rede | Pausa | NetworkAvailabilityChanged |
| Bateria | 60 min | PowerModeChanged |
| Erro 429 | `Retry-After` + 5 s | Resposta HTTP |

O scheduler nunca bloqueia a thread do Tray; cada sincronizacao roda em `Task.Run` com timeout de 30 s.

## Alternativas consideradas

| Alternativa | Probabilidade de adequacao | Motivo |
| --- | ---: | --- |
| WinCred + DPAPI fallback + SQLite separado | 88% | Padrao do Windows, reutiliza principios existentes, isola cache, suporta multiplas contas. |
| MSAL cache serializado com DPAPI em arquivo | 75% | Funciona para Microsoft, mas nao para Google. MSAL tem seu proprio cache que ja usa protecao; reutilizamos para Microsoft, mas nao forcamos para Google. |
| DPAPI direto no JSON de configuracao | 55% | Funciona para uma conta, mas escala mal para multiplas contas e tokens renovaveis. WinCred e mais apropriado. |
| Chave mestra em arquivo com AES | 40% | Exige gerenciamento de chave, derivação, rotacao. DPAPI ja faz isso. |
| SQLite criptografado (SQLCipher) | 35% | Protege em repouso, mas nao resolve autenticacao OAuth e adiciona dependencia nativa pesada. |
| Armazenar tokens no SQLite principal sem criptografia | 5% | Viola a SPEK-033 e o principio de seguranca do produto. |

## Consequencias

- Nova dependencia: `Microsoft.Identity.Client` (MSAL.NET) para Microsoft Graph.
- Nova dependencia: `Google.Apis.Calendar.v3` para Google Calendar.
- Nova dependencia: `System.Security.Cryptography.ProtectedData` ja presente via ADR-014.
- O assembly `Anamnesis.Infrastructure` ja declara `SupportedOSPlatform("windows")`; nenhuma mudanca adicional.
- Tokens nunca aparecem em `config.json`, journal, logs ou dumps.
- O cache de agenda pode ser limpo independentemente do banco de reunioes.
- Desconexao de uma conta remove apenas seus eventos e credenciais, sem afetar outras contas nem reunioes gravadas.
- Falha de WinCred e transparente para o usuario e registrada no journal como evento de saude.

## Referencias oficiais

- [DPAPI](https://learn.microsoft.com/en-us/windows/win32/secauthn/data-protection-overview)
- [Credential Manager](https://learn.microsoft.com/en-us/windows/win32/secauthn/credentials-management)
- [MSAL.NET token cache serialization](https://learn.microsoft.com/en-us/entra/msal/dotnet/how-to/token-cache-serialization)
- [Google OAuth 2.0 for installed apps](https://developers.google.com/identity/protocols/oauth2/native-app)
- [Google Calendar incremental sync](https://developers.google.com/workspace/calendar/api/guides/sync)
- [Microsoft Graph delta query](https://learn.microsoft.com/en-us/graph/delta-query-events)
