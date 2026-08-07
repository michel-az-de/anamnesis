---
title: SPEK-051 Confiabilidade do Primeiro Uso Real
aliases: [Protecao contra Transcricao Invalida, Recuperacao do Primeiro Uso Real]
tags: [especificacao, incidente, audio, whisper, retencao]
type: spec
created: 2026-08-07
updated: 2026-08-07
status: completed
summary: Impede falso sucesso de transcricoes degeneradas, evita duplicacao de audio no OBS e amplia a preservacao da gravacao.
related: ["[[SPEK-003 Retencao de Gravacao]]", "[[SPEK-008 Transcricao Local com Whisper]]", "[[SPEK-024 Captura Universal de Audio pelo OBS]]"]
---

# SPEK-051 | Confiabilidade do primeiro uso real

## Contexto do incidente

No primeiro uso real em Google Meet, uma gravacao foi aceita e arquivada com 914 linhas identicas de marcador musical. Uma segunda parte preservou fala, mas repetiu uma mesma frase 358 vezes e falhou ao gerar a ata. O OBS mantinha as entradas globais de desktop e microfone e tambem as entradas gerenciadas pelo Anamnesis, somando o mesmo sinal e saturando o audio.

## Objetivo

Preservar a gravacao quando a transcricao nao for confiavel e impedir que uma captura duplicada degrade o audio antes do Whisper.

## Regras

- Marcadores exclusivamente nao verbais e repeticoes dominantes nao contam como transcricao valida.
- Linhas degeneradas isoladas podem ser removidas quando ainda existe conteudo suficiente e diverso.
- Uma transcricao sem conteudo confiavel deixa a reuniao em `Falha`, sem gerar ata, arquivar ou habilitar retencao.
- O Whisper nao reutiliza contexto entre janelas e suprime tokens nao verbais para reduzir propagacao de alucinacoes.
- O Whisper nao imprime a transcricao no console; o conteudo permanece somente no banco e nos artefatos autorizados.
- O adaptador da Codex CLI le somente o arquivo de mensagem final; eventos de progresso no `stdout` nao sao tratados como JSON da ata.
- Uma resposta de ata invalida recebe uma unica nova tentativa da CLI, sem repetir a transcricao local.
- Quando o OBS ja possui entradas globais especiais de desktop ou microfone, o Anamnesis remove somente a entrada duplicada que ele proprio gerencia e reutiliza a entrada global.
- A gravacao arquivada somente pode ser movida para a Lixeira depois de trinta dias e mediante a confirmacao operacional ja exigida.
- Reunioes em `Falha` continuam inelegiveis para retencao.

## Criterios de aceite

- [x] Regressao rejeita a transcricao composta somente por `[MUSICA DE FUNDO]`.
- [x] Regressao remove uma frase dominante repetida sem descartar as linhas confiaveis.
- [x] Falha de qualidade preserva o caminho da gravacao e nao chama a geracao de ata.
- [x] Comando nativo e Docker usam contexto zerado e supressao de tokens nao verbais.
- [x] Comando nativo e Docker suprimem a transcricao no console.
- [x] Codex CLI com progresso no `stdout` gera ata a partir do arquivo de mensagem final.
- [x] Resposta de ata invalida e repetida uma vez antes de marcar a reuniao como falha.
- [x] OBS nao mantem fontes gerenciadas duplicadas quando entradas especiais globais estao ativas.
- [x] Retencao rejeita 29 dias e somente aceita 30 dias completos.
- [x] Suite Release permanece verde.

## Evidencias do incidente

- Gravacoes originais preservadas em `C:\Users\felip\Videos`.
- Copia de recuperacao em `C:\Users\felip\AppData\Local\Anamnesis\recuperacao-incidente-2026-08-07`.
- Banco do incidente copiado pela API de backup do SQLite, sem alterar o banco operacional.
- Reuniao `0e758cde51b045d7bddd3933d3aac45d` recuperada com transcricao, nove decisoes e seis tarefas no arquivo operacional.
- Os registros de 07:45 e 08:49 foram confirmados como partes contiguas da mesma sessao, separados por 21,8 segundos. O conteudo recuperado foi consolidado no registro de 07:45 exibido ao usuario, com rotulo explicito de recuperacao parcial e backup reversivel.
- Ensaio OBS real isolado gerou MP4 de 4,9 segundos com audio AAC e removeu somente as fontes gerenciadas duplicadas.
- Suite Release concluiu 294 testes: 3 de dominio, 56 de aplicacao e 235 de infraestrutura.
- Pacote local `0.2.0-beta.7-local` instalado com 489 arquivos verificados sem divergencias; Worker encerrou com codigo 0 e Tray permaneceu ativo.

## Fora de escopo

- Apagar ou substituir as gravacoes do incidente.
- Publicar release ou atualizar a instalacao sem validacao automatizada e ensaio local.
- Garantir diarizacao ou precisao humana com o modelo Whisper `base`.
