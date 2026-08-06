[CmdletBinding()]
param(
    [string]$Version,
    [string]$NumericVersion,
    [string]$OutputRoot,
    [string]$IsccPath,
    [switch]$PermitirArvoreDeTrabalhoSuja
)

$ErrorActionPreference = "Stop"

$repositorio = Split-Path -Parent $PSScriptRoot
$alteracoes = @(& git -C $repositorio status --porcelain)
if ($LASTEXITCODE -ne 0) {
    throw "Nao foi possivel verificar o estado Git antes de gerar o instalador."
}

$arvoreDeTrabalhoLimpa = $alteracoes.Count -eq 0
if (-not $arvoreDeTrabalhoLimpa -and -not $PermitirArvoreDeTrabalhoSuja) {
    throw "A arvore de trabalho possui alteracoes. Faça commit antes da release ou use -PermitirArvoreDeTrabalhoSuja somente para diagnostico local."
}

$versaoRelease = & (Join-Path $PSScriptRoot "Obter-VersaoRelease.ps1")
$versaoFoiInformada = -not [string]::IsNullOrWhiteSpace($Version)
$versaoNumericaFoiInformada = -not [string]::IsNullOrWhiteSpace($NumericVersion)
if ($versaoFoiInformada -xor $versaoNumericaFoiInformada) {
    throw "Informe -Version e -NumericVersion juntos ou use a versao canonica."
}

if (-not $versaoFoiInformada) {
    $Version = $versaoRelease.Versao
    $NumericVersion = $versaoRelease.VersaoNumerica
}

if ($Version -notmatch '^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$') {
    throw "A versao '$Version' nao segue SemVer."
}

if ($NumericVersion -notmatch '^\d+\.\d+\.\d+\.\d+$') {
    throw "A versao numerica '$NumericVersion' deve ter quatro componentes."
}

if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $repositorio "artifacts\releases\$Version"
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

& (Join-Path $PSScriptRoot "Publish-Alpha.ps1") `
    -OutputRoot $payload `
    -Version $Version `
    -NumericVersion $NumericVersion
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

$commit = (& git -C $repositorio rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($commit)) {
    throw "Nao foi possivel identificar o commit que gerou o instalador."
}

$canal = if ($Version -eq $versaoRelease.Versao) {
    $versaoRelease.Canal
}
elseif ($Version -match '-(?<canal>[A-Za-z]+)') {
    $Matches['canal']
}
else {
    'estavel'
}

[ordered]@{
    esquema = 1
    versao = $Version
    versaoNumerica = $NumericVersion
    canal = $canal
    tagEsperada = "v$Version"
    commit = $commit
    arvoreDeTrabalhoLimpa = $arvoreDeTrabalhoLimpa
    arquivo = $nomeInstalador
    sha256 = $hash.Hash.ToLowerInvariant()
} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $diretorioInstalador "release.json") -Encoding utf8

Write-Host "Instalador criado: $caminhoInstalador"
