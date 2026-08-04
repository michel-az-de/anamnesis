---
title: Resumo para Próxima Conversa
aliases: [Resumo para Próxima Conversa, Handoff, Contexto do Projeto]
tags: [projeto/anamnesis, handoff]
type: project
created: 2026-08-04
updated: 2026-08-04
status: growing
summary: Contexto compacto e verificável para retomar o desenvolvimento do Anamnesis em outra conversa.
related: ["[[Anamnesis Home]]", "[[Status Alpha]]", "[[Indice de SPEKs]]", "[[Protocolo de Agentes]]"]
---

# Handoff — Anamnesis

Copie a seção abaixo para iniciar outra conversa:

```text
Estamos desenvolvendo o Anamnesis, aplicativo Windows local e open source para gravar reuniões, transcrever localmente, gerar atas estruturadas por LLM e aplicar retenção segura das gravações. Repositório público: https://github.com/michel-az-de/anamnesis (conta michel-az-de, branch main).

Objetivo da alpha: Tray inicia uma gravação de teste via OBS, persiste a reunião/job no SQLite, Worker retoma após reinício, Whisper local produz transcrição, uma CLI de LLM já autenticada gera ata.md estruturada, arquivos são arquivados e a retenção envia gravações para a Lixeira somente após sucesso.

Tecnologia e decisões: .NET 10, Windows 10/11 x64, SQLite local, OBS, Whisper local. Domínio em PT-BR com sufixos técnicos em inglês (ex.: ReuniaoRepository, AtaRunner). Não usar APIs pagas: modelos entram por adaptadores de CLI autenticados por assinatura (Codex, Claude, Kimi, Ollama), sem automatizar interfaces web. A LLM retorna dados estruturados e nunca decide exclusão/estado de reunião. SOLID/KISS/DRY/GoF sem abstrações genéricas ou microserviços; novas dependências/integrações relevantes pedem ADR.

Estado atual: 27% do escopo ponderado da alpha; 0% do fluxo ponta a ponta testável. A fundação, SPEKs/ADRs, TDD inicial, ciclo de estados do domínio e SqliteJobQueue estão prontos. Há 7 testes verdes (2 Domain, 1 Application, 4 Infrastructure). Projetos Tray e Worker são apenas scaffolds. Commit remoto atual: edc5bc2.

Código já existente: Reuniao e transições em src/Anamnesis.Domain; ProcessarReuniaoHandler e contratos em src/Anamnesis.Application; DiscoArquivador e SqliteJobQueue em src/Anamnesis.Infrastructure. SqliteJobQueue tem reserva atômica, liberação, conclusão e unicidade de job ativo. Não há SqliteReuniaoRepository, Worker real, OBS, Whisper, AtaRunner CLI, política de retenção, UI Tray funcional ou instalador.

Próximo incremento: criar SPEK-005 para persistência de reunião em SQLite; depois escrever testes falhando e implementar SqliteReuniaoRepository. Isso desbloqueia o Worker e protege estado/caminho de áudio entre reinicializações.

Regras obrigatórias: antes de código, ler AGENTS.md, 30-especificacoes/00-indice.md e a SPEK alvo; se não existir SPEK, criar/aprovar antes do código; seguir Red → Green → Refactor; testes unitários não chamam OBS, rede ou CLI real. Um agente por SPEK. Atualizar SPEK e painel de alpha quando o roadmap mudar. No encerramento da resposta, usar exatamente: Feito (uma frase); Evolução estimada (percentual e base); Falta (lacunas concretas); Próximo passo sugerido (um incremento/SPEK).

Documentação canônica: 30-especificacoes/; .speks/ é somente manifesto para agentes; 10-painel/Status Alpha.md contém pesos, critérios e caminho crítico; 30-especificacoes/05-templates/Template de Entrega.md define o fechamento padrão. Abra 00-home.md no Obsidian.

Para validar: dotnet test Anamnesis.sln --configuration Release --no-restore --verbosity minimal
```

## Referências de retomada

- [[Status Alpha]]
- [[Indice de SPEKs]]
- [[Protocolo de Agentes]]
- [[Template de Entrega]]
