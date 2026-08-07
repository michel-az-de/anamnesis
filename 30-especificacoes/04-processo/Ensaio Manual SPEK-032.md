---
title: Ensaio Manual SPEK-032
aliases: [Ensaio Detector Local, Teste Meet Teams Zoom]
tags: [ensaio, spek-032, deteccao, windows, manual]
type: processo
created: 2026-08-07
updated: 2026-08-07
status: draft
summary: Roteiro passo a passo para validar a deteccao local em chamadas reais do Meet, Teams e Zoom, incluindo cenarios de falso positivo.
related: ["[[SPEK-032 Captura Instantanea e Deteccao Local]]", "[[ADR-016 Deteccao Local por Core Audio e User32]]", "[[Status Alpha]]"]
---

# Ensaio Manual — SPEK-032 Captura Instantanea e Deteccao Local

> Objetivo: confirmar que o detector local identifica chamadas reais do Meet (navegador), Teams (nativo) e Zoom (nativo), sem disparar em falsos positivos como YouTube, musica, ditado, mute ou duas plataformas simultaneas.

## Pre-requisitos

- [ ] Windows 10 ou 11 atualizado
- [ ] Microfone funcional (headset, webcam ou built-in)
- [ ] Chrome ou Edge instalado
- [ ] Executavel `Anamnesis.Tray.exe` da publicacao SPEK-032-v3
- [ ] Opcional: Teams instalado (Microsoft Store ou standalone)
- [ ] Opcional: Zoom instalado
- [ ] Uma segunda pessoa ou outro dispositivo para receber a chamada (pode ser voce mesmo em outro aparelho)

> Nao e necessario conta no Anamnesis, autenticacao de LLM, OBS, Docker ou configuracao previa. O ensaio usa apenas o modo diagnostico, que le sinais locais e grava JSONL seguro.

---

## Preparacao

### 1. Escolha uma pasta de trabalho

```powershell
$ensaio = "C:\temp\ensaio-spek-032"
New-Item -ItemType Directory -Path $ensaio -Force | Out-Null
Set-Location $ensaio
```

### 2. Copie o executavel

```powershell
$exe = "C:\rep\Anamnesis\artifacts\publish\SPEK-032-v3\Anamnesis.Tray.exe"
Copy-Item $exe .
```

> Se o caminho acima nao existir, localize o executavel da SPEK-032-v3 e ajuste a variavel `$exe`.

### 3. Crie a pasta de saida

```powershell
$saida = "$ensaio\resultados"
New-Item -ItemType Directory -Path $saida -Force | Out-Null
```

---

## Cenario 1 — Google Meet no Chrome/Edge (deteccao esperada: SIM)

### Passos

1. Abra o Chrome ou Edge e va para [meet.google.com](https://meet.google.com).
2. Inicie uma reuniao instantanea (botao "Nova reuniao" > "Iniciar uma reuniao instantanea").
3. **Permita o acesso ao microfone** quando o navegador pedir.
4. Confirme que voce esta na chamada com o microfone ativo (icone de microfone nao esta riscado).
5. Em outra janela do terminal, execute o diagnostico:

```powershell
$jsonl = "$saida\cenario-01-meet.jsonl"
if (Test-Path $jsonl) { Remove-Item $jsonl }

Start-Process `
  -FilePath '.
Anamnesis.Tray.exe' `
  -ArgumentList '--diagnostico-deteccao','--amostras','10','--intervalo-ms','1000','--saida',$jsonl `
  -Wait -NoNewWindow
```

6. Aguarde o termino (cerca de 10 segundos).
7. Encerre a reuniao do Meet.

### O que observar

```powershell
Get-Content $jsonl | ConvertFrom-Json | Format-Table horario, coletaConfiavel, microfoneAtivo, sessaoCapturaEncontrada, plataformaDetectada
```

**Esperado:**
- `coletaConfiavel`: `true` em todas as amostras
- `microfoneAtivo`: `true` a partir da 2a ou 3a amostra (pode levar 1-2 s para o Windows expor a sessao)
- `sessaoCapturaEncontrada`: `true` (indica que o navegador esta usando o microfone)
- `plataformaDetectada`: `meet` (ou `google-meet`, dependendo da normalizacao)
- Nenhum campo contem titulo bruto da janela, PID ou nome do dispositivo

---

## Cenario 2 — Microsoft Teams nativo (deteccao esperada: SIM)

### Passos

1. Abra o aplicativo Teams (nao a versao web).
2. Inicie uma chamada de teste ou reuniao. Se estiver sozinho, use o "Meet now" ou ligue para o "Test Call Bot" se disponivel.
3. **Ligue o microfone** na chamada.
4. Execute o diagnostico:

```powershell
$jsonl = "$saida\cenario-02-teams.jsonl"
if (Test-Path $jsonl) { Remove-Item $jsonl }

Start-Process `
  -FilePath '.
Anamnesis.Tray.exe' `
  -ArgumentList '--diagnostico-deteccao','--amostras','10','--intervalo-ms','1000','--saida',$jsonl `
  -Wait -NoNewWindow
```

5. Encerre a chamada.

### O que observar

**Esperado:**
- `coletaConfiavel`: `true`
- `microfoneAtivo`: `true`
- `sessaoCapturaEncontrada`: `true`
- `plataformaDetectada`: `teams` ou `ms-teams`

---

## Cenario 3 — Zoom nativo (deteccao esperada: SIM)

### Passos

1. Abra o aplicativo Zoom.
2. Inicie uma nova reuniao (botao "Nova Reuniao").
3. **Ligue o audio por computador** e certifique-se de que o microfone esta ativo.
4. Execute o diagnostico:

```powershell
$jsonl = "$saida\cenario-03-zoom.jsonl"
if (Test-Path $jsonl) { Remove-Item $jsonl }

Start-Process `
  -FilePath '.
Anamnesis.Tray.exe' `
  -ArgumentList '--diagnostico-deteccao','--amostras','10','--intervalo-ms','1000','--saida',$jsonl `
  -Wait -NoNewWindow
```

5. Encerre a reuniao.

### O que observar

**Esperado:**
- `coletaConfiavel`: `true`
- `microfoneAtivo`: `true`
- `sessaoCapturaEncontrada`: `true`
- `plataformaDetectada`: `zoom`

---

## Cenario 4 — YouTube ou musica (falso positivo esperado: NAO)

### Passos

1. **Feche todas as chamadas** (Meet, Teams, Zoom).
2. Abra o YouTube em qualquer navegador e reproduza um video com audio.
3. **Nao use o microfone**.
4. Execute o diagnostico:

```powershell
$jsonl = "$saida\cenario-04-youtube.jsonl"
if (Test-Path $jsonl) { Remove-Item $jsonl }

Start-Process `
  -FilePath '.
Anamnesis.Tray.exe' `
  -ArgumentList '--diagnostico-deteccao','--amostras','10','--intervalo-ms','1000','--saida',$jsonl `
  -Wait -NoNewWindow
```

5. Pare o video.

### O que observar

**Esperado:**
- `coletaConfiavel`: `true`
- `microfoneAtivo`: `false` (sessao de captura ausente)
- `sessaoCapturaEncontrada`: `false`
- `plataformaDetectada`: `null` ou vazio
- Renderizacao de audio (video do YouTube) pode aparecer, mas **nao deve** contar como sinal de chamada

---

## Cenario 5 — Ditado por voz / Reconhecimento de fala do Windows (falso positivo esperado: NAO)

### Passos

1. **Feche todas as chamadas e videos**.
2. Ative o ditado do Windows: pressione `Win + H` em qualquer campo de texto.
3. Fale algumas palavras e confirme que o microfone esta ativo para o ditado.
4. Execute o diagnostico:

```powershell
$jsonl = "$saida\cenario-05-ditado.jsonl"
if (Test-Path $jsonl) { Remove-Item $jsonl }

Start-Process `
  -FilePath '.
Anamnesis.Tray.exe' `
  -ArgumentList '--diagnostico-deteccao','--amostras','10','--intervalo-ms','1000','--saida',$jsonl `
  -Wait -NoNewWindow
```

5. Feche o ditado.

### O que observar

**Esperado:**
- `coletaConfiavel`: `true`
- `microfoneAtivo`: `true` (o ditado usa captura)
- `sessaoCapturaEncontrada`: `true` (o processo de ditado aparece)
- `plataformaDetectada`: `null` ou vazio (o processo de ditado **nao esta** na allowlist)
- O detector deve reconhecer que ha captura, mas nao deve identificar uma plataforma conhecida

---

## Cenario 6 — Meet com microfone mutado (deteccao esperada: NAO, ou apenas assistido)

### Passos

1. Abra um Meet no navegador e entre em uma reuniao.
2. **Mute o microfone** no proprio Meet (icone riscado).
3. Execute o diagnostico:

```powershell
$jsonl = "$saida\cenario-06-meet-mute.jsonl"
if (Test-Path $jsonl) { Remove-Item $jsonl }

Start-Process `
  -FilePath '.
Anamnesis.Tray.exe' `
  -ArgumentList '--diagnostico-deteccao','--amostras','10','--intervalo-ms','1000','--saida',$jsonl `
  -Wait -NoNewWindow
```

4. Desmute e encerre.

### O que observar

**Esperado:**
- `coletaConfiavel`: `true`
- `microfoneAtivo`: `false` (mute encerra a sessao de captura no Core Audio)
- `sessaoCapturaEncontrada`: `false`
- `plataformaDetectada`: `null` ou vazio
- O detector nao deve iniciar automaticamente sem microfone ativo

---

## Cenario 7 — Duas plataformas simultaneas (ambiguo, automatico esperado: NAO)

### Passos

1. Abra um Meet no navegador (microfone ativo).
2. Abra o Teams nativo e entre em outra chamada (microfone ativo).
3. Execute o diagnostico:

```powershell
$jsonl = "$saida\cenario-07-duas-plataformas.jsonl"
if (Test-Path $jsonl) { Remove-Item $jsonl }

Start-Process `
  -FilePath '.
Anamnesis.Tray.exe' `
  -ArgumentList '--diagnostico-deteccao','--amostras','10','--intervalo-ms','1000','--saida',$jsonl `
  -Wait -NoNewWindow
```

4. Encerre ambas as chamadas.

### O que observar

**Esperado:**
- `coletaConfiavel`: `true`
- `microfoneAtivo`: `true`
- `sessaoCapturaEncontrada`: `true`
- `plataformaDetectada`: pode alternar entre `meet` e `teams`, ou ficar ambiguo
- O campo `sinaisAmbiguos` (se existir) deve ser `true`, ou a plataforma deve ser considerada `indeterminada`
- **Nao deve** autorizar inicio automatico quando duas plataformas com captura estiverem ativas

---

## Cenario 8 — Nenhum audio, desktop limpo (baseline)

### Passos

1. Feche todos os navegadores, Teams, Zoom, Discord e players de musica.
2. Execute o diagnostico:

```powershell
$jsonl = "$saida\cenario-08-baseline.jsonl"
if (Test-Path $jsonl) { Remove-Item $jsonl }

Start-Process `
  -FilePath '.
Anamnesis.Tray.exe' `
  -ArgumentList '--diagnostico-deteccao','--amostras','5','--intervalo-ms','1000','--saida',$jsonl `
  -Wait -NoNewWindow
```

### O que observar

**Esperado:**
- `coletaConfiavel`: `true`
- `microfoneAtivo`: `false`
- `sessaoCapturaEncontrada`: `false`
- `plataformaDetectada`: `null` ou vazio
- Nenhum sinal de falso positivo

---

## Consolidacao dos resultados

### Script de analise rapida

```powershell
Set-Location $saida

foreach ($f in Get-ChildItem *.jsonl | Sort-Object Name) {
    $nome = $f.BaseName
    $amostras = Get-Content $f | ConvertFrom-Json
    $confiavel = ($amostras | Where-Object { $_.coletaConfiavel -eq $true }).Count
    $microfone = ($amostras | Where-Object { $_.microfoneAtivo -eq $true }).Count
    $captura   = ($amostras | Where-Object { $_.sessaoCapturaEncontrada -eq $true }).Count
    $plataformas = $amostras | Where-Object { $_.plataformaDetectada } | Select-Object -ExpandProperty plataformaDetectada -Unique

    Write-Output ""
    Write-Output "=== $nome ==="
    Write-Output "  Amostras: $($amostras.Count)"
    Write-Output "  Coleta confiavel: $confiavel / $($amostras.Count)"
    Write-Output "  Microfone ativo: $microfone / $($amostras.Count)"
    Write-Output "  Captura encontrada: $captura / $($amostras.Count)"
    Write-Output "  Plataformas detectadas: $($plataformas -join ', ')"
}
```

### Checklist de aprovacao

| Cenario | Esperado | Resultado | OK? |
| --- | --- | --- | --- |
| 1 Meet navegador | Detecta `meet` com microfone | | [ ] |
| 2 Teams nativo | Detecta `teams` com microfone | | [ ] |
| 3 Zoom nativo | Detecta `zoom` com microfone | | [ ] |
| 4 YouTube | Nao detecta plataforma | | [ ] |
| 5 Ditado Windows | Nao detecta plataforma | | [ ] |
| 6 Meet mutado | Nao detecta plataforma | | [ ] |
| 7 Duas plataformas | Nao autoriza automatico | | [ ] |
| 8 Baseline | Nada detectado | | [ ] |

---

## Em caso de falha

### O detector nao identifica Meet/Teams/Zoom

1. Verifique se o microfone esta realmente ativo no Windows (icone da bandeja > "Configuracoes de som" > "Entrada").
2. Confirme que a sessao de captura aparece no Mixer de Volume do Windows (`sndvol.exe`).
3. Rode com mais amostras e intervalo maior:
   ```powershell
   --amostras 20 --intervalo-ms 2000
   ```
4. Verifique se a familia do processo esta na allowlist (ver `ADR-016`).

### Falso positivo em video ou musica

1. Verifique se alguma extensao do navegador ou outro aplicativo esta usando o microfone sem seu conhecimento.
2. Rode o cenario 8 (baseline) para confirmar que o desktop esta limpo.
3. Anexe o JSONL completo ao relatorio.

### Coleta nao confiavel (`coletaConfiavel: false`)

1. Pode indicar que o servico de audio do Windows esta ocupado ou que um processo foi encerrado durante a leitura.
2. Repita o cenario.
3. Se persistir, verifique o Visualizador de Eventos do Windows em `Aplicativo e Servicos Logs > Microsoft > Windows > Audio`. 

---

## Entrega do ensaio

Ao concluir:

1. Compacte a pasta `$saida` em um ZIP.
2. Preencha o checklist acima.
3. Anote qualquer comportamento inesperado, mesmo que tenha sido reprovado no checklist.
4. Entregue o ZIP e o checklist para atualizacao da SPEK-032.

> Nao e necessario gravar tela ou audio. O JSONL ja contem todos os dados sanitizados necessarios para analise.
