[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$InstallerPath,

    [Parameter(Mandatory)]
    [string]$UpdateInstallerPath,

    [string]$ExpectedUpdateVersion = "0.2.0-beta.2",

    [string]$EvidenceRoot
)

$ErrorActionPreference = "Stop"

function Get-ValorRegistroOpcional {
    param(
        [Parameter(Mandatory)]
        [string]$Caminho,

        [Parameter(Mandatory)]
        [string]$Nome
    )

    if (-not (Test-Path -LiteralPath $Caminho)) {
        return $null
    }

    $chave = Get-ItemProperty -LiteralPath $Caminho
    $propriedade = $chave.PSObject.Properties[$Nome]
    if ($null -eq $propriedade) {
        return $null
    }

    return $propriedade.Value
}

$repositorio = Split-Path -Parent $PSScriptRoot
$instalador = [IO.Path]::GetFullPath($InstallerPath)
if (-not (Test-Path -LiteralPath $instalador -PathType Leaf)) {
    throw "O instalador nao foi encontrado: $instalador"
}

$atualizador = [IO.Path]::GetFullPath($UpdateInstallerPath)
if (-not (Test-Path -LiteralPath $atualizador -PathType Leaf)) {
    throw "O instalador de atualizacao nao foi encontrado: $atualizador"
}

$registroProdutoInstalado = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{B762A4D8-3BA7-4FB4-9A0A-A8135AB0DF2E}_is1"
$registroInicializacao = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"
$inicioExistente = Get-ValorRegistroOpcional `
    -Caminho $registroInicializacao `
    -Nome "Anamnesis"
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
$codigoReparo = $null
$codigoAtualizacao = $null
$codigoDesinstalacao = $null
$iconeInstalado = $false
$atalhoInstalado = $false
$configuracaoCriada = $false
$inicioWindowsPermaneceuOpcional = $false
$versaoInstalada = $null
$versaoAtualizada = $null
$caminhoDesinstalador = Join-Path $diretorioInstalacao "unins000.exe"
$caminhoWorkerStdout = Join-Path $evidencias "worker.stdout.log"
$caminhoWorkerStderr = Join-Path $evidencias "worker.stderr.log"
$caminhoReparoLog = Join-Path $evidencias "reparo.log"
$caminhoAtualizacaoLog = Join-Path $evidencias "atualizacao.log"
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
    $inicioWindowsPermaneceuOpcional = $null -eq (Get-ValorRegistroOpcional `
        -Caminho $registroInicializacao `
        -Nome "Anamnesis")
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
    $env:ANAMNESIS_TRAY_INSTANCE_KEY = $null
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

        $raizInstalacao = [IO.Path]::GetFullPath($diretorioInstalacao).TrimEnd('\') + '\'
        $caminhoWorkerCompleto = [IO.Path]::GetFullPath($caminhoWorker)
        if (-not $caminhoWorkerCompleto.StartsWith(
                $raizInstalacao,
                [StringComparison]::OrdinalIgnoreCase)) {
            throw "O smoke recusou remover arquivo fora da instalacao isolada: $caminhoWorkerCompleto"
        }

        Remove-Item -LiteralPath $caminhoWorkerCompleto -Force
        if (Test-Path -LiteralPath $caminhoWorkerCompleto -PathType Leaf) {
            throw "O smoke nao conseguiu simular o payload incompleto para reparo."
        }

        $processoReparo = Start-Process -FilePath $instalador -ArgumentList @(
            "/VERYSILENT",
            "/SUPPRESSMSGBOXES",
            "/NORESTART",
            "/SP-",
            "/GROUP=$grupoAtalhos",
            "/MERGETASKS=!startup,!desktopicon",
            "/DIR=$diretorioInstalacao",
            "/LOG=$caminhoReparoLog") -Wait -PassThru
        $codigoReparo = $processoReparo.ExitCode
        if ($codigoReparo -ne 0) {
            throw "O reparo falhou com codigo $codigoReparo."
        }
        Start-Sleep -Milliseconds 500
        if (-not $tray.HasExited) {
            throw "O reparo nao encerrou cooperativamente o Tray instalado."
        }
        $tray = $null
        if (-not (Test-Path -LiteralPath $caminhoWorkerCompleto -PathType Leaf)) {
            throw "O reparo nao restaurou o Worker ausente."
        }

        $processoAtualizacao = Start-Process -FilePath $atualizador -ArgumentList @(
            "/VERYSILENT",
            "/SUPPRESSMSGBOXES",
            "/NORESTART",
            "/SP-",
            "/GROUP=$grupoAtalhos",
            "/MERGETASKS=!startup,!desktopicon",
            "/DIR=$diretorioInstalacao",
            "/LOG=$caminhoAtualizacaoLog") -Wait -PassThru
        $codigoAtualizacao = $processoAtualizacao.ExitCode
        if ($codigoAtualizacao -ne 0) {
            throw "A atualizacao falhou com codigo $codigoAtualizacao."
        }
        $versaoAtualizada = Get-ValorRegistroOpcional `
            -Caminho $registroProdutoInstalado `
            -Nome "DisplayVersion"
        if ($versaoAtualizada -ne $ExpectedUpdateVersion) {
            throw "A atualizacao nao registrou a versao esperada: $versaoAtualizada"
        }
        if (-not (Test-Path -LiteralPath $caminhoConfiguracao -PathType Leaf) -or
            -not (Test-Path -LiteralPath $sentinela -PathType Leaf)) {
            throw "A atualizacao nao preservou os dados do usuario."
        }

        $tray = Start-Process `
            -FilePath $caminhoTray `
            -WorkingDirectory (Split-Path -Parent $caminhoTray) `
            -PassThru
        Start-Sleep -Seconds 3
        if ($tray.HasExited) {
            throw "O Tray instalado apos a atualizacao encerrou com codigo $($tray.ExitCode)."
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

if (-not (Test-Path -LiteralPath $caminhoReparoLog -PathType Leaf)) {
    throw "O log do reparo nao foi criado: $caminhoReparoLog"
}

if (-not (Test-Path -LiteralPath $caminhoAtualizacaoLog -PathType Leaf)) {
    throw "O log da atualizacao nao foi criado: $caminhoAtualizacaoLog"
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
$hashAtualizador = (Get-FileHash -LiteralPath $atualizador -Algorithm SHA256).Hash.ToLowerInvariant()
$inicioValidacao = Get-Date -Format "yyyy-MM-ddTHH:mm:ssK"

$resultado = @"
# Resultado do instalador

- Instalador: ``$instalador``
- SHA-256: ``$hashInstalador``
- Instalador de atualizacao: ``$atualizador``
- SHA-256 da atualizacao: ``$hashAtualizador``
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
- Codigo de reparo: ``$codigoReparo``
- Log de reparo criado: ``true``
- Codigo de atualizacao: ``$codigoAtualizacao``
- Log de atualizacao criado: ``true``
- Versao atualizada: ``$versaoAtualizada``
- Codigo de desinstalacao: ``$codigoDesinstalacao``
- Log de desinstalacao criado: ``true``
- Diretorio do programa removido: ``true``
- Dados do usuario preservados: ``true``
"@
Set-Content -LiteralPath (Join-Path $evidencias "resultado.md") -Value $resultado -Encoding utf8
Write-Host "Instalador validado. Evidencias: $evidencias"
