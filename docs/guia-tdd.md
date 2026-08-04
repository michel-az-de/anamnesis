---
title: Guia TDD
aliases: [guia-tdd, Guia TDD]
tags: [processo, qualidade]
type: note
created: 2026-08-04
updated: 2026-08-04
status: evergreen
summary: Regras de desenvolvimento orientado a testes do Anamnesis.
related: ["[[Protocolo de Agentes]]", "[[Indice de SPEKs]]"]
---

# Guia de TDD

## Ciclo

1. Escrever um teste que descreve uma regra observável.
2. Implementar somente o necessário para torná-lo verde.
3. Refatorar nomes e duplicações sem alterar o comportamento.

## Ordem de testes

1. `Domain.Tests`: estados de `Reuniao`, retenção e regras de segurança.
2. `Application.Tests`: orquestração com Fakes dos contratos.
3. `Infrastructure.Tests`: SQLite temporário, arquivos temporários e processos CLI falsos.
4. Testes manuais controlados: OBS, Whisper e um provider de IA autenticado.

## Regras

- Não testar detalhes privados de implementação.
- Não chamar um provedor real de IA nos testes automatizados.
- Não depender de OBS, áudio real, rede ou relógio real nos testes unitários.
- Todo bug corrigido deve receber um teste de regressão antes da correção.
- A exclusão física de gravação precisa de teste específico para cada transição de retenção.
