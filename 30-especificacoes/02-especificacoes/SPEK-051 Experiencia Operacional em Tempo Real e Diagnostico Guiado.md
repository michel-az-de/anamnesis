---
title: SPEK-051 Experiencia Operacional em Tempo Real e Diagnostico Guiado
aliases: [SPEK-051, Fluxo Operacional, Diagnostico Guiado]
tags: [especificacao, desktop, ux, audio, transcricao, diagnostico]
type: spek
created: 2026-08-06
updated: 2026-08-06
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
- O fluxo usa as etapas verificáveis `Preparando`, `Gravando`, `Arquivo salvo`, `Na fila`, `Transcrevendo`, `Gerando ata`, `Arquivando`, `Concluído` e `Falha`.
- A etapa ativa é destacada e as etapas concluídas permanecem visíveis. Estados sem percentual verificável usam loading indeterminado; a interface nunca inventa progresso.
- Polling, áudio e journal atualizam somente os controles afetados. Nenhum timer pode reconstruir a página, disparar `Navegar`, limpar a árvore visual ou retirar foco, seleção, rolagem, filtro, aba ou texto não salvo.
- O console correlacionado fica acessível no contexto da reunião e recebe eventos incrementalmente, sem trocar a tela ativa.

### Áudio e detecção

- Microfone e áudio do sistema exibem picos normalizados reais e um histórico curto local suficiente para perceber atividade e silêncio.
- A interface real nunca usa valores animados, aleatórios ou simulados. Quando a leitura não estiver disponível, exibe `Sem leitura` e uma ação para diagnóstico.
- A amostragem não grava conteúdo, não persiste níveis e não envia telemetria. Testes automatizados usam uma fonte falsa e determinística.
- O estado da detecção local mostra o modo `Manual`, `Assistido` ou `Automático`, o sinal seguro observado e a ação para alterar o modo. Contagem regressiva assistida permanece visível e cancelável.

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

## Evidências de implementação

- Suíte Release: 317 de 317 testes verdes, sendo 6 de domínio, 53 de aplicação e 258 de infraestrutura.
- `DesktopPocFormTests` prova atualização incremental sem recriar página, preservação de edição suja, fluxo manual, teste guiado, concorrência bloqueada e cancelamento seguro da captura.
- `SqliteReuniaoRepositoryTests.DeveRestaurarEdicaoAposReinicioEAtualizarArquivosArquivados` reabre o banco por uma nova instância e confere título, transcrição UTF-8, `ata.md` e `transcricao.md`.
- `WindowsNivelAudioSourceTests` e os testes do medidor provam normalização real, indisponibilidade honesta e histórico determinístico sem simulação no modo real.
- Inspeções visuais locais das telas de edição e teste guiado foram produzidas em `C:\tmp\anamnesis-spek-051-editor.png` e `C:\tmp\anamnesis-spek-051-guided.png`.

## Decisões pendentes

- Validar em uso real se cinco segundos são suficientes para o modelo local configurado; ampliar somente com evidência.
- Expor no fluxo o sinal seguro observado pelo detector e permitir alterar `Manual`, `Assistido` ou `Automático` sem editar JSON; o modo configurado já está visível e a contagem existente permanece cancelável.
- Avaliar edição reconciliada da ata em SPEK própria depois que título e transcrição estiverem validados.
