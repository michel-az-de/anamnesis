---
title: ADR-008 Instalador Elevado por Usuario com Inno Setup
aliases: [ADR Inno Setup, Instalador da Beta]
tags: [adr, instalador, windows, beta]
type: adr
created: 2026-08-05
updated: 2026-08-05
status: accepted
summary: Adota Inno Setup para gerar um instalador EXE x64 elevado, mantendo o produto por usuario na beta.
related: ["[[SPEK-022 Instalador da Beta em Windows Limpo]]"]
---

# ADR-008 | Instalador elevado por usuario com Inno Setup

## Contexto

A alpha possui Tray e Worker autocontidos, mas exige distribuicao manual de duas pastas. A beta precisa de um instalador unico, desinstalacao previsivel e validacao automatizada em Windows limpo.

## Decisao

Usar Inno Setup 6.7.3 para compilar um EXE de instalacao elevado por UAC com `PrivilegesRequired=admin`. A diretiva de override permanece ausente, que e o padrao do Inno Setup e impede escolher modo sem privilegio. O destino continua em `%LocalAppData%\Programs\Anamnesis`, portanto a elevacao protege a instalacao e os diagnosticos sem transformar o produto em instalacao para todos os usuarios.

O CI baixa o instalador oficial do compilador, verifica o SHA-256 fixado e executa o build. O produto nao inclui prerequisitos externos grandes ou autenticados.

## Alternativas consideradas

| Alternativa | Probabilidade de adequacao | Motivo |
| --- | ---: | --- |
| Inno Setup elevado | 85% | EXE unico, open source, UAC previsivel, atalhos, diagnostico local, modo silencioso e desinstalador simples. |
| Inno Setup sem UAC | 10% | Menos friccao, mas nao atende a exigencia de elevacao nem o fluxo de reparo solicitado. |
| WiX Toolset | 15% | MSI nativo e boa integracao MSBuild, mas maior complexidade e politica de manutencao comercial desnecessaria nesta etapa. |
| ZIP portavel | 5% | Simples, mas nao atende instalacao, atalhos e desinstalacao da beta. |

## Consequencias

- O build do instalador depende de `ISCC.exe`, mas o aplicativo continua sem dependencia de runtime adicional.
- Instalacao, atualizacao, reparo e desinstalacao exibem UAC; usuarios sem credencial administrativa precisam de aprovacao local.
- O instalador reconhece a beta legada registrada em `HKCU` e a instalacao elevada em `HKLM`, migrando a entrada de desinstalacao somente depois de concluir a copia dos binarios.
- O EXE inicial nao e assinado e pode gerar alerta de reputacao do Windows.
- Dados locais permanecem fora da pasta do programa e sobrevivem a atualizacoes e desinstalacoes.
- A validacao em Windows limpo ocorre em runner descartavel do GitHub Actions, pois Windows Home nao oferece Windows Sandbox.
