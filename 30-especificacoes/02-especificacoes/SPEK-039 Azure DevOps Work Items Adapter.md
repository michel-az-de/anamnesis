---
title: SPEK-039 Azure DevOps Work Items Adapter
aliases: [SPEK-039, Azure DevOps Tasks, Azure Boards]
tags: [especificacao, azure-devops, work-items, entra, pos-alpha]
type: spek
created: 2026-08-05
updated: 2026-08-05
status: draft
summary: Publica tarefas aprovadas como Work Items no Azure DevOps Services usando Entra ID e controle de revisao.
related: ["[[SPEK-037 Mapeamento de Tarefas Externas]]", "[[Roadmap de Produto]]"]
---

# SPEK-039 Azure DevOps Work Items Adapter

## Objetivo

Implementar o provider Azure DevOps Services da SPEK-037 para escolher organizacao, projeto e tipo de Work Item, criar itens e atualizar campos controlados pelo Anamnesis sem sobrescrever alteracoes concorrentes.

## Fora de escopo

- Azure DevOps Server local.
- PAT como autenticacao padrao.
- OAuth legado do Azure DevOps.
- Criar processos, projetos, campos personalizados ou permissoes.
- Mudar estado, concluir ou excluir Work Items automaticamente.
- Sincronizacao bidirecional, comentarios, anexos ou Service Hooks.

## Regras

- Autenticacao usa Microsoft Entra ID com MSAL, cliente publico desktop e login interativo no navegador do sistema.
- O registro Entra recebe somente `vso.work_write`; o MSAL solicita `499b84ac-1321-427f-aa17-267ca6975798/.default`.
- O primeiro corte aceita contas corporativas ou escolares. Contas Microsoft pessoais ficam fora ate suporte oficial completo.
- Organizacao, projeto e tipo de item sao informados pelo usuario e validados antes da confirmacao, evitando escopo adicional apenas para descoberta.
- Criacao usa REST 7.1, JSON Patch e `Content-Type: application/json-patch+json`.
- O primeiro corte mapeia titulo, descricao e tags. Responsavel, prazo e campos de processo ficam adiados ate existir descoberta com permissao minima e selecao explicita de identidade remota.
- Atualizacao envia operacao `test` em `/rev` antes de alterar campos para detectar concorrencia.
- Idempotencia depende primeiro da correlacao local; marcador `AnamnesisTaskId` entra na descricao.
- Estado `ResultadoDesconhecido` consulta o marcador por WIQL antes de qualquer nova criacao; ausencia de resultado deterministico exige decisao humana.
- Campo `Custom.AnamnesisTaskId` e opcional, pois exige processo herdado e permissao administrativa.
- Respostas 429 respeitam `Retry-After` e headers `X-RateLimit-*`.
- Requisicoes aceitam somente hosts oficiais configurados, como `dev.azure.com`; redirects nao recebem o bearer token automaticamente.
- O adapter nao usa estado `Done`, `Closed` ou equivalente sem comando humano futuro explicitamente especificado.
- Conta ou host nao suportado recebe diagnostico claro, sem fallback para OAuth legado ou PAT.

## Critérios de aceite

- [ ] Login Entra funciona como cliente publico e nao usa client secret ou PAT local.
- [ ] Registro e token usam apenas `vso.work_write` pelo recurso `.default` documentado.
- [ ] Usuario escolhe organizacao, projeto e tipo antes de publicar.
- [ ] Tarefa aprovada gera um unico Work Item e persiste seu ID antes de concluir o job.
- [ ] Retentativa nao duplica item depois de timeout conhecido.
- [ ] Timeout ou crash inconclusivo nao executa um segundo POST automaticamente.
- [ ] Revisao remota divergente impede update silencioso e pede nova revisao humana.
- [ ] Campos ausentes no processo sao ignorados ou apresentados como configuracao, nunca inventados.
- [ ] `AssignedTo` nunca recebe nome livre vindo da LLM.
- [ ] Redirect para host diferente e bloqueado sem encaminhar token.
- [ ] 429, token revogado e conta nao suportada produzem estados recuperaveis.
- [ ] Testes usam HTTP e MSAL fakes, sem acessar Azure DevOps real.
- [ ] Testes cobrem token revogado durante escrita, resposta repetida, timeout apos sucesso remoto e reconciliacao com zero ou multiplos resultados.

## Referencias oficiais

- [Guia de autenticacao do Azure DevOps](https://learn.microsoft.com/en-us/azure/devops/integrate/get-started/authentication/authentication-guidance?view=azure-devops)
- [Microsoft Entra OAuth para Azure DevOps](https://learn.microsoft.com/en-us/azure/devops/integrate/get-started/authentication/entra?view=azure-devops)
- [Create Work Item](https://learn.microsoft.com/en-us/rest/api/azure/devops/wit/work-items/create?view=azure-devops-rest-7.1)
- [Update Work Item](https://learn.microsoft.com/en-us/rest/api/azure/devops/wit/work-items/update?view=azure-devops-rest-7.1)
- [Escopos OAuth](https://learn.microsoft.com/en-us/azure/devops/integrate/get-started/authentication/oauth?view=azure-devops)
- [Rate limits](https://learn.microsoft.com/en-us/azure/devops/integrate/concepts/rate-limits?view=azure-devops)

## Decisoes pendentes

- Aprovar o ADR da SPEK-037 e a dependencia MSAL antes do codigo.
- Validar se todas as operacoes do primeiro corte funcionam apenas com `vso.work_write`; qualquer escopo adicional exige nova aprovacao.
- Validar uma conta corporativa de teste e os campos disponiveis em processos Agile, Scrum e Basic.
- Revalidar as restricoes de contas pessoais e a remocao do OAuth legado no inicio do incremento.
