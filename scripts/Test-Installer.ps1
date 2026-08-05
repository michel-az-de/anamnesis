[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$InstallerPath,

    [string]$EvidenceRoot
)

$ErrorActionPreference = "Stop"

$repositorio = Split-Path -Parent $PSScriptRoot
$instalador = [IO.Path]::GetFullPath($InstallerPath)
if (-not (Test-Path -LiteralPath $instalador -PathType Leaf)) {
    throw "O instalador nao foi encontrado: $instalador"
}

if ([string]::IsNullOrWhiteSpace($EvidenceRoot)) {
    $carimbo = Get-Date -Format "yyyyMMdd-HHmmss"
    $EvidenceRoot = Join-Path $repositorio "artifacts\installer-e2e\$carimbo"
}

$evidencias = [IO.Path]::GetFullPath($EvidenceRoot)
if (Test-Path -LiteralPath $evidencias) {
    if (Get-ChildItem -LiteralPath $evidencias -Force | Select-Object -First 1) {
        throw "O diretorio de evidencias deve estar vazio: $evidencias"
    }
}
else {
    New-Item -ItemType Directory -Path $evidencias | Out-Null
}

$diretorioInstalacao = Join-Path $evidencias "programa"
$diretorioDados = Join-Path $evidencias "dados-usuario"
$caminhoConfiguracao = Join-Path $diretorioDados "config.json"
$caminhoBanco = Join-Path $diretorioDados "anamnesis.db"
$diretorioArquivo = Join-Path $diretorioDados "arquivo"
$sentinela = Join-Path $diretorioDados "preservar.txt"
New-Item -ItemType Directory -Path $diretorioDados | Out-Null
Set-Content -LiteralPath $sentinela -Value "Dados do usuario devem sobreviver a desinstalacao."

$configuracao = [ordered]@{
    CaminhoBanco = $caminhoBanco
    DiretorioArquivo = $diretorioArquivo
    EnderecoObs = "ws://127.0.0.1:4455/"
    SenhaObs = $null
    CaminhoExecutavelFfmpeg = ""
    CaminhoExecutavelWhisper = ""
    CaminhoModeloWhisper = ""
    ImagemDockerWhisper = ""
    IdiomaWhisper = "pt"
    NomeCli = ""
    CaminhoExecutavelCli = ""
    ArgumentosCli = @()
}
$configuracao | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $caminhoConfiguracao -Encoding utf8

$instalado = $false
$tray = $null
$codigoInstalacao = $null
$codigoWorker = $null
$codigoDesinstalacao = $null
$caminhoDesinstalador = Join-Path $diretorioInstalacao "unins000.exe"
$caminhoWorkerStdout = Join-Path $evidencias "worker.stdout.log"
$caminhoWorkerStderr = Join-Path $evidencias "worker.stderr.log"

try {
    $argumentosInstalacao = @(
        "/VERYSILENT",
        "/SUPPRESSMSGBOXES",
        "/NORESTART",
        "/SP-",
        "/NOICONS",
        "/DIR=$diretorioInstalacao",
        "/LOG=$(Join-Path $evidencias 'instalacao.log')"
    )
    $processoInstalacao = Start-Process -FilePath $instalador -ArgumentList $argumentosInstalacao -Wait -PassThru
    $codigoInstalacao = $processoInstalacao.ExitCode
    if ($codigoInstalacao -ne 0) {
        throw "A instalacao falhou com codigo $codigoInstalacao."
    }

    $instalado = $true
    $caminhoTray = Join-Path $diretorioInstalacao "tray\Anamnesis.Tray.exe"
    $caminhoWorker = Join-Path $diretorioInstalacao "worker\Anamnesis.Worker.exe"
    foreach ($arquivoObrigatorio in @($caminhoTray, $caminhoWorker, $caminhoDesinstalador)) {
        if (-not (Test-Path -LiteralPath $arquivoObrigatorio -PathType Leaf)) {
            throw "O payload obrigatorio nao foi instalado: $arquivoObrigatorio"
        }
    }

    $configuracaoAnterior = $env:ANAMNESIS_CONFIGURACAO
    $env:ANAMNESIS_CONFIGURACAO = $caminhoConfiguracao
    try {
        $worker = Start-Process `
            -FilePath $caminhoWorker `
            -WorkingDirectory (Split-Path -Parent $caminhoWorker) `
            -RedirectStandardOutput $caminhoWorkerStdout `
            -RedirectStandardError $caminhoWorkerStderr `
            -Wait `
            -PassThru
        $codigoWorker = $worker.ExitCode
        if ($codigoWorker -ne 0) {
            throw "O Worker instalado falhou com codigo $codigoWorker."
        }

        $tray = Start-Process `
            -FilePath $caminhoTray `
            -WorkingDirectory (Split-Path -Parent $caminhoTray) `
            -PassThru
        Start-Sleep -Seconds 3
        if ($tray.HasExited) {
            throw "O Tray instalado encerrou durante o smoke com codigo $($tray.ExitCode)."
        }

        Stop-Process -Id $tray.Id
        $tray.WaitForExit()
        $tray = $null
    }
    finally {
        $env:ANAMNESIS_CONFIGURACAO = $configuracaoAnterior
    }
}
finally {
    if ($tray -and -not $tray.HasExited) {
        Stop-Process -Id $tray.Id -ErrorAction SilentlyContinue
    }

    if ($instalado -and (Test-Path -LiteralPath $caminhoDesinstalador -PathType Leaf)) {
        $desinstalacao = Start-Process `
            -FilePath $caminhoDesinstalador `
            -ArgumentList @("/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART") `
            -Wait `
            -PassThru
        $codigoDesinstalacao = $desinstalacao.ExitCode
    }
}

if ($codigoDesinstalacao -ne 0) {
    throw "A desinstalacao falhou com codigo $codigoDesinstalacao."
}

if (Test-Path -LiteralPath $diretorioInstalacao) {
    throw "O diretorio do programa permaneceu depois da desinstalacao: $diretorioInstalacao"
}

if (-not (Test-Path -LiteralPath $sentinela -PathType Leaf)) {
    throw "A desinstalacao removeu dados do usuario."
}

$hashInstalador = (Get-FileHash -LiteralPath $instalador -Algorithm SHA256).Hash.ToLowerInvariant()
$inicioValidacao = Get-Date -Format "yyyy-MM-ddTHH:mm:ssK"

$resultado = @"
# Resultado do instalador

- Instalador: ``$instalador``
- SHA-256: ``$hashInstalador``
- Windows: ``$([Environment]::OSVersion.VersionString)``
- Evidencia registrada em: ``$inicioValidacao``
- Codigo de instalacao: ``$codigoInstalacao``
- Codigo do Worker: ``$codigoWorker``
- Tray ativo durante smoke: ``true``
- Codigo de desinstalacao: ``$codigoDesinstalacao``
- Diretorio do programa removido: ``true``
- Dados do usuario preservados: ``true``
"@
Set-Content -LiteralPath (Join-Path $evidencias "resultado.md") -Value $resultado -Encoding utf8
Write-Host "Instalador validado. Evidencias: $evidencias"
