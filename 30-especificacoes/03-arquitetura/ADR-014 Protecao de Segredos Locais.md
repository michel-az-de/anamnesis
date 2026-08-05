---
title: ADR-014 Protecao de Segredos Locais
aliases: [DPAPI na Configuracao, Senha do OBS Protegida]
tags: [adr, seguranca, configuracao, windows, dpapi]
type: adr
created: 2026-08-05
updated: 2026-08-05
status: accepted
summary: Segredos da configuracao local sao protegidos por DPAPI no escopo do usuario, com migracao transparente do formato legado.
related: ["[[SPEK-044 Configuracao Local Protegida]]", "[[SPEK-010 Tray Configuracao e Diagnosticos]]", "[[SPEK-025 Prontidao Automatica do OBS]]"]
---

# ADR-014 | Protecao de segredos locais

## Contexto

A senha do servidor WebSocket do OBS ficava em texto claro em `%LOCALAPPDATA%\Anamnesis\config.json`. O arquivo e legivel por qualquer processo do mesmo usuario, e o menu do Tray abre esse arquivo no Bloco de Notas, o que expoe a senha em tela.

O risco absoluto e baixo, porque a senha protege um servidor que so escuta em `127.0.0.1`. Mas o produto vai receber credenciais de agenda e de ferramentas externas nas SPEKs 033 a 039, e o registro de decisoes ja tem um gate aberto para "credenciais protegidas". Definir agora o mecanismo evita que cada integracao invente o seu.

## Decisao

Proteger o campo com DPAPI no escopo `CurrentUser`, atraves de `System.Security.Cryptography.ProtectedData`. O valor protegido e gravado em base64 com o prefixo `dpapi:`.

O prefixo e o que torna a migracao transparente: um valor sem ele e tratado como texto claro legado, carrega normalmente e e reescrito protegido no salvamento seguinte. Nenhuma acao do usuario e necessaria, e nenhuma configuracao existente quebra.

Falha ao desproteger vira `InvalidDataException` com instrucao acionavel, porque a causa real e sempre a mesma: o arquivo veio de outro usuario ou de outra maquina.

O assembly de Infrastructure passa a declarar `SupportedOSPlatform("windows")`. Isso nao restringe nada de novo: a camada ja dependia de shell32, do explorer.exe, do OBS e do Docker Desktop. A declaracao apenas torna explicito o que era implicito, e dispensa anotar cada ponto de uso.

## Alternativas consideradas

| Alternativa | Probabilidade de adequacao | Motivo |
| --- | ---: | --- |
| DPAPI por usuario, com prefixo e migracao | 85% | Proporcional ao risco, sem novo fluxo para o usuario e sem quebrar configuracao existente. |
| Windows Credential Manager | 60% | Armazenamento mais adequado para credenciais, mas separa o segredo do arquivo de configuracao e complica backup, diagnostico e o E2E de caixa preta. Vale reavaliar quando entrarem tokens de OAuth. |
| P/Invoke direto a crypt32 | 45% | Evitaria o pacote novo, seguindo o precedente do shell32 em `LixeiraWindows`, mas reimplementa a mao o que a BCL ja oferece testado. |
| Manter em texto claro | 20% | Aceitavel para um websocket local, mas deixaria o produto sem mecanismo definido justamente antes das integracoes que trarao credenciais reais. |

## Consequencias

- Um `config.json` copiado para outra maquina ou outro usuario deixa de funcionar para o campo de senha, e a mensagem de erro instrui a preencher de novo. E o comportamento desejado de DPAPI por usuario.
- Nova dependencia: `System.Security.Cryptography.ProtectedData` 10.0.10, alinhada a versao das demais bibliotecas do projeto.
- O mecanismo fica pronto para as credenciais das SPEKs 033 a 039, que devem reutilizar `SegredoLocal` em vez de criar o seu proprio.
- O campo continua visivel no arquivo, agora ilegivel: abrir a configuracao no Bloco de Notas nao expoe mais a senha.
- A protecao cobre apenas o campo de senha. Caminhos e demais preferencias seguem em texto claro, porque nao sao segredos.
