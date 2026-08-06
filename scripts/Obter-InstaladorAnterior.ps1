[CmdletBinding()]
param(
    [string]$OutputRoot,
    [string]$CaminhoVersao
)

$ErrorActionPreference = "Stop"

$repositorio = Split-Path -Parent $PSScriptRoot
$versao = & (Join-Path $PSScriptRoot "Obter-VersaoRelease.ps1") `
    -CaminhoVersao $CaminhoVersao

if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path `
        $repositorio `
        ".ci\release-anterior\$($versao.VersaoAnteriorParaSmoke)"
}

$diretorio = [IO.Path]::GetFullPath($OutputRoot)
New-Item -ItemType Directory -Path $diretorio -Force | Out-Null

$nomeArquivo = "Anamnesis-$($versao.VersaoAnteriorParaSmoke)-win-x64-setup.exe"
$caminhoFinal = Join-Path $diretorio $nomeArquivo
$caminhoTemporario = "$caminhoFinal.download"

function Assert-HashEsperado {
    param(
        [Parameter(Mandatory)]
        [string]$Caminho
    )

    $hash = (Get-FileHash -LiteralPath $Caminho -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($hash -ne $versao.Sha256InstaladorAnterior) {
        throw "O instalador anterior possui SHA-256 inesperado: $hash"
    }
}

if (Test-Path -LiteralPath $caminhoFinal -PathType Leaf) {
    Assert-HashEsperado -Caminho $caminhoFinal
    Write-Output $caminhoFinal
    return
}

try {
    if (Test-Path -LiteralPath $caminhoTemporario -PathType Leaf) {
        Remove-Item -LiteralPath $caminhoTemporario -Force
    }

    $baixado = $false
    for ($tentativa = 1; $tentativa -le 3 -and -not $baixado; $tentativa++) {
        try {
            Invoke-WebRequest `
                -Uri $versao.UrlInstaladorAnterior `
                -OutFile $caminhoTemporario `
                -UseBasicParsing
            $baixado = $true
        }
        catch {
            if ($tentativa -eq 3) {
                throw
            }

            Start-Sleep -Seconds (2 * $tentativa)
        }
    }

    Assert-HashEsperado -Caminho $caminhoTemporario
    Move-Item -LiteralPath $caminhoTemporario -Destination $caminhoFinal
}
finally {
    if (Test-Path -LiteralPath $caminhoTemporario -PathType Leaf) {
        Remove-Item -LiteralPath $caminhoTemporario -Force
    }
}

Write-Output $caminhoFinal
