---
title: SPEK-027 Desktop Windows para Estado Visivel
aliases: [SPEK-027, Desktop Windows, Estado Visível]
tags: [especificacao, desktop, windows, tray, pos-alpha]
type: spek
created: 2026-08-05
updated: 2026-08-05
status: completed
summary: POC nativa Windows para validar navegação, histórico e comportamento antes da integração com dados reais.
related: ["[[Status Alpha]]", "[[SPEK-010 Tray Configuracao e Diagnosticos]]", "[[SPEK-023 Orquestracao do Worker pelo Tray]]"]
---

# SPEK-027 Desktop Windows para Estado Visível

## Objetivo

Entregar uma POC executável e nativa do Windows dentro do projeto `Anamnesis.Tray`, permitindo validar a experiência visual de gravação, processamento, histórico, conteúdo da reunião e configurações sem depender de OBS, Docker, Whisper ou CLI reais.

## Fora de escopo

- Consultar SQLite, fila ou arquivos reais.
- Iniciar OBS, Docker, Whisper, Worker ou CLI.
- Substituir o fluxo operacional atual do ícone de bandeja.
- Persistir alterações feitas durante a simulação.
- Definir a tecnologia visual definitiva após a POC.

## Regras

- O argumento `--poc-desktop` abre diretamente uma janela WinForms nativa.
- O modo POC não carrega configuração local nem aciona integrações externas.
- A janela oferece Início, Reuniões, Ao vivo, Tarefas, Atividade e Configurações.
- Iniciar gravação ativa cronômetro e indicadores simulados de áudio.
- Encerrar gravação cria uma reunião simulada em processamento e, depois, concluída.
- Uma reunião permite consultar resumo, transcrição, decisões, tarefas e arquivos simulados.
- A POC deve usar a identidade visual documentada e continuar legível em 1280 x 720.
- A POC deve herdar o tema de aplicativos do Windows ao abrir, incluindo modo escuro e modo claro.
- Cores de fundo, superfícies, textos, bordas, seleção e estados devem vir de tokens semânticos da paleta do tema.
- O modo normal do Tray permanece compatível.

## Critérios de aceite

- [x] `--poc-desktop` é reconhecido sem interferir nos demais argumentos.
- [x] A janela abre sem configuração, OBS, Docker, Whisper ou CLI instalados.
- [x] A navegação principal atualiza o conteúdo sem abrir outra janela.
- [x] A gravação simulada percorre parado, gravando, processando e concluído.
- [x] O histórico e os detalhes de reunião podem ser explorados.
- [x] A publicação `win-x64` produz um `Anamnesis.Tray.exe` executável com a POC.
- [x] Testes automatizados e validação visual no Windows estão registrados.
- [x] O tema escuro do Windows produz superfícies escuras, texto legível e controles coerentes.
- [x] O tema claro permanece disponível sem alterar o comportamento funcional.

## Testes associados

- `DesktopPocOptionsTests`
- `DesktopPocStateTests`
- `DesktopPocThemeTests`
- `dotnet test Anamnesis.sln --configuration Release --no-restore --verbosity minimal`
- Abertura manual do executável publicado com `--poc-desktop`.

## Evidências da entrega

- Ciclo Red: os testes falharam pela ausência de `DesktopPocOptions`, `DesktopPocState` e `EtapaDesktopPoc`.
- Ciclo Green: 77 testes verdes em `Release`, incluindo navegação, gravação simulada e paletas clara e escura.
- Tema do ambiente validado: `AppsUseLightTheme=0`, portanto a POC abriu no modo escuro.
- Publicação autocontida: `artifacts/poc-desktop/win-x64-system-theme-v2`.
- Atalho executável: `artifacts/poc-desktop/win-x64-system-theme-v2/Abrir Anamnesis POC.lnk`.
- Janela publicada: processo responsivo com título `Anamnesis`.
- Moldura nativa: `DwmGetWindowAttribute(DWMWA_USE_IMMERSIVE_DARK_MODE)=1` na janela publicada.

## Tema e tokens

| Token | Uso |
| --- | --- |
| `Fundo` e `Superficie` | Janela, páginas, cartões e campos |
| `Navegacao` e `Selecao` | Barra lateral e item ativo |
| `Texto` e `TextoSecundario` | Hierarquia e contraste de leitura |
| `Borda` | Separação discreta entre superfícies |
| `Destaque`, `Positivo` e `Perigo` | Ações e estados semânticos |

O WinForms usa `SystemColorMode.System` para controles nativos. `WindowsTitleBarTheme` aplica o atributo DWM oficial à moldura, enquanto `DesktopPocPalette` resolve os tokens customizados para claro ou escuro na abertura da janela.

## Como executar

1. Abra `artifacts/poc-desktop/win-x64-system-theme-v2/Abrir Anamnesis POC.lnk`.
2. Ou execute `dotnet run --project src/Anamnesis.Tray -- --poc-desktop`.

## Decisões pendentes

- Após a validação do usuário, decidir entre evoluir WinForms ou migrar o shell para WinUI 3.
- Definir em outra SPEK as consultas reais de reuniões, jobs e artefatos.
