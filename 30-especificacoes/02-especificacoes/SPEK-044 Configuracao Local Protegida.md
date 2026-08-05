---
title: SPEK-044 Configuracao Local Protegida
aliases: [SPEK-044, Senha do OBS Protegida]
tags: [especificacao, configuracao, seguranca, dpapi, robustez, pos-alpha]
type: spek
created: 2026-08-05
updated: 2026-08-05
status: completed
summary: A senha do OBS deixa de ficar em texto claro na configuracao local, com migracao transparente do formato anterior.
related: ["[[ADR-014 Protecao de Segredos Locais]]", "[[SPEK-010 Tray Configuracao e Diagnosticos]]", "[[SPEK-025 Prontidao Automatica do OBS]]"]
---

# SPEK-044 Configuracao local protegida

## Objetivo

Remover o unico segredo em texto claro do produto e definir o mecanismo que as integracoes futuras vao reutilizar.

## Contexto do defeito

`SenhaObs` era gravada em texto claro em `%LOCALAPPDATA%\Anamnesis\config.json`. O menu do Tray abre esse arquivo no Bloco de Notas, entao a senha aparecia em tela em uso normal.

O risco absoluto e baixo, porque protege um servidor que so escuta em `127.0.0.1`. O motivo de tratar agora e outro: as SPEKs 033 a 039 trarao credenciais de agenda e de ferramentas externas, e o registro de decisoes ja mantem um gate aberto para credenciais protegidas.

## Fora de escopo

- Proteger caminhos, enderecos e demais preferencias, que nao sao segredos.
- Interface para digitar ou trocar a senha, que continua sendo edicao do arquivo.
- Windows Credential Manager, avaliado no ADR-014 e adiado para quando entrarem tokens de OAuth.
- Sincronizar configuracao entre maquinas.

## Regras

- A senha do OBS e protegida por DPAPI no escopo do usuario antes de ser gravada.
- O valor protegido carrega um prefixo que o distingue de um valor legado em texto claro.
- Um valor sem prefixo carrega normalmente e e reescrito protegido no salvamento seguinte, sem acao do usuario.
- Falha ao desproteger produz erro com instrucao acionavel, e nunca uma senha vazia silenciosa.
- Um valor nulo ou vazio permanece nulo ou vazio, sem prefixo.
- Proteger um valor ja protegido nao o protege duas vezes.
- Segredos de integracoes futuras reutilizam este mecanismo em vez de criar o seu.

## Critérios de aceite

- [x] A senha nao aparece em texto claro no arquivo salvo.
- [x] A senha salva e recuperada integralmente na leitura seguinte.
- [x] Uma configuracao legada em texto claro continua carregando e e migrada no salvamento seguinte.
- [x] A configuracao padrao, sem senha, continua sendo criada e carregada.
- [x] A suite existente permanece verde e o build sem avisos.

## Testes associados

- `ArquivoConfiguracaoTests.NaoDeveGravarASenhaDoObsEmTextoClaro`.
- `ArquivoConfiguracaoTests.DeveRecuperarASenhaDoObsProtegida`.
- `ArquivoConfiguracaoTests.DeveMigrarSenhaLegadaEmTextoClaro`, que cobre carga, migracao e releitura.
- `ArquivoConfiguracaoTests.DeveCriarECarregarConfiguracaoPadrao`, ja existente, cobre o caminho sem senha.

## Execucao local

- `dotnet test Anamnesis.sln`, 143 testes verdes e 0 avisos.

## Decisoes pendentes

- Nenhuma para este incremento. A escolha entre DPAPI e Windows Credential Manager para tokens de OAuth fica registrada no ADR-014 para ser reavaliada na SPEK-033.
