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

$registroProdutoInstalado = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{B762A4D8-3BA7-4FB4-9A0A-A8135AB0DF2E}_is1"
$registroInicializacao = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"
$inicioExistente = Get-ItemPropertyValue `
    -LiteralPath $registroInicializacao `
    -Name "Anamnesis" `
    -ErrorAction SilentlyContinue
if ((Test-Path -LiteralPath $registroProdutoInstalado) -or $null -ne $inicioExistente) {
    throw "O smoke foi interrompido para preservar uma instalacao real do Anamnesis neste usuario. Execute em Windows limpo ou no runner efemero da CI."
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
$grupoAtalhos = "AnamnesisSmoke-$([Guid]::NewGuid().ToString('N'))"
$atalhoMenuIniciar = Join-Path `
    ([Environment]::GetFolderPath([Environment+SpecialFolder]::Programs)) `
    "$grupoAtalhos\Anamnesis.lnk"
New-Item -ItemType Directory -Path $diretorioDados | Out-Null
Set-Content -LiteralPath $sentinela -Value "Dados do usuario devem sobreviver a desinstalacao."

$instalado = $false
$tray = $null
$codigoInstalacao = $null
$codigoWorker = $null
$codigoSegundaAbertura = $null
$codigoDesinstalacao = $null
$iconeInstalado = $false
$atalhoInstalado = $false
$configuracaoCriada = $false
$inicioWindowsPermaneceuOpcional = $false
$versaoInstalada = $null
$caminhoDesinstalador = Join-Path $diretorioInstalacao "unins000.exe"
$caminhoWorkerStdout = Join-Path $evidencias "worker.stdout.log"
$caminhoWorkerStderr = Join-Path $evidencias "worker.stderr.log"
$caminhoDesinstalacaoLog = Join-Path $evidencias "desinstalacao.log"

try {
    $argumentosInstalacao = @(
        "/VERYSILENT",
        "/SUPPRESSMSGBOXES",
        "/NORESTART",
        "/SP-",
        "/GROUP=$grupoAtalhos",
        "/MERGETASKS=!startup,!desktopicon",
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
    $caminhoIcone = Join-Path $diretorioInstalacao "tray\Anamnesis.ico"
    $caminhoSchema = Join-Path $diretorioInstalacao "tray\ata.schema.json"
    foreach ($arquivoObrigatorio in @(
        $caminhoTray,
        $caminhoWorker,
        $caminhoIcone,
        $caminhoSchema,
        $caminhoDesinstalador)) {
        if (-not (Test-Path -LiteralPath $arquivoObrigatorio -PathType Leaf)) {
            throw "O payload obrigatorio nao foi instalado: $arquivoObrigatorio"
        }
    }
    $iconeInstalado = (Get-Item -LiteralPath $caminhoIcone).Length -gt 100
    if (-not $iconeInstalado) {
        throw "O icone instalado esta vazio ou invalido."
    }
    $atalhoInstalado = Test-Path -LiteralPath $atalhoMenuIniciar -PathType Leaf
    if (-not $atalhoInstalado) {
        throw "O atalho publico unico nao foi criado: $atalhoMenuIniciar"
    }
    $inicioWindowsPermaneceuOpcional = $null -eq (Get-ItemPropertyValue `
        -LiteralPath $registroInicializacao `
        -Name "Anamnesis" `
        -ErrorAction SilentlyContinue)
    if (-not $inicioWindowsPermaneceuOpcional) {
        throw "A inicializacao com o Windows foi criada mesmo com a tarefa desmarcada."
    }
    $versaoInstalada = [Diagnostics.FileVersionInfo]::GetVersionInfo($caminhoTray).ProductVersion
    if ($versaoInstalada -ne "0.2.0-beta.1") {
        throw "A versao do Tray instalado e inesperada: $versaoInstalada"
    }

    $configuracaoAnterior = $env:ANAMNESIS_CONFIGURACAO
    $diretorioDadosAnterior = $env:ANAMNESIS_DIRETORIO_DADOS
    $instanciaAnterior = $env:ANAMNESIS_TRAY_INSTANCE_KEY
    $env:ANAMNESIS_CONFIGURACAO = $caminhoConfiguracao
    $env:ANAMNESIS_DIRETORIO_DADOS = $diretorioDados
    $env:ANAMNESIS_TRAY_INSTANCE_KEY = "installer-smoke-$([Guid]::NewGuid().ToString('N'))"
    try {
        $tray = Start-Process `
            -FilePath $caminhoTray `
            -WorkingDirectory (Split-Path -Parent $caminhoTray) `
            -PassThru
        Start-Sleep -Seconds 3
        if ($tray.HasExited) {
            throw "O Tray instalado encerrou durante o smoke com codigo $($tray.ExitCode)."
        }
        if (-not (Test-Path -LiteralPath $caminhoConfiguracao -PathType Leaf)) {
            throw "O primeiro uso nao criou a configuracao local."
        }
        $configuracaoCriada = $true
        $configuracaoPrimeiroUso = Get-Content -LiteralPath $caminhoConfiguracao -Raw | ConvertFrom-Json
        if ($configuracaoPrimeiroUso.CaminhoBanco -ne $caminhoBanco -or
            $configuracaoPrimeiroUso.DiretorioArquivo -ne $diretorioArquivo) {
            throw "O primeiro uso saiu do diretorio de dados isolado do smoke."
        }

        $segundaAbertura = Start-Process `
            -FilePath $caminhoTray `
            -WorkingDirectory (Split-Path -Parent $caminhoTray) `
            -Wait `
            -PassThru
        $codigoSegundaAbertura = $segundaAbertura.ExitCode
        if ($codigoSegundaAbertura -ne 0) {
            throw "A segunda abertura do Tray falhou com codigo $codigoSegundaAbertura."
        }
        if ($tray.HasExited) {
            throw "A instancia primaria do Tray encerrou apos a segunda abertura."
        }

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

        Stop-Process -Id $tray.Id
        $tray.WaitForExit()
        $tray = $null
    }
    finally {
        $env:ANAMNESIS_CONFIGURACAO = $configuracaoAnterior
        $env:ANAMNESIS_DIRETORIO_DADOS = $diretorioDadosAnterior
        $env:ANAMNESIS_TRAY_INSTANCE_KEY = $instanciaAnterior
    }
}
finally {
    if ($tray -and -not $tray.HasExited) {
        Stop-Process -Id $tray.Id -ErrorAction SilentlyContinue
    }

    if ($instalado -and (Test-Path -LiteralPath $caminhoDesinstalador -PathType Leaf)) {
        $desinstalacao = Start-Process `
            -FilePath $caminhoDesinstalador `
            -ArgumentList @(
                "/VERYSILENT",
                "/SUPPRESSMSGBOXES",
                "/NORESTART",
                "/LOG=$caminhoDesinstalacaoLog") `
            -Wait `
            -PassThru
        $codigoDesinstalacao = $desinstalacao.ExitCode
    }
}

if ($codigoDesinstalacao -ne 0) {
    throw "A desinstalacao falhou com codigo $codigoDesinstalacao."
}

if (-not (Test-Path -LiteralPath $caminhoDesinstalacaoLog -PathType Leaf)) {
    throw "O log da desinstalacao nao foi criado: $caminhoDesinstalacaoLog"
}

if (Test-Path -LiteralPath $diretorioInstalacao) {
    throw "O diretorio do programa permaneceu depois da desinstalacao: $diretorioInstalacao"
}

if (Test-Path -LiteralPath $atalhoMenuIniciar) {
    throw "O atalho permaneceu depois da desinstalacao: $atalhoMenuIniciar"
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
- Segunda abertura sinalizou instancia existente: ``$($codigoSegundaAbertura -eq 0)``
- Icone proprio instalado: ``$iconeInstalado``
- Atalho publico unico instalado: ``$atalhoInstalado``
- Configuracao criada no primeiro uso: ``$configuracaoCriada``
- Inicializacao com Windows permaneceu opcional: ``$inicioWindowsPermaneceuOpcional``
- Versao instalada: ``$versaoInstalada``
- Codigo de desinstalacao: ``$codigoDesinstalacao``
- Log de desinstalacao criado: ``true``
- Diretorio do programa removido: ``true``
- Dados do usuario preservados: ``true``
"@
Set-Content -LiteralPath (Join-Path $evidencias "resultado.md") -Value $resultado -Encoding utf8
Write-Host "Instalador validado. Evidencias: $evidencias"
