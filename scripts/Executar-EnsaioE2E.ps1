[CmdletBinding()]
param(
    [string]$DiretorioEvidencias = (Join-Path $PSScriptRoot ("..\\artifacts\\e2e\\" + (Get-Date -Format 'yyyyMMdd-HHmmss')))
)

$ErrorActionPreference = 'Stop'
$diretorioCompleto = [System.IO.Path]::GetFullPath($DiretorioEvidencias)

if (Test-Path -LiteralPath $diretorioCompleto) {
    throw "O diretório de evidências já existe: $diretorioCompleto"
}

New-Item -ItemType Directory -Path $diretorioCompleto | Out-Null
$env:ANAMNESIS_E2E_EVIDENCIAS = $diretorioCompleto
$dotnet = Join-Path $env:ProgramFiles 'dotnet\dotnet.exe'

& $dotnet test (Join-Path $PSScriptRoot '..\tests\Anamnesis.Infrastructure.Tests\Anamnesis.Infrastructure.Tests.csproj') --configuration Release --no-restore --verbosity minimal --filter 'FullyQualifiedName~FluxoAlphaE2ETests'
if ($LASTEXITCODE -ne 0) {
    throw "O ensaio E2E falhou. As evidências parciais foram preservadas em: $diretorioCompleto"
}

Write-Host "Ensaio E2E concluído. Evidências: $diretorioCompleto"
