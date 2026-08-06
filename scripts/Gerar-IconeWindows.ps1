[CmdletBinding()]
param(
    [string]$OutputIco,
    [string]$OutputPng
)

$ErrorActionPreference = "Stop"

$repositorio = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($OutputIco)) {
    $OutputIco = Join-Path $repositorio "src\Anamnesis.Tray\Assets\Anamnesis.ico"
}
if ([string]::IsNullOrWhiteSpace($OutputPng)) {
    $OutputPng = Join-Path $repositorio "src\Anamnesis.Tray\Assets\Anamnesis.png"
}

Add-Type -AssemblyName System.Drawing

function New-AnamnesisFrame([int]$Size) {
    $bitmap = [System.Drawing.Bitmap]::new(
        $Size,
        $Size,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.Clear([System.Drawing.Color]::Transparent)
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality

        $navy = [System.Drawing.ColorTranslator]::FromHtml("#10172E")
        $copper = [System.Drawing.ColorTranslator]::FromHtml("#B87333")
        $cream = [System.Drawing.ColorTranslator]::FromHtml("#F3EEE4")
        $outerMargin = [Math]::Max(1.0, $Size * 0.047)
        $ringMargin = $Size * 0.132
        $ringWidth = [Math]::Max(1.0, $Size * 0.039)

        $background = [System.Drawing.SolidBrush]::new($navy)
        try {
            $graphics.FillEllipse(
                $background,
                [single]$outerMargin,
                [single]$outerMargin,
                [single]($Size - (2 * $outerMargin)),
                [single]($Size - (2 * $outerMargin)))
        }
        finally {
            $background.Dispose()
        }

        $ring = [System.Drawing.Pen]::new($copper, [single]$ringWidth)
        try {
            $graphics.DrawEllipse(
                $ring,
                [single]$ringMargin,
                [single]$ringMargin,
                [single]($Size - (2 * $ringMargin)),
                [single]($Size - (2 * $ringMargin)))
        }
        finally {
            $ring.Dispose()
        }

        $center = $Size / 2.0
        $outerRadius = $Size * 0.344
        $innerRadius = $Size * 0.086
        $points = [System.Drawing.PointF[]]::new(16)
        for ($index = 0; $index -lt 16; $index++) {
            $angle = (-[Math]::PI / 2.0) + ($index * [Math]::PI / 8.0)
            $radius = if (($index % 2) -eq 0) { $outerRadius } else { $innerRadius }
            $points[$index] = [System.Drawing.PointF]::new(
                [single]($center + ([Math]::Cos($angle) * $radius)),
                [single]($center + ([Math]::Sin($angle) * $radius)))
        }

        $star = [System.Drawing.SolidBrush]::new($copper)
        try {
            $graphics.FillPolygon($star, $points)
        }
        finally {
            $star.Dispose()
        }

        $coreRadius = [Math]::Max(1.0, $Size * 0.039)
        $core = [System.Drawing.SolidBrush]::new($cream)
        try {
            $graphics.FillEllipse(
                $core,
                [single]($center - $coreRadius),
                [single]($center - $coreRadius),
                [single](2 * $coreRadius),
                [single](2 * $coreRadius))
        }
        finally {
            $core.Dispose()
        }

        $stream = [System.IO.MemoryStream]::new()
        $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
        return [pscustomobject]@{
            Size = $Size
            Bytes = $stream.ToArray()
        }
    }
    finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

$diretorioIco = Split-Path -Parent ([IO.Path]::GetFullPath($OutputIco))
$diretorioPng = Split-Path -Parent ([IO.Path]::GetFullPath($OutputPng))
New-Item -ItemType Directory -Path $diretorioIco -Force | Out-Null
New-Item -ItemType Directory -Path $diretorioPng -Force | Out-Null

$frames = @(16, 24, 32, 48, 256) | ForEach-Object { New-AnamnesisFrame $_ }
$headerSize = 6 + (16 * $frames.Count)
$offset = $headerSize
$file = [System.IO.File]::Create([IO.Path]::GetFullPath($OutputIco))
$writer = [System.IO.BinaryWriter]::new($file)
try {
    $writer.Write([uint16]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]$frames.Count)
    foreach ($frame in $frames) {
        $tamanhoDiretorio = if ($frame.Size -eq 256) { 0 } else { $frame.Size }
        $writer.Write([byte]$tamanhoDiretorio)
        $writer.Write([byte]$tamanhoDiretorio)
        $writer.Write([byte]0)
        $writer.Write([byte]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]32)
        $writer.Write([uint32]$frame.Bytes.Length)
        $writer.Write([uint32]$offset)
        $offset += $frame.Bytes.Length
    }
    foreach ($frame in $frames) {
        $writer.Write([byte[]]$frame.Bytes)
    }
}
finally {
    $writer.Dispose()
    $file.Dispose()
}

[System.IO.File]::WriteAllBytes(
    [IO.Path]::GetFullPath($OutputPng),
    [byte[]]$frames[-1].Bytes)

Write-Host "Icone Windows gerado: $([IO.Path]::GetFullPath($OutputIco))"
