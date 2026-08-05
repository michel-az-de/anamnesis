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
- Uma segunda instancia aguarda o dono atual liberar o mutex; depois da transferencia, consulta a fila sob exclusividade e pode processar somente o trabalho que ainda restar.
- A mera contencao nao produz codigo de saida diferente de zero: o Tray nao deve alarmar o usuario por uma situacao normal.
- Mutex abandonado equivale a aquisicao bem-sucedida, porque significa que o Worker anterior morreu sem liberar e o novo e justamente quem deve retomar.
- Deter o mutex e a invariante que torna correta a liberacao incondicional de reservas, e essa premissa fica escrita no contrato da fila e no consumidor.
- A guarda fica na raiz de composicao do Worker, depois do ramo de retencao, para nao transformar comandos pontuais de retencao em no-op durante um processamento longo.
- Se outro Worker ja detiver o mutex, a nova instancia aguarda a transferencia da exclusividade e consulta a fila depois de adquiri-la. Ela nao pode desistir entre a ultima consulta do dono anterior e a liberacao do mutex, pois esse intervalo perderia o aviso do job ja enfileirado pelo Tray.
- Datas persistidas sao normalizadas para UTC antes da serializacao, para que a ordenacao textual da fila corresponda a ordem cronologica.
- O enfileiramento de um job e atomico: insercao e leitura ocorrem na mesma transacao e a leitura e deterministica.
- Nenhuma dependencia nova e necessaria: `Mutex` e da biblioteca base.

## Critérios de aceite

- [x] Dois processos Worker iniciados sobre o mesmo banco processam o job uma unica vez e ambos encerram com codigo zero.
- [x] Enquanto aguarda, a segunda instancia nao altera a fila; depois da transferencia, o mesmo job concluido nao e processado novamente.
- [x] Uma instancia liberada permite que a proxima adquira a exclusividade.
- [x] Bancos diferentes admitem Workers simultaneos.
- [x] O nome do mutex e o mesmo para caminhos equivalentes do mesmo banco e diferente para bancos distintos.
- [x] Enfileirar a mesma reuniao concorrentemente produz um unico job ativo.
- [x] A fila e ordenada pelo instante UTC, e nao pelo texto da data.
- [x] A suite existente permanece verde.
- [x] Um job enfileirado durante o encerramento do Worker ativo e processado pela instancia que aguardava o mutex, sem janela TOCTOU.
- [x] O E2E black box encerra e mata a arvore do Worker quando o processo excede o limite de tempo.

## Testes associados

- `InstanciaUnicaWorkerTests` para aquisicao, exclusao mutua, transferencia bloqueante, liberacao, isolamento por banco e derivacao do nome.
- `SqliteJobQueueTests` para enfileiramento concorrente e ordenacao por instante UTC.
- `WorkerBlackBoxE2ETests.DeveProcessarUmaUnicaVezQuandoDoisWorkersIniciamJuntos`, com dois processos reais, Whisper lento e contador de invocacoes.
- `WorkerBlackBoxE2ETests.DeveProcessarJobEnfileiradoDuranteTransferenciaDeExclusividade`, com o dono anterior retido ate o sucessor estar aguardando.
- `WorkerBlackBoxE2ETests.DeveEncerrarArvoreDoWorkerQuandoExcedeLimite`, com Whisper travado, timeout e verificacao de liberacao do mutex apos `Kill(entireProcessTree: true)`.
- Nenhum teste unitario chama OBS, rede ou CLI real.

## Execucao local

- Red registrado contra o binario anterior a correcao: codigo de saida 1 e `Falha do Worker: A reuniao esta em 'EmTranscricao'`.
- Green apos a correcao: `dotnet test Anamnesis.sln --configuration Release`, 129 testes verdes e 0 avisos.
- Reabertura por regressao: o teste de transferencia nao compilava porque so existia aquisicao imediata; a espera fixa antes do encerramento ainda deixava uma janela TOCTOU apos a ultima leitura.
- Green da reabertura: 11 testes direcionados de `InstanciaUnicaWorkerTests` e `WorkerBlackBoxE2ETests` verdes em Release; o E2E mata a arvore travada em cinco segundos e libera o mutex abandonado.

## Decisoes pendentes

- Nenhuma para este incremento. O prazo de validade de reserva foi avaliado e recusado no ADR-012, por piorar a recuperacao de queda sem proteger nada sob a exclusividade.
