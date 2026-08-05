---
title: ADR-013 Engine SQLite Embarcada
aliases: [Engine SQLite Propria, e_sqlite3]
tags: [adr, sqlite, persistencia, instalador, windows]
type: adr
created: 2026-08-05
updated: 2026-08-05
status: accepted
summary: O Anamnesis embarca a engine SQLite em vez de usar a do Windows, para nao depender do build do sistema operacional.
related: ["[[SPEK-043 Persistencia Local Deterministica]]", "[[SPEK-004 Fila Local de Jobs]]", "[[SPEK-040 Concorrencia de Worker e Fila]]"]
---

# ADR-013 | Engine SQLite embarcada

## Contexto

A reserva atomica da fila usa `UPDATE ... RETURNING`, sintaxe disponivel a partir do SQLite 3.35. O indice unico parcial de gravacao ativa exige 3.8.

O pacote em uso era `SQLitePCLRaw.bundle_winsqlite3`, que nao traz engine: usa a `winsqlite3.dll` do proprio Windows. A versao dessa biblioteca varia por build do sistema. Nesta maquina de desenvolvimento e 3.51.1 e tudo funciona, mas builds de Windows 10 anteriores ao 21H2 trazem versoes que rejeitam `RETURNING` com erro de sintaxe.

O defeito, portanto, nao aparece em teste nem em desenvolvimento: aparece na maquina do usuario, em tempo de execucao, dependendo de quando ele atualizou o Windows.

## Decisao

Trocar para `SQLitePCLRaw.bundle_e_sqlite3`, que embarca a engine no proprio pacote, na versao **3.0.5**.

A versao importa: `2.1.11`, a que correspondia a linha em uso, carrega a vulnerabilidade de alta gravidade GHSA-2m69-gcr7-jv3q em `SQLitePCLRaw.lib.e_sqlite3`. A 3.0.5 nao. Adotar a linha 2.x teria trocado um risco de compatibilidade por um risco de seguranca.

Um teste passa a exigir `sqlite_version() >= 3.35`, transformando em garantia verificavel o que antes era premissa sobre a maquina do usuario.

## Alternativas consideradas

| Alternativa | Probabilidade de adequacao | Motivo |
| --- | ---: | --- |
| Embarcar a engine com `bundle_e_sqlite3` 3.0.5 | 85% | Elimina a variacao por build do SO e torna a versao minima testavel. Custo de 1,9 MB em um pacote de 121 MB. |
| Manter `winsqlite3` e trocar `RETURNING` por `UPDATE` mais `SELECT` em transacao | 55% | Funcionaria em qualquer build, mas troca uma instrucao atomica por duas, ampliando a superficie de concorrencia da fila justamente onde ela e critica. |
| Manter `winsqlite3` e checar a versao no diagnostico do Tray | 40% | Detecta o problema mas nao o resolve: o usuario recebe um aviso que so o Windows Update pode atender. |
| Embarcar `bundle_e_sqlite3` na linha 2.1.11 | 15% | Resolveria a compatibilidade, mas com vulnerabilidade conhecida de alta gravidade. |

## Consequencias

- A versao da engine deixa de depender do build do Windows e passa a ser a mesma em desenvolvimento, em CI e na maquina do usuario.
- **A responsabilidade de corrigir vulnerabilidades da engine passa a ser do projeto.** Com `winsqlite3`, o Windows Update corrigia; agora, atualizar o pacote e tarefa nossa. O aviso `NU1903` do NuGet e o mecanismo de deteccao, e por isso o build precisa continuar em zero avisos.
- O pacote publicado cresce 1,9 MB, verificado em publicacao real: `e_sqlite3.dll` fica na raiz da saida, e o total self-contained vai de 119 MB para 121 MB.
- `--runtime win-x64`, ja usado pelo script de publicacao e pelo CI, garante que apenas o binario nativo dessa arquitetura seja incluido.
- Fica aberto o suporte a outras arquiteturas sem mudanca de codigo, ja que o pacote traz binarios para `win-arm64` e `win-x86`.
