---
title: SPEK-058 Experiencia de Configuracao e Abertura
aliases: [SPEK-058, Wizard de Primeiro Acesso, Abertura de Produto]
tags: [especificacao, desktop, configuracao, onboarding, motion, windows, pos-alpha]
type: spek
created: 2026-08-08
updated: 2026-08-08
status: completed
summary: Torna a abertura, o primeiro acesso, a configuração e as ações de gravação uma experiência visual coesa e funcional.
related: ["[[SPEK-029 Polimento Visual e Motion Desktop]]", "[[SPEK-045 Experiencia Windows Instalada e Primeiro Uso]]", "[[Status Alpha]]"]
---

# SPEK-058 | Experiência de configuração e abertura

## Objetivo

Fazer o Anamnesis parecer um produto desde a abertura fria: apresentar a marca com motion curto, orientar a primeira configuração em um wizard rápido, permitir editar e salvar a configuração local por um formulário real e identificar visualmente as ações de gravação.

## Fluxo

```text
abertura fria
   |
   v
marca animada curta
   |
   +--> configuração ainda não existe --> wizard em três passos --> salvar --> produto
   |
   +--> configuração existente -------------------------------> produto

Configurações --> editar --> validar localmente --> salvar --> aplicar no próximo início
```

## Regras

- A abertura iniciada pela pessoa apresenta a marca com animação curta; inicialização silenciosa com `--background`, validações e diagnósticos não abrem splash.
- Splash e wizard respeitam `SPI_GETCLIENTAREAANIMATION`; sem motion, o estado final aparece imediatamente.
- O wizard aparece somente quando o arquivo de configuração ainda não existia no começo da execução.
- O wizard possui no máximo três passos, progresso explícito, navegação por teclado e uma ação dominante por passo.
- Fechar o wizard antes da conclusão não inicia o Tray com uma configuração presumida como aprovada.
- A configuração visual edita valores reais, valida URI do OBS e caminhos obrigatórios e persiste pelo `ArquivoConfiguracao`.
- Mudanças que exigem recomposição dos adaptadores informam claramente que passam a valer na próxima abertura.
- Senha do OBS não é exibida no formulário e continua protegida pela infraestrutura existente.
- Botões de iniciar, acompanhar e encerrar gravação usam ícones vetoriais próprios, sem emoji, fonte de símbolos ou arquivo raster adicional.
- Ícones, marca e motion usam os tokens do `Command Deck`, tema claro/escuro, foco visível, nome acessível e escala por DPI.
- Nenhuma nova dependência externa é adicionada.

## Critérios de aceite

- [x] A marca animada é renderizada por vetor e conclui em até 1,2 segundo.
- [x] `--background` não apresenta splash nem wizard.
- [x] Primeiro acesso oferece três passos curtos e salva uma configuração válida.
- [x] Uma configuração já existente pula o wizard.
- [x] A página Configurações contém campos editáveis reais, diagnósticos e ação `Salvar alterações`.
- [x] Erros de validação ficam no formulário e não sobrescrevem o arquivo válido.
- [x] Salvamento preserva segredo OBS e campos avançados não editados.
- [x] Ações de gravação exibem ícones distintos para iniciar, acompanhar e encerrar.
- [x] Splash, wizard, formulário e botões são operáveis por teclado e expõem nomes acessíveis.
- [x] Testes WinForms pertencem a `InterfaceWindowsGrupo` e não acessam OBS, rede ou CLI real.

## Testes planejados

- `DesktopProductExperienceTests`
- `DesktopPocFormTests`
- `ArquivoConfiguracaoTests`
- `dotnet test Anamnesis.sln --configuration Release --no-restore --verbosity minimal`

## Decisões

- A mesma linguagem visual do `Command Deck` será estendida com marca vetorial, progresso de onboarding, seções de formulário e ícones de ação.
- Configuração salva durante a execução fica disponível na próxima abertura, evitando reconstruir repositórios, filas e adaptadores em estado parcial.
- O wizard usa a descoberta local já existente como ponto de partida e pede somente decisões compreensíveis para a pessoa.

## Evidências

- Red: o recorte falhou pela ausência de `DesktopStartupExperience`, `PrimeiroUsoConcluido`, `DesktopConfigurationDraft`, `PrimeiroAcessoForm` e `DesktopActionIcon`.
- Green: 9 testes da experiência cobrem abertura interativa, modos silenciosos, primeiro acesso pendente, migração segura, validação, wizard, splash, ícones e salvamento real.
- Suíte integrada: 350 de 350 testes Release verdes, sendo 3 de Domain, 61 de Application e 286 de Infrastructure.
- Splash: marca vetorial com anel progressivo e estrela de oito pontas; duração animada de 1.050 ms e estado final por 180 ms quando o Windows desativa motion.
- Wizard: três passos, progresso explícito, entrada de 167 ms, navegação por teclado e persistência somente na conclusão.
- Formulário real: pasta, OBS, inicialização com Windows e CLI são editáveis; senha do OBS e opções avançadas são preservadas.
- Ícones vetoriais: iniciar, acompanhar, encerrar e salvar não dependem de emoji, fonte de símbolos ou raster adicional.
- Evidência visual: `artifacts/poc-desktop/product-experience/first-run-storage-dark.png`.
- Evidência visual: `artifacts/poc-desktop/product-experience/settings-real-dark.png`.
- Evidência visual: `artifacts/poc-desktop/product-experience/home-record-icon-dark.png`.
- Evidência visual: `artifacts/poc-desktop/product-experience/live-stop-icon-dark.png`.

## Decisões pendentes

- Avaliar em incremento futuro a aplicação imediata de configurações que não exigem reinício.
