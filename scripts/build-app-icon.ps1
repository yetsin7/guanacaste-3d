<#
.SYNOPSIS
Regenerates the Guanacaste 3D application icon (multi-size .ico).

.DESCRIPTION
Renders the transparent brand PNG with
System.Drawing at every size Windows uses for Start, taskbar, Explorer and
Alt+Tab, then packs the PNG frames into one ICO file. Run it after changing the
logo. The same PNG is also used by the application and website.
#>
[CmdletBinding()]
param(
    [string]$OutputPath
)

$projectRoot = Split-Path -Parent $PSScriptRoot
if (-not $OutputPath) {
    $OutputPath = Join-Path $projectRoot 'src\Guanacaste3D.Desktop\resources\brand\guanacaste3d.ico'
}

Add-Type -AssemblyName System.Drawing

# Preserve the same transparent logo artwork across all Windows icon sizes.
function New-Frame([int]$Size) {
    $imagePath = Join-Path $projectRoot 'src\Guanacaste3D.Desktop\resources\brand\guanacaste3d-logo.png'
    $source = [System.Drawing.Image]::FromFile($imagePath)
    $bitmap = New-Object System.Drawing.Bitmap $Size, $Size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.Clear([System.Drawing.Color]::Transparent)
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.DrawImage($source, 0, 0, $Size, $Size)
    }
    finally { $graphics.Dispose(); $source.Dispose() }
    return $bitmap
}

$sizes = 16, 24, 32, 48, 64, 128, 256
$frames = foreach ($size in $sizes) {
    $bitmap = New-Frame $size
    $stream = New-Object System.IO.MemoryStream
    $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
    $bitmap.Dispose()
    [pscustomobject]@{ Size = $size; Png = $stream.ToArray() }
    $stream.Dispose()
}

# ICO container: 6-byte header, one 16-byte directory entry per frame, then PNG payloads.
$outputDirectory = Split-Path -Parent $OutputPath
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
$writer = New-Object System.IO.BinaryWriter ([System.IO.File]::Create($OutputPath))
try {
    $writer.Write([uint16]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]$frames.Count)
    $offset = 6 + 16 * $frames.Count
    foreach ($frame in $frames) {
        $dimension = if ($frame.Size -ge 256) { 0 } else { $frame.Size }
        $writer.Write([byte]$dimension)
        $writer.Write([byte]$dimension)
        $writer.Write([byte]0)
        $writer.Write([byte]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]32)
        $writer.Write([uint32]$frame.Png.Length)
        $writer.Write([uint32]$offset)
        $offset += $frame.Png.Length
    }
    foreach ($frame in $frames) {
        $writer.Write($frame.Png)
    }
}
finally {
    $writer.Dispose()
}

Write-Output "Wrote $OutputPath with frames: $($sizes -join ', ')"
