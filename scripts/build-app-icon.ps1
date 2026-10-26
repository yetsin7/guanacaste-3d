<#
.SYNOPSIS
Regenerates the Guanacaste 3D application icon (multi-size .ico).

.DESCRIPTION
Renders the brand mark from resources/brand/guanacaste3d-logo.svg geometry with
System.Drawing at every size Windows uses for Start, taskbar, Explorer and
Alt+Tab, then packs the PNG frames into one ICO file. Run it after changing the
logo and keep resources/brand.xaml in sync with the same coordinates.
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

# Mark geometry in the SVG's 256-unit space; scaled to each frame size below.
function Get-Point([double]$x, [double]$y, [double]$scale) {
    return New-Object System.Drawing.PointF(([single]($x * $scale)), ([single]($y * $scale)))
}

function New-Frame([int]$size) {
    $bitmap = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $graphics.Clear([System.Drawing.Color]::Transparent)
        $scale = $size / 256.0

        $radius = 56 * $scale
        $rect = New-Object System.Drawing.RectangleF 0, 0, $size, $size
        $path = New-Object System.Drawing.Drawing2D.GraphicsPath
        $diameter = 2 * $radius
        $path.AddArc($rect.X, $rect.Y, $diameter, $diameter, 180, 90)
        $path.AddArc($rect.Right - $diameter, $rect.Y, $diameter, $diameter, 270, 90)
        $path.AddArc($rect.Right - $diameter, $rect.Bottom - $diameter, $diameter, $diameter, 0, 90)
        $path.AddArc($rect.X, $rect.Bottom - $diameter, $diameter, $diameter, 90, 90)
        $path.CloseFigure()
        $gradient = New-Object System.Drawing.Drawing2D.LinearGradientBrush `
            (New-Object System.Drawing.PointF 0, 0), (New-Object System.Drawing.PointF $size, $size), `
            ([System.Drawing.Color]::FromArgb(255, 0x3D, 0x9B, 0xFF)), ([System.Drawing.Color]::FromArgb(255, 0x00, 0x59, 0xD1))
        $graphics.FillPath($gradient, $path)

        $faces = @(
            @{ Color = '#FFFFFF'; Points = @(@(128, 58), @(200, 98), @(128, 138), @(56, 98)) },
            @{ Color = '#CFE4FF'; Points = @(@(56, 98), @(128, 138), @(128, 218), @(56, 178)) },
            @{ Color = '#8FC1FF'; Points = @(@(128, 138), @(200, 98), @(200, 178), @(128, 218)) }
        )
        foreach ($face in $faces) {
            $points = [System.Drawing.PointF[]] @($face.Points | ForEach-Object { Get-Point $_[0] $_[1] $scale })
            $brush = New-Object System.Drawing.SolidBrush ([System.Drawing.ColorTranslator]::FromHtml($face.Color))
            $graphics.FillPolygon($brush, $points)
            $brush.Dispose()
        }

        $sunRadius = 14 * $scale
        $sunCenter = Get-Point 196 52 $scale
        $sunBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.ColorTranslator]::FromHtml('#FFB020'))
        $graphics.FillEllipse($sunBrush, $sunCenter.X - $sunRadius, $sunCenter.Y - $sunRadius, 2 * $sunRadius, 2 * $sunRadius)
        $sunBrush.Dispose()
        $gradient.Dispose()
        $path.Dispose()
    }
    finally {
        $graphics.Dispose()
    }
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
