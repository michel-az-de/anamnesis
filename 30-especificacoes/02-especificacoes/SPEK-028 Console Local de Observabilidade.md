---
title: SPEK-028 Console Local de Observabilidade
aliases: [SPEK-028, Console de Observabilidade, Telemetria Local]
tags: [especificacao, desktop, observabilidade, telemetria, pos-alpha]
type: spek
created: 2026-08-05
updated: 2026-08-05
status: completed
summary: Console visual local para investigar eventos, tempos e falhas do fluxo do Anamnesis sem expor conteúdo sensível.
related: ["[[Status Alpha]]", "[[SPEK-027 Desktop Windows para Estado Visivel]]", "[[SPEK-023 Orquestracao do Worker pelo Tray]]"]
---

# SPEK-028 Console Local de Observabilidade

## Objetivo

Adicionar à POC desktop uma área de observabilidade no estilo console, capaz de mostrar eventos operacionais, correlação, duração e métricas do ciclo simulado de gravação e processamento. A tela deve ajudar a encontrar bugs e medir comportamento sem depender de terminal.

## Fora de escopo

- Ler logs reais do Tray, Worker, OBS, Docker, Whisper ou CLIs.
- Persistir eventos entre execuções.
- Enviar telemetria para serviços externos.
- Exibir áudio, transcrição, prompts, respostas da LLM, tokens, senhas ou caminhos pessoais.
- Definir retenção ou exclusão a partir dos eventos observados.

## Regras

- A navegação principal oferece a seção `Observabilidade`.
- A POC mantém um fluxo de eventos estruturados com horário, nível, componente, evento, mensagem segura, correlação e duração opcional.
- O ciclo simulado registra início de gravação, captura saudável, fim de gravação, criação e reserva do job, transcrição, geração da ata, arquivamento e conclusão.
- A tela permite filtrar por texto, nível e componente.
- As métricas são calculadas a partir dos eventos visíveis no estado da simulação, incluindo total, alertas, duração média e jobs na fila.
- Eventos de demonstração deixam explícito que a telemetria é simulada.
- Logs nunca contêm conteúdo de reunião, segredos, argumentos completos de CLI ou caminhos pessoais.
- Cores de console, níveis e superfícies usam tokens semânticos do tema claro ou escuro do Windows.
- O modo normal do Tray permanece compatível.

## Fluxo esperado

```text
gravação iniciada
      |
      v
captura saudável -> gravação encerrada -> job criado
                                          |
                                          v
                               Whisper -> Ata -> Arquivo
                                          |
                                          v
                                  métricas atualizadas
```

## Critérios de aceite

- [x] O menu `Observabilidade` abre uma única tela dentro da janela principal.
- [x] O console mostra eventos simulados com horário, nível, componente, correlação e duração.
- [x] Texto, nível e componente filtram a lista sem alterar o estado original.
- [x] Total de eventos, alertas, duração média e fila atual são derivados do estado.
- [x] Iniciar, encerrar e concluir o fluxo simulado adicionam eventos coerentes.
- [x] A tela permanece legível em 1280 x 720 nos temas escuro e claro.
- [x] Nenhum conteúdo sensível aparece nos eventos predefinidos.
- [x] Testes automatizados cobrem o modelo, os filtros, as métricas e a navegação visual.
- [x] A publicação `win-x64` abre a POC com o console disponível.

## Testes planejados

- `DesktopPocObservabilityTests`
- `DesktopPocFormTests`
- `DesktopPocThemeTests`
- `dotnet test Anamnesis.sln --configuration Release --no-restore --verbosity minimal`

## Decisões

- A POC usa um estado em memória, sem nova dependência ou ADR.
- O padrão visual define tokens semânticos próprios para console e níveis.
- A futura integração real deve adaptar logs estruturados existentes, preservando o mesmo contrato visual e as regras de privacidade.

## Evidências da entrega

- Ciclo Red: a compilação falhou pela ausência de `DesktopPocObservabilityState`, `NivelEventoPoc`, `ConsoleFundo` e `ConsoleTexto`.
- Ciclo Green: 83 testes verdes em `Release`, sendo 3 de Domain, 11 de Application e 69 de Infrastructure.
- Estado coberto: eventos iniciais seguros, filtros combinados, correlação, fila e conclusão do processamento.
- Interface coberta: navegação STA, console do fluxo, tokens de tema e captura visual automatizada.
- Evidência visual: `artifacts/poc-desktop/observability-evidence/console-dark-v2.png`.
- Publicação autocontida: `artifacts/poc-desktop/win-x64-observability-v1`.
- Atalho executável: `artifacts/poc-desktop/win-x64-observability-v1/Abrir Anamnesis POC.lnk`.
- Validação operacional: processo publicado responsivo, título `Anamnesis` e argumento `--poc-desktop`.

## Decisões pendentes

- Definir na SPEK-029 a origem real dos eventos e o limite de retenção local.
- Avaliar exportação de um pacote de diagnóstico sanitizado somente após validar o console.
