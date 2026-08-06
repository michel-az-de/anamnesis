[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$CanonicalOutputRoot,

    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$Version,

    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$NumericVersion,

    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$OutputRoot,

    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$IsccPath
)

$ErrorActionPreference = "Stop"

if ($Version -notmatch '^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$') {
    throw "A versao diagnostica '$Version' nao segue SemVer."
}
if ($NumericVersion -notmatch '^\d+\.\d+\.\d+\.\d+$') {
    throw "A versao numerica diagnostica '$NumericVersion' deve ter quatro componentes."
}

$repositorio = Split-Path -Parent $PSScriptRoot
$saidaCanonica = [IO.Path]::GetFullPath($CanonicalOutputRoot)
$manifestoCanonico = Join-Path $saidaCanonica "installer\release.json"
if (-not (Test-Path -LiteralPath $manifestoCanonico -PathType Leaf)) {
    throw "O manifesto da publicacao canonica nao foi encontrado: $manifestoCanonico"
}

$releaseCanonico = Get-Content -LiteralPath $manifestoCanonico -Raw | ConvertFrom-Json
$versaoNumericaCanonica = [Version]$releaseCanonico.versaoNumerica
$versaoNumericaProbe = [Version]$NumericVersion
if ($versaoNumericaProbe -ge $versaoNumericaCanonica) {
    throw "O probe $versaoNumericaProbe deve ser inferior ao instalador canonico $versaoNumericaCanonica."
}

$instaladorCanonico = Join-Path $saidaCanonica "installer\$($releaseCanonico.arquivo)"
if (-not (Test-Path -LiteralPath $instaladorCanonico -PathType Leaf) -or
    (Get-FileHash -LiteralPath $instaladorCanonico -Algorithm SHA256).Hash -ne
        $releaseCanonico.sha256) {
    throw "O instalador canonico nao existe ou diverge do SHA-256 do manifesto."
}

$payload = Join-Path $saidaCanonica "payload"
foreach ($arquivoObrigatorio in @(
    (Join-Path $payload "tray\Anamnesis.Tray.exe"),
    (Join-Path $payload "tray\Anamnesis.ico"),
    (Join-Path $payload "tray\Anamnesis.png"),
    (Join-Path $payload "tray\ata.schema.json"),
    (Join-Path $payload "worker\Anamnesis.Worker.exe"),
    (Join-Path $payload "LICENSE"))) {
    if (-not (Test-Path -LiteralPath $arquivoObrigatorio -PathType Leaf)) {
        throw "O payload canonico esta incompleto: $arquivoObrigatorio"
    }
}
$trayCanonico = [Diagnostics.FileVersionInfo]::GetVersionInfo(
    (Join-Path $payload "tray\Anamnesis.Tray.exe"))
$workerCanonico = [Diagnostics.FileVersionInfo]::GetVersionInfo(
    (Join-Path $payload "worker\Anamnesis.Worker.exe"))
if ($trayCanonico.ProductVersion -ne $releaseCanonico.versao -or
    $workerCanonico.ProductVersion -ne $releaseCanonico.versao -or
    $trayCanonico.FileVersion -ne $releaseCanonico.versaoNumerica -or
    $workerCanonico.FileVersion -ne $releaseCanonico.versaoNumerica) {
    throw "O payload nao corresponde as versoes declaradas no manifesto canonico."
}

$iscc = [IO.Path]::GetFullPath($IsccPath)
if (-not (Test-Path -LiteralPath $iscc -PathType Leaf)) {
    throw "O compilador ISCC.exe nao foi encontrado: $iscc"
}

$saida = [IO.Path]::GetFullPath($OutputRoot)
$raizCanonica = $saidaCanonica.TrimEnd('\') + '\'
if ($saida.Equals($saidaCanonica, [StringComparison]::OrdinalIgnoreCase) -or
    $saida.StartsWith($raizCanonica, [StringComparison]::OrdinalIgnoreCase)) {
    throw "A saida diagnostica deve ficar fora da publicacao canonica."
}
if (Test-Path -LiteralPath $saida) {
    throw "O diretorio de saida do probe ja existe: $saida"
}
New-Item -ItemType Directory -Path $saida | Out-Null

$scriptInno = Join-Path $repositorio "installer\Anamnesis.iss"
& $iscc `
    "/DAppVersion=$Version" `
    "/DAppNumericVersion=$NumericVersion" `
    "/DSourceRoot=$payload" `
    "/DOutputDir=$saida" `
    $scriptInno |
    ForEach-Object { Write-Host $_ }
if ($LASTEXITCODE -ne 0) {
    throw "A compilacao do probe de downgrade falhou."
}

$nomeInstalador = "Anamnesis-$Version-win-x64-setup.exe"
$caminhoInstalador = Join-Path $saida $nomeInstalador
if (-not (Test-Path -LiteralPath $caminhoInstalador -PathType Leaf)) {
    throw "O compilador nao produziu o probe esperado: $caminhoInstalador"
}

$versaoArquivo =
    [Diagnostics.FileVersionInfo]::GetVersionInfo($caminhoInstalador).FileVersion.Trim()
if ([Version]$versaoArquivo -ne $versaoNumericaProbe) {
    throw "O probe foi compilado com versao inesperada: $versaoArquivo"
}

$hash = (Get-FileHash -LiteralPath $caminhoInstalador -Algorithm SHA256).Hash.ToLowerInvariant()
[ordered]@{
    esquema = 1
    publicavel = $false
    finalidade = "provar bloqueio de downgrade"
    versao = $Version
    versaoNumerica = $NumericVersion
    versaoCanonica = $releaseCanonico.versao
    versaoNumericaCanonica = $releaseCanonico.versaoNumerica
    manifestoCanonico = $manifestoCanonico
    arquivo = $nomeInstalador
    sha256 = $hash
} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $saida "downgrade-probe.json") -Encoding utf8

Write-Output $caminhoInstalador
