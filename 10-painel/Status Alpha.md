---
title: Status Alpha
aliases: [Painel Alpha, Roadmap Alpha]
tags: [projeto/anamnesis, dashboard, alpha]
type: dashboard
created: 2026-08-04
updated: 2026-08-06
status: growing
summary: Medição ponderada e auditável do caminho até uma alpha local testável.
related: ["[[Anamnesis Home]]", "[[Projeto MOC]]", "[[Indice de SPEKs]]", "[[Protocolo de Agentes]]", "[[Roadmap de Produto]]"]
---

# Status da versão alpha

> **Progresso do escopo de engenharia: 100%**
> **Fluxo ponta a ponta hermético testado: 100%**
> **Fluxo ponta a ponta com pré-requisitos reais: 100%**
> **Achados de robustez fechados: 15 de 16**

O primeiro indicador reconhece a fundação já entregue. O segundo registra o fluxo integrado exercitado sem rede externa, com substitutos locais para os binários indisponíveis. O terceiro só avança quando for possível executar, na mesma máquina, o fluxo completo com OBS, Whisper e uma CLI autenticada configurados. Assim, evitamos confundir estrutura pronta com uma alpha utilizável.

O quarto indicador acompanha dezesseis achados conhecidos: os três primeiros indicadores mediam funcionalidade entregue, não resiliência. Ele é deliberadamente uma contagem, e não um percentual de robustez. Quinze foram corrigidos nas SPEKs 040 a 044, 046 e 048; a concentração de 24% da base em dois arquivos de interface segue aberta por decisão de escopo.

Duas limitações continuam registradas: um Worker morto no meio da transcrição custa uma execução extra antes de recuperar, porque a recuperação vive na máquina de estados e exige SPEK própria; e a cena anterior do OBS não sobrevive a um Tray encerrado à força.

## Medição ponderada

| Entrega para a alpha | Peso | Concluído | Avanço | Evidência atual |
| --- | ---: | ---: | ---: | --- |
| Especificações, ADRs e protocolo multi-LLM | 5% | 100% | 5% | SPEKs 001–004, ADRs 001–003 e protocolo versionados |
| Solução .NET, qualidade e TDD inicial | 5% | 100% | 5% | Solução compilável e 285 testes automatizados verdes, incluindo OBS, Docker, Tray, Worker black box, Desktop real, observabilidade, motion, detecção local e release Windows |
| Ciclo de vida de reunião no domínio | 10% | 100% | 10% | Estados, falha, retentativa, arquivamento e retenção cobertos por testes e persistência |
| Fila local durável | 10% | 100% | 10% | `SqliteJobQueue` com reserva atômica, liberação e conclusão testadas |
| Persistência de reuniões | 10% | 100% | 10% | `SqliteReuniaoRepository` persiste e restaura o agregado com testes em banco temporário |
| Worker, retomada e política de tentativas | 10% | 100% | 10% | Host compõe adaptadores locais, recupera reservas e consome jobs sequencialmente |
| Gravação automática com OBS | 15% | 100% | 15% | `ObsGravador` via obs-websocket v5 e fluxo de gravação com persistência e job testados |
| Preparação de áudio e transcrição local | 15% | 100% | 15% | FFmpeg real prepara o áudio e whisper.cpp executa localmente pela imagem Docker oficial |
| Ata estruturada por CLI de LLM | 10% | 100% | 10% | Codex CLI autenticado usa UTF-8, JSON validado e renderiza ata Markdown determinística |
| Arquivamento e retenção segura | 5% | 100% | 5% | Política de sete dias, simulação, transições compensáveis e Lixeira do Windows cobertas por testes |
| Tray, configuração e diagnósticos | 3% | 100% | 3% | Tray WinForms, configuração JSON local e diagnósticos de pré-requisitos com testes |
| Empacotamento e teste manual de alpha | 2% | 100% | 2% | `win-x64-final-8` validado ponta a ponta com OBS, Whisper Docker, Codex e Lixeira reais |
| **Total** | **100%** |  | **100%** |  |

## O que define uma alpha testável

A alpha estará pronta quando, em uma máquina Windows limpa e com pré-requisitos documentados, for possível:

1. abrir o Tray e confirmar os diagnósticos de OBS, Whisper e da CLI de LLM escolhida;
2. iniciar e encerrar uma gravação de teste pelo OBS;
3. persistir a reunião e criar um job local durável;
4. retomar o processamento após reiniciar o Worker;
5. produzir uma transcrição local e uma `ata.md` estruturada a partir de uma CLI já autenticada;
6. arquivar ata e transcrição em pasta configurada;
7. aplicar a retenção somente após o arquivamento, primeiro em modo de simulação e depois para a Lixeira do Windows.

## Caminho crítico

1. Alpha concluída nesta máquina.
2. Instalador da beta validado em VM Windows limpa pelo GitHub Actions.

## Entrega pós-alpha

| Entrega | Estado | Evidência |
| --- | --- | --- |
| Instalador `0.1.0-beta.1` | SPEK 100% concluída | Workflow `31017790651`, EXE, SHA-256 e logs publicados |
| Worker automático pelo Tray | SPEK 100% concluída | 53 testes e workflow `31020179379` verdes |
| Captura universal de áudio | SPEK 100% concluída | OBS real capturou sistema e microfone; Whisper transcreveu e Codex gerou ata no E2E `20260805-real-04` |
| Prontidão automática do OBS | SPEK 100% concluída | Tray iniciou OBS inicialmente fechado e concluiu o E2E real `obs-preflight-e2e/20260805-real-04` |
| Prontidão automática do Docker | SPEK 100% concluída | Worker iniciou Docker parado, transcreveu e arquivou no E2E real `docker-preflight-e2e/20260805-real-02` |
| POC desktop Windows | SPEK 100% concluída | Janela WinForms navegável, tema claro/escuro inclusive na moldura DWM, ciclo simulado coberto por teste STA e executável em `artifacts/poc-desktop/win-x64-system-theme-v2` |
| Console local de observabilidade | SPEK 100% concluída | Eventos seguros, filtros, correlação, métricas e ciclo simulado cobertos por 83 testes; executável em `artifacts/poc-desktop/win-x64-observability-v1` |
| Design System Desktop v3 | SPEK 100% concluída | Command Deck sólido, sem transparência, com ícones vetoriais, inputs próprios, motion contextual e 96 testes; executável em `artifacts/poc-desktop/win-x64-command-deck-v2` |
| Desktop com dados reais | SPEK 100% concluída | Histórico e jobs no SQLite, comandos reais, manifesto, recuperação de gravação órfã, publicação e E2E controlado de Tray + Worker |
| Observabilidade operacional real | SPEK 100% concluída | Journal SQLite isolado, 14 códigos correlacionados, console com dados reais, retenção de 14 dias, canário de privacidade e publicação `artifacts/publish/SPEK-031` ([[ADR-015 Journal SQLite Isolado]]) |
| Captura instantânea e detecção local | 12 de 13 critérios atendidos | Política assistida e automática opt-in, Core Audio + User32 reais, recuperação segura, JSONL sanitizado, E2E controlado e 249 testes verdes; ensaio Meet + Teams/Zoom pendente ([[ADR-016 Deteccao Local por Core Audio e User32]]) |
| Experiência Windows instalada | SPEK 100% concluída | Instalador `0.2.0-beta.1` validado nesta máquina e no runner `windows-2025`; identidade própria, uma instância, um atalho, startup `--background`, primeiro uso, Worker interno e desinstalação com logs comprovados pelo run `31068430123` ([[SPEK-045 Experiencia Windows Instalada e Primeiro Uso]]) |
| Instalador resiliente com atualização e reparo | SPEK 100% concluída | O run `31109639731` migrou beta.1 para beta.4, preservou startup, atalho, configuração, banco e reunião, bloqueou dois downgrades modernos, reparou o Worker e desinstalou com encerramento cooperativo ([[SPEK-047 Instalador Resiliente com Atualizacao e Reparo]]) |
| Concorrência de Worker e fila | SPEK 100% concluída | Mutex por banco impede dois Workers no mesmo job; Red registrado com `A reunião está em 'EmTranscricao'` e saída 1, Green com E2E de dois processos reais ([[ADR-012 Instancia Unica do Worker]]) |
| Ciclo de vida e recuperação da reunião | SPEK 100% concluída | Falha ao encerrar registra `Falha` e libera o índice de gravação ativa; nova gravação possível sem reiniciar o Tray, provado contra SQLite real |
| Resiliência de processos externos | SPEK 100% concluída | Deadlock da CLI reproduzido em 30 s e corrigido para menos de 1 s; restauração de cena do OBS com limite de 5 s, verificado por teste |
| Persistência local determinística | SPEK 100% concluída | Esquema preparado uma vez por instância, banco em WAL e engine SQLite embarcada com versão verificada; +1,9 MB em 121 MB publicados ([[ADR-013 Engine SQLite Embarcada]]) |
| Configuração local protegida | SPEK 100% concluída | Senha do OBS protegida por DPAPI com migração transparente do formato em texto claro ([[ADR-014 Protecao de Segredos Locais]]) |
| Espera finita pela exclusividade do Worker | SPEK 100% concluída | Teto de 5 minutos, saída zero, fila intacta e evento `worker.exclusividade_expirada` persistido no journal ([[SPEK-046 Limite da Espera por Exclusividade]]) |
| Inicialização concorrente do journal | SPEK 100% concluída | Trava de arquivo entre processos, checagem read-only do schema e cem primeiros acessos concorrentes estabilizados ([[SPEK-048 Inicializacao Concorrente do Journal SQLite]]) |
| Release canônico do instalador Windows | SPEK 100% concluída | O run [`31126791469`](https://github.com/michel-az-de/anamnesis/actions/runs/31126791469) concluiu 285 testes Release, smoke instalado e promoção. A [release `v0.2.0-beta.6`](https://github.com/michel-az-de/anamnesis/releases/tag/v0.2.0-beta.6) é imutável, atestada por `gh release verify` e tem EXE, `SHA256SUMS.txt` e `release.json` verificados por `gh release verify-asset`; SHA-256 oficial do EXE: `80ca930b38954100d6f733200c4ed7e8cbc291ea3c52a1ea498b0806b0d38cb9` ([[SPEK-049 Release Canonico do Instalador Windows]]) |
| Fluxo de processamento assistido no Tray | SPEK 100% concluída | Título manual no Command Deck e no menu do Tray, barra indeterminada, console correlacionado por `ReuniaoId`, abertura direta na aba Transcrição e polling incremental sem recriar a página ativa; 285 testes Release verdes ([[SPEK-050 Fluxo de Processamento Assistido no Tray]]) |

## Próximo incremento

**Próximo ciclo: concluir a [[SPEK-032 Captura Instantanea e Deteccao Local]].** A SPEK-050 corrigiu o refresh durante transcrição e tornou o fluxo manual de reunião visível e acionável.

## Roadmap pós-alpha

O planejamento pós-alpha está documentado em [[Roadmap de Produto]]. As SPEKs 030, 031, 045, 046, 048, 049 e 050 foram concluídas; a SPEK-032 está em validação com 12 de 13 critérios atendidos; e as SPEKs 033 a 039 permanecem em rascunho para agendas Google e Microsoft, Obsidian, Trello e Azure DevOps.

Esse roadmap não altera os 100% da alpha. Novos percentuais só serão criados quando houver uma versão-alvo com pesos próprios, evitando misturar produto futuro com a medição já encerrada.

## Como atualizar esta medição

Atualize a linha correspondente apenas quando houver SPEK, teste automatizado e implementação mínima concluídos. O percentual é deliberadamente ponderado por valor para a alpha, não pela quantidade de arquivos ou commits.
