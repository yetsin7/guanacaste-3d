<#
.SYNOPSIS
Builds a downloadable Windows application installer with native file thumbnails and an offline runtime prerequisite.
#>
[CmdletBinding()]
param([string]$Version = '0.23.2')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$packageRoot = Join-Path $projectRoot "artifacts\release-$Version"
$package = Join-Path $packageRoot 'guanacaste-3d-installer'
$application = Join-Path $package 'application'
New-Item -ItemType Directory -Path $application -Force | Out-Null
dotnet publish (Join-Path $projectRoot 'src\Guanacaste3D.Desktop\Guanacaste3D.Desktop.csproj') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o $application -nologo
if ($LASTEXITCODE -ne 0) { throw 'The Windows application did not publish.' }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'install-packaged-windows.ps1') -Destination (Join-Path $package 'install.ps1') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'register-thumbnails.ps1') -Destination $package -Force
& (Join-Path $PSScriptRoot 'package-runtime.ps1') -Destination $package
@'
@echo off
rem Installs the application and file previews for the current Windows user.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1"
pause
'@ | Set-Content -LiteralPath (Join-Path $package 'install.cmd') -Encoding ascii
@'
# Guanacaste 3D for Windows x64

Extract this entire ZIP, close Guanacaste 3D after saving your work, and run install.cmd.
The installer creates a Start Menu shortcut and enables .g3d/.g3dt/.g3df/.g3dx file thumbnails.
The application is self-contained. The thumbnail provider uses the bundled .NET 10 Desktop
runtime installer only if needed; Windows may request administrator credentials for that prerequisite.
No SDK is needed. Refresh Explorer with F5 after installation.
This is an unsigned technical preview, not a certified construction tool.
'@ | Set-Content -LiteralPath (Join-Path $package 'readme.md') -Encoding UTF8
$zip = Join-Path $packageRoot "guanacaste-3d-$Version-win-x64-installer.zip"
if (Test-Path -LiteralPath $zip) { Remove-Item -LiteralPath $zip -Force }
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::CreateFromDirectory($package, $zip, [IO.Compression.CompressionLevel]::Optimal, $true)
Write-Output $zip
