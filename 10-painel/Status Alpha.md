---
title: Status Alpha
aliases: [Painel Alpha, Roadmap Alpha]
tags: [projeto/anamnesis, dashboard, alpha]
type: dashboard
created: 2026-08-04
updated: 2026-08-04
status: growing
summary: Medição ponderada e auditável do caminho até uma alpha local testável.
related: ["[[Anamnesis Home]]", "[[Projeto MOC]]", "[[Indice de SPEKs]]", "[[Protocolo de Agentes]]"]
---

# Status da versão alpha

> **Progresso do escopo de engenharia: 27%**  
> **Fluxo ponta a ponta testável: 0%**

O primeiro indicador reconhece a fundação já entregue. O segundo só avança quando for possível executar, na mesma máquina, o fluxo completo: gravar, transcrever, gerar ata, arquivar e aplicar a retenção configurada. Assim, evitamos confundir estrutura pronta com uma alpha utilizável.

## Medição ponderada

| Entrega para a alpha | Peso | Concluído | Avanço | Evidência atual |
| --- | ---: | ---: | ---: | --- |
| Especificações, ADRs e protocolo multi-LLM | 5% | 100% | 5% | SPEKs 001–004, ADRs 001–003 e protocolo versionados |
| Solução .NET, qualidade e TDD inicial | 5% | 100% | 5% | Solução compilável e 7 testes automatizados verdes |
| Ciclo de vida de reunião no domínio | 10% | 60% | 6% | Estados e transições essenciais cobertos por testes; persistência ainda ausente |
| Fila local durável | 10% | 100% | 10% | `SqliteJobQueue` com reserva atômica, liberação e conclusão testadas |
| Persistência de reuniões | 10% | 0% | 0% | Falta `SqliteReuniaoRepository` |
| Worker, retomada e política de tentativas | 10% | 0% | 0% | Falta consumidor real da fila e recuperação após reinício |
| Gravação automática com OBS | 15% | 0% | 0% | Falta adaptador `ObsGravador` e gatilho inicial de reunião |
| Preparação de áudio e transcrição local | 15% | 0% | 0% | Falta `WhisperTranscritor` e diagnóstico dos binários/modelos |
| Ata estruturada por CLI de LLM | 10% | 0% | 0% | Falta adaptador de processo, contrato de saída e validação |
| Arquivamento e retenção segura | 5% | 20% | 1% | `DiscoArquivador` existe; faltam política, exclusão recuperável e testes |
| Tray, configuração e diagnósticos | 3% | 0% | 0% | Projeto criado, sem interface operacional |
| Empacotamento e teste manual de alpha | 2% | 0% | 0% | Falta publicação self-contained e roteiro de validação |
| **Total** | **100%** |  | **27%** |  |

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

1. Especificar e implementar `SqliteReuniaoRepository`.
2. Ligar Worker, `ProcessarReuniaoHandler` e `IJobQueue`, com tentativa e retomada.
3. Integrar OBS para uma gravação de teste controlada.
4. Integrar Whisper local para uma transcrição de ponta a ponta.
5. Criar o `ProcessAtaRunner`: executa uma CLI configurada, recebe JSON validado e gera a ata sem acoplar a um provedor.
6. Concluir retenção segura, Tray mínimo e publicação `win-x64`.

## Próximo incremento

**SPEK-005 — Persistência de reunião em SQLite.** Ela desbloqueia o Worker e impede que a gravação, o caminho do áudio e o estado da reunião se percam entre reinicializações.

## Como atualizar esta medição

Atualize a linha correspondente apenas quando houver SPEK, teste automatizado e implementação mínima concluídos. O percentual é deliberadamente ponderado por valor para a alpha, não pela quantidade de arquivos ou commits.
