---
title: SPEK-047 Instalador Resiliente com Atualizacao e Reparo
aliases: [Instalador Resiliente, Atualizacao e Reparo Windows]
tags: [especificacao, instalador, windows, atualizacao, reparo]
type: spek
created: 2026-08-06
updated: 2026-08-06
status: validating
summary: Evolui o instalador por usuario para orientar instalacao, atualizacao ou reparo, aceitar termos simples e encerrar o Tray de forma cooperativa.
related: ["[[SPEK-045 Experiencia Windows Instalada e Primeiro Uso]]", "[[ADR-008 Instalador por Usuario com Inno Setup]]", "[[ADR-017 Provisionamento do Aplicativo Windows]]"]
---

# SPEK-047 | Instalador resiliente com atualizacao e reparo

## Objetivo

Entregar um unico instalador Windows que reconhece o estado local do Anamnesis, explica a acao que executara, aceita termos simples de uso, trata o Tray aberto sem dialogo generico de falha e abre o produto ao concluir uma instalacao interativa.

```text
Sem instalacao        -> instalar
Versao anterior       -> atualizar
Mesma versao ou arquivo ausente -> reparar
Tray aberto           -> solicitar encerramento seguro -> continuar ou explicar o bloqueio
```

## Fora de escopo

- Canal automatico de atualizacao, download em segundo plano ou telemetria.
- Assinatura Authenticode e reputacao SmartScreen.
- Encerrar a forca uma gravacao ativa ou um Worker que ainda esteja processando.
- Instalar OBS, Docker, FFmpeg, Whisper ou CLIs autenticadas de terceiros.
- Parecer juridico ou coleta de aceite fora do computador do usuario.

## Regras

- O `AppId` permanece estavel e a instalacao continua por usuario, sem UAC, em `%LocalAppData%\Programs\Anamnesis`.
- O assistente identifica o estado pelo registro do mesmo `AppId`, versao instalada e presenca dos executaveis obrigatorios de Tray e Worker.
- Sem registro anterior, a acao e **Instalar**. Com versao diferente e payload presente, a acao e **Atualizar**. Com a mesma versao ou payload incompleto, a acao e **Reparar**.
- Atualizacao e reparo usam o diretorio ja instalado e reescrevem somente os binarios do produto. Banco, configuracao, reunioes, gravacoes e arquivos do usuario permanecem fora do escopo do instalador.
- Antes de copiar arquivos, o instalador solicita ao Tray em execucao que encerre cooperativamente. Se houver gravacao ativa, processo antigo ou Worker em processamento, a instalacao para com orientacao clara e sem finalizar processo a forca.
- A pagina interativa apresenta termos de uso curtos em PT-BR e exige aceite para continuar. O texto informa uso responsavel de gravacoes, armazenamento local, dependencias externas e a licenca MIT sem prometer garantia inexistente.
- O assistente reutiliza o icone aprovado e a paleta `#10172E`, `#B87333` e `#F3EEE4`, com logotipo tambem nas paginas de boas-vindas e conclusao.
- Apos concluir em modo interativo, o instalador oferece e executa a abertura do Tray. O modo silencioso preserva sua semantica sem iniciar uma interface.
- Logs separados de instalacao, reparo, atualizacao e desinstalacao integram a evidencia automatizada.

## Criterios de aceite

- [x] O contrato automatizado exige termos, imagem de marca, `AppId` estavel e os tres estados do assistente.
- [x] Um Tray de versao nova recebe pedido de encerramento cooperativo e sai quando nao ha gravacao ativa.
- [ ] Um Tray com gravacao ativa nao e finalizado a forca e o instalador devolve orientacao compreensivel.
- [x] O instalador compila com Inno Setup 6.7.3 e mostra a acao `Instalar`, `Atualizar` ou `Reparar` no resumo final.
- [ ] O smoke em diretorio isolado instala, abre o Tray, repara um payload incompleto, atualiza para uma versao de teste e preserva dados do usuario.
- [ ] A atualizacao e o reparo preservam o atalho publico, a configuracao, o banco e a desinstalacao posterior.
- [x] A instalacao interativa continua oferecendo a abertura do Anamnesis ao concluir.

## Testes associados

- `InstallerContractTests`
- `WindowsShellTests`
- `Test-Installer.ps1`
- `.github/workflows/beta-installer.yml`

## Decisoes pendentes

- Assinatura de codigo e canal automatico de distribuicao terao SPEK e ADR proprias.
- Falta executar o smoke ampliado em runner Windows limpo. A instalacao real deste usuario foi preservada de proposito durante a validacao local.
