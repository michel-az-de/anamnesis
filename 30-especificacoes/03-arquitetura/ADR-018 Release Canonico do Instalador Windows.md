---
title: ADR-018 Release Canonico do Instalador Windows
aliases: [ADR Release GitHub, ADR Versao Canonica]
tags: [adr, release, instalador, github]
type: adr
created: 2026-08-06
updated: 2026-08-06
status: accepted
summary: Define uma fonte unica de versao e um pacote Windows rastreavel para GitHub Actions.
related: ["[[SPEK-049 Release Canonico do Instalador Windows]]", "[[ADR-008 Instalador por Usuario com Inno Setup]]"]
---

# ADR-018 | Release canonico do instalador Windows

## Contexto

O instalador acumulou diretorios locais de auditoria e tentativa, enquanto versoes de produto, versoes numericas, nomes de arquivos e caminhos de artifact foram repetidos em scripts e pipelines. Isso permite que o mesmo numero de versao aponte para bytes diferentes e dificulta promover uma release rastreavel no GitHub.

## Decisao

Adotar `release/versao.json` como fonte unica da release, incluindo URL e SHA-256 da release anterior oficial. O build padrao exige arvore Git limpa e produz `artifacts/releases/<versao>/installer/`, com EXE, SHA-256 e manifesto contendo o SHA completo do commit. O executavel e seu payload continuam fora do Git; configuracao, scripts, pipelines e tags sao versionados.

O GitHub Actions e a autoridade de build e promocao. O smoke instala uma release anterior imutavel, verifica seu hash, atualiza para o candidato construido uma unica vez e promove EXE, `SHA256SUMS.txt` e `release.json` somente para a tag exata. Releases imutaveis bloqueiam mudanca da tag e dos assets e fornecem atestado verificavel.

O `GITHUB_TOKEN` nao possui a permissao administrativa de leitura necessaria para consultar antecipadamente a configuracao `Immutable releases`. Como controle compensatorio sem segredo administrativo, o job verifica `isImmutable` imediatamente depois da criacao. Se o valor for falso, remove a release mutavel sem apagar a tag, confirma sua ausencia e falha. A publicacao so continua para os atestados quando a imutabilidade esta ativa.

Uma recompilacao da mesma tag pode variar por toolchain, timestamps ou ambiente e serve apenas como candidata de diagnostico. Ela nao e uma origem alternativa do pacote oficial.

## Alternativas consideradas

| Alternativa | Probabilidade de adequacao | Motivo |
| --- | ---: | --- |
| Fonte unica + GitHub Release imutavel | 95% | Preserva bytes e atestado, evita builds oficiais divergentes e mantem o repositorio leve. |
| Builds oficiais independentes nos dois provedores | 10% | Exigiria toolchain deterministica e ainda poderia produzir bytes diferentes para a mesma versao. |
| Versionar EXEs no Git | 10% | Historico pesado e commits pouco revisaveis, sem resolver a reproducibilidade. |
| Manter diretorios por tentativa | 5% | Facilita depuracao pontual, mas cria ambiguidade operacional e risco de distribuicao errada. |

## Consequencias

- Cada tag publicada corresponde a assets imutaveis com hash e atestado verificaveis.
- Uma configuracao administrativa indevidamente desativada causa rollback automatico da release mutavel; permanece um intervalo curto entre criacao e compensacao, eliminado apenas com credencial administrativa de leitura ou aprovacao manual.
- O pacote oficial e obtido da GitHub Release. Reconstrucao nunca substitui esses bytes.
- O smoke de atualizacao prova compatibilidade com um instalador anterior realmente publicado.
- A maquina local mantem somente o pacote canonico atual em `artifacts/releases/`.
