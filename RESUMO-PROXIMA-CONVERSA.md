---
title: Resumo para Próxima Conversa
aliases: [Resumo para Próxima Conversa, Handoff, Contexto do Projeto]
tags: [projeto/anamnesis, handoff]
type: project
created: 2026-08-04
updated: 2026-08-08
status: growing
summary: Contexto compacto e verificável para retomar o desenvolvimento do Anamnesis em outra conversa.
related: ["[[Anamnesis Home]]", "[[Status Alpha]]", "[[Indice de SPEKs]]", "[[Protocolo de Agentes]]"]
---

# Handoff Anamnesis

Copie a seção abaixo para iniciar outra conversa:

```text
Estamos desenvolvendo o Anamnesis, aplicativo Windows local e open source para gravar reuniões, transcrever localmente, gerar atas estruturadas por LLM e aplicar retenção segura das gravações. Repositório público: https://github.com/michel-az-de/anamnesis (conta michel-az-de, branch main).

Estado atual: alpha concluída e prévia visual local `0.2.0-beta.13-ux-preview.1` instalada nesta máquina sobre a beta.12. A atualização só ocorreu após confirmar 0 reuniões ativas, 0 jobs pendentes e 0 Worker em execução; banco, configuração e instalação anterior foram preservados em backup. O Tray possui ícone próprio, instância única, menu completo na bandeja, inicialização opcional com o Windows e Desktop real. Ele inicia e encerra gravações pelo OBS, persiste reunião e job no SQLite e inicia o Worker. O Worker retoma jobs, prepara áudio com FFmpeg, transcreve com Whisper local em Docker, gera ata estruturada pela Codex CLI autenticada, arquiva os artefatos e aplica retenção segura na Lixeira.

Tecnologia e decisões: .NET 10, Windows 10/11 x64, SQLite local, OBS, Whisper local. Domínio em PT-BR com sufixos técnicos em inglês (ex.: ReuniaoRepository, AtaRunner). Não usar APIs pagas: modelos entram por adaptadores de CLI autenticados por assinatura (Codex, Claude, Kimi, Ollama), sem automatizar interfaces web. A LLM retorna dados estruturados e nunca decide exclusão/estado de reunião. SOLID/KISS/DRY/GoF sem abstrações genéricas ou microserviços; novas dependências/integrações relevantes pedem ADR.

Validação atual: 100% do escopo ponderado da alpha, fluxo hermético e fluxo com pré-requisitos reais. Há 351 testes Release verdes. A SPEK-051 tratou o incidente do primeiro Google Meet real: preservou as duas gravações, recuperou a reunião útil, consolidou o trecho das 08:49 no registro visível das 07:45 com identificação de recuperação parcial, bloqueou falso sucesso de transcrição degenerada, removeu duplicação de fontes no OBS, isolou a mensagem final da Codex CLI, adicionou uma retentativa de JSON e ampliou a retenção de 7 para 30 dias. As SPEKs 052 e 053 tornaram resumo, transcrição, decisões e tarefas selecionáveis/copiáveis e passaram a solicitar atas narrativas factuais com título, data e duração. A SPEK-054 adicionou lembretes locais confirmados, persistidos no SQLite e notificados uma unica vez pelo Tray. As SPEKs 056 e 057 adicionaram busca local em todo o conteúdo e exportação local em PDF/DOCX; a SPEK-036 passou a publicar atas no Obsidian com idempotência e preservação de edições manuais. A SPEK-058 adicionou abertura animada, wizard de três passos, configuração visual persistida e ícones vetoriais de gravação. A SPEK-029 tornou a troca de menu imediata e geometricamente estável, refinou a hierarquia editorial da tela Início, do detalhe e do histórico de reuniões e corrigiu navegação concorrente, cantos laterais e fundo de atenção. A diarização continua especificada na SPEK-055. A release oficial continua sendo a `v0.2.0-beta.6` até publicação canônica do próximo hotfix.

Captura universal: a cena OBS `Anamnesis` é criada e reutilizada com as entradas globais especiais de desktop e microfone quando já existem. O Anamnesis remove somente suas próprias fontes gerenciadas duplicadas; se não houver entrada global, cria `wasapi_output_capture` e `wasapi_input_capture` nos dispositivos padrão. Assim, captura o som do Windows e o microfone sem integração específica com Teams, Meet, Zoom ou navegador. A cena anterior é restaurada após a gravação.

Desktop: as SPEKs 027 a 031, 045 e 058 entregaram uma janela WinForms nativa com o Design System `Command Deck`, identidade visual aplicada ao EXE, instalador, splash, wizard, janela e bandeja, além de configuração persistida e diagnóstico acionável. O modo normal consulta reuniões, jobs e os 500 eventos operacionais mais recentes nos SQLite locais, inicia e encerra a gravação pelos casos de uso, acompanha o Worker por polling de dois segundos e abre artefatos por caminhos persistidos. O journal isolado preserva 14 códigos correlacionados por 14 dias e nunca armazena mensagem livre de exceção, áudio, transcrição, ata, prompt, segredo ou caminho pessoal. Concorrência, cancelamento, reinício e bancos legados não deixam uma gravação órfã bloqueando o produto. A SPEK-046 limita a espera oculta pela exclusividade a cinco minutos, preserva a fila no timeout e registra o diagnóstico no journal. A SPEK-048 serializa entre processos somente a primeira preparação do journal e pula DDL quando WAL, colunas e índices já estão prontos. O modo simulado permanece isolado em `--poc-desktop`.

Próximo incremento: concluir o ensaio manual da SPEK-032 no Google Meet e em Teams ou Zoom, medindo latência e falsos positivos sem iniciar gravação automaticamente sem confirmação do usuário.

Roadmap futuro: as SPEKs 032 a 039 estão em rascunho e especificam captura instantânea por sinais locais, agendas Google e Microsoft somente leitura, publicação Markdown no Obsidian e tarefas aprovadas para Trello ou Azure DevOps. Integrações são opt-in, tokens ficam protegidos pelo Windows, escritas externas exigem confirmação humana e falhas externas não alteram reunião ou retenção. Consulte [[Roadmap de Produto]].

Regras obrigatórias: antes de código, ler AGENTS.md, 30-especificacoes/00-indice.md e a SPEK alvo; se não existir SPEK, criar/aprovar antes do código; seguir Red → Green → Refactor; testes unitários não chamam OBS, rede ou CLI real. Um agente por SPEK. Atualizar SPEK e painel de alpha quando o roadmap mudar. No encerramento da resposta, usar exatamente: Feito (uma frase); Evolução estimada (percentual e base); Falta (lacunas concretas); Próximo passo sugerido (um incremento/SPEK).

Documentação canônica: 30-especificacoes/; .speks/ é somente manifesto para agentes; 10-painel/Status Alpha.md contém pesos, critérios e caminho crítico; 30-especificacoes/05-templates/Template de Entrega.md define o fechamento padrão. Abra 00-home.md no Obsidian.

Para validar: dotnet test Anamnesis.sln --configuration Release --no-restore --verbosity minimal
```

## Referências de retomada

- [[Status Alpha]]
- [[Roadmap de Produto]]
- [[Indice de SPEKs]]
- [[Protocolo de Agentes]]
- [[Template de Entrega]]
