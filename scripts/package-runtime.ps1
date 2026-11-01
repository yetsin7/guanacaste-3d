<#
.SYNOPSIS
Downloads and verifies the official .NET 10 Windows Desktop runtime for offline installers.
#>
[CmdletBinding()]
param([Parameter(Mandatory)][string]$Destination)
$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Path $Destination -Force | Out-Null
$releases = Invoke-RestMethod 'https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json'
$release = $releases.releases | Where-Object { $_.'release-version' -eq $releases.'latest-release' } | Select-Object -First 1
$runtime = $release.windowsdesktop.files | Where-Object { $_.rid -eq 'win-x64' -and $_.name -like '*.exe' } | Select-Object -First 1
if (-not $runtime -or ([uri]$runtime.url).Host -ne 'builds.dotnet.microsoft.com') { throw 'The official runtime metadata is incomplete.' }
$target = Join-Path $Destination 'windowsdesktop-runtime-win-x64.exe'
if (-not (Test-Path -LiteralPath $target) -or (Get-FileHash -LiteralPath $target -Algorithm SHA512).Hash -ne $runtime.hash) {
    Invoke-WebRequest -Uri $runtime.url -OutFile $target
}
if ((Get-FileHash -LiteralPath $target -Algorithm SHA512).Hash -ne $runtime.hash) { throw 'The downloaded runtime checksum does not match Microsoft release metadata.' }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'ensure-desktop-runtime.ps1') -Destination $Destination -Force
@{ version = $release.windowsdesktop.version; url = $runtime.url; sha512 = $runtime.hash } |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $Destination 'runtime-provenance.json') -Encoding UTF8
