---
title: SPEK-045 Experiencia Windows Instalada e Primeiro Uso
aliases: [Shell Windows Profissional, Instalador Pronto para Uso]
tags: [especificacao, windows, tray, instalador, primeiro-uso]
type: spek
created: 2026-08-05
updated: 2026-08-05
status: in-progress
summary: Entrega o Anamnesis como um unico produto Windows instalado, identificado, operacional na bandeja e verificavel no primeiro uso.
related: ["[[SPEK-022 Instalador da Beta em Windows Limpo]]", "[[SPEK-030 Desktop com Dados Reais]]", "[[ADR-017 Provisionamento do Aplicativo Windows]]"]
---

# SPEK-045 | Experiencia Windows instalada e primeiro uso

## Objetivo

Permitir instalar e usar o Anamnesis como uma ferramenta Windows unica: o instalador abre o Desktop real, mantem o aplicativo disponivel na bandeja, inicia o Worker internamente e apresenta prontidao acionavel sem exigir que o usuario execute componentes do projeto separadamente.

```text
Instalar -> abrir Anamnesis -> validar prontidao -> permanecer na bandeja
                                  |
                                  v
                     gravar -> processar -> consultar
```

## Fora de escopo

- Instalar silenciosamente OBS, Docker Desktop, FFmpeg, modelo Whisper ou CLI de LLM.
- Automatizar autenticacao de CLI, interface web ou consentimento de terceiros.
- Assinatura Authenticode, reputacao SmartScreen e atualizacao automatica.
- Alterar captura, transcricao, geracao de ata ou retencao ja especificadas.

## Regras

- O simbolo segue a identidade visual: estrela simplificada de oito pontas, no maximo um anel, fundo profundo e destaque cobre.
- Um unico `.ico` multirresolucao identifica EXE, janela, barra de tarefas, bandeja, atalhos, instalador e desinstalador.
- Somente uma instancia do Tray fica ativa por usuario. Uma segunda abertura solicita que a primeira mostre a janela e termina sem criar outro detector ou outro icone.
- Fechar a janela preserva o processo na bandeja. Sair e uma acao explicita e pede confirmacao enquanto houver gravacao ativa.
- O menu oferece Abrir Anamnesis, estado atual, Iniciar ou Encerrar gravacao, Processar pendencias, silenciar deteccao, Diagnosticos, Abrir configuracoes, Iniciar com o Windows e Sair.
- Acoes reais nao usam o rotulo "de teste". Os estados habilitados acompanham SQLite e sessao real.
- Inicializacao com o Windows e selecionavel por usuario, marcada por padrao no assistente interativo e editavel pelo menu. O argumento `--background` inicia somente na bandeja.
- O Worker permanece interno e nao recebe atalho publico.
- Primeira execucao cria configuracao e dados em `%LocalAppData%\Anamnesis`, descobre somente caminhos locais padrao existentes e nunca sobrescreve configuracao anterior.
- Ausencia de dependencia externa nao derruba o shell. O diagnostico explica a pendencia; com pre-requisitos ja presentes e configurados, o produto fica pronto sem executar Worker ou editar JSON separadamente.
- O instalador continua por usuario, autocontido e sem UAC. Atualizacao e desinstalacao preservam banco, configuracao e reunioes.
- O smoke instalado valida payload, versao, icone, atalho publico unico, inicializacao opcional, processo do Tray, criacao de configuracao e desinstalacao com logs.
- A validacao local usa o aplicativo realmente instalado e coleta captura da janela, menu da bandeja e evidencias de processo/configuracao, sem iniciar ou encerrar uma gravacao sem acao explicita.

## Criterios de aceite

- [x] Icone proprio aparece em todas as superficies Windows do produto e permanece legivel em 16 px.
- [x] Uma segunda abertura nao cria outro Tray e traz a janela existente para frente.
- [x] Fechar a janela mantem a bandeja; sair durante gravacao exige confirmacao.
- [x] Menu dinamico oferece as acoes definitivas e reflete pronto, gravando, processamento pendente e recuperacao.
- [x] Inicializacao com o Windows pode ser ativada ou desativada por usuario e usa `--background`.
- [x] O instalador cria somente o atalho publico Anamnesis e inicia o Desktop real ao terminar.
- [ ] Primeiro uso cria configuracao valida, preserva dados anteriores e informa dependencias ausentes sem encerrar o shell.
- [x] Com os pre-requisitos desta maquina, o aplicativo instalado abre pronto e inicia o Worker internamente.
- [x] Testes automatizados cobrem shell e contrato do instalador sem OBS, rede ou CLI real.
- [ ] Instalacao, execucao visual e desinstalacao geram logs, capturas e hashes reproduziveis.

## Testes associados

- `WindowsShellTests`
- `InstallerContractTests`
- `DesktopPocFormTests`
- `ArquivoConfiguracaoTests`
- `Test-Installer.ps1`

## Entrega em validacao

- Instalador final local: `0.2.0-beta.1`, 65.024.601 bytes, SHA-256 `b90ce4e22e41d31a453e7d0504abd34e9e36c155c1d90a5ebc4acc4dda66f17c`.
- Instalacao real por usuario em `%LocalAppData%\Programs\Anamnesis`, com um atalho publico, inicializacao `--background` e uma unica instancia ativa.
- Pipeline desta maquina exibido como pronto para OBS, FFmpeg, Whisper em Docker, modelo local e Codex CLI.
- Worker instalado executado com codigo 0, fila vazia e stderr vazio.
- Suite Release: 244 testes verdes, sendo 3 Domain, 51 Application e 190 Infrastructure.
- Evidencias locais: `artifacts/evidencias/SPEK-045/resultado-final.md`, capturas da janela, configuracoes e menu da bandeja, log do instalador e logs do Worker.
- O smoke anterior concluiu instalacao e desinstalacao; o smoke ampliado e isolado aguarda o runner Windows limpo da CI para fechar os dois criterios restantes sem atingir a instalacao real.

## Decisoes pendentes

- Nenhuma decisao bloqueante para este corte. O ADR-017 separa o aplicativo pronto do provisionamento consentido de dependencias externas.
- Assinatura e atualizacao automatica permanecem como proxima etapa de distribuicao.
