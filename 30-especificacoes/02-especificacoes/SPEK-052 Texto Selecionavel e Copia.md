---
title: SPEK-052 Texto Selecionavel e Copia
aliases: [Texto Copiavel, Copiar Conteudo da Reuniao]
tags: [especificacao, tray, acessibilidade, produtividade]
type: spec
created: 2026-08-07
updated: 2026-08-07
status: completed
summary: Torna resumo, transcricao, decisoes e tarefas selecionaveis e copiaveis no detalhe da reuniao.
related: ["[[SPEK-050 Fluxo de Processamento Assistido no Tray]]"]
---

# SPEK-052 | Texto selecionavel e copia

## Objetivo

Permitir reaproveitar qualquer conteudo textual da reuniao sem abrir os arquivos no Explorer.

## Regras

- Resumo, transcricao, decisoes e tarefas usam controle somente leitura com selecao por mouse e teclado.
- `Ctrl+C` copia a selecao sem permitir editar o conteudo persistido.
- Cada bloco oferece `Copiar texto`, que copia todo o conteudo do bloco.
- A copia nao grava evento, nao altera o banco e nao envia conteudo para rede.
- O controle mantem contraste, quebra de linha, rolagem e navegacao por teclado.

## Criterios de aceite

- [x] Texto pode ser selecionado e permanece somente leitura.
- [x] `Ctrl+C` fica habilitado no controle.
- [x] `Copiar texto` existe em todos os blocos textuais do detalhe.
- [x] Conteudo longo possui rolagem sem expandir indefinidamente a pagina.
- [x] Regressao WinForms cobre propriedades de selecao, copia e somente leitura.
- [x] Suite Release permanece verde.

## Evidencias

- `DesktopPocFormTests.DetalheDeveExibirTextoSelecionavelSomenteLeituraEAcaoDeCopia` falhou com colecao vazia antes da implementacao e passou com `RichTextBox` somente leitura.
- Suite Release: 297 testes verdes, incluindo o clique nos controles internos do cartao de reuniao.
- Versao instalada `0.2.0-beta.8-local.1`: 489 arquivos conferidos por SHA-256 e evidencia visual da transcricao recuperada com `Copiar texto`.

## Fora de escopo

- Editar a transcricao ou a ata.
- Copiar audio, caminhos ou segredos.
