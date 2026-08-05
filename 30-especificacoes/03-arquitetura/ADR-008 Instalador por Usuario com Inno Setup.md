---
title: ADR-008 Instalador por Usuario com Inno Setup
aliases: [ADR Inno Setup, Instalador da Beta]
tags: [adr, instalador, windows, beta]
type: adr
created: 2026-08-05
updated: 2026-08-05
status: accepted
summary: Adota Inno Setup para gerar um instalador EXE x64 sem elevacao na beta.
related: ["[[SPEK-022 Instalador da Beta em Windows Limpo]]"]
---

# ADR-008 | Instalador por usuario com Inno Setup

## Contexto

A alpha possui Tray e Worker autocontidos, mas exige distribuicao manual de duas pastas. A beta precisa de um instalador unico, desinstalacao previsivel e validacao automatizada em Windows limpo.

## Decisao

Usar Inno Setup 6.7.3 para compilar um EXE de instalacao por usuario com `PrivilegesRequired=lowest` e destino em `%LocalAppData%\Programs\Anamnesis`.

O CI baixa o instalador oficial do compilador, verifica o SHA-256 fixado e executa o build. O produto nao inclui prerequisitos externos grandes ou autenticados.

## Alternativas consideradas

| Alternativa | Probabilidade de adequacao | Motivo |
| --- | ---: | --- |
| Inno Setup | 85% | EXE unico, open source, instalacao sem UAC, atalhos, modo silencioso e desinstalador simples. |
| WiX Toolset | 15% | MSI nativo e boa integracao MSBuild, mas maior complexidade e politica de manutencao comercial desnecessaria nesta etapa. |
| ZIP portavel | 5% | Simples, mas nao atende instalacao, atalhos e desinstalacao da beta. |

## Consequencias

- O build do instalador depende de `ISCC.exe`, mas o aplicativo continua sem dependencia de runtime adicional.
- O EXE inicial nao e assinado e pode gerar alerta de reputacao do Windows.
- Dados locais permanecem fora da pasta do programa e sobrevivem a atualizacoes e desinstalacoes.
- A validacao em Windows limpo ocorre em runner descartavel do GitHub Actions, pois Windows Home nao oferece Windows Sandbox.
