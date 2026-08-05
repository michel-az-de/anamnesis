---
title: SPEK-041 Resiliencia de Processos Externos
aliases: [SPEK-041, Processos Externos Sem Travar]
tags: [especificacao, cli, obs, whisper, ffmpeg, robustez, pos-alpha]
type: spek
created: 2026-08-05
updated: 2026-08-05
status: completed
summary: Nenhum processo externo pode travar o Anamnesis indefinidamente nem esconder a causa da falha.
related: ["[[SPEK-009 Ata Estruturada por CLI]]", "[[SPEK-024 Captura Universal de Audio pelo OBS]]", "[[SPEK-025 Prontidao Automatica do OBS]]", "[[SPEK-026 Prontidao Automatica do Docker]]"]
---

# SPEK-041 Resiliencia de processos externos

## Objetivo

Garantir que a integracao com OBS, CLI de ata, Whisper e FFmpeg sempre termine, com erro compreensivel, em vez de travar o processo ou trocar a causa real por um efeito colateral.

## Contexto do defeito

O runner da CLI escrevia a transcricao inteira na entrada padrao e so depois comecava a ler a saida. Uma CLI que emitisse qualquer coisa antes de consumir toda a entrada enchia o buffer do pipe, e os dois processos travavam um esperando o outro, sem limite de tempo. O teste reproduziu o travamento com uma transcricao de um megabyte.

Ao encerrar a gravacao, a restauracao da cena do OBS usava um token nao cancelavel. Se o OBS parasse de responder, o encerramento nunca retornava. O laco que correlaciona a resposta a solicitacao tambem descartava mensagens sem limite.

A limpeza de arquivos temporarios do Whisper e do FFmpeg trocava o erro real por um erro de E/S quando o arquivo ainda estava bloqueado.

As mensagens de timeout dos preflights citavam duracoes fixas que nao correspondiam aos parametros usados.

## Fora de escopo

- Nova tentativa automatica de qualquer processo externo.
- Cancelamento do processamento em curso pela interface.
- Persistir a cena anterior do OBS entre reinicios do Tray, registrada como limitacao aceita.
- Substituir o protocolo obs-websocket ou o modo de invocacao do Whisper.

## Regras

- O runner da CLI comeca a drenar saida e erro antes de escrever a entrada.
- A falha da CLI inclui a saida de erro na mensagem, como Whisper e FFmpeg ja faziam.
- A restauracao da cena do OBS tem limite de tempo proprio e nunca segura o encerramento da gravacao.
- O caminho da gravacao encerrada tem prioridade sobre qualquer restauracao visual.
- O laco de correlacao de respostas do OBS tem limite de mensagens descartadas.
- A limpeza de arquivos temporarios nunca substitui a excecao que a provocou.
- Mensagens de timeout derivam dos parametros reais de espera, e nao de texto fixo.
- Cada processo externo possui deadline interno, mesmo quando o chamador fornece `CancellationToken.None`.
- Ao atingir o deadline ou receber cancelamento, o adaptador encerra com seguranca toda a arvore do processo iniciado.
- Nenhuma dependencia nova e necessaria, portanto esta SPEK nao exige ADR.

## Critérios de aceite

- [x] Uma CLI que escreve antes de drenar a entrada conclui com transcricao de um megabyte.
- [x] A falha da CLI expoe o texto emitido no erro padrao e o codigo de saida.
- [x] Encerrar a gravacao retorna o caminho mesmo com o OBS sem responder a restauracao de cena.
- [x] As mensagens de timeout dos preflights refletem os parametros injetados.
- [x] A suite existente permanece verde.
- [x] CLI de ata, Whisper, FFmpeg e `docker info` encerram dentro do deadline interno com chamador nao cancelavel.
- [x] Os processos travados sao encerrados sem deixar filhos executando.

## Testes associados

- `CliAtaRunnerTests.DeveConcluirQuandoACliEscreveAntesDeDrenarAEntrada`, com guarda de tempo que falha em vez de pendurar a suite.
- `CliAtaRunnerTests.DeveIncluirSaidaDeErroNaFalhaDaCli`.
- `ObsGravadorTests.DeveEncerrarMesmoQuandoRestauracaoDeCenaNaoResponde`, com o servidor falso silenciando apos o `StopRecord`.
- `ObsProcessPreflightTests` e `DockerProcessPreflightTests` exigindo mensagem derivada.
- `ProcessosExternosDeadlineTests` cobre CLI de ata, Whisper, FFmpeg e verificacao do Docker com `CancellationToken.None`.
- Os fakes travados registram o PID e o teste confirma que a arvore do processo foi encerrada.
- Nenhum teste unitario chama OBS, rede ou CLI real: o OBS e um servidor falso local e a CLI e um script.

## Execucao local

- Red registrado: o teste da CLI travou os 30 segundos da guarda antes da correcao.
- Green apos a correcao: mesmo teste em menos de um segundo; o teste do OBS consome os 5 segundos do limite de restauracao, como esperado.
- `dotnet test Anamnesis.sln`, 136 testes verdes e 0 avisos.
- Red adicional: quatro erros de compilacao provaram a ausencia de deadlines injetaveis nos adaptadores.
- Green adicional: 4 testes de processo travado passaram em 2 segundos; 12 testes afetados passaram em 1 segundo, Release e sem avisos.

## Deadlines internos

| Processo | Deadline padrao |
| --- | ---: |
| CLI de ata | 10 minutos |
| FFmpeg | 10 minutos |
| Whisper | 60 minutos |
| `docker info` | 15 segundos por verificacao |

Os valores podem ser reduzidos por opcao ou construtor em testes. Timeout e cancelamento encerram toda a arvore do processo antes de devolver o erro ao chamador.

## Decisoes pendentes

- Nenhuma para este incremento. A cena anterior do OBS continua em memoria: um Tray encerrado a forca entre iniciar e encerrar deixa o OBS na cena Anamnesis. Persistir isso exigiria coluna nova para um efeito puramente visual, e fica registrado como limitacao aceita.
