---
title: SPEK-040 Concorrencia de Worker e Fila
aliases: [SPEK-040, Instancia Unica do Worker]
tags: [especificacao, worker, fila, sqlite, concorrencia, robustez, pos-alpha]
type: spek
created: 2026-08-05
updated: 2026-08-05
status: completed
summary: Garante que apenas um Worker consuma a fila local, para que nenhum job seja processado duas vezes.
related: ["[[ADR-012 Instancia Unica do Worker]]", "[[SPEK-004 Fila Local de Jobs]]", "[[SPEK-006 Worker e Retomada de Processamento]]", "[[SPEK-023 Orquestracao do Worker pelo Tray]]"]
---

# SPEK-040 Concorrencia de Worker e fila

## Objetivo

Impedir que dois processos Worker consumam o mesmo job da fila local, eliminando transcricao duplicada e a falha espuria de reuniao que a corrida provoca.

## Contexto do defeito

O Tray dispara o Worker de tres pontos sem guarda de instancia unica: na inicializacao, no menu de pendencias e ao finalizar uma gravacao. Todo Worker que sobe executa `ReuniaoConsumer.RetomarAsync`, que zera `reservado_em` de todos os jobs nao concluidos.

Um segundo Worker iniciado enquanto o primeiro processa libera a reserva do primeiro e reserva o mesmo job. Os dois executam FFmpeg, Whisper e a CLI sobre o mesmo arquivo, e a maquina de estados da reuniao rejeita a transicao em ambos os caminhos. A reuniao termina em `Falha` mesmo com a transcricao concluida.

A reserva em si ja e atomica: `ReservarProximoAsync` usa um unico `UPDATE ... RETURNING`. A corrida esta apenas no caminho de retomada.

## Fora de escopo

- Processar jobs em paralelo dentro de um mesmo Worker.
- Politica de tentativas, backoff ou fila de descarte, tratada na SPEK-006.
- Recuperacao de gravacao orfa no Tray, tratada na SPEK-042.
- Reduzir de duas para uma as execucoes necessarias para recuperar uma reuniao presa em `EmTranscricao` apos queda, que vive na maquina de estados e exige SPEK propria.
- Servico do Windows, agendador ou execucao sem o Tray.
- Fila externa ou coordenacao entre maquinas.

## Regras

- O Worker adquire um mutex nomeado antes de tocar na fila, conforme ADR-012.
- O nome do mutex deriva do caminho normalizado do banco e nao e constante, para isolar usuarios distintos e bancos temporarios de teste.
- Uma segunda instancia encerra com codigo de saida zero e mensagem propria, sem alterar fila, reunioes ou artefatos.
- Codigo de saida zero e obrigatorio: o Tray trata saida diferente de zero como falha de inicializacao e alarmaria o usuario por uma situacao normal.
- Mutex abandonado equivale a aquisicao bem-sucedida, porque significa que o Worker anterior morreu sem liberar e o novo e justamente quem deve retomar.
- Deter o mutex e a invariante que torna correta a liberacao incondicional de reservas, e essa premissa fica escrita no contrato da fila e no consumidor.
- A guarda fica na raiz de composicao do Worker, depois do ramo de retencao, para nao transformar comandos pontuais de retencao em no-op durante um processamento longo.
- Antes de encerrar, o Worker faz uma segunda passagem na fila apos uma carencia curta, porque o Tray enfileira o job antes de lancar o Worker.
- Datas persistidas sao normalizadas para UTC antes da serializacao, para que a ordenacao textual da fila corresponda a ordem cronologica.
- O enfileiramento de um job e atomico: insercao e leitura ocorrem na mesma transacao e a leitura e deterministica.
- Nenhuma dependencia nova e necessaria: `Mutex` e da biblioteca base.

## Critérios de aceite

- [x] Dois processos Worker iniciados sobre o mesmo banco processam o job uma unica vez e ambos encerram com codigo zero.
- [x] A segunda instancia nao altera estado da reuniao nem invoca o transcritor.
- [x] Uma instancia liberada permite que a proxima adquira a exclusividade.
- [x] Bancos diferentes admitem Workers simultaneos.
- [x] O nome do mutex e o mesmo para caminhos equivalentes do mesmo banco e diferente para bancos distintos.
- [x] Enfileirar a mesma reuniao concorrentemente produz um unico job ativo.
- [x] A fila e ordenada pelo instante UTC, e nao pelo texto da data.
- [x] A suite existente permanece verde.

## Testes associados

- `InstanciaUnicaWorkerTests` para aquisicao, exclusao mutua, liberacao, isolamento por banco e derivacao do nome.
- `SqliteJobQueueTests` para enfileiramento concorrente e ordenacao por instante UTC.
- `WorkerBlackBoxE2ETests.DeveProcessarUmaUnicaVezQuandoDoisWorkersIniciamJuntos`, com dois processos reais, Whisper lento e contador de invocacoes.
- Nenhum teste unitario chama OBS, rede ou CLI real.

## Execucao local

- Red registrado contra o binario anterior a correcao: codigo de saida 1 e `Falha do Worker: A reuniao esta em 'EmTranscricao'`.
- Green apos a correcao: `dotnet test Anamnesis.sln --configuration Release`, 129 testes verdes e 0 avisos.

## Decisoes pendentes

- Nenhuma para este incremento. O prazo de validade de reserva foi avaliado e recusado no ADR-012, por piorar a recuperacao de queda sem proteger nada sob a exclusividade.
