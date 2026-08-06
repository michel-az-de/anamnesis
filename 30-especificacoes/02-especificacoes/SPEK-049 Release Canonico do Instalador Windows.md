---
title: SPEK-049 Release Canonico do Instalador Windows
aliases: [Release Canonico, Versao Canonica do Instalador]
tags: [especificacao, release, instalador, github]
type: spek
created: 2026-08-06
updated: 2026-08-06
status: completed
summary: Centraliza a versao do instalador e promove assets imutaveis no GitHub.
related: ["[[SPEK-047 Instalador Resiliente com Atualizacao e Reparo]]", "[[ADR-018 Release Canonico do Instalador Windows]]"]
---

# SPEK-049 | Release canonico do instalador Windows

## Objetivo

Eliminar diretorios de tentativa e versoes divergentes do instalador. Cada release deve nascer de uma unica declaracao, usar uma versao anterior oficial no smoke, gerar um pacote Windows rastreavel por hash e commit completo e manter seus bytes oficiais de forma duravel.

## Regras

- `release/versao.json` e a unica fonte de versao, versao numerica, canal e identidade da release anterior oficial usada no smoke.
- O smoke baixa o instalador anterior pela URL declarada e valida seu SHA-256. Ele nunca recompila o `HEAD` atual com metadados de uma versao antiga.
- Uma release publica usa uma tag existente `v<versao>`. Os assets promovidos no GitHub sao imutaveis; uma recompilacao e apenas candidata e nunca substitui o pacote oficial.
- Se a verificacao imediatamente posterior detectar uma release mutavel, o workflow remove somente a release, preserva a tag e falha antes de qualquer atestacao. Uma nova promocao exige reativar `Immutable releases`.
- O build padrao gera `artifacts/releases/<versao>/installer/`, com EXE, `SHA256SUMS.txt` e `release.json`.
- EXEs, payloads e evidencias continuam ignorados pelo Git. Fonte, configuracao, scripts e tags ficam no Git; EXE, hashes e manifesto ficam preservados como assets da release imutavel.
- GitHub Actions e a autoridade de build: usa Windows efemero, .NET 10 e Inno Setup 6.7.3 verificado, executa o smoke instalado e so entao promove os tres assets.
- O build canonico recusa arvore Git suja e registra o SHA completo do commit. O modo sujo existe somente para diagnostico local e nunca produz pacote distribuivel aprovado.
- Smoke reprovado publica apenas diagnosticos. Smoke aprovado em branch gera artifact temporario identificado como candidato; apenas tag exata promove release oficial.
- Pipelines nunca criam nem movem tags. O GitHub promove somente a tag exata depois do smoke aprovado.
- Depois de uma release canonica confirmada, a pasta local legada `artifacts/beta/` pode ser removida. Outros artefatos, como E2E e POC, permanecem fora desse escopo.

## Criterios de aceite

- [x] A versao `0.2.0-beta.5`, sua versao numerica e a identidade verificavel da release anterior estao declaradas uma unica vez em `release/versao.json`.
- [x] `Build-Installer.ps1` usa a configuracao por padrao, falha ao receber apenas uma das versoes e gera manifesto com SHA-256 e commit completo.
- [x] O build distribuivel exige arvore Git limpa e associa o manifesto ao SHA completo do commit.
- [x] O Inno Setup e a publicacao autocontida nao carregam uma versao beta fixa como padrao independente.
- [x] O workflow GitHub baixa a release anterior oficial, constroi uma vez e separa candidato temporario de promocao por tag.
- [x] A promocao GitHub exige releases imutaveis, recusa sobrescrita, compensa a publicacao mutavel e verifica release e assets com atestado.
- [x] README e runbook explicam build, hash e promocao GitHub.
- [x] Os contratos cobrem fonte unica, release anterior real, promocao duravel e GitHub como autoridade unica.
- [x] A tag `v0.2.0-beta.5` conclui o smoke instalado e produz release imutavel verificada no run `31116896797`.
- [x] `artifacts/beta/` legado estava ausente na checkout auditada apos existir o pacote canonico verificado.

## Fora de escopo

- Assinatura Authenticode, SmartScreen e atualizacao automatica.
- Espelhar assets em outro provedor.
- Versionar binarios grandes no Git ou introduzir Git LFS nesta entrega.

## Evidencias locais

- Red inicial: 4 de 14 contratos falharam ao exigir release anterior oficial e promocao imutavel.
- Red de endurecimento: o contrato de promocao falhou ao exigir a compensacao `gh release delete` para uma publicacao mutavel.
- Green focado: 14 de 14 contratos do instalador verdes.
- Bootstrap: `v0.2.0-beta.1` promovida a partir do run `31086786207`, asset `8961793262`, com release imutavel e `gh release verify`/`verify-asset` verdes. SHA-256 do EXE: `5852e3e82ab9c80cf72ab85b8cd4425aeab3aa6faf401b3da0a2fc3db23dbecd`.
- Green completo local: 281 de 281 testes Release aprovados na branch Windows integrada. Os runs `31099937896`, `31100566863`, `31101580716`, `31104316644`, `31105631929`, `31106730785` e `31108401017` revelaram, respectivamente, rejeicao do Worker legado, flutuacao no stress do journal, uma tentativa invalida de reparo com o instalador legado, contencao do `ThreadPool` no sinal de instancia unica, reutilizacao do caminho `HKCU` depois da migracao elevada para `HKLM`, reativacao indevida da tarefa de startup e ausencia da causa explicita no primeiro ramo de bloqueio de downgrade.
- Correcao de provedor: 2 de 20 contratos falharam ao encontrar a configuracao extra e seu gatilho no workflow. Depois da remocao, os 20 de 20 contratos do instalador e os 281 de 281 testes Release passaram novamente.
- Green instalado da candidata: o run `31109639731` confirmou as sete regressoes no runner `windows-2025`, publicou o artifact temporario `candidato-anamnesis-0.2.0-beta.4-31109639731` e produziu EXE com SHA-256 `e1321b5a00f463334841f633353456527558f80f4be7579151bd1ecab9b05ba7`. A promocao imutavel ainda depende da tag exata.
- Falha controlada da primeira promocao: a tag `v0.2.0-beta.4`, preservada no commit `925c22819de89efe4ec0b6b091f1421382787754`, executou o run `31112674427`. O smoke instalado ficou verde, mas `publicar-release-github` falhou antes de criar a release porque a ausencia esperada em `gh release view` foi tratada como erro terminante pelo PowerShell estrito. A correcao usa uma consulta de lista com retorno de sucesso e promove a proxima versao `0.2.0-beta.5`.
- Release canonica: a tag `v0.2.0-beta.5` aponta para `bc62897822c5f9777c14c7832b12df74b3cf6be1`. O run `31116896797` terminou verde nos jobs `validar-instalador` e `publicar-release-github`; a release oficial esta em `https://github.com/michel-az-de/anamnesis/releases/tag/v0.2.0-beta.5` com `isImmutable=true`.
- Atestacao e assets: `gh release verify v0.2.0-beta.5` e `gh release verify-asset` para EXE, `SHA256SUMS.txt` e `release.json` validaram a atestacao Sigstore. A auditoria local dos tres assets confirmou `release.json` com versao `0.2.0-beta.5`, tag esperada, commit completo e arvore limpa. O SHA-256 oficial do EXE e `d9a93b43d65c3ebc85069a8c600ba24f36c8a1d1bde1bccfa2bacf9e36742dc5`, igual ao manifesto e a `SHA256SUMS.txt`.
- Limpeza posterior: `artifacts/beta/` nao existe na checkout auditada depois da confirmacao da release canonica, portanto nada foi removido.

## Decisoes pendentes

- Nenhuma nesta SPEK.
