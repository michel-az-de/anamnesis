---
title: SPEK-029 Polimento Visual e Motion Desktop
aliases: [SPEK-029, Polimento Visual Desktop, Command Deck]
tags: [especificacao, desktop, design-system, motion, windows, pos-alpha]
type: spek
created: 2026-08-05
updated: 2026-08-08
status: completed
summary: Revisão visual sólida e precisa da POC para uma experiência desktop premium, sem transparências ou decoração excessiva.
related: ["[[Status Alpha]]", "[[Design System Desktop]]", "[[SPEK-027 Desktop Windows para Estado Visivel]]", "[[SPEK-028 Console Local de Observabilidade]]"]
---

# SPEK-029 Polimento Visual e Motion Desktop

## Objetivo

Transformar a POC desktop em um `Command Deck` premium, sólido, preciso e reconhecível como produto Windows. A interface deve ter densidade, hierarquia, resposta e personalidade, sem parecer um mockup translúcido ou uma coleção de controles WinForms nativos.

## Fora de escopo

- Migrar WinForms para WinUI 3.
- Conectar reuniões, jobs ou eventos reais.
- Adicionar áudio, efeitos sonoros, vídeo ou renderização 3D.
- Adicionar bibliotecas visuais ou dependências externas.
- Usar neon excessivo, fantasia, estética religiosa ou aparência genérica de ficção científica.
- Usar transparência, Acrylic, Mica, vidro, blur, grades, órbitas ou partículas decorativas.
- Manter inputs, selects ou toggles com aparência nativa inconsistente.

## Regras

- A direção visual é `Command Deck`: canvas sólido, superfícies opacas, bordas precisas, cobre como foco e verde como saúde.
- Cor, tipografia, espaçamento, geometria, elevação e motion devem possuir tokens semânticos documentados.
- Toda cor usada em uma superfície possui alpha 255.
- Cartões usam contraste entre planos, borda discreta e raio contido. Não usam brilho, gradiente ou sombra pesada.
- Botões principais, secundários, perigosos e itens de navegação possuem estados default, hover, pressionado, selecionado, foco e desabilitado.
- A navegação troca o conteúdo imediatamente e mantém sua geometria estável; motion fica restrito ao feedback de hover, pressionado e foco.
- Páginas nunca coexistem durante a troca, nem ficam sobrepostas, deslocadas ou presas fora do destino.
- Itens laterais mantêm folga mínima para que cantos e bordas não sejam recortados pelo contêiner.
- Estados globais de sucesso, processamento e atenção usam fundo e texto semanticamente coerentes.
- O canvas não possui animação contínua.
- O aplicativo respeita `SPI_GETCLIENTAREAANIMATION`.
- Motion usa durações Fluent de 83 ms, 167 ms e 250 ms com curva de desaceleração.
- A moldura DWM recebe tema, cor e cantos, mas o backdrop é explicitamente desativado.
- Inputs, selects e toggles usam componentes próprios, sólidos e coerentes com os temas.
- Ícones de navegação são vetoriais e não dependem de glifos Unicode.
- Tema claro, tema escuro e modo sem animação mantêm contraste e hierarquia.
- O modo normal do Tray permanece compatível.

## Fluxo visual

```text
entrada do usuário
       |
       v
hover ou clique -> mudança precisa de cor/borda -> troca imediata -> conteúdo estável
       |                                                               |
       +-------------------- preferência do Windows -------------------+
                                   remove motion
```

## Componentes do incremento

| Componente | Responsabilidade |
| --- | --- |
| `DesktopBackdropPanel` | Canvas sólido sem decoração ou animação contínua |
| `DesktopSurfacePanel` | Superfícies opacas, cantos contidos, borda e hover |
| `DesktopActionButton` | Variantes de ação com resposta visual e foco |
| `DesktopNavigationButton` | Ícone vetorial, estado selecionado, hover e marcador lateral |
| `DesktopTextField` | Campo de texto sólido e coerente com a paleta |
| `DesktopSelectField` | Select owner-drawn com estados do sistema |
| `DesktopToggle` | Alternância própria com foco e texto de estado |
| `DesktopPocMotion` | Curvas, interpolação e transição de página |
| `DesktopPocSystemPreferences` | Preferência de animação do Windows |
| `WindowsTitleBarTheme` | Tema, cantos, borda, título e backdrop desativado |

## Critérios de aceite

- [x] Canvas, superfícies, botões e navegação usam somente cores opacas.
- [x] O canvas não desenha grade, órbitas, partículas, glow ou animação contínua.
- [x] A composição inicial possui leitura clara, densidade equilibrada e uma única ação dominante.
- [x] Cartões e botões possuem raios contidos, bordas precisas e estados coerentes.
- [x] Navegação usa ícones vetoriais consistentes e marcador selecionado discreto.
- [x] Inputs, selects e toggles não exibem aparência WinForms padrão.
- [x] A troca de páginas é imediata mesmo quando motion estiver habilitado, sem deslocamento ou salto do conteúdo.
- [x] A troca de páginas é imediata quando motion estiver desabilitado.
- [x] Cliques consecutivos durante motion terminam com somente a última página visível, preenchendo todo o canvas.
- [x] A navegação lateral não exibe pixels ou bordas recortadas no lado direito.
- [x] O estado `Ação necessária` não reutiliza o fundo verde de sucesso.
- [x] O DWM mantém tema e cantos, com backdrop desativado.
- [x] Dark e light mode permanecem legíveis em 1280 x 720.
- [x] Teclado, foco e acessibilidade continuam funcionais.
- [x] Testes automatizados cobrem opacidade, componentes, motion e DWM sólido.
- [x] Uma publicação `win-x64` abre a revisão premium responsiva.

## Testes planejados

- `DesktopPocDesignSystemTests`
- `DesktopPocFormTests`
- `DesktopPocThemeTests`
- `WindowsTitleBarThemeTests`
- `InterfaceWindowsGrupo` executa formulários WinForms em série no runner.
- `dotnet test Anamnesis.sln --configuration Release --no-restore --verbosity minimal`

## Decisões

- O incremento evolui WinForms sem nova dependência e sem ADR.
- A revisão substitui vidro e cenário decorativo por contraste entre superfícies sólidas.
- O backdrop do sistema é desativado para impedir qualquer percepção de defeito visual.
- A presença de game vem de precisão, densidade e resposta, não de cenário ou glow.
- Os controles continuam em WinForms, mas recebem pintura e estados próprios sem nova dependência.

## Evidências da entrega v1 substituída

- Ciclo Red: a compilação falhou pela ausência dos tokens, política de efeitos, componentes visuais e atributos DWM especificados.
- Ciclo Green: 96 testes verdes em `Release`, sendo 3 de Domain, 11 de Application e 82 de Infrastructure.
- Motion coberto: curva determinística, durações de 83, 167 e 250 ms, transição espacial e caminho imediato sem animação.
- Fallback coberto: transparência e ambiente animado podem ser removidos sem alterar conteúdo ou navegação.
- Evidência visual dark: `artifacts/poc-desktop/polish-evidence/home-command-center-dark-v2.png`.
- Evidência visual ao vivo: `artifacts/poc-desktop/polish-evidence/live-command-center-dark-v2.png`.
- Evidência visual do console: `artifacts/poc-desktop/polish-evidence/observability-command-center-dark-v2.png`.
- Evidência visual light: `artifacts/poc-desktop/polish-evidence/observability-command-center-light-v3.png`.
- Publicação autocontida: `artifacts/poc-desktop/win-x64-command-center-v1`.
- Validação operacional: processo responsivo, 46,7 MB de memória e até 0,62% de um núcleo em duas amostras ociosas de cinco segundos.
- Logs: `artifacts/poc-desktop/polish-evidence/validation-release-v1.log` e `artifacts/poc-desktop/polish-evidence/runtime-command-center-v1.log`.
- Integração Windows 11 validada: dark title bar, cantos arredondados e backdrop principal retornaram sucesso pelo DWM.

O feedback visual de 2026-08-05 reprovou a direção translúcida por parecer defeituosa e pouco premium. As evidências acima ficam preservadas como histórico, mas não representam mais o alvo aprovado desta SPEK.

## Evidências da revisão sólida

- Ciclo Red: a compilação falhou pela ausência de `Superficies`, `DesktopNavigationIcon`, `DesktopTextField`, `DesktopSelectField`, `DesktopToggle`, canvas sem decoração e backdrop sólido.
- Ciclo Green: 96 testes verdes em `Release`, sendo 3 de Domain, 11 de Application e 82 de Infrastructure.
- Auditoria estática: nenhum `Color.Transparent`, gradiente ou brush translúcido permanece nos componentes `DesktopPoc`.
- Componentes próprios: ícones vetoriais, botões, navegação, campo de texto, select e toggle possuem pintura sólida.
- Configurações: composição em duas colunas sem scrollbar, com comportamento, IA, armazenamento e diagnósticos.
- Evidência inicial dark: `artifacts/poc-desktop/solid-evidence/home-command-deck-dark-v3.png`.
- Evidência ao vivo dark: `artifacts/poc-desktop/solid-evidence/live-command-deck-dark-v3.png`.
- Evidência de configurações dark: `artifacts/poc-desktop/solid-evidence/settings-command-deck-dark-v4.png`.
- Evidência de observabilidade dark: `artifacts/poc-desktop/solid-evidence/observability-command-deck-dark-v4.png`.
- Evidência de observabilidade light: `artifacts/poc-desktop/solid-evidence/observability-command-deck-light-v3.png`.
- Publicação autocontida: `artifacts/poc-desktop/win-x64-command-deck-v2`.
- Validação operacional: processo responsivo, 49,3 MB e 0,31% de um núcleo em amostra ociosa de cinco segundos.
- DWM real: dark title bar `1`, cantos arredondados `2` e system backdrop `1`, que significa backdrop desativado.
- Logs: `artifacts/poc-desktop/solid-evidence/validation-command-deck-v4.log` e `artifacts/poc-desktop/solid-evidence/runtime-command-deck-v2.log`.
- Regressão CI: o run `31123148796` excedeu o limite da janela STA enquanto outra janela WinForms podia executar em paralelo. `DesktopPocFormTests` e `DeteccaoPromptFormTests` agora pertencem à coleção não paralela `InterfaceWindowsGrupo`.

## Referências técnicas

- [Materiais em aplicativos Windows](https://learn.microsoft.com/windows/apps/develop/ui/materials)
- [DWM_SYSTEMBACKDROP_TYPE](https://learn.microsoft.com/windows/win32/api/dwmapi/ne-dwmapi-dwm_systembackdrop_type)
- [Timing e easing do Fluent](https://learn.microsoft.com/windows/apps/design/motion/timing-and-easing)
- [SystemParametersInfo](https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-systemparametersinfoa)

## Decisões pendentes

- Avaliar a migração para WinUI 3 depois que a linguagem visual e os fluxos reais estiverem validados.
- Definir na SPEK-030 a conexão da interface com reuniões, jobs, eventos e artefatos reais.

## Correção de regressão 2026-08-07

- Red: três testes reproduziram páginas sobrepostas em navegação rápida, pintura recortada na lateral e fundo verde em estado de atenção.
- Green: uma única transição de página fica ativa; novo clique conclui a anterior antes de iniciar a próxima.
- A largura dos itens laterais reserva dois pixels para pintura dos cantos e o estado global escolhe fundo semântico.
- Validação isolada: 327 testes Release verdes, sem incluir as alterações de agenda ainda em andamento no checkout principal.

## Correção de estabilidade visual 2026-08-08

- O teste ao vivo reprovou a transição espacial porque a página nova podia permanecer uma largura inteira fora do canvas antes de saltar ao destino.
- A navegação passa a trocar páginas de forma imediata, preservando motion somente nos estados dos controles.
- A geometria do menu lateral e do conteúdo deve permanecer idêntica antes e depois de qualquer troca.
- Red: o teste encontrou duas páginas simultâneas logo após o clique; Green: a troca mantém uma única página em `DockStyle.Fill`.
- Validação isolada: 328 testes Release verdes, sendo 3 de Domain, 61 de Application e 264 de Infrastructure.

## Revisão editorial do detalhe da reunião 2026-08-08

O teste visual com uma ata real mostrou que a tela de detalhe ainda tinha aparência de protótipo: título sem contexto, cinco abas isoladas em cápsulas, conteúdo repetitivo e cartões altos demais para o texto disponível.

- O cabeçalho do detalhe comunica contexto, título, metadados e estado da reunião em uma única hierarquia de leitura.
- As abas formam uma navegação contínua, sem cápsulas ou bordas permanentes; somente a aba ativa recebe marcador de destaque.
- Cada aba expõe um título editorial e uma descrição curta antes do conteúdo, evitando repetição mecânica entre aba e cartão.
- Blocos de texto calculam sua altura a partir do conteúdo e preservam uma largura confortável de leitura, sem grandes áreas vazias.
- Texto selecionável, atalho de cópia, foco visível, tema claro/escuro e escala por DPI continuam obrigatórios.
- Red: testes de interface reprovam abas com região arredondada, ausência de contexto acessível e texto com tipografia compacta de protótipo.
- Green: cabeçalho editorial, estado da reunião, abas contínuas com `AccessibleRole.PageTab`, títulos contextuais e blocos proporcionais foram implementados sem alterar os dados ou as ações existentes.
- Evidência visual dark: `artifacts/poc-desktop/editorial-evidence/detail-decisions-dark.png`.
- Validação Release: build com 0 avisos e 0 erros; 339 testes verdes, sendo 3 de Domain, 61 de Application e 275 de Infrastructure.

## Revisão editorial do histórico de reuniões 2026-08-08

O teste visual da listagem real mostrou filtros fragmentados, um vazio central sem função e reuniões apresentadas como cartões isolados sem navegação completa por teclado.

- Busca, período e estado ocupam uma grade responsiva, com contexto e nomes acessíveis explícitos.
- A área de resultados informa quantas reuniões estão visíveis e atualiza a contagem após filtros locais ou busca persistida.
- Reuniões formam uma superfície contínua com separadores discretos, sem cápsulas, sombras ou espaços entre cartões.
- Cada reunião expõe `AccessibleRole.ListItem`, foco visível e abertura por mouse, Enter ou Espaço.
- Título, plataforma, data, duração, estado e trecho correspondente mantêm a mesma informação factual.
- Red: testes reprovam a ausência de contexto e contagem, busca não responsiva, itens arredondados e falta de acionamento por teclado.
- Green: busca e filtros passaram para uma grade responsiva; contagem acompanha o resultado; reuniões usam superfície contínua, `AccessibleRole.ListItem`, foco e abertura por Enter ou Espaço.
- Evidências visuais dark: `artifacts/poc-desktop/editorial-evidence/meetings-before.png` e `artifacts/poc-desktop/editorial-evidence/meetings-after.png`.
- Validação Release: build com 0 avisos e 0 erros; 341 testes verdes, sendo 3 de Domain, 61 de Application e 277 de Infrastructure.
- Prévia instalada: `0.2.0-beta.13-ux-preview.1`, com hash do binário instalado igual ao payload, SQLite íntegro, 25 reuniões e 23 jobs preservados.

## Revisão editorial da tela Início 2026-08-08

O teste visual da tela inicial mostrou uma ação principal desconectada do contexto, três cartões técnicos com o mesmo peso e uma grande área vazia. A página funcionava, mas não comunicava com clareza o próximo passo do usuário.

- A captura local ocupa uma única superfície de comando, com estado honesto, explicação curta e uma ação dominante.
- Recuperação pendente, gravação ativa e prontidão mantêm textos e destinos compatíveis com o estado real da sessão.
- OBS, Worker e SQLite formam uma única área contínua de prontidão, sem cartões técnicos concorrentes.
- Reuniões recentes permanecem acessíveis abaixo do comando principal, com acesso direto ao histórico completo.
- A hierarquia usa os contextos `CAPTURA LOCAL`, `PRONTIDÃO DO SISTEMA` e `REUNIÕES RECENTES` para orientar a leitura.
- Tema claro/escuro, foco visível, teclado e largura útil em 1280 x 720 continuam obrigatórios.
- Red: o teste de interface reprovou a ausência dos contextos editoriais, antes de alcançar a ação sem nome acessível e os estados técnicos fragmentados em cartões independentes.
- Green: a captura passou para uma superfície de comando com ação acessível; OBS, Worker e SQLite compartilham uma única superfície; o histórico recente ganhou hierarquia própria sem alterar comandos nem dados reais.
- Evidências visuais dark: `artifacts/poc-desktop/editorial-evidence/home-before.png` e `artifacts/poc-desktop/editorial-evidence/home-after.png`.
- Validação Release: 351 testes verdes, sendo 3 de Domain, 61 de Application e 287 de Infrastructure; os 24 testes de `DesktopPocFormTests` também passaram em conjunto.

## Correção do ciclo de vida dos selects 2026-08-08

O uso dos filtros de Observabilidade expôs uma `ObjectDisposedException`: o `ContextMenuStrip` era descartado dentro do próprio evento `Closed`, antes de o WinForms concluir o fechamento nativo.

- O menu de opções não pode ser descartado durante a execução síncrona de `Closed`.
- O descarte deve ocorrer na próxima passagem da fila da interface e continuar obrigatório ao destruir o `DesktopSelectField`.
- Abrir, selecionar, fechar e reabrir filtros não pode produzir exceção não tratada.
- Red: um teste de ciclo de vida exige que o menu permaneça válido ao retornar de `Close` e seja descartado após o processamento da fila WinForms.
- Green: o select agenda o descarte, mantém uma única referência de menu aberto e encerra o recurso no `Dispose` do controle.
- Validação Release: 352 testes verdes, sendo 3 de Domain, 61 de Application e 288 de Infrastructure; os 25 testes de `DesktopPocFormTests` passaram em conjunto.
- Prévia instalada: `0.2.0-beta.15-ux-preview.1`, com binário instalado igual ao payload, Tray responsivo, SQLite íntegro, 25 reuniões, 23 jobs, nenhum job pendente e nenhuma reunião ativa.
