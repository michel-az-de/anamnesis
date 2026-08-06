---
title: SPEK-046 Limite da Espera por Exclusividade
aliases: [SPEK-046, Teto da Espera do Worker]
tags: [especificacao, worker, fila, concorrencia, robustez, pos-alpha]
type: spek
created: 2026-08-06
updated: 2026-08-06
status: completed
summary: A espera de um Worker pela exclusividade tem teto e diagnostico duravel, sem alterar a fila quando o limite expira.
related: ["[[ADR-012 Instancia Unica do Worker]]", "[[SPEK-031 Observabilidade Operacional Real]]", "[[SPEK-040 Concorrencia de Worker e Fila]]", "[[SPEK-041 Resiliencia de Processos Externos]]"]
---

# SPEK-046 Limite da espera por exclusividade

## Objetivo

Dar um teto finito a espera do Worker pelo mutex de instancia unica e tornar a expiracao observavel no journal local, sem reservar jobs ou alterar reunioes e artefatos.

## Contexto do defeito

A SPEK-040 corrigiu uma janela TOCTOU: o sucessor passou a aguardar a transferencia da exclusividade em vez de encerrar imediatamente. Essa espera garante que um lancamento feito depois do enfileiramento tenha oportunidade de consultar a fila quando o dono anterior termina normalmente.

O problema e que `Mutex.WaitOne()` sem timeout pode manter processos invisiveis bloqueados para sempre. O Tray inicia o Worker com `CreateNoWindow` na inicializacao, depois de uma gravacao e ao processar pendencias. Uma mensagem apenas em `stdout` tambem desaparece nesse lancamento oculto.

## Limite e risco residual

O laco do dono atual drena a fila antes de liberar o mutex. Em operacao normal, um job enfileirado durante processamento sera visto pelo proprio dono; a espera do sucessor protege principalmente o intervalo entre a ultima consulta e a liberacao, normalmente de milissegundos.

O teto nao preserva a garantia de retomada integral quando o dono fica travado alem do limite. Nesse caso excepcional, o sucessor encerra, a fila permanece duravel e o journal registra o diagnostico, mas um novo acionamento do Worker sera necessario depois que o dono morrer ou for encerrado. Essa troca e preferivel a acumular processos ocultos sem limite.

## Fora de escopo

- Reverter a espera para encerramento imediato.
- Encerrar ou supervisionar o Worker que detem a exclusividade.
- Criar backoff, fila externa ou servico residente novo.
- Exibir notificacao intrusiva para uma contencao normal.

## Regras

- A API usada em producao aplica um teto finito definido em codigo e nao permite substitui-lo por espera infinita.
- Um overload interno aceita limite curto somente para testes e rejeita valores negativos, infinitos ou maiores que o aceito pelo `Mutex`.
- Quando o limite expira, o Worker retorna codigo zero, escreve mensagem propria e registra `worker.exclusividade_expirada` no journal com nivel informativo.
- A expiracao ocorre antes da retomada da fila e nao altera job, reuniao ou artefato.
- A transferencia dentro do teto e o mutex abandonado continuam sendo aquisicoes bem-sucedidas.
- Bancos diferentes continuam admitindo Workers simultaneos.
- Nenhuma dependencia nova e necessaria; o ADR-012 recebe apenas a consequencia atualizada.

## Criterios de aceite

- [x] Uma espera contida termina no teto e devolve ausencia de exclusividade.
- [x] A API de producao nao oferece caminho para espera infinita.
- [x] Limites invalidos sao rejeitados antes de tocar no mutex.
- [x] O Worker encerra com codigo zero e mensagem propria quando nao adquire a exclusividade.
- [x] A expiracao persiste evento seguro e correlacionavel no journal oculto do Worker.
- [x] Job pendente permanece pendente, sem reserva ou incremento de tentativa.
- [x] Transferencia dentro do teto, mutex abandonado e bancos diferentes nao regrediram.
- [x] A suite Release e a verificacao de formatacao permanecem verdes.

## Testes associados

- `InstanciaUnicaWorkerTests.NaoDeveAguardarIndefinidamentePelaExclusividade`.
- `InstanciaUnicaWorkerTests.NaoDeveAceitarLimiteInfinitoOuInvalido`.
- `InstanciaUnicaWorkerTests.DeveTransferirExclusividadeDentroDoLimite`.
- `InstanciaUnicaWorkerTests.DeveAdquirirMutexAbandonadoDentroDoLimite`.
- `WorkerExclusividadeTests.DeveEncerrarSemTocarFilaEPersistirDiagnosticoQuandoLimiteExpira`.
- `JornalOperacionalTests.DeveExporCatalogoMinimoUnicoEEstavel`.

## Execucao local

- Teto de producao: 5 minutos em `InstanciaUnicaWorker.LimitePadraoDeEspera`; o overload com `TimeSpan` e interno e validado.
- Red: o catalogo falhou porque faltava `worker.exclusividade_expirada`; a suite tambem registrou `CS0122`, `CS0117` e `CS1501` para o ramo e o overload ainda inexistentes.
- Green focado: 11 testes de catalogo, mutex e Worker passaram; os nove testes de mutex foram repetidos dez vezes sem intermitencia.
- Green completo: `dotnet test Anamnesis.sln --configuration Release --no-restore --verbosity minimal`, 249 testes verdes, 0 falhas.
- Refactor: `dotnet format Anamnesis.sln --verify-no-changes --no-restore --verbosity minimal`, sem divergencias.

## Decisoes pendentes

- Nenhuma para este incremento.
