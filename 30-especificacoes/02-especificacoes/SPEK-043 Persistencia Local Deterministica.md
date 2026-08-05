---
title: SPEK-043 Persistencia Local Deterministica
aliases: [SPEK-043, Banco Local Deterministico]
tags: [especificacao, sqlite, persistencia, wal, robustez, pos-alpha]
type: spek
created: 2026-08-05
updated: 2026-08-05
status: completed
summary: O banco local prepara o esquema uma vez por instancia, opera em WAL e usa uma engine SQLite de versao garantida.
related: ["[[ADR-013 Engine SQLite Embarcada]]", "[[SPEK-005 Persistencia de Reuniao em SQLite]]", "[[SPEK-004 Fila Local de Jobs]]", "[[SPEK-040 Concorrencia de Worker e Fila]]"]
---

# SPEK-043 Persistencia local deterministica

## Objetivo

Tornar o acesso ao banco local previsivel em custo e em versao de engine, sem mudar o contrato de nenhum repositorio.

## Contexto do defeito

Cada operacao de `SqliteReuniaoRepository` e `SqliteJobQueue` abria uma conexao so para aplicar o esquema e depois outra para a consulta. Com pooling desligado, uma unica leitura custava duas aberturas de arquivo mais `CREATE TABLE`, consulta a `pragma_table_info`, um `UPDATE` varrendo a tabela inteira e um `CREATE INDEX`. O `UPDATE`, rotulado como migracao de banco legado, rodava em toda chamada.

`SqliteArtefatoRepository` e `SqliteReuniaoQuery` ja aplicavam o esquema na mesma conexao, entao o padrao estava inconsistente entre os quatro adaptadores.

`SqliteArtefatoRepository` tambem usava `DateTimeOffset.UtcNow` diretamente, deixando `arquivado_em` fora do controle dos testes enquanto todo o resto do projeto injeta `TimeProvider`.

Por fim, a reserva atomica da fila usa `RETURNING`, que exige SQLite 3.35, mas o pacote em uso delegava a engine ao Windows.

## Fora de escopo

- Migracoes versionadas de esquema ou ferramenta de migracao.
- Trocar SQLite por outro mecanismo de persistencia.
- Alterar o contrato de qualquer repositorio ou consulta.
- Banco separado para o jornal de observabilidade, tratado na SPEK-031.

## Regras

- O esquema e aplicado uma unica vez por instancia de adaptador, na mesma conexao da primeira operacao.
- Os quatro adaptadores SQLite compartilham a mesma forma de abrir conexao e preparar o banco.
- O banco local opera em WAL, para que o polling do Desktop nao bloqueie a escrita do Worker.
- Todo instante persistido vem de um `TimeProvider` injetado e e normalizado para UTC.
- A engine SQLite e embarcada e sua versao minima e verificada por teste, conforme ADR-013.
- O build permanece em zero avisos, porque e o aviso do NuGet que revela vulnerabilidade na engine embarcada.

## Critérios de aceite

- [x] Tres operacoes seguidas na mesma instancia preparam o esquema uma unica vez.
- [x] O banco fica em modo WAL apos a primeira operacao.
- [x] O instante de arquivamento do manifesto vem do relogio injetado.
- [x] Instantes com offsets diferentes sao persistidos em UTC e ordenados pelo momento real.
- [x] A engine reporta versao 3.35 ou superior.
- [x] O pacote publicado continua funcional, com o binario nativo na saida.
- [x] A suite existente permanece verde e o build sem avisos.

## Testes associados

- `SqliteReuniaoRepositoryTests.DevePrepararEsquemaUmaUnicaVezPorInstancia` e `DeveManterOBancoLocalEmModoWal`.
- `SqliteArtefatoRepositoryTests.DeveUsarORelogioInjetadoParaOInstanteDeArquivamento`.
- `SqliteReuniaoQueryTests.DeveNormalizarUtcAntesDeOrdenarPorCriacao`.
- `SqliteJobQueueTests.DeveUsarEngineComSuporteARetorno`.
- Os testes de persistencia e de fila ja existentes cobrem a regressao funcional da troca de engine.

## Execucao local

- `dotnet test Anamnesis.sln`, 140 testes verdes e 0 avisos.
- Publicacao real verificada com `--runtime win-x64 --self-contained`: `e_sqlite3.dll` de 1,9 MB na raiz da saida, total de 121 MB.
- Red de robustez: a consulta ordenou `12:00 +03:00` antes de `10:00 +00:00`, embora o primeiro instante fosse uma hora mais antigo.
- Green de robustez: todos os campos temporais do agregado passam por `ToUniversalTime()` antes da serializacao ISO.

## Decisoes pendentes

- Nenhuma para este incremento. Atualizar o pacote da engine quando o NuGet apontar vulnerabilidade passa a ser tarefa recorrente do projeto, conforme registrado no ADR-013.
