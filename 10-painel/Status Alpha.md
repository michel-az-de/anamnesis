---
title: Status Alpha
aliases: [Painel Alpha, Roadmap Alpha]
tags: [projeto/anamnesis, dashboard, alpha]
type: dashboard
created: 2026-08-04
updated: 2026-08-05
status: growing
summary: Medição ponderada e auditável do caminho até uma alpha local testável.
related: ["[[Anamnesis Home]]", "[[Projeto MOC]]", "[[Indice de SPEKs]]", "[[Protocolo de Agentes]]"]
---

# Status da versão alpha

> **Progresso do escopo de engenharia: 100%**
> **Fluxo ponta a ponta hermético testado: 100%**
> **Fluxo ponta a ponta com pré-requisitos reais: 100%**

O primeiro indicador reconhece a fundação já entregue. O segundo registra o fluxo integrado exercitado sem rede externa, com substitutos locais para os binários indisponíveis. O terceiro só avança quando for possível executar, na mesma máquina, o fluxo completo com OBS, Whisper e uma CLI autenticada configurados. Assim, evitamos confundir estrutura pronta com uma alpha utilizável.

## Medição ponderada

| Entrega para a alpha | Peso | Concluído | Avanço | Evidência atual |
| --- | ---: | ---: | ---: | --- |
| Especificações, ADRs e protocolo multi-LLM | 5% | 100% | 5% | SPEKs 001–004, ADRs 001–003 e protocolo versionados |
| Solução .NET, qualidade e TDD inicial | 5% | 100% | 5% | Solução compilável e 49 testes automatizados verdes, incluindo Tray e Worker black box |
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

1. Alpha concluída nesta máquina; repetir o roteiro em uma instalação Windows limpa antes de distribuir uma beta.

## Próximo incremento

**Próximo ciclo: instalador e validação em Windows limpo.** Abrir uma SPEK de beta antes de alterar o empacotamento.

## Como atualizar esta medição

Atualize a linha correspondente apenas quando houver SPEK, teste automatizado e implementação mínima concluídos. O percentual é deliberadamente ponderado por valor para a alpha, não pela quantidade de arquivos ou commits.
