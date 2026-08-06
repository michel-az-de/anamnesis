[CmdletBinding()]
param(
    [string]$Version = "0.2.0-beta.1",
    [string]$NumericVersion = "0.2.0.0",
    [string]$OutputRoot,
    [string]$IsccPath
)

$ErrorActionPreference = "Stop"

$repositorio = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $repositorio "artifacts\beta\$Version"
}

$saida = [IO.Path]::GetFullPath($OutputRoot)
if (Test-Path -LiteralPath $saida) {
    throw "O diretorio de saida ja existe: $saida"
}

if ([string]::IsNullOrWhiteSpace($IsccPath)) {
    $candidatos = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
    )
    $IsccPath = $candidatos | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
}

if ([string]::IsNullOrWhiteSpace($IsccPath) -or -not (Test-Path -LiteralPath $IsccPath -PathType Leaf)) {
    throw "O compilador ISCC.exe do Inno Setup 6 nao foi encontrado."
}

$payload = Join-Path $saida "payload"
$diretorioInstalador = Join-Path $saida "installer"
New-Item -ItemType Directory -Path $diretorioInstalador | Out-Null

& (Join-Path $PSScriptRoot "Publish-Alpha.ps1") -OutputRoot $payload
if ($LASTEXITCODE -ne 0) {
    throw "A publicacao autocontida falhou."
}

$scriptInno = Join-Path $repositorio "installer\Anamnesis.iss"
& $IsccPath `
    "/DAppVersion=$Version" `
    "/DAppNumericVersion=$NumericVersion" `
    "/DSourceRoot=$payload" `
    "/DOutputDir=$diretorioInstalador" `
    $scriptInno
if ($LASTEXITCODE -ne 0) {
    throw "A compilacao do instalador falhou."
}

$nomeInstalador = "Anamnesis-$Version-win-x64-setup.exe"
$caminhoInstalador = Join-Path $diretorioInstalador $nomeInstalador
if (-not (Test-Path -LiteralPath $caminhoInstalador -PathType Leaf)) {
    throw "O compilador nao produziu o instalador esperado: $caminhoInstalador"
}

$hash = Get-FileHash -LiteralPath $caminhoInstalador -Algorithm SHA256
"$($hash.Hash.ToLowerInvariant())  $nomeInstalador" |
    Set-Content -LiteralPath (Join-Path $diretorioInstalador "SHA256SUMS.txt") -Encoding ascii

Write-Host "Instalador criado: $caminhoInstalador"
