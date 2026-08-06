[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$OutputRoot
)

$ErrorActionPreference = "Stop"

$saida = [IO.Path]::GetFullPath($OutputRoot)
if (Test-Path -LiteralPath $saida) {
    throw "O diretorio do Inno Setup ja existe: $saida"
}

New-Item -ItemType Directory -Path $saida | Out-Null
$url = 'https://github.com/jrsoftware/issrc/releases/download/is-6_7_3/innosetup-6.7.3.exe'
$shaEsperado = '9c73c3bae7ed48d44112a0f48e66742c00090bdb5bef71d9d3c056c66e97b732'
$instalador = Join-Path $saida 'innosetup-6.7.3.exe'

Invoke-WebRequest -Uri $url -OutFile $instalador
$shaObtido = (Get-FileHash -LiteralPath $instalador -Algorithm SHA256).Hash.ToLowerInvariant()
if ($shaObtido -ne $shaEsperado) {
    throw "SHA-256 inesperado para Inno Setup: $shaObtido"
}

$processo = Start-Process -FilePath $instalador -ArgumentList @(
    '/VERYSILENT',
    '/SUPPRESSMSGBOXES',
    '/NORESTART',
    '/SP-',
    "/DIR=$saida"
) -Wait -PassThru
if ($processo.ExitCode -ne 0) {
    throw "A instalacao do Inno Setup falhou com codigo $($processo.ExitCode)."
}

$iscc = Join-Path $saida 'ISCC.exe'
if (-not (Test-Path -LiteralPath $iscc -PathType Leaf)) {
    throw "ISCC.exe nao foi instalado no destino esperado: $iscc"
}

Write-Output $iscc
