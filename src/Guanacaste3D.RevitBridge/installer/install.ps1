<#
.SYNOPSIS
Installs (or removes) the Guanacaste 3D add-in for every Revit version on this computer that the package supports.

.DESCRIPTION
Per user, without administrator rights: each RevitYYYY folder of the package is copied to
%APPDATA%\Autodesk\Revit\Addins\YYYY\Guanacaste3D and a manifest is written next to it. Revit must be closed while
the add-in is installed or removed. The first time Revit starts with the add-in it asks whether to trust it: choose
"Cargar siempre" (Always load).
#>
[CmdletBinding()]
param([switch]$Uninstall)

$ErrorActionPreference = 'Stop'
$package = $PSScriptRoot
$addinsRoot = Join-Path $env:APPDATA 'Autodesk\Revit\Addins'
$done = @()

foreach ($source in Get-ChildItem $package -Directory -Filter 'Revit20*') {
    $year = $source.Name.Substring(5)
    $revit = Join-Path $env:ProgramFiles "Autodesk\Revit $year\Revit.exe"
    $addins = Join-Path $addinsRoot $year
    $target = Join-Path $addins 'Guanacaste3D'
    $manifest = Join-Path $addins 'Guanacaste3D.RevitBridge.addin'
    if ($Uninstall) {
        if (Test-Path $manifest) { Remove-Item $manifest -Force }
        if (Test-Path $target) { Remove-Item $target -Recurse -Force }
        $done += "Revit $year"
        continue
    }
    if (-not (Test-Path $revit)) { continue }
    New-Item -ItemType Directory -Force $target | Out-Null
    Copy-Item (Join-Path $source.FullName '*') $target -Recurse -Force
    $assembly = [System.Security.SecurityElement]::Escape((Join-Path $target 'Guanacaste3D.RevitBridge.dll'))
    @"
<?xml version="1.0" encoding="utf-8"?>
<RevitAddIns>
  <AddIn Type="Application">
    <Name>Guanacaste 3D</Name>
    <Assembly>$assembly</Assembly>
    <AddInId>5B0C7F3E-2D4A-4E61-9C83-6A1F0B2E7D45</AddInId>
    <FullClassName>Guanacaste3D.RevitBridge.BridgeApplication</FullClassName>
    <VendorId>GUANACASTE3D</VendorId>
    <VendorDescription>Guanacaste 3D</VendorDescription>
  </AddIn>
</RevitAddIns>
"@ | Set-Content -Path $manifest -Encoding UTF8
    $done += "Revit $year"
}

if ($done.Count -eq 0) {
    Write-Host 'No se encontró Revit 2025, 2026 o 2027 en esta computadora.' -ForegroundColor Yellow
    exit 1
}
if ($Uninstall) {
    Write-Host ("Complemento Guanacaste 3D desinstalado de: " + ($done -join ', ')) -ForegroundColor Green
} else {
    Write-Host ("Complemento Guanacaste 3D instalado para: " + ($done -join ', ')) -ForegroundColor Green
    Write-Host 'Abra Revit; la primera vez elija «Cargar siempre». Encontrará la pestaña «Guanacaste 3D» en la cinta.'
}
