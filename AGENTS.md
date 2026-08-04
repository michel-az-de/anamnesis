# Protocolo de desenvolvimento — Anamnesis

## Fonte de verdade

O desenvolvimento é orientado pelas especificações em `30-especificacoes/`. A pasta `.speks/` é o manifesto oculto para ferramentas e agentes. Antes de alterar código:

1. Leia `30-especificacoes/00-indice.md` e a especificação aplicável.
2. Se a mudança não estiver especificada, atualize ou crie uma SPEK antes do código.
3. Escreva ou ajuste o teste que demonstra o comportamento esperado.
4. Implemente o mínimo necessário, execute os testes e atualize o status da SPEK.

## Regras de design

- Domínio em PT-BR; sufixos técnicos em inglês: `ReuniaoRepository`, `AtaRunner`, `ProcessarReuniaoHandler`.
- Interfaces somente nas fronteiras substituíveis: persistência, arquivos, OBS, transcrição e modelos.
- O modelo de IA retorna dados estruturados; ele não decide exclusão de arquivos ou estado da reunião.
- Não apagar gravações fora do caso de uso de retenção, nem sem teste de transição de estado.
- Não adicionar dependências, abstrações genéricas, filas externas ou serviços remotos sem ADR.

## Protocolo multi-LLM

- Um agente implementa uma SPEK por vez.
- Outro agente pode revisar, mas não deve alterar os mesmos arquivos em paralelo.
- Toda resposta de implementação deve citar SPEKs atendidas, testes executados e decisões pendentes.
- CLIs autenticados por assinatura são adaptadores; não automatize a interface web de provedores.

## Qualidade

- Siga Red → Green → Refactor.
- Testes unitários não chamam OBS, rede, CLI real ou modelos reais.
- Para cada bug, primeiro adicione um teste de regressão.
