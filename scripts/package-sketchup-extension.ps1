<#
.SYNOPSIS
Packs the Guanacaste 3D exporter for SketchUp as an .rbz extension.

.DESCRIPTION
An .rbz is a zip of the loader file and its folder. Users install it from SketchUp with
Extensions > Extension Manager > Install Extension. The package is written to
src/Guanacaste3D.SketchUpExporter/bin/Guanacaste3D-SketchUp.rbz.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$source = Join-Path $projectRoot 'src\Guanacaste3D.SketchUpExporter'
$output = Join-Path $source 'bin'
New-Item -ItemType Directory -Force $output | Out-Null
$rbz = Join-Path $output 'Guanacaste3D-SketchUp.rbz'
if (Test-Path $rbz) { Remove-Item $rbz -Force }
# Entry names use forward slashes: SketchUp's installer does not accept the backslashes Compress-Archive writes.
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::Open($rbz, 'Create')
try {
    $files = @(Get-Item (Join-Path $source 'guanacaste3d_exporter.rb')) + @(Get-ChildItem (Join-Path $source 'guanacaste3d_exporter') -Recurse -File)
    foreach ($file in $files) {
        $entry = $file.FullName.Substring($source.Length + 1).Replace('\', '/')
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $file.FullName, $entry) | Out-Null
    }
}
finally {
    $archive.Dispose()
}
Write-Host "SketchUp extension: $rbz"
