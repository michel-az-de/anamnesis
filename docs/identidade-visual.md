---
title: Identidade Visual
aliases: [identidade-visual]
tags: [design, projeto/anamnesis]
type: note
created: 2026-08-04
updated: 2026-08-04
status: evergreen
summary: Direção visual, símbolo, paleta e tom da marca Anamnesis.
related: ["[[Visao do Produto]]"]
---

# Identidade visual - Anamnesis

![Símbolo aprovado do Anamnesis](../src/Anamnesis.Tray/Assets/Anamnesis.svg)

## Essência

Anamnesis é a memória recuperada conscientemente. O produto transforma uma conversa efêmera em registro claro, verificável e orientado à ação.

**Assinatura:** O que foi dito, lembrado com clareza.

## Símbolo

O símbolo parte de uma estrela de oito pontas contida por anéis e uma moldura quadrada sutil:

- estrela: lucidez, orientação e conhecimento;
- anéis: memória, contexto e recuperação;
- quadrado: ordem, registro e estrutura.

O símbolo acima é o ativo aprovado em SVG. O gerador `scripts/Gerar-IconeWindows.ps1` produz o `.ico` multirresolução de 16, 24, 32, 48 e 256 px usado pelo EXE, janela, barra de tarefas, bandeja, atalhos, instalador e desinstalador.

## Paleta

| Papel | Cor |
| --- | --- |
| Fundo profundo | `#10172E` |
| Destaque principal | `#B87333` |
| Fundo claro | `#F3EEE4` |
| Estado positivo | `#4D8B7A` |
| Texto em fundo claro | `#10172E` |
| Texto em fundo profundo | `#F3EEE4` |

## Tom

Calmo, preciso, reservado e culto. Evitar estética de robô, microfones, neon, fantasia ou iconografia religiosa literal.

## Tipografia

- Interface: Inter.
- Títulos e material institucional: Manrope.

Ambas devem ter fallback para `Segoe UI` no Windows.
