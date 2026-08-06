---
title: SPEK-047 Instalador Resiliente com Atualizacao e Reparo
aliases: [Instalador Resiliente, Atualizacao e Reparo Windows]
tags: [especificacao, instalador, windows, atualizacao, reparo]
type: spek
created: 2026-08-06
updated: 2026-08-06
status: validating
summary: Evolui o instalador elevado por usuario para diagnosticar instalacao, comparar versoes, orientar reparo e encerrar o Tray de forma cooperativa.
related: ["[[SPEK-045 Experiencia Windows Instalada e Primeiro Uso]]", "[[ADR-008 Instalador por Usuario com Inno Setup]]", "[[ADR-017 Provisionamento do Aplicativo Windows]]"]
---

# SPEK-047 | Instalador resiliente com atualizacao e reparo

## Objetivo

Entregar um unico instalador Windows elevado que reconhece o estado local do Anamnesis, compara versoes reais dos binarios, explica a acao recomendada, aceita termos simples de uso e trata o Tray aberto sem dialogo generico de falha.

```text
Sem instalacao                         -> instalar e oferecer abertura
Versao instalada anterior              -> atualizar sem reabrir o Tray
Mesma versao ou arquivo obrigatorio ausente -> reparar sem reabrir o Tray
Versao instalada mais recente          -> bloquear downgrade sem alterar dados
Falha registrada anteriormente         -> exibir diagnostico e recomendar reparo
Tray aberto                             -> solicitar encerramento seguro e manter o wizard aberto se falhar
```

## Fora de escopo

- Canal automatico de atualizacao, download em segundo plano ou telemetria.
- Assinatura Authenticode e reputacao SmartScreen.
- Encerrar a forca uma gravacao ativa ou um Worker que ainda esteja processando.
- Instalar OBS, Docker, FFmpeg, Whisper ou CLIs autenticadas de terceiros.
- Parecer juridico ou coleta de aceite fora do computador do usuario.

## Regras

- O `AppId` permanece estavel. O instalador exige elevacao UAC, mas mantem a instalacao por usuario em `%LocalAppData%\Programs\Anamnesis`; ele nao transforma o produto em instalacao para todos os usuarios.
- A instalacao elevada reconhece tanto o registro legado por usuario em `HKCU` quanto o registro elevado em `HKLM`. Depois de uma instalacao bem-sucedida, ela migra somente o registro de desinstalacao legado para evitar duas entradas do mesmo produto.
- O assistente identifica o estado pelo registro do mesmo `AppId`, pela versao de arquivo dos executaveis instalados e pela presenca dos executaveis obrigatorios de Tray e Worker.
- O Tray e a referencia canonica da versao do produto para bloquear downgrade. Um Worker legado ou com versao divergente nunca bloqueia sozinho a atualizacao: ele torna a instalacao inconsistente e recomenda reparo.
- Sem registro e sem instalacao anterior, a acao e **Instalar**. Com binarios mais antigos, a acao e **Atualizar**. Com a mesma versao ou payload incompleto, a acao e **Reparar**. Uma versao instalada mais nova bloqueia downgrade automatico.
- Atualizacao e reparo usam o diretorio ja instalado e reescrevem somente os binarios do produto. Banco, configuracao, reunioes, gravacoes e arquivos do usuario permanecem fora do escopo do instalador.
- Antes de copiar arquivos, o wizard mostra o diagnostico, a versao instalada, a versao do pacote, a integridade do payload e a acao recomendada. O usuario recebe uma opcao explicita de reparo quando ela for aplicavel.
- Antes de copiar arquivos, o instalador solicita ao Tray em execucao que encerre cooperativamente. Se houver gravacao ativa, processo antigo ou Worker em processamento, ele permanece no wizard com orientacao e opcao de tentar novamente, sem finalizar processo a forca ou induzir cancelamento.
- A pagina interativa apresenta termos de uso curtos em PT-BR e exige aceite para continuar. O texto informa uso responsavel de gravacoes, armazenamento local, dependencias externas e a licenca MIT sem prometer garantia inexistente.
- O assistente reutiliza o icone aprovado e a paleta `#10172E`, `#B87333` e `#F3EEE4`, com logotipo tambem nas paginas de boas-vindas e conclusao.
- Apos concluir em modo interativo, o instalador oferece e executa a abertura do Tray somente em uma instalacao realmente nova. Atualizacao e reparo nunca reabrem o Tray.
- Diagnosticos de instalacao e falhas de preflight sao persistidos em log local e o ultimo diagnostico conhecido aparece no wizard da proxima execucao.

## Criterios de aceite

- [x] O contrato automatizado exige termos, imagem de marca, `AppId` estavel e os tres estados do assistente.
- [x] O instalador exige UAC, mantem o destino por usuario e nao permite executar sem privilegio administrativo.
- [x] O wizard compara versoes de arquivo, bloqueia downgrade e distingue instalar, atualizar e reparar de modo compreensivel.
- [x] Um diagnostico persistido de tentativa anterior aparece no wizard e oferece reparo quando aplicavel.
- [x] Um Tray de versao nova recebe pedido de encerramento cooperativo e sai quando nao ha gravacao ativa.
- [x] Um Tray com gravacao ativa nao e finalizado a forca e o instalador devolve orientacao compreensivel.
- [x] O instalador compila com Inno Setup 6.7.3 e mostra a acao `Instalar`, `Atualizar` ou `Reparar` no resumo final.
- [ ] O smoke em diretorio isolado instala, abre o Tray, repara um payload incompleto, atualiza para uma versao de teste e preserva dados do usuario.
- [ ] A atualizacao e o reparo preservam o atalho publico, a configuracao, o banco e a desinstalacao posterior.
- [x] A instalacao nova oferece abrir o Anamnesis ao concluir; atualizacao e reparo nao o abrem.

## Testes associados

- `InstallerContractTests`
- `WindowsShellTests`
- `DesktopRealSessionTests`
- `Test-Installer.ps1`
- `.github/workflows/beta-installer.yml`

## Evidencias locais

- Red: o contrato `DowngradeDeveSerBloqueadoAntesDeClassificarPayloadIncompletoComoReparo` falhou antes da comparacao binaria anteceder a classificacao de reparo.
- Green: a comparacao canonica avalia o Tray antes de classificar payload incompleto. O Worker so indica divergencia e recomenda reparo.
- Regressao adicional: um Tray `0.2.0-beta.1` combinado com Worker legado `1.0.0.0` era classificado como falso downgrade.
- Red adicional: comparar o Worker instalado com o pacote novo fazia uma atualizacao normal aparecer como inconsistente e o diagnostico afirmava uma divergencia que nem sempre existia.
- Green adicional: o Tray continua sendo a fonte da versao do produto; o Worker e comparado ao Tray instalado e o smoke substitui somente o Worker efemero por `1.0.0.0` antes de exigir atualizacao bem-sucedida.
- O smoke ampliado remove o Worker somente na instalacao efemera, prova que o pacote antigo continua bloqueado, verifica a reuniao sentinela no SQLite e restaura o payload com a versao atual.
- `InstallerContractTests`: 10 de 10 verdes.
- Suite integrada: 268 de 268 testes verdes em Release, repetidos tres vezes no Windows apos a correcao da concorrencia.
- Inno Setup 6.7.3 compilou o instalador `0.2.0-beta.2`; SHA-256 local `bf6cfdc55d1c24e752ee6683b5704b2a20364f5221ec13f61f5ae69868d020e0`.
- A instalacao real deste usuario foi preservada. A validacao instalada deste incremento sera executada somente no runner Windows efemero.

## Decisoes pendentes

- Assinatura de codigo e canal automatico de distribuicao terao SPEK e ADR proprias.
- Falta executar o smoke ampliado em runner Windows limpo. A instalacao real deste usuario foi preservada de proposito durante a validacao local.
