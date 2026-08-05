[CmdletBinding()]
param(
    [string]$DiretorioEvidencias = (Join-Path $PSScriptRoot ("..\artifacts\tray-worker-e2e\" + (Get-Date -Format 'yyyyMMdd-HHmmss')))
)

$ErrorActionPreference = 'Stop'
$diretorioCompleto = [IO.Path]::GetFullPath($DiretorioEvidencias)

if (Test-Path -LiteralPath $diretorioCompleto) {
    throw "O diretório de evidências já existe: $diretorioCompleto"
}

New-Item -ItemType Directory -Path $diretorioCompleto | Out-Null
$env:ANAMNESIS_TRAY_WORKER_E2E_EVIDENCIAS = $diretorioCompleto
$dotnet = Join-Path $env:ProgramFiles 'dotnet\dotnet.exe'

& $dotnet test (Join-Path $PSScriptRoot '..\tests\Anamnesis.Infrastructure.Tests\Anamnesis.Infrastructure.Tests.csproj') --configuration Release --no-restore --verbosity minimal --filter 'FullyQualifiedName~TrayBlackBoxE2ETests'
if ($LASTEXITCODE -ne 0) {
    throw "O ensaio Tray para Worker falhou. Evidências parciais: $diretorioCompleto"
}

Write-Host "Ensaio Tray para Worker concluído. Evidências: $diretorioCompleto"
