<#
.SYNOPSIS
Builds the downloadable Guanacaste 3D add-in and thumbnail installer for Revit.

.DESCRIPTION
Compiles src/Guanacaste3D.RevitBridge once per Revit version installed on this machine (each version needs its own
Revit API assemblies) and assembles src/Guanacaste3D.RevitBridge/bin/Release/package/Guanacaste3D-Revit with a
RevitYYYY folder per version, the installer scripts and the readme, plus Guanacaste3D-Revit.zip beside it.
#>
[CmdletBinding()]
param([string[]]$Versions = @('2025', '2026', '2027'))

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$bridge = Join-Path $projectRoot 'src\Guanacaste3D.RevitBridge'
$packageRoot = Join-Path $bridge 'bin\Release\package'
$package = Join-Path $packageRoot 'Guanacaste3D-Revit'
$caller = Get-Location
try {
    Set-Location $bridge
    $resolvedPackage = [IO.Path]::GetFullPath($package)
    if (-not $resolvedPackage.StartsWith([IO.Path]::GetFullPath($packageRoot) + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'The package directory is outside the expected build output.'
    }
    if (Test-Path -LiteralPath $resolvedPackage) { Remove-Item -LiteralPath $resolvedPackage -Recurse -Force }
    New-Item -ItemType Directory -Force $package | Out-Null
    $built = @()
    foreach ($year in $Versions) {
        if (-not (Test-Path (Join-Path $env:ProgramFiles "Autodesk\Revit $year\RevitAPI.dll"))) { continue }
        # A separate output folder keeps a running Revit's loaded copy from blocking the build.
        $output = Join-Path $bridge "bin\Release\package-build\Revit$year\"
        dotnet build Guanacaste3D.RevitBridge.csproj -c Release -p:RevitVersion=$year -p:OutputPath=$output -nologo -v:q
        if ($LASTEXITCODE -ne 0) { throw "The add-in did not build for Revit $year." }
        $target = Join-Path $package "Revit$year"
        New-Item -ItemType Directory -Force $target | Out-Null
        Copy-Item (Join-Path $output 'Guanacaste3D.RevitBridge.dll') $target
        $built += $year
    }
    if ($built.Count -eq 0) { throw 'No Revit version with its API was found on this machine.' }
    Copy-Item (Join-Path $bridge 'installer\*') $package
    dotnet publish (Join-Path $projectRoot 'src\windows-thumbnails\windows-thumbnails.csproj') -c Release --self-contained false -o (Join-Path $package 'thumbnail-handler') -nologo
    if ($LASTEXITCODE -ne 0) { throw 'The thumbnail provider did not publish.' }
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'register-thumbnails.ps1') -Destination $package
    & (Join-Path $PSScriptRoot 'package-runtime.ps1') -Destination $package
    $zip = Join-Path $packageRoot 'Guanacaste3D-Revit.zip'
    if (Test-Path $zip) { Remove-Item $zip -Force }
    # Forward-slash entry names, which every unzip tool reads (Compress-Archive in Windows PowerShell writes backslashes).
    Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::Open($zip, 'Create')
    try {
        foreach ($file in Get-ChildItem $package -Recurse -File) {
            $entry = 'Guanacaste3D-Revit/' + $file.FullName.Substring($package.Length + 1).Replace('\', '/')
            [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $file.FullName, $entry) | Out-Null
        }
    }
    finally {
        $archive.Dispose()
    }
    Write-Host "Package for Revit $($built -join ', '): $zip"
}
finally {
    Set-Location $caller
}
