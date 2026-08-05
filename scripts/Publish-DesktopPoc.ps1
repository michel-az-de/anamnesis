[CmdletBinding()]
param(
    [string]$OutputRoot
)

$ErrorActionPreference = "Stop"

$repositorio = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $repositorio "artifacts\poc-desktop\win-x64"
}

$saida = [IO.Path]::GetFullPath($OutputRoot)
if (Test-Path -LiteralPath $saida) {
    throw "O diretório de saída já existe: $saida. Escolha outro -OutputRoot para evitar sobrescrever artefatos."
}

$dotnet = (Get-Command dotnet -ErrorAction SilentlyContinue).Source
if (-not $dotnet) {
    $dotnet = "C:\Program Files\dotnet\dotnet.exe"
}

if (-not (Test-Path -LiteralPath $dotnet)) {
    throw "O SDK .NET 10 não foi encontrado."
}

$projeto = Join-Path $repositorio "src\Anamnesis.Tray\Anamnesis.Tray.csproj"
& $dotnet restore $projeto --runtime win-x64 --verbosity minimal
if ($LASTEXITCODE -ne 0) {
    throw "A restauração da POC desktop falhou."
}

& $dotnet publish $projeto `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    --no-restore `
    --output $saida `
    --verbosity minimal
if ($LASTEXITCODE -ne 0) {
    throw "A publicação da POC desktop falhou."
}

$executavel = Join-Path $saida "Anamnesis.Tray.exe"
$caminhoAtalho = Join-Path $saida "Abrir Anamnesis POC.lnk"
$shell = New-Object -ComObject WScript.Shell
$atalho = $shell.CreateShortcut($caminhoAtalho)
$atalho.TargetPath = $executavel
$atalho.Arguments = "--poc-desktop"
$atalho.WorkingDirectory = $saida
$atalho.IconLocation = "$executavel,0"
$atalho.Description = "Abrir a POC desktop nativa do Anamnesis"
$atalho.Save()

Write-Host "POC desktop publicada em: $saida"
Write-Host "Abra: $caminhoAtalho"

