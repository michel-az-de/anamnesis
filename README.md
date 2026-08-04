# Anamnesis

> O que foi dito, lembrado com clareza.

Aplicativo Windows local para capturar reuniões, transcrevê-las, gerar atas estruturadas e aplicar retenção segura às gravações.

## Princípios

- Domínio em PT-BR; sufixos técnicos em inglês (`ReuniaoRepository`, `AtaRunner`, `ProcessarReuniaoHandler`).
- TDD: regras de domínio e casos de uso nascem com testes.
- Integrações trocáveis por contratos pequenos: OBS, transcrição, modelos de IA, arquivos e persistência.
- Nenhuma gravação é apagada enquanto a ata não estiver arquivada e validada.
- Sem microserviços, filas externas ou abstrações genéricas prematuras.

## Estrutura

```text
src/Anamnesis.Domain          regras e entidades puras
src/Anamnesis.Application     casos de uso e contratos
src/Anamnesis.Infrastructure  adaptadores de disco, OBS, CLI e SQLite
src/Anamnesis.Tray            agente na sessão do usuário
src/Anamnesis.Worker          consumidor de jobs em segundo plano
tests/                        testes de domínio e aplicação
30-especificacoes/            cofre Obsidian e SPEKs canônicas
.speks/                       manifesto para descoberta por agentes
```

Abra esta pasta no Obsidian e comece por `00-home.md`.

## Status da alpha

O roadmap ponderado está em **27%**. O fluxo completo ainda não é testável: a parte de gravação, transcrição, geração da ata e retenção segura será entregue nas próximas SPEKs. Consulte o [painel de alpha](10-painel/Status%20Alpha.md) para ver pesos, evidências e caminho crítico.

## Primeiro ciclo de desenvolvimento

1. Implementar `ObsGravador` e o detector de reunião no `Tray`.
2. Persistir jobs no SQLite com `SqliteReuniaoRepository`.
3. Implementar `WhisperTranscritor` com Whisper.net.
4. Adicionar `CodexCliAtaRunner`, `ClaudeCliAtaRunner`, `KimiCliAtaRunner` e `OllamaAtaRunner`.
5. Empacotar como aplicativo self-contained `win-x64`, com instalador e assistente inicial.

## Requisitos de build

- .NET 10 SDK
- Windows 10/11 x64

O projeto usa .NET 10 LTS para manter suporte até novembro de 2028.

## Licença

Distribuído sob a [licença MIT](LICENSE).
