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

O escopo ponderado da alpha e os fluxos E2E hermético e real estão em **100%**. A suíte Release possui **281 testes verdes**. A versão canônica declarada em [`release/versao.json`](release/versao.json) possui Desktop real, ícone próprio, menu na bandeja, instância única, Worker interno e instalador por usuário validado por contratos e preparado para o smoke isolado. Consulte o [painel de alpha](10-painel/Status%20Alpha.md) para evidências e limitações atuais.

## Instalar no Windows

O instalador é autocontido para o Anamnesis, solicita elevação UAC e mantém os dados do produto no perfil do usuário. Ele apresenta termos simples de uso, identifica se deve instalar, atualizar ou reparar os binários e preserva configuração, banco e reuniões. Se o Anamnesis estiver aberto, o instalador pede um encerramento seguro; ele não encerra à força uma gravação ativa nem um processamento em andamento. Após uma instalação interativa, a opção de abrir o Anamnesis fica marcada.

OBS, Docker Desktop, FFmpeg, modelo Whisper e uma CLI autenticada continuam dependências locais separadas e aparecem na tela de Configurações como `PRONTO` ou `PENDENTE`.

Para construir o instalador canônico localmente:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Build-Installer.ps1
```

Depois, execute `artifacts\releases\<versao>\installer\Anamnesis-<versao>-win-x64-setup.exe`. O instalador cria somente o atalho **Anamnesis**; o Worker permanece interno. Fechar a janela mantém o aplicativo na bandeja e **Sair** encerra o processo.

## Release do instalador

A versão canônica está em [`release/versao.json`](release/versao.json). Cada pacote produz `SHA256SUMS.txt` e `release.json`; binários não entram no Git. O GitHub executa o smoke com uma release anterior real e promove assets imutáveis somente por tag. Consulte o [runbook de release](docs/release.md) para gerar, verificar e publicar.

## Validar

```powershell
dotnet test Anamnesis.sln --configuration Release --no-restore --verbosity minimal
```

## Requisitos de build

- .NET 10 SDK
- Windows 10/11 x64
- Inno Setup 6 apenas para construir o instalador

O projeto usa .NET 10 LTS para manter suporte até novembro de 2028.

## Licença

Distribuído sob a [licença MIT](LICENSE).
