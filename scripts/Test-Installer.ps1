[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$InstallerPath,

    [Parameter(Mandatory)]
    [string]$UpdateInstallerPath,

    [Parameter(Mandatory)]
    [string]$DowngradeInstallerPath,

    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$ExpectedInitialVersion,

    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$ExpectedInitialNumericVersion,

    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$ExpectedUpdateVersion,

    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$ExpectedUpdateNumericVersion,

    [string]$ProbePath,

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

function Get-CaminhoRegistroProdutoInstalado {
    $candidatos = @(
        "HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{B762A4D8-3BA7-4FB4-9A0A-A8135AB0DF2E}_is1",
        "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{B762A4D8-3BA7-4FB4-9A0A-A8135AB0DF2E}_is1"
    )

    return $candidatos |
        Where-Object { Test-Path -LiteralPath $_ } |
        Select-Object -First 1
}

function Invoke-InstallerProbe {
    param(
        [Parameter(Mandatory)]
        [string]$DotnetPath,

        [Parameter(Mandatory)]
        [string]$Probe,

        [Parameter(Mandatory)]
        [ValidateSet("seed", "verify")]
        [string]$Comando,

        [Parameter(Mandatory)]
        [string]$Banco,

        [Parameter(Mandatory)]
        [Guid]$ReuniaoId
    )

    $reuniaoTexto = $ReuniaoId.ToString("D")
    & $DotnetPath $Probe $Comando $Banco $reuniaoTexto
    if ($LASTEXITCODE -ne 0) {
        throw "O probe do instalador falhou no comando '$Comando' com codigo $LASTEXITCODE."
    }
}

function Assert-LogContem {
    param(
        [Parameter(Mandatory)]
        [string]$Caminho,

        [Parameter(Mandatory)]
        [string]$Trecho,

        [Parameter(Mandatory)]
        [string]$Contexto
    )

    if (-not (Test-Path -LiteralPath $Caminho -PathType Leaf)) {
        throw "O log de $Contexto nao foi criado: $Caminho"
    }
    $conteudo = Get-Content -LiteralPath $Caminho -Raw
    if ($conteudo.IndexOf($Trecho, [StringComparison]::OrdinalIgnoreCase) -lt 0) {
        throw "O log de $Contexto nao comprovou a causa esperada '$Trecho'."
    }
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

$instaladorDowngrade = [IO.Path]::GetFullPath($DowngradeInstallerPath)
if (-not (Test-Path -LiteralPath $instaladorDowngrade -PathType Leaf)) {
    throw "O probe de downgrade nao foi encontrado: $instaladorDowngrade"
}
$versaoInstaladorDowngrade =
    [Diagnostics.FileVersionInfo]::GetVersionInfo($instaladorDowngrade).FileVersion.Trim()
if ([Version]$versaoInstaladorDowngrade -ge [Version]$ExpectedUpdateNumericVersion) {
    throw "O probe de downgrade $versaoInstaladorDowngrade nao e inferior a versao atual $ExpectedUpdateNumericVersion."
}
$caminhoManifestoDowngrade = Join-Path `
    (Split-Path -Parent $instaladorDowngrade) `
    "downgrade-probe.json"
if (-not (Test-Path -LiteralPath $caminhoManifestoDowngrade -PathType Leaf)) {
    throw "O manifesto do probe de downgrade nao foi encontrado: $caminhoManifestoDowngrade"
}
$manifestoDowngrade = Get-Content -LiteralPath $caminhoManifestoDowngrade -Raw | ConvertFrom-Json
$hashInstaladorDowngradeEsperado =
    (Get-FileHash -LiteralPath $instaladorDowngrade -Algorithm SHA256).Hash.ToLowerInvariant()
if ($manifestoDowngrade.publicavel -ne $false -or
    $manifestoDowngrade.arquivo -ne (Split-Path -Leaf $instaladorDowngrade) -or
    $manifestoDowngrade.sha256 -ne $hashInstaladorDowngradeEsperado -or
    $manifestoDowngrade.versaoNumerica -ne $versaoInstaladorDowngrade -or
    $manifestoDowngrade.versaoNumericaCanonica -ne $ExpectedUpdateNumericVersion) {
    throw "O EXE de downgrade nao corresponde ao probe moderno vinculado a versao candidata."
}

$dotnet = (Get-Command dotnet -ErrorAction SilentlyContinue).Source
if (-not $dotnet) {
    throw "O SDK .NET 10 nao foi encontrado para executar o probe do instalador."
}

if ([string]::IsNullOrWhiteSpace($ProbePath)) {
    $ProbePath = Join-Path `
        $repositorio `
        "tests\Anamnesis.InstallerProbe\bin\Release\net10.0-windows\Anamnesis.InstallerProbe.dll"
}
$probe = [IO.Path]::GetFullPath($ProbePath)
if (-not (Test-Path -LiteralPath $probe -PathType Leaf)) {
    throw "O probe do instalador nao foi encontrado: $probe"
}

$registroProdutoInstalado = Get-CaminhoRegistroProdutoInstalado
$registroInicializacao = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"
$inicioExistente = Get-ValorRegistroOpcional `
    -Caminho $registroInicializacao `
    -Nome "Anamnesis"
if ($registroProdutoInstalado -or $null -ne $inicioExistente) {
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
$atalhoMenuIniciar = Join-Path `
    ([Environment]::GetFolderPath([Environment+SpecialFolder]::Programs)) `
    "Anamnesis\Anamnesis.lnk"
$atalhoMenuIniciarComum = Join-Path `
    ([Environment]::GetFolderPath([Environment+SpecialFolder]::CommonPrograms)) `
    "Anamnesis\Anamnesis.lnk"
New-Item -ItemType Directory -Path $diretorioDados | Out-Null
Set-Content -LiteralPath $sentinela -Value "Dados do usuario devem sobreviver a desinstalacao."

$instalado = $false
$tray = $null
$codigoInstalacao = $null
$codigoWorker = $null
$codigoSegundaAbertura = $null
$codigoReparo = $null
$codigoAtualizacao = $null
$codigoAtualizacaoTrayLegado = $null
$codigoDowngradeBloqueado = $null
$codigoDowngradeIncompletoBloqueado = $null
$codigoDesinstalacao = $null
$iconeInstalado = $false
$atalhoInstalado = $false
$configuracaoCriada = $false
$inicioWindowsPermaneceuOpcional = $false
$versaoInstalada = $null
$versaoWorkerInicial = $null
$workerInicialLegado = $false
$versaoAtualizada = $null
$versoesBinariosAtualizados = $false
$reuniaoSentinela = [Guid]::NewGuid()
$reuniaoSentinelaPreservada = $false
$hashConfiguracaoAntes = $null
$hashBancoAntesAtualizacao = $null
$atalhoPreservado = $false
$workerLegadoAtualizado = $false
$downgradeComPayloadIncompletoBloqueado = $false
$trayLegadoPreservado = $false
$trayLegadoEncerradoPeloHarness = $false
$encerramentoCooperativoReparo = $false
$encerramentoCooperativoDesinstalacao = $false
$dadosPreservadosAposDesinstalacao = $false
$trayDeixadoAtivoParaDesinstalacao = $false
$caminhoDesinstalador = Join-Path $diretorioInstalacao "unins000.exe"
$caminhoWorkerStdout = Join-Path $evidencias "worker.stdout.log"
$caminhoWorkerStderr = Join-Path $evidencias "worker.stderr.log"
$caminhoReparoLog = Join-Path $evidencias "reparo.log"
$caminhoAtualizacaoTrayLegadoLog = Join-Path $evidencias "atualizacao-tray-legado-bloqueada.log"
$caminhoAtualizacaoLog = Join-Path $evidencias "atualizacao.log"
$caminhoDowngradeLog = Join-Path $evidencias "downgrade-bloqueado.log"
$caminhoDowngradeIncompletoLog = Join-Path $evidencias "downgrade-payload-incompleto-bloqueado.log"
$caminhoDesinstalacaoLog = Join-Path $evidencias "desinstalacao.log"

try {
    $argumentosInstalacao = @(
        "/VERYSILENT",
        "/SUPPRESSMSGBOXES",
        "/NORESTART",
        "/SP-",
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
    $registroProdutoInstalado = Get-CaminhoRegistroProdutoInstalado
    if (-not $registroProdutoInstalado) {
        throw "O instalador nao criou o registro de desinstalacao em HKLM nem HKCU."
    }
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
    if (Test-Path -LiteralPath $atalhoMenuIniciarComum -PathType Leaf) {
        throw "O instalador por usuario criou um atalho comum indevido: $atalhoMenuIniciarComum"
    }
    $inicioWindowsPermaneceuOpcional = $null -eq (Get-ValorRegistroOpcional `
        -Caminho $registroInicializacao `
        -Nome "Anamnesis")
    if (-not $inicioWindowsPermaneceuOpcional) {
        throw "A inicializacao com o Windows foi criada mesmo com a tarefa desmarcada."
    }
    $binarioTrayInstalado = [Diagnostics.FileVersionInfo]::GetVersionInfo($caminhoTray)
    $binarioWorkerInstalado = [Diagnostics.FileVersionInfo]::GetVersionInfo($caminhoWorker)
    $versaoInstalada = $binarioTrayInstalado.ProductVersion
    $versaoWorkerInicial = "$($binarioWorkerInstalado.ProductVersion)/$($binarioWorkerInstalado.FileVersion)"
    if ($binarioTrayInstalado.ProductVersion -ne $ExpectedInitialVersion -or
        $binarioTrayInstalado.FileVersion -ne $ExpectedInitialNumericVersion) {
        throw "A versao inicial do Tray e inesperada: " +
            "$($binarioTrayInstalado.ProductVersion)/$($binarioTrayInstalado.FileVersion)"
    }
    $workerInicialLegado =
        $binarioWorkerInstalado.ProductVersion -ne $ExpectedInitialVersion -or
        $binarioWorkerInstalado.FileVersion -ne $ExpectedInitialNumericVersion
    if ($workerInicialLegado) {
        Write-Warning "Worker divergente na release anterior oficial: $versaoWorkerInicial"
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

        Invoke-InstallerProbe `
            -DotnetPath $dotnet `
            -Probe $probe `
            -Comando seed `
            -Banco $caminhoBanco `
            -ReuniaoId $reuniaoSentinela
        $hashConfiguracaoAntes = (Get-FileHash -LiteralPath $caminhoConfiguracao -Algorithm SHA256).Hash
        $hashBancoAntesAtualizacao = (Get-FileHash -LiteralPath $caminhoBanco -Algorithm SHA256).Hash
        $hashTrayAntesAtualizacaoLegada =
            (Get-FileHash -LiteralPath $caminhoTray -Algorithm SHA256).Hash
        $hashWorkerAntesAtualizacaoLegada =
            (Get-FileHash -LiteralPath $caminhoWorker -Algorithm SHA256).Hash
        $displayVersionAntesAtualizacaoLegada = Get-ValorRegistroOpcional `
            -Caminho $registroProdutoInstalado `
            -Nome "DisplayVersion"

        $raizInstalacao = [IO.Path]::GetFullPath($diretorioInstalacao).TrimEnd('\') + '\'
        $caminhoWorkerCompleto = [IO.Path]::GetFullPath($caminhoWorker)
        if (-not $caminhoWorkerCompleto.StartsWith(
                $raizInstalacao,
                [StringComparison]::OrdinalIgnoreCase)) {
            throw "O smoke recusou remover arquivo fora da instalacao isolada: $caminhoWorkerCompleto"
        }

        $tentativaAtualizacaoComTrayLegado = Start-Process -FilePath $atualizador -ArgumentList @(
            "/VERYSILENT",
            "/SUPPRESSMSGBOXES",
            "/NORESTART",
            "/SP-",
            "/LOG=$caminhoAtualizacaoTrayLegadoLog") -Wait -PassThru
        $codigoAtualizacaoTrayLegado = $tentativaAtualizacaoComTrayLegado.ExitCode
        if ($codigoAtualizacaoTrayLegado -eq 0) {
            throw "A atualizacao encerrou ou substituiu silenciosamente o Tray legado."
        }
        Assert-LogContem `
            -Caminho $caminhoAtualizacaoTrayLegadoLog `
            -Trecho "Anamnesis continua aberto" `
            -Contexto "atualizacao bloqueada pelo Tray legado"
        Start-Sleep -Milliseconds 500
        $processoTrayLegado = Get-Process -Id $tray.Id -ErrorAction SilentlyContinue
        if (-not $processoTrayLegado -or $tray.HasExited) {
            throw "A atualizacao bloqueada nao preservou o Tray legado em execucao."
        }
        $caminhoProcessoTrayLegado = [IO.Path]::GetFullPath($processoTrayLegado.Path)
        if (-not $caminhoProcessoTrayLegado.StartsWith(
                $raizInstalacao,
                [StringComparison]::OrdinalIgnoreCase) -or
            -not $caminhoProcessoTrayLegado.Equals(
                [IO.Path]::GetFullPath($caminhoTray),
                [StringComparison]::OrdinalIgnoreCase)) {
            throw "O smoke recusou encerrar processo fora do Tray isolado: $caminhoProcessoTrayLegado"
        }
        $trayLegadoPreservado = $true
        $binarioTrayAposBloqueio = [Diagnostics.FileVersionInfo]::GetVersionInfo($caminhoTray)
        $binarioWorkerAposBloqueio = [Diagnostics.FileVersionInfo]::GetVersionInfo($caminhoWorker)
        $versaoWorkerAposBloqueio =
            "$($binarioWorkerAposBloqueio.ProductVersion)/$($binarioWorkerAposBloqueio.FileVersion)"
        if ((Get-ValorRegistroOpcional `
                -Caminho $registroProdutoInstalado `
                -Nome "DisplayVersion") -ne $displayVersionAntesAtualizacaoLegada -or
            $binarioTrayAposBloqueio.ProductVersion -ne $ExpectedInitialVersion -or
            $binarioTrayAposBloqueio.FileVersion -ne $ExpectedInitialNumericVersion -or
            $versaoWorkerAposBloqueio -ne $versaoWorkerInicial -or
            (Get-FileHash -LiteralPath $caminhoTray -Algorithm SHA256).Hash -ne
                $hashTrayAntesAtualizacaoLegada -or
            (Get-FileHash -LiteralPath $caminhoWorker -Algorithm SHA256).Hash -ne
                $hashWorkerAntesAtualizacaoLegada) {
            throw "A atualizacao bloqueada alterou registro ou binarios da release legada."
        }
        Invoke-InstallerProbe `
            -DotnetPath $dotnet `
            -Probe $probe `
            -Comando verify `
            -Banco $caminhoBanco `
            -ReuniaoId $reuniaoSentinela
        if ((Get-FileHash -LiteralPath $caminhoConfiguracao -Algorithm SHA256).Hash -ne
            $hashConfiguracaoAntes) {
            throw "A atualizacao bloqueada alterou a configuracao do usuario."
        }
        if ((Get-FileHash -LiteralPath $caminhoBanco -Algorithm SHA256).Hash -ne
            $hashBancoAntesAtualizacao) {
            throw "A atualizacao bloqueada alterou o banco local do usuario."
        }
        if (-not (Test-Path -LiteralPath $sentinela -PathType Leaf)) {
            throw "A atualizacao bloqueada removeu dados do usuario."
        }

        Stop-Process -Id $tray.Id -Force
        $null = $tray.WaitForExit(10000)
        if (-not $tray.HasExited) {
            throw "O harness nao conseguiu simular o fechamento manual do Tray legado isolado."
        }
        $trayLegadoEncerradoPeloHarness = $true
        $tray = $null

        $processoAtualizacao = Start-Process -FilePath $atualizador -ArgumentList @(
            "/VERYSILENT",
            "/SUPPRESSMSGBOXES",
            "/NORESTART",
            "/SP-",
            "/LOG=$caminhoAtualizacaoLog") -Wait -PassThru
        $codigoAtualizacao = $processoAtualizacao.ExitCode
        if ($codigoAtualizacao -ne 0) {
            throw "A atualizacao falhou com codigo $codigoAtualizacao."
        }
        $registroProdutoInstalado = Get-CaminhoRegistroProdutoInstalado
        if (-not $registroProdutoInstalado) {
            throw "A atualizacao concluiu sem registro de desinstalacao vigente."
        }
        $versaoAtualizada = Get-ValorRegistroOpcional `
            -Caminho $registroProdutoInstalado `
            -Nome "DisplayVersion"
        if ($versaoAtualizada -ne $ExpectedUpdateVersion) {
            throw "A atualizacao nao registrou a versao esperada: $versaoAtualizada"
        }
        $versaoTrayAtualizada = [Diagnostics.FileVersionInfo]::GetVersionInfo($caminhoTray)
        $versaoWorkerAtualizada = [Diagnostics.FileVersionInfo]::GetVersionInfo($caminhoWorker)
        $versoesBinariosAtualizados =
            $versaoTrayAtualizada.ProductVersion -eq $ExpectedUpdateVersion -and
            $versaoWorkerAtualizada.ProductVersion -eq $ExpectedUpdateVersion -and
            $versaoTrayAtualizada.FileVersion -eq $ExpectedUpdateNumericVersion -and
            $versaoWorkerAtualizada.FileVersion -eq $ExpectedUpdateNumericVersion
        if (-not $versoesBinariosAtualizados) {
            throw "A atualizacao registrou $versaoAtualizada, mas os binarios divergem: " +
                "Tray=$($versaoTrayAtualizada.ProductVersion)/$($versaoTrayAtualizada.FileVersion); " +
                "Worker=$($versaoWorkerAtualizada.ProductVersion)/$($versaoWorkerAtualizada.FileVersion)"
        }
        $workerLegadoAtualizado = $workerInicialLegado -and $versoesBinariosAtualizados
        if (-not (Test-Path -LiteralPath $caminhoConfiguracao -PathType Leaf) -or
            -not (Test-Path -LiteralPath $sentinela -PathType Leaf)) {
            throw "A atualizacao nao preservou os dados do usuario."
        }
        if ((Get-FileHash -LiteralPath $caminhoConfiguracao -Algorithm SHA256).Hash -ne
            $hashConfiguracaoAntes) {
            throw "A atualizacao alterou a configuracao do usuario."
        }
        if ((Get-FileHash -LiteralPath $caminhoBanco -Algorithm SHA256).Hash -ne
            $hashBancoAntesAtualizacao) {
            throw "A atualizacao alterou o banco local do usuario."
        }
        Invoke-InstallerProbe `
            -DotnetPath $dotnet `
            -Probe $probe `
            -Comando verify `
            -Banco $caminhoBanco `
            -ReuniaoId $reuniaoSentinela
        $reuniaoSentinelaPreservada = $true
        $atalhoPreservado = Test-Path -LiteralPath $atalhoMenuIniciar -PathType Leaf
        if (-not $atalhoPreservado) {
            throw "A atualizacao removeu o atalho do Menu Iniciar."
        }
        if ($null -ne (Get-ValorRegistroOpcional -Caminho $registroInicializacao -Nome "Anamnesis")) {
            throw "A atualizacao alterou a opcao de inicializacao com o Windows."
        }

        $hashTrayAtualizado = (Get-FileHash -LiteralPath $caminhoTray -Algorithm SHA256).Hash
        $hashWorkerAtualizado = (Get-FileHash -LiteralPath $caminhoWorker -Algorithm SHA256).Hash

        $tentativaDowngrade = Start-Process -FilePath $instaladorDowngrade -ArgumentList @(
            "/VERYSILENT",
            "/SUPPRESSMSGBOXES",
            "/NORESTART",
            "/SP-",
            "/LOG=$caminhoDowngradeLog") -Wait -PassThru
        $codigoDowngradeBloqueado = $tentativaDowngrade.ExitCode
        if ($codigoDowngradeBloqueado -eq 0) {
            throw "O instalador permitiu downgrade da versao $ExpectedUpdateVersion."
        }
        Assert-LogContem `
            -Caminho $caminhoDowngradeLog `
            -Trecho "downgrade autom" `
            -Contexto "downgrade bloqueado"
        $versaoAposDowngrade = Get-ValorRegistroOpcional `
            -Caminho $registroProdutoInstalado `
            -Nome "DisplayVersion"
        $trayAposDowngrade = [Diagnostics.FileVersionInfo]::GetVersionInfo($caminhoTray)
        $workerAposDowngrade = [Diagnostics.FileVersionInfo]::GetVersionInfo($caminhoWorker)
        if ($versaoAposDowngrade -ne $ExpectedUpdateVersion -or
            $trayAposDowngrade.ProductVersion -ne $ExpectedUpdateVersion -or
            $trayAposDowngrade.FileVersion -ne $ExpectedUpdateNumericVersion -or
            $workerAposDowngrade.ProductVersion -ne $ExpectedUpdateVersion -or
            $workerAposDowngrade.FileVersion -ne $ExpectedUpdateNumericVersion -or
            (Get-FileHash -LiteralPath $caminhoTray -Algorithm SHA256).Hash -ne
                $hashTrayAtualizado -or
            (Get-FileHash -LiteralPath $caminhoWorker -Algorithm SHA256).Hash -ne
                $hashWorkerAtualizado -or
            (Get-FileHash -LiteralPath $caminhoConfiguracao -Algorithm SHA256).Hash -ne
                $hashConfiguracaoAntes -or
            (Get-FileHash -LiteralPath $caminhoBanco -Algorithm SHA256).Hash -ne
                $hashBancoAntesAtualizacao) {
            throw "A tentativa de downgrade alterou a versao instalada."
        }
        Invoke-InstallerProbe `
            -DotnetPath $dotnet `
            -Probe $probe `
            -Comando verify `
            -Banco $caminhoBanco `
            -ReuniaoId $reuniaoSentinela

        $tray = Start-Process `
            -FilePath $caminhoTray `
            -WorkingDirectory (Split-Path -Parent $caminhoTray) `
            -PassThru
        Start-Sleep -Seconds 3
        if ($tray.HasExited) {
            throw "O Tray atual encerrou antes do reparo com codigo $($tray.ExitCode)."
        }

        Remove-Item -LiteralPath $caminhoWorker -Force
        $tentativaDowngradeIncompleto = Start-Process -FilePath $instaladorDowngrade -ArgumentList @(
            "/VERYSILENT",
            "/SUPPRESSMSGBOXES",
            "/NORESTART",
            "/SP-",
            "/LOG=$caminhoDowngradeIncompletoLog") -Wait -PassThru
        $codigoDowngradeIncompletoBloqueado = $tentativaDowngradeIncompleto.ExitCode
        if ($codigoDowngradeIncompletoBloqueado -eq 0) {
            throw "O instalador permitiu downgrade quando o Worker da versao mais nova estava ausente."
        }
        Assert-LogContem `
            -Caminho $caminhoDowngradeIncompletoLog `
            -Trecho "downgrade autom" `
            -Contexto "downgrade com payload incompleto bloqueado"
        if (Test-Path -LiteralPath $caminhoWorker -PathType Leaf) {
            throw "A tentativa de downgrade recriou o Worker com um pacote mais antigo."
        }
        $trayAposDowngradeIncompleto = [Diagnostics.FileVersionInfo]::GetVersionInfo($caminhoTray)
        if ($trayAposDowngradeIncompleto.ProductVersion -ne $ExpectedUpdateVersion -or
            $trayAposDowngradeIncompleto.FileVersion -ne $ExpectedUpdateNumericVersion -or
            (Get-FileHash -LiteralPath $caminhoTray -Algorithm SHA256).Hash -ne
                $hashTrayAtualizado -or
            (Get-ValorRegistroOpcional `
                -Caminho $registroProdutoInstalado `
                -Nome "DisplayVersion") -ne $ExpectedUpdateVersion -or
            (Get-FileHash -LiteralPath $caminhoConfiguracao -Algorithm SHA256).Hash -ne
                $hashConfiguracaoAntes -or
            (Get-FileHash -LiteralPath $caminhoBanco -Algorithm SHA256).Hash -ne
                $hashBancoAntesAtualizacao) {
            throw "A tentativa de downgrade alterou o Tray da instalacao incompleta."
        }
        Invoke-InstallerProbe `
            -DotnetPath $dotnet `
            -Probe $probe `
            -Comando verify `
            -Banco $caminhoBanco `
            -ReuniaoId $reuniaoSentinela
        $downgradeComPayloadIncompletoBloqueado = $true
        if ($tray.HasExited) {
            throw "O downgrade bloqueado encerrou indevidamente o Tray atual."
        }

        $reparoPayloadIncompleto = Start-Process -FilePath $atualizador -ArgumentList @(
            "/VERYSILENT",
            "/SUPPRESSMSGBOXES",
            "/NORESTART",
            "/SP-",
            "/LOG=$caminhoReparoLog") -Wait -PassThru
        $codigoReparo = $reparoPayloadIncompleto.ExitCode
        if ($codigoReparo -ne 0 -or
            -not (Test-Path -LiteralPath $caminhoWorker -PathType Leaf) -or
            [Diagnostics.FileVersionInfo]::GetVersionInfo($caminhoWorker).ProductVersion -ne
                $ExpectedUpdateVersion) {
            throw "O reparo da versao atual nao restaurou o Worker ausente."
        }
        Assert-LogContem `
            -Caminho $caminhoReparoLog `
            -Trecho "encerramento cooperativo enviada ao Tray" `
            -Contexto "reparo cooperativo"
        $null = $tray.WaitForExit(10000)
        $encerramentoCooperativoReparo = $tray.HasExited
        if (-not $encerramentoCooperativoReparo) {
            throw "O reparo da versao atual nao encerrou cooperativamente o Tray."
        }
        $tray = $null
        Invoke-InstallerProbe `
            -DotnetPath $dotnet `
            -Probe $probe `
            -Comando verify `
            -Banco $caminhoBanco `
            -ReuniaoId $reuniaoSentinela
        if ((Get-FileHash -LiteralPath $caminhoConfiguracao -Algorithm SHA256).Hash -ne
            $hashConfiguracaoAntes -or
            (Get-FileHash -LiteralPath $caminhoBanco -Algorithm SHA256).Hash -ne
            $hashBancoAntesAtualizacao) {
            throw "O reparo da versao atual alterou dados do usuario."
        }
        if (-not (Test-Path -LiteralPath $atalhoMenuIniciar -PathType Leaf)) {
            throw "O reparo da versao atual removeu o atalho do Menu Iniciar."
        }
        if ($null -ne (Get-ValorRegistroOpcional -Caminho $registroInicializacao -Nome "Anamnesis")) {
            throw "O reparo da versao atual alterou a opcao de inicializacao com o Windows."
        }

        $tray = Start-Process `
            -FilePath $caminhoTray `
            -WorkingDirectory (Split-Path -Parent $caminhoTray) `
            -PassThru
        Start-Sleep -Seconds 3
        if ($tray.HasExited) {
            throw "O Tray instalado apos a atualizacao encerrou com codigo $($tray.ExitCode)."
        }
        $trayDeixadoAtivoParaDesinstalacao = $true
    }
    finally {
        $env:ANAMNESIS_CONFIGURACAO = $configuracaoAnterior
        $env:ANAMNESIS_DIRETORIO_DADOS = $diretorioDadosAnterior
        $env:ANAMNESIS_TRAY_INSTANCE_KEY = $instanciaAnterior
    }
}
finally {
    try {
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
            if ($trayDeixadoAtivoParaDesinstalacao -and $tray -and -not $tray.HasExited) {
                $null = $tray.WaitForExit(10000)
            }
            $encerramentoCooperativoDesinstalacao =
                $trayDeixadoAtivoParaDesinstalacao -and $tray -and $tray.HasExited
        }
    }
    finally {
        if ($tray -and -not $tray.HasExited) {
            Stop-Process -Id $tray.Id -ErrorAction SilentlyContinue
        }
    }
}

if ($codigoDesinstalacao -ne 0) {
    throw "A desinstalacao falhou com codigo $codigoDesinstalacao."
}

if (-not $encerramentoCooperativoDesinstalacao) {
    throw "A desinstalacao nao encerrou cooperativamente o Tray em execucao."
}

if (-not (Test-Path -LiteralPath $caminhoDesinstalacaoLog -PathType Leaf)) {
    throw "O log da desinstalacao nao foi criado: $caminhoDesinstalacaoLog"
}
Assert-LogContem `
    -Caminho $caminhoDesinstalacaoLog `
    -Trecho "encerramento cooperativo enviada ao Tray" `
    -Contexto "desinstalacao cooperativa"

if (-not (Test-Path -LiteralPath $caminhoReparoLog -PathType Leaf)) {
    throw "O log do reparo nao foi criado: $caminhoReparoLog"
}

if (-not (Test-Path -LiteralPath $caminhoAtualizacaoTrayLegadoLog -PathType Leaf)) {
    throw "O log da atualizacao bloqueada pelo Tray legado nao foi criado: $caminhoAtualizacaoTrayLegadoLog"
}

if (-not (Test-Path -LiteralPath $caminhoAtualizacaoLog -PathType Leaf)) {
    throw "O log da atualizacao nao foi criado: $caminhoAtualizacaoLog"
}

if (-not (Test-Path -LiteralPath $caminhoDowngradeLog -PathType Leaf)) {
    throw "O log do downgrade bloqueado nao foi criado: $caminhoDowngradeLog"
}

if (-not (Test-Path -LiteralPath $caminhoDowngradeIncompletoLog -PathType Leaf)) {
    throw "O log do downgrade com payload incompleto nao foi criado: $caminhoDowngradeIncompletoLog"
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
if (-not (Test-Path -LiteralPath $caminhoConfiguracao -PathType Leaf) -or
    -not (Test-Path -LiteralPath $caminhoBanco -PathType Leaf) -or
    (Get-FileHash -LiteralPath $caminhoConfiguracao -Algorithm SHA256).Hash -ne
        $hashConfiguracaoAntes -or
    (Get-FileHash -LiteralPath $caminhoBanco -Algorithm SHA256).Hash -ne
        $hashBancoAntesAtualizacao) {
    throw "A desinstalacao alterou ou removeu a configuracao ou o banco local."
}
Invoke-InstallerProbe `
    -DotnetPath $dotnet `
    -Probe $probe `
    -Comando verify `
    -Banco $caminhoBanco `
    -ReuniaoId $reuniaoSentinela
$dadosPreservadosAposDesinstalacao = $true

$hashInstalador = (Get-FileHash -LiteralPath $instalador -Algorithm SHA256).Hash.ToLowerInvariant()
$hashAtualizador = (Get-FileHash -LiteralPath $atualizador -Algorithm SHA256).Hash.ToLowerInvariant()
$hashInstaladorDowngrade =
    (Get-FileHash -LiteralPath $instaladorDowngrade -Algorithm SHA256).Hash.ToLowerInvariant()
$inicioValidacao = Get-Date -Format "yyyy-MM-ddTHH:mm:ssK"

$resultado = @"
# Resultado do instalador

- Instalador: ``$instalador``
- SHA-256: ``$hashInstalador``
- Instalador de atualizacao: ``$atualizador``
- SHA-256 da atualizacao: ``$hashAtualizador``
- Probe de downgrade: ``$instaladorDowngrade``
- Versao numerica do probe de downgrade: ``$versaoInstaladorDowngrade``
- SHA-256 do probe de downgrade: ``$hashInstaladorDowngrade``
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
- Versao inicial do Worker: ``$versaoWorkerInicial``
- Worker inicial legado aceito da release oficial: ``$workerInicialLegado``
- Codigo da atualizacao bloqueada pelo Tray legado: ``$codigoAtualizacaoTrayLegado``
- Tray legado preservado pelo instalador: ``$trayLegadoPreservado``
- Fechamento manual do Tray legado simulado pelo harness: ``$trayLegadoEncerradoPeloHarness``
- Codigo de reparo: ``$codigoReparo``
- Log de reparo criado: ``true``
- Tray atual encerrado cooperativamente no reparo: ``$encerramentoCooperativoReparo``
- Codigo de atualizacao: ``$codigoAtualizacao``
- Log de atualizacao criado: ``true``
- Versao atualizada: ``$versaoAtualizada``
- Binarios atualizados para a mesma versao: ``$versoesBinariosAtualizados``
- Worker legado atualizado sem falso downgrade: ``$workerLegadoAtualizado``
- Codigo do downgrade bloqueado: ``$codigoDowngradeBloqueado``
- Codigo do downgrade com Worker ausente: ``$codigoDowngradeIncompletoBloqueado``
- Downgrade com payload incompleto bloqueado: ``$downgradeComPayloadIncompletoBloqueado``
- Reuniao sentinela preservada: ``$reuniaoSentinelaPreservada``
- Hash da configuracao preservado: ``true``
- Hash do banco preservado na atualizacao: ``true``
- Atalho preservado em reparo e atualizacao: ``$atalhoPreservado``
- Codigo de desinstalacao: ``$codigoDesinstalacao``
- Tray encerrado cooperativamente na desinstalacao: ``$encerramentoCooperativoDesinstalacao``
- Log de desinstalacao criado: ``true``
- Diretorio do programa removido: ``true``
- Configuracao, banco e reuniao preservados apos desinstalacao: ``$dadosPreservadosAposDesinstalacao``
"@
Set-Content -LiteralPath (Join-Path $evidencias "resultado.md") -Value $resultado -Encoding utf8
Write-Host "Instalador validado. Evidencias: $evidencias"
