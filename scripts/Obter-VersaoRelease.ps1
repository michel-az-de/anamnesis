[CmdletBinding()]
param(
    [string]$CaminhoVersao
)

$ErrorActionPreference = "Stop"

$repositorio = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($CaminhoVersao)) {
    $CaminhoVersao = Join-Path $repositorio "release\versao.json"
}

if (-not (Test-Path -LiteralPath $CaminhoVersao -PathType Leaf)) {
    throw "A declaracao canonica de release nao foi encontrada: $CaminhoVersao"
}

try {
    $declaracao = Get-Content -LiteralPath $CaminhoVersao -Raw | ConvertFrom-Json
}
catch {
    throw "A declaracao canonica de release nao e um JSON valido: $CaminhoVersao"
}

$camposObrigatorios = @(
    "versao",
    "versaoNumerica",
    "canal",
    "versaoAnteriorParaSmoke",
    "versaoNumericaAnteriorParaSmoke",
    "urlInstaladorAnterior",
    "sha256InstaladorAnterior"
)
foreach ($campo in $camposObrigatorios) {
    if ([string]::IsNullOrWhiteSpace([string]$declaracao.$campo)) {
        throw "O campo obrigatorio '$campo' esta ausente em $CaminhoVersao"
    }
}

$versao = [string]$declaracao.versao
$versaoNumerica = [string]$declaracao.versaoNumerica
$versaoAnterior = [string]$declaracao.versaoAnteriorParaSmoke
$versaoNumericaAnterior = [string]$declaracao.versaoNumericaAnteriorParaSmoke
$urlInstaladorAnterior = [string]$declaracao.urlInstaladorAnterior
$sha256InstaladorAnterior = ([string]$declaracao.sha256InstaladorAnterior).ToLowerInvariant()

if ($versao -notmatch '^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$') {
    throw "A versao '$versao' nao segue SemVer."
}

foreach ($valorNumerico in @($versaoNumerica, $versaoNumericaAnterior)) {
    if ($valorNumerico -notmatch '^\d+\.\d+\.\d+\.\d+$') {
        throw "A versao numerica '$valorNumerico' deve ter quatro componentes."
    }
}

if ($versaoAnterior -notmatch '^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$') {
    throw "A versao anterior '$versaoAnterior' nao segue SemVer."
}

$prefixoVersao = $versao.Split('-')[0]
$prefixoNumerico = ($versaoNumerica.Split('.')[0..2] -join '.')
if ($prefixoVersao -ne $prefixoNumerico) {
    throw "A versao '$versao' e a versao numerica '$versaoNumerica' possuem prefixos diferentes."
}

if ([version]$versaoNumericaAnterior -ge [version]$versaoNumerica) {
    throw "A versao anterior do smoke deve ser menor que a versao atual."
}

$prefixoVersaoAnterior = $versaoAnterior.Split('-')[0]
$prefixoNumericoAnterior = ($versaoNumericaAnterior.Split('.')[0..2] -join '.')
if ($prefixoVersaoAnterior -ne $prefixoNumericoAnterior) {
    throw "A versao anterior '$versaoAnterior' e a versao numerica '$versaoNumericaAnterior' possuem prefixos diferentes."
}

$uriInstaladorAnterior = $null
if (-not [Uri]::TryCreate(
        $urlInstaladorAnterior,
        [UriKind]::Absolute,
        [ref]$uriInstaladorAnterior) -or
    $uriInstaladorAnterior.Scheme -ne [Uri]::UriSchemeHttps) {
    throw "A URL do instalador anterior deve ser HTTPS: $urlInstaladorAnterior"
}

$nomeInstaladorAnterior = "Anamnesis-$versaoAnterior-win-x64-setup.exe"
$nomeNaUrl = [Uri]::UnescapeDataString($uriInstaladorAnterior.Segments[-1])
if (-not [string]::Equals(
        $nomeNaUrl,
        $nomeInstaladorAnterior,
        [StringComparison]::Ordinal)) {
    throw "A URL do instalador anterior nao termina com o arquivo esperado: $nomeInstaladorAnterior"
}

if ($sha256InstaladorAnterior -notmatch '^[0-9a-f]{64}$') {
    throw "O SHA-256 do instalador anterior deve conter 64 caracteres hexadecimais."
}

[pscustomobject]@{
    Versao = $versao
    VersaoNumerica = $versaoNumerica
    Canal = [string]$declaracao.canal
    Tag = "v$versao"
    VersaoAnteriorParaSmoke = $versaoAnterior
    VersaoNumericaAnteriorParaSmoke = $versaoNumericaAnterior
    UrlInstaladorAnterior = $urlInstaladorAnterior
    Sha256InstaladorAnterior = $sha256InstaladorAnterior
}
