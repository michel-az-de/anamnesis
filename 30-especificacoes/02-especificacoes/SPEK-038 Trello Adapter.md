---
title: SPEK-038 Trello Adapter
aliases: [SPEK-038, Trello]
tags: [especificacao, trello, tarefas, oauth, pos-alpha]
type: spek
created: 2026-08-05
updated: 2026-08-05
status: draft
summary: Publica tarefas aprovadas como cards Trello com correlacao local, limites respeitados e escopo minimo.
related: ["[[SPEK-037 Mapeamento de Tarefas Externas]]", "[[Roadmap de Produto]]"]
---

# SPEK-038 Trello Adapter

## Objetivo

Implementar o provider Trello da SPEK-037 para selecionar quadro e lista, criar cards e atualizar somente os campos publicados pelo Anamnesis.

## Fora de escopo

- Criar quadros, listas, membros ou automacoes.
- Fechar, arquivar ou excluir cards automaticamente.
- Ler todos os comentarios ou anexos.
- Sincronizacao bidirecional ou webhooks.
- Solicitar acesso `account` ou armazenar senha do usuario.

## Regras

- Autenticacao usa fluxo oficial delegado `1/authorize` com escopos `read,write`, sem `account`, origem exata cadastrada e expiracao padrao de 30 dias.
- O token fica protegido pelo usuario Windows e pode ser revogado ou desconectado.
- A tela informa que o token alcanca a conta Trello inteira dentro dos escopos concedidos, nao apenas o quadro selecionado.
- O retorno por fragmento usa pagina loopback de uso unico e nonce de alta entropia. Fragmento, query string e headers de autenticacao nunca entram em log ou tracing e a URL e limpa imediatamente.
- A configuracao escolhe um quadro e uma lista validos; o adapter nao assume isolamento da credencial apenas por essa selecao.
- Criacao usa `POST /1/cards` com `idList`, nome, descricao e prazo quando disponivel.
- Atualizacao usa `PUT /1/cards/{id}` somente para campos declarados como propriedade do Anamnesis.
- `TarefaLocalId` entra como marcador discreto na descricao desde a primeira criacao.
- Estado `ResultadoDesconhecido` procura o marcador no quadro e lista configurados antes de qualquer nova criacao; resultado inconclusivo exige escolha humana.
- Custom Field e opcional, pois depende de recurso e configuracao do quadro.
- Limites e respostas 429 consideram API key compartilhada e token, usam headers oficiais, `Retry-After` quando houver e backoff com jitter.
- Limites estruturais de quadro sao lidos do campo `limits`, nunca codificados.
- O adapter nunca move card para concluido nem arquiva sem uma SPEK futura e nova confirmacao.

## Critérios de aceite

- [ ] Conectar solicita somente `read,write` e nao grava token em texto puro.
- [ ] Origem diferente, callback repetido, nonce incorreto ou fragmento ausente sao rejeitados sem logar o token.
- [ ] Quadro e lista podem ser escolhidos e validados antes da publicacao.
- [ ] Tarefa aprovada gera um unico card correlacionado.
- [ ] Repeticao retorna o mesmo card ou o recupera pelo marcador antes de criar.
- [ ] Timeout ou crash inconclusivo nao executa um segundo `POST /1/cards` automaticamente.
- [ ] Mudanca aprovada atualiza apenas os campos pertencentes ao Anamnesis.
- [ ] 429 e limite estrutural produzem retentativa recuperavel e mensagem clara.
- [ ] Token revogado leva a reconexao sem perder a correlacao local.
- [ ] Testes usam HTTP fake e nunca acessam Trello real.
- [ ] Testes cobrem token revogado durante escrita, resposta repetida, timeout apos sucesso remoto e reconciliacao com zero ou multiplos resultados.

## Referencias oficiais

- [Cards REST API](https://developer.atlassian.com/cloud/trello/rest/api-group-cards/)
- [Autorizacao Trello](https://developer.atlassian.com/cloud/trello/guides/rest-api/authorization/)
- [Rate limits](https://developer.atlassian.com/cloud/trello/guides/rest-api/rate-limits/)
- [Limites de objetos](https://developer.atlassian.com/cloud/trello/guides/rest-api/limits/)
- [Custom Fields](https://developer.atlassian.com/cloud/trello/guides/rest-api/getting-started-with-custom-fields/)

## Decisoes pendentes

- Executar POC de autenticacao antes da implementacao, pois Cards nao usa Forge nem OAuth 2.0 moderno nos endpoints oficiais atuais.
- Registrar em ADR como distribuir a API key e receber o retorno do fluxo desktop.
- Manter a SPEK bloqueada se a POC nao provar captura local segura do fragmento sem servidor remoto e sem origem wildcard.
- Revalidar autenticacao e limites oficiais no inicio do incremento.
