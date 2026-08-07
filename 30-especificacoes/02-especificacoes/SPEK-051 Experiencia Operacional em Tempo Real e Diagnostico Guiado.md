---
title: SPEK-051 Experiencia Operacional em Tempo Real e Diagnostico Guiado
aliases: [SPEK-051, Fluxo Operacional, Diagnostico Guiado]
tags: [especificacao, desktop, ux, audio, transcricao, diagnostico]
type: spek
created: 2026-08-06
updated: 2026-08-07
status: em validação
summary: Unifica captura, processamento, edição, áudio real e diagnóstico em um fluxo estável, observável e sem refresh visível.
related: ["[[SPEK-024 Captura Universal de Audio pelo OBS]]", "[[SPEK-029 Polimento Visual e Motion Desktop]]", "[[SPEK-032 Captura Instantanea e Deteccao Local]]", "[[SPEK-050 Fluxo de Processamento Assistido no Tray]]"]
---

# SPEK-051 | Experiência operacional em tempo real e diagnóstico guiado

## Objetivo

Transformar o Command Deck em uma experiência operacional coerente: a pessoa entende em qual etapa a reunião está, confirma se microfone e áudio do sistema estão chegando, acompanha logs seguros em tempo real, edita título e transcrição sem perder o contexto e executa um teste guiado completo de gravação e transcrição com resultado acionável.

## Regras

### Navegação e atualização visual

- A navegação principal mantém a hierarquia existente do Command Deck e reúne as ações da reunião em um fluxo contínuo: preparação, gravação, processamento, revisão e conclusão.
- A barra lateral usa uma única superfície contínua: somente o item ativo ou sob interação recebe fundo, sem contorno permanente em cada opção. Ícones vetoriais, texto e foco respeitam escala de DPI, permanecem nítidos e navegáveis por teclado.
- `Atividade` é uma linha do tempo operacional, não um painel decorativo. Cada item associado a reunião mostra contexto, estado e a ação explícita `Abrir reunião`; clique, Enter ou Espaço abrem o detalhe correspondente. Eventos técnicos sem reunião pertencem à `Observabilidade`.
- A linha do tempo resume quantos itens estão em andamento, concluídos ou com falha e possui estado vazio orientando a primeira ação.
- O fluxo usa as etapas verificáveis `Preparando`, `Gravando`, `Arquivo salvo`, `Na fila`, `Transcrevendo`, `Gerando ata`, `Arquivando`, `Concluído` e `Falha`.
- A etapa ativa é destacada e as etapas concluídas permanecem visíveis. Estados sem percentual verificável usam loading indeterminado; a interface nunca inventa progresso.
- Polling, áudio e journal atualizam somente os controles afetados. Nenhum timer pode reconstruir a página, disparar `Navegar`, limpar a árvore visual ou retirar foco, seleção, rolagem, filtro, aba ou texto não salvo.
- O console correlacionado fica acessível no contexto da reunião e recebe eventos incrementalmente, sem trocar a tela ativa.

### Áudio e detecção

- Microfone e áudio do sistema exibem picos normalizados reais e um histórico curto local suficiente para perceber atividade e silêncio.
- A interface real nunca usa valores animados, aleatórios ou simulados. Quando a leitura não estiver disponível, exibe `Sem leitura` e uma ação para diagnóstico.
- A amostragem não grava conteúdo, não persiste níveis e não envia telemetria. Testes automatizados usam uma fonte falsa e determinística.
- O estado da detecção local mostra o modo `Manual`, `Assistido` ou `Automático`, o sinal seguro observado e a ação para alterar o modo. Contagem regressiva assistida permanece visível e cancelável.

### Presença no Windows e notificações

- Quando a detecção automática inicia uma gravação, um indicador compacto permanece visível acima da área de notificação do Windows durante toda a captura. Ele mostra `Gravando automaticamente`, plataforma, título seguro, tempo decorrido e as ações `Abrir` e `Encerrar`.
- O indicador não aparece para gravação manual, não ocupa um botão adicional na barra de tarefas, não rouba foco e não pisca. Seu estado é estável e muda somente em transições relevantes.
- O ícone da área de notificação também comunica o estado de gravação por texto acessível e uma variação visual estática, sem animação contínua.
- Notificações locais são emitidas uma única vez no início automático, na conclusão do processamento e em falha acionável. Polling repetido não duplica notificações.
- Clicar em uma notificação ou no indicador abre o Anamnesis no contexto operacional disponível. Nenhuma notificação inclui transcrição, segredo, caminho privado ou diagnóstico bruto.
- O polimento visual usa espaçamento consistente em múltiplos de quatro, hierarquia clara entre título, estado e ações, navegação sempre alcançável e ausência de grandes áreas vazias sem função. Etapas e sinais permanecem legíveis em `1180 x 760` com escala de 100%.

### Edição segura

- Título e transcrição podem entrar em modo de edição por ação explícita.
- Edição possui estados `Visualizando`, `Editando`, `Salvando`, `Salvo` e `Falha`, com ações explícitas `Salvar` e `Cancelar`.
- Atualizações de polling não sobrescrevem campos sujos. Sair da reunião com alteração não salva pede confirmação.
- O título usa a normalização já definida e não pode resultar em vazio persistido.
- A transcrição editada preserva UTF-8 e passa por caso de uso e repositório, sem acesso direto da interface ao SQLite ou aos arquivos.
- A ata estruturada, decisões e tarefas permanecem somente leitura neste incremento para evitar divergência silenciosa entre representações.

### Teste guiado

- A configuração oferece `Testar áudio e transcrição` com instrução para falar durante cinco segundos.
- O teste é bloqueado enquanto houver gravação ativa e pode ser cancelado antes do encerramento da captura.
- O fluxo executa componentes reais configurados: prepara, grava, encerra, enfileira, transcreve e mostra o resultado ou a falha. A reunião recebe título iniciado por `Teste de áudio` e segue a retenção normal.
- A tela mostra as mesmas etapas operacionais, loading honesto e console seguro filtrado pela correlação do teste.
- Sucesso exige transcrição não vazia, apresenta um trecho reconhecido e o estado `Tudo certo`.
- Falha apresenta etapa, componente, mensagem segura e correlação, além das ações `Copiar diagnóstico` e `Corrigir agora`. A correção abre a configuração ou o diagnóstico relacionado, sem tentar alterar o ambiente silenciosamente.
- Nenhum conteúdo de áudio ou transcrição entra no journal de observabilidade.

### Arquitetura e qualidade

- A interface chama casos de uso e fronteiras substituíveis; não acessa OBS, Core Audio, Worker, SQLite ou arquivos diretamente.
- Não são adicionadas dependências, serviços remotos ou telemetria.
- Para cada regressão há teste primeiro. Testes unitários não chamam OBS, rede, CLI, Docker, Worker ou modelos reais.

## Critérios de aceite

- [x] O fluxo mostra a etapa atual e o histórico das etapas verificáveis sem percentual falso.
- [x] Polling e sinais em tempo real não recriam a página nem alteram foco, seleção, rolagem, filtros, aba ou edição não salva.
- [x] O console da reunião recebe somente atualizações incrementais e mantém a correlação ativa.
- [x] O modo real remove os medidores simulados e mostra valores reais ou `Sem leitura`.
- [x] Microfone e sistema têm gráfico curto de atividade, indicação de silêncio e leitura acessível em texto.
- [ ] O modo e o estado da detecção local ficam visíveis no fluxo; a contagem assistida continua cancelável.
- [x] A gravação iniciada automaticamente mantém indicador compacto próximo à barra de tarefas, sem foco ou pisca, com plataforma, tempo, `Abrir` e `Encerrar`.
- [x] O ícone e as notificações do Windows comunicam início automático, conclusão e falha uma única vez, sem conteúdo sensível.
- [x] A navegação, as etapas e o cartão operacional permanecem legíveis em `1180 x 760`, com espaçamento coerente e sem área vazia dominante.
- [x] A barra lateral mantém uma única superfície nítida em escala de DPI, com apenas um item ativo, sem cápsulas permanentes e com foco de teclado visível.
- [x] Cada reunião em `Atividade` é uma ação acessível, mostra contexto e estado, abre o detalhe correto e participa do resumo operacional e do estado vazio.
- [x] O título pode ser editado, salvo, cancelado e restaurado após reiniciar a sessão.
- [x] A transcrição pode ser editada, salva, cancelada e restaurada em UTF-8 após reiniciar a sessão.
- [x] Polling não sobrescreve título ou transcrição com alterações locais ainda não salvas.
- [x] O teste guiado grava cinco segundos, acompanha as etapas reais e bloqueia concorrência com gravação ativa.
- [x] Sucesso do teste exige transcrição não vazia e mostra trecho reconhecido com `Tudo certo`.
- [x] Falha do teste mostra etapa, componente, mensagem segura e correlação copiável.
- [x] `Copiar diagnóstico` não inclui transcrição, áudio, segredo nem caminho privado; `Corrigir agora` abre o destino apropriado.
- [x] O teste guiado fica registrado como reunião de teste e respeita a retenção normal.
- [x] Regressões automatizadas cobrem estados, edição, sinal de áudio, atualização incremental, sucesso e falha do diagnóstico.
- [x] A suíte Release permanece verde e não recebe dependência nova.

## Testes associados

- Estado operacional puro: mapeamento de reunião e job para etapas, loading e ações disponíveis.
- Sessão Desktop: edição persistida e proteção contra sobrescrita durante polling.
- Formulário WinForms STA: preservação de foco, texto sujo, aba, filtro e controles durante atualizações.
- Fonte de áudio: normalização, indisponibilidade e histórico determinístico por fakes.
- Teste guiado: gravação ativa bloqueada, cancelamento, transcrição vazia, sucesso e falha acionável.
- Privacidade: diagnóstico copiável sem conteúdo sensível.
- Presença no Windows: indicador automático sem ativação, ações explícitas e descarte ao encerrar.
- Notificações: deduplicação por transição, abertura contextual e conteúdo seguro.
- Regressão visual: navegação inferior visível, cartão operacional compacto e etapas legíveis em `1180 x 760`.
- Navegação e atividade: item ativo único, foco por teclado, ausência de cápsulas permanentes, resumo operacional e abertura da reunião correta por card.

## Fora de escopo

- Percentual real de Whisper, LLM ou arquivamento enquanto os adaptadores não fornecerem progresso verificável.
- Editor reconciliado da ata estruturada, decisões e tarefas.
- Troca de WinForms, redesenho completo do Design System ou nova dependência visual.
- Telemetria remota, gravação contínua dos níveis de áudio ou envio de conteúdo.
- Exclusão especial da gravação de teste fora do caso de uso de retenção.

## Fluxo esperado

```text
Preparar e conferir áudio
          ↓
Nomear reunião → Gravar → Encerrar
          ↓
Arquivo salvo → Na fila → Transcrever → Gerar ata → Arquivar
          ↓                         ↘ console correlacionado
Revisar e editar título/transcrição → Salvar → Concluído

Configuração → Testar áudio e transcrição → Falar por 5 s
          ↓
Etapas + gráfico + console → Tudo certo
                         ↘ Falha → Copiar diagnóstico → Corrigir agora
```

## Sequência TDD

1. Red: formalizar estados e impedir que polling sobrescreva uma edição local.
2. Green: compor o estado operacional puro e atualizar controles existentes incrementalmente.
3. Red: provar que o modo real não pode apresentar medidor simulado.
4. Green: ler picos reais por fronteira local e mostrar `Sem leitura` quando indisponível.
5. Red: título e transcrição não possuem persistência editável.
6. Green: criar casos de uso específicos e ações Salvar/Cancelar.
7. Red: o diagnóstico guiado não diferencia sucesso, transcrição vazia e falha por etapa.
8. Green: orquestrar o teste real com estado, correlação e ações seguras.
9. Refactor: reduzir duplicação visual preservando os limites atuais do Desktop.
10. Red: provar que a captura automática não possui presença persistente e que polling pode repetir notificações.
11. Green: adicionar indicador compacto sem ativação, estado visual do ícone e notificações deduplicadas.
12. Refactor: aplicar a grade de quatro pixels, reduzir peso visual concorrente e eliminar espaço morto no fluxo guiado.
13. Red: provar que a barra lateral parece uma coleção de botões e que os cards de atividade não abrem a reunião.
14. Green: tornar a navegação uma superfície contínua e a atividade um feed acessível com resumo, contexto e abertura explícita.

## Evidências de implementação

- Suíte Release: 326 de 326 testes verdes, sendo 6 de domínio, 53 de aplicação e 267 de infraestrutura.
- `DesktopPocFormTests` prova atualização incremental sem recriar página, preservação de edição suja, fluxo manual, teste guiado, concorrência bloqueada e cancelamento seguro da captura.
- `SqliteReuniaoRepositoryTests.DeveRestaurarEdicaoAposReinicioEAtualizarArquivosArquivados` reabre o banco por uma nova instância e confere título, transcrição UTF-8, `ata.md` e `transcricao.md`.
- `WindowsNivelAudioSourceTests` e os testes do medidor provam normalização real, indisponibilidade honesta e histórico determinístico sem simulação no modo real.
- `GravacaoAutomaticaWidgetTests` prova presença persistente sem ativação ou botão adicional na barra, cronômetro, abertura contextual e encerramento único.
- `NotificacoesDesktopStateTests` prova semeadura silenciosa, notificação somente por transição, deduplicação de polling e falha sem diagnóstico bruto.
- `DesktopPocFormTests` prova a barra lateral como superfície contínua com item ativo único, cards de atividade acessíveis, resumo por estado, estado vazio e abertura do detalhe pelo `ReuniaoId` selecionado.
- Inspeções visuais locais do teste guiado compacto e do indicador automático foram produzidas em `C:\tmp\anamnesis-spek-051-guided-polish-2.png` e `C:\tmp\anamnesis-spek-051-auto-widget.png`.
- A inspeção visual do feed operacional refinado foi produzida em `C:\tmp\anamnesis-spek-051-activity-polish.png` a `1180 x 760`.

## Referências de UX consultadas

- [Fluent 2 Layout](https://fluent2.microsoft.design/layout): grade base de quatro pixels, proximidade e hierarquia por espaçamento.
- [Windows app notifications](https://learn.microsoft.com/windows/apps/develop/notifications/app-notifications/app-notifications-ux-guidance): notificações úteis, pouco ruidosas e com abertura no contexto correto.
- [Windows notification area](https://learn.microsoft.com/windows/win32/uxguide/winenv-notification): estado persistente somente enquanto relevante, sem pisca ou animação contínua, com janela compacta próxima à área de notificação.
- [Windows compact overlay](https://learn.microsoft.com/windows/apps/develop/ui/manage-app-windows): referência para presença pequena, sempre visível e separada da janela principal.

## Decisões pendentes

- Validar em uso real se cinco segundos são suficientes para o modelo local configurado; ampliar somente com evidência.
- Expor no fluxo o sinal seguro observado pelo detector e permitir alterar `Manual`, `Assistido` ou `Automático` sem editar JSON; o modo configurado já está visível e a contagem existente permanece cancelável.
- Avaliar edição reconciliada da ata em SPEK própria depois que título e transcrição estiverem validados.
