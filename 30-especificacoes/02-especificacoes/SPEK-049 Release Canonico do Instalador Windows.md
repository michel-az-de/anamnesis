---
title: SPEK-049 Release Canonico do Instalador Windows
aliases: [Release Canonico, Versao Canonica do Instalador]
tags: [especificacao, release, instalador, gitlab, github]
type: spek
created: 2026-08-06
updated: 2026-08-06
status: validating
summary: Centraliza a versao do instalador, promove assets imutaveis no GitHub e os espelha byte a byte no GitLab.
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
- GitLab CI nao recompila o instalador. Em tag protegida, valida a fonte, baixa os tres assets oficiais do GitHub, verifica commit e hashes e publica os mesmos bytes no Generic Package Registry.
- O build canonico recusa arvore Git suja e registra o SHA completo do commit. O modo sujo existe somente para diagnostico local e nunca produz pacote distribuivel aprovado.
- Smoke reprovado publica apenas diagnosticos. Smoke aprovado em branch gera artifact temporario identificado como candidato; apenas tag exata promove release oficial.
- Pipelines nunca criam nem movem tags. O GitLab usa apenas tag protegida, runner `windows-release` protegido e efemero e o `CI_JOB_TOKEN` do proprio projeto.
- Depois de uma release canonica confirmada, a pasta local legada `artifacts/beta/` pode ser removida. Outros artefatos, como E2E e POC, permanecem fora desse escopo.

## Criterios de aceite

- [x] A versao `0.2.0-beta.4`, sua versao numerica e a identidade verificavel da release anterior estao declaradas uma unica vez em `release/versao.json`.
- [x] `Build-Installer.ps1` usa a configuracao por padrao, falha ao receber apenas uma das versoes e gera manifesto com SHA-256 e commit completo.
- [x] O build distribuivel exige arvore Git limpa e associa o manifesto ao SHA completo do commit.
- [x] O Inno Setup e a publicacao autocontida nao carregam uma versao beta fixa como padrao independente.
- [x] O workflow GitHub baixa a release anterior oficial, constroi uma vez e separa candidato temporario de promocao por tag.
- [x] A promocao GitHub exige releases imutaveis, recusa sobrescrita, compensa a publicacao mutavel e verifica release e assets com atestado.
- [x] `.gitlab-ci.yml` aceita somente tag protegida e espelha os bytes oficiais no Generic Package Registry sem executar instalador.
- [x] README e runbook explicam build, hash, promocao GitHub e espelho GitLab.
- [x] Os contratos cobrem fonte unica, release anterior real, promocao duravel e isolamento do GitLab.
- [ ] A tag `v0.2.0-beta.4` conclui o smoke instalado e produz release imutavel verificada.
- [ ] `artifacts/beta/` legado e removido somente apos existir um pacote canonico verificado em `artifacts/releases/`.

## Fora de escopo

- Assinatura Authenticode, SmartScreen e atualizacao automatica.
- Criar um remoto GitLab ou proteger seu runner/tag sem URL e permissao administrativa fornecidas.
- Versionar binarios grandes no Git ou introduzir Git LFS nesta entrega.

## Evidencias locais

- Red inicial: 4 de 14 contratos falharam ao exigir release anterior oficial, promocao imutavel e tag GitLab protegida.
- Red de endurecimento: o contrato de promocao falhou ao exigir a compensacao `gh release delete` para uma publicacao mutavel.
- Green focado: 14 de 14 contratos do instalador verdes.
- Bootstrap: `v0.2.0-beta.1` promovida a partir do run `31086786207`, asset `8961793262`, com release imutavel e `gh release verify`/`verify-asset` verdes. SHA-256 do EXE: `5852e3e82ab9c80cf72ab85b8cd4425aeab3aa6faf401b3da0a2fc3db23dbecd`.
- Green completo local: 279 de 279 testes Release aprovados na branch Windows integrada. Os runs `31099937896`, `31100566863`, `31101580716`, `31104316644` e `31105631929` revelaram, respectivamente, rejeicao do Worker legado, flutuacao no stress do journal, uma tentativa invalida de reparo com o instalador legado, contencao do `ThreadPool` no sinal de instancia unica e reutilizacao do caminho `HKCU` depois da migracao elevada para `HKLM`; as cinco regressoes possuem testes e correcoes locais e aguardam nova execucao instalada.

## Decisoes pendentes

- URL do remoto GitLab, protecao da tag e cadastro do runner `windows-release` protegido e efemero.
