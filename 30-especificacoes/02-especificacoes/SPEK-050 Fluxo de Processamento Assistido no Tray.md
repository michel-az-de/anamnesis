---
title: SPEK-050 Fluxo de Processamento Assistido no Tray
aliases: [SPEK-050, Processamento Visível, Título Manual da Reunião]
tags: [especificacao, tray, reuniao, transcricao, observabilidade, ux]
type: spek
created: 2026-08-06
updated: 2026-08-06
status: concluído
summary: Permite nomear a gravação manual, acompanhar o processamento sem refresh da tela e abrir o detalhe concluído com a transcrição.
related: ["[[SPEK-030 Desktop com Dados Reais]]", "[[SPEK-031 Observabilidade Operacional Real]]", "[[SPEK-029 Polimento Visual e Motion Desktop]]"]
---

# SPEK-050 | Fluxo de processamento assistido no Tray

## Objetivo

Dar continuidade visível a uma reunião iniciada manualmente: a pessoa define o título antes da gravação, acompanha a transcrição sem perder a aba escolhida, acessa o console seguro da reunião e abre o detalhe final já com a transcrição disponível.

## Regras

- Todo início manual de gravação pelo Command Deck ou menu do Tray pede um título antes de chamar `ControlarGravacaoHandler`.
- O título é normalizado com `Trim`; cancelar o diálogo não cria reunião, job nem chamada ao OBS. Um campo vazio usa `Reunião sem título` para preservar o fluxo manual atual.
- Durante o processamento real sem percentual verificável, a interface mostra barra indeterminada com o estado derivado do job e da reunião. Ela nunca inventa porcentagem de Whisper, CLI ou arquivamento.
- A barra e sua ação contextual permitem abrir o console de observabilidade filtrado pelo `ReuniaoId` corrente. O console mantém as regras de redacao, retenção e limite da SPEK-031.
- Quando a reunião estiver arquivada e tiver transcrição persistida, a ação de conclusão abre o detalhe daquela reunião diretamente na aba de transcrição.
- O polling local continua leve, somente com a janela visível e sem sobreposição, mas não pode chamar `Navegar(_paginaAtual)` nem reconstruir a árvore da página para atualizar dados. Navegação, foco, filtros e campos em edição permanecem estáveis.
- Atualizações de estado global, indicador de processamento e console são incrementais. Dados de uma tela só podem ser reconstruídos por ação explícita da pessoa ou por uma atualização localizada que preserve seu estado.
- O Tray continua usando casos de uso, queries e journal existentes. Nenhuma tela acessa SQLite, OBS, Worker ou arquivos diretamente.
- Testes unitários não chamam OBS, rede, CLI, Worker ou modelos reais.

## Critérios de aceite

- [x] O início manual pelo Command Deck pede título editável antes de iniciar a gravação.
- [x] O menu do Tray usa o mesmo fluxo de título manual.
- [x] Cancelar o título não cria reunião, job nem chamada ao gravador.
- [x] Título vazio preserva o padrão `Reunião sem título`; título informado aparece no histórico e detalhe persistidos.
- [x] Uma reunião em transcrição ou processamento apresenta estado e barra indeterminada sem percentual falso.
- [x] A ação da barra abre o console seguro filtrado pela reunião em processamento.
- [x] A conclusão apresenta ação que abre o detalhe correto já na aba de transcrição.
- [x] Eventos novos do Worker não recriam a aba ativa, não removem controles nem alteram a navegação, filtros ou foco da pessoa.
- [x] Regressões cobrem título, cancelamento, polling durante transcrição, console correlacionado e abertura da transcrição concluída.
- [x] A suíte Release permanece verde sem dependências novas.

## Evidências

- `DesktopPocStateTests` cobre título informado e o padrão para campo vazio.
- `DesktopPocFormTests.FluxoManualDeveExibirProgressoFiltrarLogsEAbrirTranscricaoAoConcluir` cobre cancelamento, título normalizado, barra indeterminada, filtro `r:{ReuniaoId}`, preservação do campo de filtro durante polling e abertura da aba Transcrição.
- `DesktopPocFormTests.ModoRealDeveManterDetalheAbertoNaAbaSelecionadaQuandoJobMuda` impede a reconstrução do detalhe ativo.
- `dotnet test Anamnesis.sln -c Release --no-restore`: 284 testes verdes, sem dependências novas.

## Fora de escopo

- Progresso percentual real de Whisper, geração de ata ou arquivamento.
- Edição posterior de título, transcrição ou ata.
- Telemetria remota, logs com conteúdo confidencial ou novo armazenamento.
- Redesenho do Design System Desktop ou migração para outra tecnologia de interface.

## Fluxo esperado

```text
Iniciar manualmente
        ↓
Título editável ou padrão
        ↓
Gravação e job persistidos
        ↓
Processando: barra indeterminada + abrir console correlacionado
        ↓
Transcrição e ata concluídas
        ↓
Abrir detalhe da reunião na aba Transcrição
```

## Sequência TDD

1. Red: título manual e cancelamento não possuem contrato de interface.
2. Green: compor o diálogo mínimo reutilizado por Desktop e Tray.
3. Red: polling recria a aba ativa quando eventos da transcrição chegam.
4. Green: separar atualização incremental de navegação e preservar o estado visual.
5. Red: processamento não tem ação contextual para logs e conclusão.
6. Green: derivar indicador, filtro correlacionado e abertura do detalhe com transcrição.
7. Refactor: concentrar a decisão de atualização visual sem criar abstração genérica de UI.

## Decisões pendentes

- Nenhuma. O título é opcional com padrão explícito e o progresso é indeterminado até existir fonte percentual verificável.
