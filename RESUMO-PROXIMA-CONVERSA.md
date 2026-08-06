---
title: Resumo para Próxima Conversa
aliases: [Resumo para Próxima Conversa, Handoff, Contexto do Projeto]
tags: [projeto/anamnesis, handoff]
type: project
created: 2026-08-04
updated: 2026-08-06
status: growing
summary: Contexto compacto e verificável para retomar o desenvolvimento do Anamnesis em outra conversa.
related: ["[[Anamnesis Home]]", "[[Status Alpha]]", "[[Indice de SPEKs]]", "[[Protocolo de Agentes]]"]
---

# Handoff Anamnesis

Copie a seção abaixo para iniciar outra conversa:

```text
Estamos desenvolvendo o Anamnesis, aplicativo Windows local e open source para gravar reuniões, transcrever localmente, gerar atas estruturadas por LLM e aplicar retenção segura das gravações. Repositório público: https://github.com/michel-az-de/anamnesis (conta michel-az-de, branch main).

Estado atual: alpha concluída e experiência Windows `0.2.0-beta.1` instalada nesta máquina. O Tray possui ícone próprio, instância única, menu completo na bandeja, inicialização opcional com o Windows e Desktop real. Ele inicia e encerra gravações pelo OBS, persiste reunião e job no SQLite e inicia o Worker. O Worker retoma jobs, prepara áudio com FFmpeg, transcreve com Whisper local em Docker, gera ata estruturada pela Codex CLI autenticada, arquiva os artefatos e aplica retenção segura na Lixeira.

Tecnologia e decisões: .NET 10, Windows 10/11 x64, SQLite local, OBS, Whisper local. Domínio em PT-BR com sufixos técnicos em inglês (ex.: ReuniaoRepository, AtaRunner). Não usar APIs pagas: modelos entram por adaptadores de CLI autenticados por assinatura (Codex, Claude, Kimi, Ollama), sem automatizar interfaces web. A LLM retorna dados estruturados e nunca decide exclusão/estado de reunião. SOLID/KISS/DRY/GoF sem abstrações genéricas ou microserviços; novas dependências/integrações relevantes pedem ADR.

Validação atual: 100% do escopo ponderado da alpha, fluxo hermético e fluxo com pré-requisitos reais. Há 249 testes verdes. O instalador `0.2.0-beta.1` foi aplicado por usuário, preservou configuração e histórico, criou um atalho e startup `--background`; o Worker instalado terminou com código 0 e stderr vazio. O smoke do run `31068430123` também concluiu em runner Windows limpo com instalação, primeiro uso, instância única, Worker, desinstalação e logs verdes. Os E2Es reais provaram inicialização automática do OBS e Docker, MP4 com áudio AAC, transcrição local reconhecível, `ata.md` e reunião arquivada.

Captura universal: a cena OBS `Anamnesis` é criada e reutilizada com `wasapi_output_capture` e `wasapi_input_capture` nos dispositivos padrão. Assim, captura o som do Windows e o microfone sem integração específica com Teams, Meet, Zoom ou navegador. A cena anterior é restaurada após a gravação.

Desktop: as SPEKs 027 a 031 e 045 entregaram uma janela WinForms nativa com o Design System `Command Deck`, identidade visual aplicada ao EXE, instalador, janela e bandeja, além de primeiro uso e diagnóstico acionável. O modo normal consulta reuniões, jobs e os 500 eventos operacionais mais recentes nos SQLite locais, inicia e encerra a gravação pelos casos de uso, acompanha o Worker por polling de dois segundos e abre artefatos por caminhos persistidos. O journal isolado preserva 14 códigos correlacionados por 14 dias e nunca armazena mensagem livre de exceção, áudio, transcrição, ata, prompt, segredo ou caminho pessoal. Concorrência, cancelamento, reinício e bancos legados não deixam uma gravação órfã bloqueando o produto. A SPEK-046 limita a espera oculta pela exclusividade a cinco minutos, preserva a fila no timeout e registra o diagnóstico no journal. O modo simulado permanece isolado em `--poc-desktop`.

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
