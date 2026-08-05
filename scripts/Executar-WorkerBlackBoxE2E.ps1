[CmdletBinding()]
param(
    [string]$DiretorioEvidencias = (Join-Path $PSScriptRoot ("..\\artifacts\\worker-blackbox-e2e\\" + (Get-Date -Format 'yyyyMMdd-HHmmss')))
)

$ErrorActionPreference = 'Stop'
$diretorioCompleto = [System.IO.Path]::GetFullPath($DiretorioEvidencias)

if (Test-Path -LiteralPath $diretorioCompleto) {
    throw "O diretório de evidências já existe: $diretorioCompleto"
}

New-Item -ItemType Directory -Path $diretorioCompleto | Out-Null
$env:ANAMNESIS_WORKER_E2E_EVIDENCIAS = $diretorioCompleto
$dotnet = Join-Path $env:ProgramFiles 'dotnet\dotnet.exe'

& $dotnet test (Join-Path $PSScriptRoot '..\tests\Anamnesis.Infrastructure.Tests\Anamnesis.Infrastructure.Tests.csproj') --configuration Release --no-restore --verbosity minimal --filter 'FullyQualifiedName~WorkerBlackBoxE2ETests'
if ($LASTEXITCODE -ne 0) {
    throw "O ensaio black box falhou. As evidências parciais foram preservadas em: $diretorioCompleto"
}

Write-Host "Ensaio black box concluído. Evidências: $diretorioCompleto"
