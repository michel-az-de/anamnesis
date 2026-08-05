[CmdletBinding()]
param(
    [string]$CaminhoConfiguracao = (Join-Path $env:LOCALAPPDATA 'Anamnesis\config.json'),
    [string]$DiretorioEvidencias = (Join-Path $PSScriptRoot ("..\artifacts\obs-audio-e2e\" + (Get-Date -Format 'yyyyMMdd-HHmmss'))),
    [int]$DuracaoSegundos = 12
)

$ErrorActionPreference = 'Stop'
$repositorio = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$evidencias = [IO.Path]::GetFullPath($DiretorioEvidencias)
$configuracao = [IO.Path]::GetFullPath($CaminhoConfiguracao)
$dotnet = Join-Path $env:ProgramFiles 'dotnet\dotnet.exe'
$tray = Join-Path $repositorio 'src\Anamnesis.Tray\bin\Release\net10.0-windows\Anamnesis.Tray.dll'
$worker = Join-Path $repositorio 'src\Anamnesis.Worker\bin\Release\net10.0-windows\Anamnesis.Worker.exe'

foreach ($arquivo in @($configuracao, $dotnet, $tray, $worker)) {
    if (-not (Test-Path -LiteralPath $arquivo -PathType Leaf)) {
        throw "Pré-requisito ausente: $arquivo"
    }
}

if (Test-Path -LiteralPath $evidencias) {
    throw "O diretório de evidências já existe: $evidencias"
}

New-Item -ItemType Directory -Path $evidencias | Out-Null
$config = Get-Content -Raw -LiteralPath $configuracao | ConvertFrom-Json
$ffplay = Join-Path (Split-Path -Parent $config.CaminhoExecutavelFfmpeg) 'ffplay.exe'
if (-not (Test-Path -LiteralPath $ffplay -PathType Leaf)) {
    throw "Pré-requisito ausente: $ffplay"
}
$inicio = Get-Date
$frase = 'Anamnesis está gravando o áudio desta reunião com sucesso.'
$configuracaoAnterior = $env:ANAMNESIS_CONFIGURACAO
$workerAnterior = $env:ANAMNESIS_WORKER_EXECUTAVEL
$env:ANAMNESIS_CONFIGURACAO = $configuracao
$env:ANAMNESIS_WORKER_EXECUTAVEL = $worker
$caminhoTts = Join-Path $evidencias 'tts.wav'

Add-Type -AssemblyName System.Speech
$voz = [System.Speech.Synthesis.SpeechSynthesizer]::new()
try {
    $voz.Volume = 100
    $voz.Rate = -1
    $voz.SetOutputToWaveFile($caminhoTts)
    $voz.Speak($frase)
}
finally {
    $voz.Dispose()
}

try {
    $processoTray = Start-Process `
        -FilePath $dotnet `
        -ArgumentList @($tray, '--gravar-teste-segundos', $DuracaoSegundos) `
        -WorkingDirectory $repositorio `
        -RedirectStandardOutput (Join-Path $evidencias 'tray.stdout.log') `
        -RedirectStandardError (Join-Path $evidencias 'tray.stderr.log') `
        -PassThru

    $limiteInicio = (Get-Date).AddSeconds(60)
    $gravacaoDetectada = $null
    while ((Get-Date) -lt $limiteInicio -and -not $gravacaoDetectada) {
        Start-Sleep -Milliseconds 250
        $gravacaoDetectada = Get-ChildItem (Join-Path $env:USERPROFILE 'Videos') -File |
            Where-Object { $_.LastWriteTime -ge $inicio } |
            Sort-Object LastWriteTime -Descending |
            Select-Object -First 1
        if ($processoTray.HasExited -and -not $gravacaoDetectada) {
            throw "O Tray encerrou antes de iniciar a gravação. Código: $($processoTray.ExitCode)."
        }
    }
    if (-not $gravacaoDetectada) {
        Stop-Process -Id $processoTray.Id -Force
        throw 'O OBS não criou o arquivo de gravação em 60 segundos.'
    }

    Start-Sleep -Seconds 2
    $reprodutor = Start-Process `
        -FilePath $ffplay `
        -ArgumentList @('-nodisp', '-autoexit', '-loglevel', 'error', $caminhoTts) `
        -Wait `
        -PassThru
    if ($reprodutor.ExitCode -ne 0) {
        throw "A reprodução da frase de teste falhou com código $($reprodutor.ExitCode)."
    }

    if (-not $processoTray.WaitForExit(90000)) {
        Stop-Process -Id $processoTray.Id -Force
        throw 'O Tray não encerrou a gravação em 90 segundos.'
    }
    $processoTray.WaitForExit()
    if ($processoTray.ExitCode -ne 0) {
        throw "Tray falhou com código $($processoTray.ExitCode)."
    }

    $saidaTray = Get-Content -Raw -LiteralPath (Join-Path $evidencias 'tray.stdout.log')
    $linhaReuniao = ($saidaTray -split "`r?`n") |
        Where-Object { $_ -like 'ReuniaoId=*' } |
        Select-Object -First 1
    if (-not $linhaReuniao) {
        throw 'O Tray não informou o identificador da reunião.'
    }

    $reuniaoId = [Guid]::Parse($linhaReuniao.Substring('ReuniaoId='.Length))
    $processoWorker = Start-Process `
        -FilePath $worker `
        -WorkingDirectory (Split-Path -Parent $worker) `
        -RedirectStandardOutput (Join-Path $evidencias 'worker.stdout.log') `
        -RedirectStandardError (Join-Path $evidencias 'worker.stderr.log') `
        -Wait `
        -PassThru

    if ($processoWorker.ExitCode -ne 0) {
        throw "Worker falhou com código $($processoWorker.ExitCode)."
    }

    $diretorioReuniao = Get-ChildItem -LiteralPath $config.DiretorioArquivo -Directory -Recurse |
        Where-Object { $_.Name -eq $reuniaoId.ToString('N') } |
        Select-Object -First 1
    if (-not $diretorioReuniao) {
        throw 'Diretório arquivado da reunião não encontrado.'
    }

    $caminhoAta = Join-Path $diretorioReuniao.FullName 'ata.md'
    $caminhoTranscricao = Join-Path $diretorioReuniao.FullName 'transcricao.md'
    foreach ($arquivo in @($caminhoAta, $caminhoTranscricao)) {
        if (-not (Test-Path -LiteralPath $arquivo -PathType Leaf) -or (Get-Item $arquivo).Length -eq 0) {
            throw "Evidência ausente ou vazia: $arquivo"
        }
    }

    $gravacao = Get-ChildItem (Join-Path $env:USERPROFILE 'Videos') -File |
        Where-Object { $_.LastWriteTime -ge $inicio } |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1
    if (-not $gravacao) {
        throw 'Arquivo novo de gravação do OBS não encontrado.'
    }

    $ffprobe = Join-Path (Split-Path -Parent $config.CaminhoExecutavelFfmpeg) 'ffprobe.exe'
    $sondagemAudio = & $ffprobe -v error -select_streams a -show_entries stream=codec_name,channels,sample_rate -of json $gravacao.FullName
    if ($LASTEXITCODE -ne 0) {
        throw 'ffprobe não confirmou a trilha de áudio.'
    }
    $sondagemAudio | Set-Content -LiteralPath (Join-Path $evidencias 'ffprobe-audio.json') -Encoding utf8
    $dadosAudio = $sondagemAudio | ConvertFrom-Json
    if (@($dadosAudio.streams).Count -eq 0) {
        throw 'A gravação não contém trilha de áudio.'
    }

    $resultado = @"
# Resultado OBS e áudio real

- Reunião: ``$reuniaoId``
- Frase reproduzida: ``$frase``
- Gravação OBS: ``$($gravacao.FullName)``
- Tamanho da gravação: ``$($gravacao.Length)`` bytes
- Ata: ``$caminhoAta``
- Transcrição: ``$caminhoTranscricao``
- Tray: código ``$($processoTray.ExitCode)``
- Worker: código ``$($processoWorker.ExitCode)``
- OBS: cena gerenciada ``Anamnesis``
- Whisper: Docker local
- LLM: ``$($config.NomeCli)``
"@
    Set-Content -LiteralPath (Join-Path $evidencias 'resultado.md') -Value $resultado -Encoding utf8
    Copy-Item -LiteralPath $caminhoAta -Destination (Join-Path $evidencias 'ata.md')
    Copy-Item -LiteralPath $caminhoTranscricao -Destination (Join-Path $evidencias 'transcricao.md')
    Write-Host "E2E real concluído. Evidências: $evidencias"
}
finally {
    $env:ANAMNESIS_CONFIGURACAO = $configuracaoAnterior
    $env:ANAMNESIS_WORKER_EXECUTAVEL = $workerAnterior
}
