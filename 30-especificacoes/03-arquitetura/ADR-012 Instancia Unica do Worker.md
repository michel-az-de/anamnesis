---
title: ADR-012 Instancia Unica do Worker
aliases: [Mutex do Worker, Exclusividade da Fila]
tags: [adr, worker, fila, concorrencia, windows]
type: adr
created: 2026-08-05
updated: 2026-08-05
status: accepted
summary: Apenas um processo Worker por banco local consome a fila, garantido por mutex nomeado derivado do caminho do banco.
related: ["[[SPEK-040 Concorrencia de Worker e Fila]]", "[[SPEK-006 Worker e Retomada de Processamento]]", "[[SPEK-023 Orquestracao do Worker pelo Tray]]"]
---

# ADR-012 | Instancia unica do Worker

## Contexto

O Tray lanca o Worker em tres momentos: ao abrir, ao finalizar uma gravacao e pelo menu de pendencias. Nenhum deles verificava se ja existia um Worker rodando.

Todo Worker que sobe executa `ReuniaoConsumer.RetomarAsync`, que libera as reservas ativas da fila para recuperar o trabalho de uma execucao anterior. Essa liberacao e correta sob a premissa de que ninguem mais esta processando — premissa que nada garantia.

Com dois Workers, o segundo liberava a reserva do primeiro e reservava o mesmo job. Os dois executavam FFmpeg, Whisper e a CLI sobre o mesmo arquivo, e a maquina de estados da reuniao rejeitava a transicao em ambos. A reuniao terminava em `Falha` com a transcricao ja concluida. O teste de caixa preta reproduziu exatamente isso: codigo de saida 1 e `A reuniao esta em 'EmTranscricao'`.

## Decisao

O Worker adquire um mutex nomeado antes de tocar na fila. Se outro processo ja for o dono, a nova instancia aguarda a transferencia da exclusividade, informa a contencao e so entao consulta a fila.

A espera e intencional. Encerrar imediatamente deixaria uma janela TOCTOU: o dono anterior poderia ja ter feito sua ultima leitura, enquanto o job que motivou a nova instancia ja estaria enfileirado. A transferencia garante que todo lancamento feito depois do enfileiramento tenha uma consulta da fila sob exclusividade.

O nome deriva do caminho normalizado do banco, e nao e constante: `Local\Anamnesis.Worker.<sha256[..32]>`. Isso mantem Workers de usuarios diferentes independentes e impede que bancos temporarios de teste colidam entre si ou com o Worker real da maquina de quem desenvolve.

O escopo e `Local\` e nao `Global\`: a instalacao e por usuario e o banco vive em `%LOCALAPPDATA%`, e criar objetos `Global\` exige privilegio que nem todo token interativo possui.

Mutex abandonado conta como aquisicao bem-sucedida: significa que o Worker anterior morreu sem liberar, e o novo e justamente quem deve retomar a fila.

Deter o mutex passa a ser a invariante documentada que torna correta a liberacao incondicional de reservas. A premissa deixa de ser implicita.

## Alternativas consideradas

| Alternativa | Probabilidade de adequacao | Motivo |
| --- | ---: | --- |
| Mutex nomeado por banco | 90% | Fecha o defeito na raiz, libera no encerramento do processo e cobre morte por `Kill`. |
| Prazo de validade na reserva | 35% | Nao distingue Worker morto de Worker lento: roubar uma reserva viva reproduz o mesmo defeito, so que atrasado. E piora a recuperacao de queda, que hoje e imediata, para o tamanho do prazo. |
| Prazo com renovacao periodica | 45% | Corrigiria os dois lados, mas exige heartbeat durante o processamento; complexidade injustificada para um app local de usuario unico. |
| Arquivo de trava exclusivo | 50% | Tambem e liberado pelo sistema no encerramento, mas confunde trava com qualquer outra falha de E/S e sofre com antivirus e indexadores. |
| Semaforo nomeado | 20% | Semaforo nao tem dono: um Worker morto nao devolve a vaga enquanto houver outro handle aberto. |

## Consequencias

- A liberacao de reservas na retomada continua incondicional, e agora com a premissa garantida e escrita em `IJobQueue` e em `ReuniaoConsumer.RetomarAsync`.
- Um Worker morto tem o job retomado pelo Worker seguinte imediatamente, sem espera de prazo.
- `Main` do Worker passou a ser sincrono: liberar um mutex exige a mesma thread que o adquiriu, o que a continuacao de um `async Main` nao garante.
- A guarda fica na raiz de composicao do Worker, e nao em `ReuniaoConsumer` nem em `SqliteJobQueue`, porque o E2E hermetico da alpha exercita o consumidor dentro do proprio processo de teste.
- A guarda fica depois do ramo de retencao: `--retencao-simular` e `--retencao-aplicar` sao comandos pontuais que nao tocam na fila e nao podem virar no-op durante um processamento longo.
- O Tray enfileira o job antes de lancar o Worker. Se o dono atual ja tiver feito sua ultima leitura, o sucessor permanece aguardando, adquire o mutex depois da liberacao e processa o job. Nao existe intervalo entre consulta e liberacao capaz de descartar o aviso.
- Um sucessor pode permanecer bloqueado enquanto o dono processa. Os limites dos processos externos definidos na SPEK-041 impedem que uma ferramenta travada retenha a exclusividade indefinidamente em operacao normal.
- Nenhuma dependencia nova: `Mutex` e da biblioteca base.
- Risco residual aceito: o mesmo usuario em duas sessoes simultaneas do Windows compartilha `%LOCALAPPDATA%` mas nao o espaco `Local\`, e teria dois Workers. Cenario fora do uso previsto do produto.
