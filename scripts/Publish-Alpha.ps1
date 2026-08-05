[CmdletBinding()]
param(
    [string]$OutputRoot
)

$ErrorActionPreference = "Stop"

$repositorio = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $repositorio "artifacts\alpha\win-x64"
}
$dotnet = (Get-Command dotnet -ErrorAction SilentlyContinue).Source
if (-not $dotnet) {
    $dotnet = "C:\Program Files\dotnet\dotnet.exe"
}

if (-not (Test-Path -LiteralPath $dotnet)) {
    throw "O SDK .NET não foi encontrado. Instale o SDK .NET 10 antes de publicar a alpha."
}

$saida = [IO.Path]::GetFullPath($OutputRoot)
$destinos = @(
    @{ Projeto = "src\Anamnesis.Tray\Anamnesis.Tray.csproj"; Diretorio = (Join-Path $saida "tray") },
    @{ Projeto = "src\Anamnesis.Worker\Anamnesis.Worker.csproj"; Diretorio = (Join-Path $saida "worker") }
)

foreach ($destino in $destinos) {
    if (Test-Path -LiteralPath $destino.Diretorio) {
        throw "O diretório de saída já existe: $($destino.Diretorio). Escolha outro -OutputRoot para evitar sobrescrever artefatos."
    }
}

& $dotnet restore (Join-Path $repositorio "Anamnesis.sln") --runtime win-x64 --verbosity minimal
if ($LASTEXITCODE -ne 0) {
    throw "A restauração da solução falhou."
}

foreach ($destino in $destinos) {
    $projeto = Join-Path $repositorio $destino.Projeto
    & $dotnet publish $projeto --configuration Release --runtime win-x64 --self-contained true --no-restore --output $destino.Diretorio --verbosity minimal
    if ($LASTEXITCODE -ne 0) {
        throw "A publicação falhou para $projeto."
    }
}

Write-Host "Alpha publicada em: $saida"
