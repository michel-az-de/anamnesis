---
title: SPEK-036 Publicacao Local no Obsidian
aliases: [SPEK-036, Obsidian Publisher]
tags: [especificacao, obsidian, markdown, integracao-local, pos-alpha]
type: spek
created: 2026-08-05
updated: 2026-08-05
status: draft
summary: Publica uma ata arquivada como Markdown novo em um vault Obsidian, sem plugin, rede ou sobrescrita.
related: ["[[SPEK-019 Ata Markdown Estruturada]]", "[[SPEK-030 Desktop com Dados Reais]]", "[[Roadmap de Produto]]"]
---

# SPEK-036 Publicacao local no Obsidian

## Objetivo

Permitir que o usuario publique atas concluidas em um vault Obsidian como arquivos Markdown comuns, preservando privacidade, idempotencia e edicoes manuais.

## Fora de escopo

- Instalar ou exigir plugin do Obsidian.
- Exigir Obsidian aberto durante a publicacao.
- Sincronizar alteracoes do vault de volta ao Anamnesis.
- Atualizar, anexar ou sobrescrever nota ja publicada.
- Copiar audio para o vault.
- Usar rede, Obsidian Sync ou CLI no primeiro corte.

## Regras

- A integracao e opt-in e o usuario escolhe vault e subpasta.
- O caminho selecionado precisa conter `.obsidian`, ser resolvido de forma canonica e permanecer dentro do vault.
- Nenhum arquivo pode ser criado dentro de `.obsidian`.
- Somente reuniao com ata arquivada e arquivo `ata.md` existente pode ser publicada.
- Uma reuniao produz no maximo uma nota por vault usando `ReuniaoId` como chave idempotente.
- A nota e criada em `Anamnesis/Reunioes/AAAA/MM/` ou subpasta configurada.
- O nome usa data, titulo sanitizado e identificador curto, sem depender apenas do titulo.
- A escrita ocorre em arquivo temporario no mesmo volume e termina por movimento atomico.
- Arquivo existente nunca e sobrescrito. Reexecucao retorna a nota ja correlacionada.
- Propriedades YAML incluem `anamnesis_id`, data, status, origem e tags estaveis.
- Tarefas usam checkbox Markdown, sem transformar o Obsidian em fonte de verdade.
- O botao abrir usa `obsidian://open` apenas depois da criacao local e nao transporta o conteudo da ata na URI.
- A URI contem somente vault e caminho codificados, sem callback externo, conteudo da nota ou argumento derivado diretamente da ata.
- Falha de publicacao nao muda o estado da reuniao e nao interfere na retencao.
- A tela avisa quando o vault estiver em OneDrive, Dropbox ou outro caminho possivelmente sincronizado.
- Vault possivelmente sincronizado exige confirmacao explicita e informa que a copia nao sera removida pela retencao do Anamnesis.
- Cada segmento do destino e verificado contra reparse points antes da escrita e novamente antes do movimento final.
- Propriedades YAML usam serializacao com escaping; embeds HTTP, HTML ativo e conteudo que possa carregar recurso remoto sao removidos ou exigem revisao.

## Fluxo

```mermaid
flowchart LR
    A["Ata arquivada"] --> P["ObsidianPublisher"]
    P --> V["Validar vault e destino"]
    V --> T["Escrever temporario"]
    T --> M["Movimento atomico para novo Markdown"]
    M --> O["Abrir por URI opcional"]
```

## Critérios de aceite

- [ ] O usuario seleciona um vault valido e uma subpasta segura.
- [ ] Uma ata concluida gera Markdown com propriedades, resumo, decisoes e tarefas.
- [ ] Repetir a publicacao nao duplica nem sobrescreve a nota.
- [ ] Caminho com traversal, link simbolico para fora ou destino `.obsidian` e rejeitado.
- [ ] Reparse point criado entre validacao e movimento final interrompe a publicacao.
- [ ] Falha no meio da escrita nao deixa nota parcial com o nome final.
- [ ] Edicao manual posterior permanece intacta.
- [ ] Abrir no Obsidian e opcional e nao inclui a ata nos argumentos do processo.
- [ ] Reuniao, job e politica de retencao permanecem inalterados.
- [ ] Vault sincronizado exige consentimento e a tela explica que a nota publicada tem ciclo de vida independente.
- [ ] Testes usam diretorio temporario e nunca iniciam o Obsidian real.
- [ ] Teste arquitetural prova que o publisher nao recebe interface de retencao nem caminho da gravacao.

## Referencias oficiais

- [Como o Obsidian armazena dados](https://obsidian.md/help/data-storage)
- [Properties em YAML](https://obsidian.md/help/properties)
- [Obsidian Flavored Markdown](https://obsidian.md/help/Editing%2Band%2Bformatting/Obsidian%2BFlavored%2BMarkdown)
- [Obsidian URI](https://obsidian.md/help/Extending%2BObsidian/Obsidian%2BURI)
- [Obsidian CLI](https://obsidian.md/help/cli)

## Decisoes pendentes

- Aprovar ADR de publicacao Markdown direta antes do codigo.
- Definir o template inicial e como representar identificadores de tarefas sem poluir a leitura.
- Manter CLI oficial e plugin como alternativas futuras somente se houver sincronizacao bidirecional.
