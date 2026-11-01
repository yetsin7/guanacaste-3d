<#
.SYNOPSIS
Ensures the packaged thumbnail provider can load the .NET 10 Desktop runtime.
.DESCRIPTION
Uses the official offline runtime installer bundled with the release when no x64
.NET 10 Desktop runtime is registered. Windows requests elevation only in that case.
#>
[CmdletBinding()]
param([string]$PackageDirectory = $PSScriptRoot)
$ErrorActionPreference = 'Stop'
$runtimeKey = 'HKLM:\SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App'
if ((Test-Path $runtimeKey) -and ((Get-Item $runtimeKey).Property | Where-Object { $_ -like '10.*' })) { return }
$installer = Join-Path $PackageDirectory 'windowsdesktop-runtime-win-x64.exe'
if (-not (Test-Path -LiteralPath $installer)) { throw 'The packaged .NET 10 Desktop runtime installer is missing.' }
$process = Start-Process -FilePath $installer -ArgumentList '/install', '/quiet', '/norestart' -Verb RunAs -WindowStyle Hidden -PassThru -Wait
if ($process.ExitCode -notin 0, 3010) { throw "The .NET Desktop runtime installer failed with exit code $($process.ExitCode)." }
