---
title: SPEK-048 Inicializacao Concorrente do Journal SQLite
aliases: [SPEK-048, Preparacao Concorrente do Journal]
tags: [especificacao, observabilidade, sqlite, concorrencia, robustez]
type: spek
created: 2026-08-06
updated: 2026-08-06
status: completed
summary: Serializa entre processos somente a primeira preparacao do journal para impedir SQLITE_BUSY na corrida entre Tray e Worker.
related: ["[[SPEK-031 Observabilidade Operacional Real]]", "[[SPEK-043 Persistencia Local Deterministica]]", "[[ADR-015 Journal SQLite Isolado]]"]
---

# SPEK-048 Inicializacao concorrente do journal SQLite

## Objetivo

Impedir que Tray e Worker falhem com `SQLite Error 5: database is locked` ao criarem simultaneamente o journal e seus indices no primeiro uso.

## Contexto do defeito

`BancoLocal` evita repetir a preparacao somente dentro de uma instancia. Duas instancias independentes podem abrir o mesmo journal novo e executar em paralelo `PRAGMA journal_mode=WAL`, `CREATE TABLE` e `CREATE INDEX`. O CI reproduziu a corrida em `SqliteEventoOperacionalRepositoryTests.DuasInstanciasDevemEscreverConcorrentementeNoMesmoJournal`, dentro de `SqliteSchema.InicializarEventosOperacionaisAsync`.

O timeout SQLite de um segundo continua correto para eventos comuns e para a natureza best-effort da observabilidade. Ele, sozinho, nao transforma a sequencia de preparacao em uma secao critica entre processos.

## Fora de escopo

- Alterar contratos publicos de persistencia ou observabilidade.
- Serializar escritas, consultas ou limpeza depois que o journal estiver preparado.
- Alterar o banco principal de reunioes e jobs.
- Aumentar o timeout operacional do journal acima de um segundo.
- Adicionar dependencia, servico ou coordenacao remota.

## Regras

- Somente a preparacao do journal, incluindo WAL e schema, usa exclusividade entre processos.
- A exclusividade usa um arquivo de trava adjacente e deterministico, aberto com compartilhamento negado pelo Windows.
- Sob a trava, a instancia consulta WAL, as treze colunas e os quatro indices esperados. Schema completo marca a instancia como preparada sem repetir `PRAGMA journal_mode=WAL` ou DDL.
- O arquivo de trava permanece no disco. Apaga-lo depois do uso permitiria que uma terceira instancia criasse outro arquivo enquanto a segunda ainda detem o handle antigo.
- A espera pela trava e limitada e cancelavel; contencao prolongada continua sendo descartada pelo `JornalOperacional` sem afetar o fluxo observado.
- O handle e sempre descartado. Encerramento ou queda do processo libera a exclusividade pelo sistema operacional.
- Journals em caminhos diferentes nunca se bloqueiam entre si.
- Depois da preparacao, o journal preserva `Pooling=false`, WAL e timeout SQLite de um segundo definidos no ADR-015.
- Nenhuma mensagem, caminho pessoal ou conteudo de reuniao e gravado no arquivo de trava.
- O teste de serializacao observa a violacao de compartilhamento por um seam interno minimo; nao usa atraso arbitrario para inferir que a segunda instancia chegou a trava.
- Cancelar durante a contencao encerra a espera, descarta a conexao e nao impede uma terceira instancia de adquirir a mesma trava.
- Um teste caixa-preta usa dois processos auxiliares locais, sem OBS, rede ou CLI de modelo, para disputar o mesmo arquivo de trava.
- O stress de cem journals valida serializacao, schema e primeiras escritas, nao o deadline produtivo. Ele pode injetar por construtor interno um limite maior somente no teste para que preempcao do runner nao simule contencao externa prolongada; o construtor publico e o teste cronometrado continuam protegendo o limite produtivo de um segundo.

## Criterios de aceite

- [x] Duas instancias independentes nao entram simultaneamente na preparacao do mesmo journal.
- [x] A segunda instancia reconhece o schema completo e nao disputa DDL com o primeiro `INSERT`.
- [x] A trava e observavel por outro handle de arquivo, cobrindo Tray e Worker em processos separados.
- [x] Liberar a primeira preparacao permite que a segunda prossiga e grave normalmente.
- [x] A corrida existente de quarenta escritas concorrentes permanece verde em execucoes repetidas.
- [x] Contencao prolongada da primeira escrita continua retornando em menos de dois segundos pelo sink best-effort.
- [x] Journals diferentes podem ser preparados em paralelo.
- [x] A suite Release permanece verde e sem nova dependencia.
- [x] A regressao de serializacao nao depende de `Task.Delay` e prova que a segunda instancia detectou a contencao real.
- [x] Cancelar uma espera contida nao vaza conexao ou trava e uma nova instancia consegue preparar o banco.
- [x] Dois processos reais disputam o mesmo protocolo de arquivo e transferem a exclusividade com saida zero.

## Testes associados

- Regressao deterministica em `BancoLocalTests`: duas instancias para o mesmo caminho executam o delegate de schema uma unica vez.
- Regressao do protocolo de arquivo: um handle exclusivo impede a preparacao ate ser liberado.
- `SqliteEventoOperacionalRepositoryTests.DuasInstanciasDevemEscreverConcorrentementeNoMesmoJournal` executado repetidamente.
- `SqliteEventoOperacionalRepositoryTests.DeveEstabilizarPrimeiraInicializacaoConcorrenteEmCemRepeticoes` cria cem journals novos e disputa schema e primeiro `INSERT` em cada um.
- O stress usa um seam interno apenas para separar estabilidade do protocolo e deadline; `ContencaoNaTravaDePreparacaoNaoDeveBloquearFluxoObservado` continua exercitando o construtor publico com um segundo.
- `SqliteEventoOperacionalRepositoryTests.ContencaoNaPrimeiraEscritaNaoDeveBloquearFluxoObservado` preservado.
- `SqliteEventoOperacionalRepositoryTests.ContencaoNaTravaDePreparacaoNaoDeveBloquearFluxoObservado` prova o teto best-effort com outro handle exclusivo.
- `BancoLocalTests` cobre sinal deterministico da violacao de compartilhamento e cancelamento seguido por nova aquisicao.
- `BancoLocalProcessoTests` executa dois processos auxiliares reais sobre o mesmo arquivo de trava.

## Sequencia TDD

1. Red: coordenar dois `BancoLocal` independentes e provar que ambos entram simultaneamente na preparacao atual.
2. Green: adicionar a exclusividade de preparacao somente ao journal.
3. Refactor: isolar aquisicao, timeout e descarte da trava sem ampliar a API publica.

## Riscos residuais

- Software externo pode manter o arquivo de trava aberto sem compartilhamento. Nesse caso o evento e descartado dentro do limite, como qualquer outra falha best-effort do journal.
- Um processo que nao use o adaptador do Anamnesis ainda pode acessar diretamente o SQLite; o timeout nativo continua sendo a defesa para esse caso.

## Entrega

- Red real no CI: `DuasInstanciasDevemEscreverConcorrentementeNoMesmoJournal` falhou na primeira inicializacao com `SQLite Error 5: database is locked`, em `SqliteSchema.InicializarEventosOperacionaisAsync`.
- Red deterministico: `BancoLocalTests` nao compilou com `CS1739` enquanto a exclusividade e a verificacao de schema pronto nao existiam.
- Green: a preparacao do journal usa arquivo de trava exclusivo, cancelavel e limitado a um segundo; a segunda instancia consulta WAL, treze colunas e quatro indices antes de decidir por DDL.
- Testes focados: 12 de 12 verdes, incluindo cem journals novos disputados na primeira escrita e o limite da trava real.
- Suite integrada: `dotnet test Anamnesis.sln --configuration Release --no-restore --verbosity minimal`, 266 de 266 testes verdes em Release.
- Qualidade: `dotnet format Anamnesis.sln --verify-no-changes --no-restore --verbosity minimal` e `git diff --check` verdes; nenhuma dependencia nova.
- Revisao P3 reabriu a SPEK para substituir a espera temporal, cobrir cancelamento/descarte e provar o protocolo em dois processos reais.
- Red P3: o teste deterministico falhou com `CS1739` sem o seam de contencao; o caixa-preta falhou porque `Anamnesis.JournalProbe.dll` ainda nao existia.
- Green P3: `BancoLocalTests`, `BancoLocalProcessoTests` e `SqliteEventoOperacionalRepositoryTests` somaram 14 de 14 testes focados verdes.
- O teste unitario aguarda a violacao real de compartilhamento, cancela durante essa contencao e confirma que uma terceira instancia prepara o mesmo banco.
- O caixa-preta iniciou dois processos `dotnet` reais, recebeu o sinal de contencao do segundo e confirmou a transferencia da trava com codigo de saida zero, sem OBS, rede ou CLI de modelo.
- A suite completa foi repetida apos a revisao P3: 266 de 266 testes verdes em Release.
- Red no runner `31093154432`: o stress de cem journals disputou recursos com testes de processos externos; uma instancia foi preemptada por mais de um segundo e o teste direto excedeu o limite best-effort aceito.
- Green de isolamento: a classe de stress usa a colecao local sem paralelismo, preservando cem repeticoes, duas escritas por corrida e o timeout produtivo de um segundo.
- Red estrutural deterministico: com a trava externa ocupada, o SQLite criava o arquivo do banco antes de obter exclusividade.
- Green estrutural: o cold path agora executa semaforo, `.init.lock`, `OpenAsync` e preparacao do schema nessa ordem; o fast path continua abrindo diretamente depois da primeira preparacao.
- Validacao local final: os testes de journal e CLI passaram juntos e a suite completa ficou 268 de 268 verde em tres execucoes consecutivas no Windows.
- Red no runner `31100566863`: mesmo isolado do paralelismo xUnit, o dono da trava foi preemptado na iteracao 40 por mais de um segundo e o stress confundiu o descarte best-effort produtivo com falha do protocolo.
- Green: o construtor publico continua fixo em um segundo; um overload interno permite cinco segundos somente ao stress de protocolo. Dez execucoes consecutivas cobriram mil journals disputados sem falha.
- Regressao focada: 15 de 15 testes de `BancoLocal`, processos e journal verdes, incluindo os deadlines produtivos inalterados. Suite completa: 274 de 274 testes Release verdes.
