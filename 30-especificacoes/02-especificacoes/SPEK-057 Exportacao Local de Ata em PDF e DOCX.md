---
title: SPEK-057 Exportacao Local de Ata em PDF e DOCX
aliases: [SPEK-057, Ata Compartilhavel, Exportar Ata]
tags: [especificacao, exportacao, pdf, docx, desktop, pos-alpha]
type: spek
created: 2026-08-07
updated: 2026-08-07
status: completed
summary: Exporta uma ata concluida para PDF ou DOCX local com layout legivel e escrita atomica.
related: ["[[SPEK-019 Ata Markdown Estruturada]]", "[[SPEK-036 Publicacao Local no Obsidian]]"]
---

# SPEK-057 | Exportacao local de ata em PDF e DOCX

## Objetivo

Permitir que uma ata concluida vire um documento compartilhavel sem depender de servico remoto, Office instalado ou API paga.

## Regras

- Somente reuniao com ata gerada pode ser exportada.
- PDF e DOCX incluem titulo, data, duracao, resumo executivo, decisoes e tarefas.
- O formato segue um brief profissional: hierarquia clara, margens consistentes e listas reais no DOCX.
- A exportacao e totalmente local e nao inclui gravacao nem transcricao integral.
- O destino e escolhido pela pessoa e a extensao precisa corresponder ao formato.
- A escrita usa arquivo temporario no mesmo diretorio e movimento final atomico.
- Arquivo existente somente pode ser substituido depois da confirmacao da interface.
- Falha de exportacao nao altera reuniao, job, artefatos ou retencao.
- O primeiro corte nao adiciona dependencia NuGet.

## Criterios de aceite

- [x] PDF valido abre e renderiza sem texto cortado ou glifos ausentes.
- [x] DOCX valido abre e renderiza com titulo, secoes, listas e rodape.
- [x] Acentos em portugues sao preservados.
- [x] Reuniao sem ata e rejeitada com mensagem acionavel.
- [x] Extensao divergente e rejeitada.
- [x] Escrita interrompida nao deixa arquivo final parcial.
- [x] A interface oferece Exportar PDF e Exportar DOCX na aba Arquivos.
- [x] Testes estruturais e renderizacao de amostra validam os dois formatos.

## Sequencia TDD

1. Red: caso de uso rejeita ausencia de ata e nao possui exportador.
2. Green: implementar contrato, validacao e geradores locais minimos.
3. Red: interface nao oferece os formatos nem confirma sobrescrita.
4. Green: integrar dialogos nativos e mensagens seguras.
5. Refactor: compartilhar o modelo de documento entre os formatos.

## Entrega

- Red: os testes falharam pela ausencia dos contratos, casos de uso e geradores.
- Green: PDF 1.4 e DOCX OOXML sao gerados localmente, sem dependencia NuGet ou Office instalado em producao.
- Seguranca: extensao, sobrescrita, cancelamento e movimento atomico possuem testes.
- Interface: a aba Arquivos oferece PDF, DOCX e publicacao no Obsidian.
- Evidencias: `artifacts/evidencias/SPEK-057/ata.pdf`, `ata.docx` e `desktop-exportacao.png`.
- Renderizacao: PDF validado pelo Poppler; DOCX renderizado pelo Word local em modo invisivel porque LibreOffice nao estava instalado; todas as paginas foram inspecionadas.
- Validacao final: 327 testes Release verdes no conjunto do produto.
