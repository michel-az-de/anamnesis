[CmdletBinding()]
param(
    [string]$CaminhoConfiguracao = (Join-Path $env:LOCALAPPDATA 'Anamnesis\config.json'),
    [string]$DiretorioEvidencias = (Join-Path $PSScriptRoot ("..\artifacts\obs-preflight-e2e\" + (Get-Date -Format 'yyyyMMdd-HHmmss'))),
    [int]$DuracaoSegundos = 12
)

$ErrorActionPreference = 'Stop'
$evidencias = [IO.Path]::GetFullPath($DiretorioEvidencias)
if (Get-Process -Name 'obs64' -ErrorAction SilentlyContinue) {
    throw 'O E2E exige o OBS inicialmente fechado.'
}

$inicio = Get-Date
& (Join-Path $PSScriptRoot 'Executar-ObsAudioRealE2E.ps1') `
    -CaminhoConfiguracao $CaminhoConfiguracao `
    -DiretorioEvidencias $evidencias `
    -DuracaoSegundos $DuracaoSegundos

$obs = Get-Process -Name 'obs64' -ErrorAction SilentlyContinue |
    Where-Object { $_.StartTime -ge $inicio } |
    Select-Object -First 1
if (-not $obs) {
    throw 'O Tray concluiu, mas não há processo OBS iniciado pelo preflight.'
}

$registro = @"

## Preflight do OBS

- OBS inicialmente fechado: ``true``
- OBS iniciado pelo Tray: ``true``
- Processo OBS: ``$($obs.Id)``
- Inicio do processo: ``$($obs.StartTime.ToString('O'))``
"@
Add-Content -LiteralPath (Join-Path $evidencias 'resultado.md') -Value $registro -Encoding utf8
Write-Host "E2E de preflight concluído. Evidências: $evidencias"
