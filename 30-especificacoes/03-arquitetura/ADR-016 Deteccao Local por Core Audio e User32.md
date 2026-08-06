---
title: ADR-016 Deteccao Local por Core Audio e User32
aliases: [Detector Windows de Reuniao, Sinais Locais de Chamada]
tags: [adr, windows, core-audio, user32, privacidade, deteccao]
type: adr
created: 2026-08-05
updated: 2026-08-05
status: accepted
summary: O detector observa sessoes de audio e janelas locais por polling, normaliza os sinais em memoria e nunca persiste titulos ou historico.
related: ["[[SPEK-032 Captura Instantanea e Deteccao Local]]", "[[SPEK-031 Observabilidade Operacional Real]]"]
---

# ADR-016 | Deteccao local por Core Audio e User32

## Contexto

O Anamnesis precisa sugerir ou iniciar uma gravacao poucos segundos depois que o usuario entra em uma chamada. Integrar cada plataforma, ler DOM ou observar historico de navegacao aumentaria acoplamento e risco de privacidade. Somente o processo em execucao tambem e insuficiente: Teams, Zoom ou Discord podem permanecer abertos sem uma chamada.

## Decisao

O Tray executa polling local a cada segundo, em thread MTA de fundo, combinando:

- Core Audio `IMMDeviceEnumerator` e `IAudioSessionManager2` para enumerar sessoes ativas de captura e renderizacao em todos os endpoints ativos;
- `IAudioSessionControl2.GetProcessId` para normalizar a familia do processo associada a cada sessao;
- User32 `EnumWindows`, `IsWindowVisible`, `GetWindowThreadProcessId` e `GetWindowTextW`, com `DWMWA_CLOAKED`, para classificar janelas superiores visiveis;
- `Microsoft.Windows.CsWin32` `0.3.298`, fixado com `PrivateAssets=all`, para gerar bindings em compilacao sem adicionar runtime ao produto.

Uma sessao de captura ativa representa uso do microfone, nao prova fala, transmissao ou ausencia de mute. Uma sessao de renderizacao apenas aumenta a confianca do modo assistido e nunca autoriza inicio automatico.

Para aplicativo nativo, o inicio automatico exige sessao de captura cuja familia esteja na allowlist. Para navegador, exige sessao de captura da familia do navegador e assinatura conhecida em uma janela da mesma familia. Correlacao usa familia normalizada, porque navegadores e clientes Electron distribuem audio e janela entre processos diferentes.

Quando duas plataformas possuem captura simultanea, o sinal e ambiguo e nao autoriza inicio automatico. Falha do servico de audio, dispositivo invalidado, processo encerrado durante a leitura ou erro de janela apenas reduz a confianca; o modo manual permanece disponivel.

Cada uma das tres leituras informa sucesso ou falha. Qualquer falha torna o snapshot `ColetaConfiavel=false`: a politica cancela contagens e avisos abertos, pausa inicio e encerramento automaticos e reinicia o relogio de ausencia quando a coleta retorna. O retorno `BOOL` de `EnumWindows` e validado; zero vira falha do probe em vez de uma lista vazia confiavel. O journal recebe somente a mudanca de saude `degradada` ou `restaurada`, sem detalhes nativos e sem spam por polling.

Titulos sao comparados em memoria e descartados no mesmo ciclo. O detector nao persiste nem registra titulo bruto, PID, caminho de executavel, dispositivo, processo desconhecido ou conteudo de agenda. Somente codigos normalizados e fechados chegam ao journal.

## Allowlist inicial

| Classe | Familias ou assinaturas iniciais | Uso |
| --- | --- | --- |
| Aplicativos nativos | `ms-teams`, `teams`, `zoom`, `discord` | Captura ativa da mesma familia identifica plataforma local |
| Navegadores | `chrome`, `msedge`, `firefox`, `brave` | Nunca identificam chamada sozinhos |
| Janelas de chamada | `Google Meet`, `Meet -`, `Microsoft Teams`, `Zoom Meeting`, `Discord` | Literais configuraveis, comparados sem diferenciar maiusculas |

A configuracao pode acrescentar familias e assinaturas. Ela guarda apenas padroes genericos definidos pelo usuario, nunca titulos observados.

## Alternativas consideradas

| Alternativa | Probabilidade de adequacao | Motivo |
| --- | ---: | --- |
| Core Audio + User32 por polling e CsWin32 | 88% | Usa contratos publicos do Windows, mantem o custo previsivel e evita interop manual fragil. |
| Core Audio com callbacks | 65% | Reduz polling, mas exige MTA, callbacks, lista propria de sessoes e tratamento complexo de ciclo de vida. |
| `LibraryImport` e interfaces COM manuais | 68% | Evita pacote de build, mas aumenta risco de assinatura, vtable, marshalling e liberacao incorreta. |
| NAudio | 62% | API conveniente, porem adiciona uma dependencia runtime maior que a necessidade do detector. |
| Registro `CapabilityAccessManager` | 25% | Nao e contrato publico apropriado para observacao operacional. |
| DOM, extensao ou automacao de plataforma | 5% | Viola privacidade, aumenta manutencao e esta fora do produto local. |

## Consequencias

- O detector funciona sem conta externa, rede, extensao ou adapter por plataforma.
- O automatico continua opt-in e conservador; falsos negativos sao preferidos a gravacoes indevidas.
- O polling de um segundo e compativel com os limiares de cinco segundos e nao bloqueia a thread visual.
- Falha de probe favorece falso negativo seguro e nunca pode ser interpretada como fim de chamada.
- Titulo e idioma da janela podem mudar entre versoes; por isso o modo assistido e o padrao e a allowlist e configuravel.
- Apps sem sessao Core Audio atribuivel ou sem janela superior classificavel continuam dependentes do inicio manual.
- A validacao manual deve cobrir Meet, Teams ou Zoom, Discord, YouTube, ditado, mute, headset secundario e duas plataformas simultaneas.

## Referencias oficiais

- [Audio sessions](https://learn.microsoft.com/en-us/windows/win32/coreaudio/audio-sessions)
- [IAudioSessionEnumerator](https://learn.microsoft.com/en-us/windows/win32/api/audiopolicy/nn-audiopolicy-iaudiosessionenumerator)
- [IAudioSessionControl2.GetProcessId](https://learn.microsoft.com/en-us/windows/win32/api/audiopolicy/nf-audiopolicy-iaudiosessioncontrol2-getprocessid)
- [IMMDeviceEnumerator.EnumAudioEndpoints](https://learn.microsoft.com/en-us/windows/win32/api/mmdeviceapi/nf-mmdeviceapi-immdeviceenumerator-enumaudioendpoints)
- [EnumWindows](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-enumwindows)
- [CsWin32](https://microsoft.github.io/CsWin32/docs/getting-started.html)
