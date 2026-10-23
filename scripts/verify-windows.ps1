<#
.SYNOPSIS
Builds, checks, and packages Guanacaste 3D as a portable Windows x64 application.

.DESCRIPTION
Runs restore, Release build, and the core regression checks, then publishes a
self-contained single-file executable to artifacts\portable-win-x64 with a
SHA-256 checksum. Optionally starts the published executable and confirms that
its main window stays open. This produces a portable folder, not an installer.

.EXAMPLE
powershell -ExecutionPolicy Bypass -File .\scripts\verify-windows.ps1 -SmokeLaunch
#>
[CmdletBinding()]
param(
    [switch]$SkipPackage,
    [switch]$SmokeLaunch
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Invoke-Dotnet {
    param([Parameter(Mandatory = $true)][string[]]$Arguments)
    Write-Host ('> dotnet ' + ($Arguments -join ' ')) -ForegroundColor Cyan
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet exited with code $LASTEXITCODE."
    }
}

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'The dotnet CLI was not found. Install the .NET 10 SDK and retry.'
}

$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$outputRoot = Join-Path $projectRoot 'artifacts\portable-win-x64'
$executable = Join-Path $outputRoot 'Guanacaste3D.Desktop.exe'

Push-Location -LiteralPath $projectRoot
try {
    Invoke-Dotnet @('restore', 'Guanacaste3D.slnx')
    Invoke-Dotnet @('build', 'Guanacaste3D.slnx', '-c', 'Release', '--no-restore')
    Invoke-Dotnet @('run', '--project', 'tests/Guanacaste3D.Checks/Guanacaste3D.Checks.csproj',
        '-c', 'Release', '--no-build', '--no-restore')
    Write-Host 'Build and core regression checks passed.' -ForegroundColor Green

    if (-not $SkipPackage) {
        Invoke-Dotnet @('publish', 'src/Guanacaste3D.Desktop/Guanacaste3D.Desktop.csproj',
            '-c', 'Release', '-r', 'win-x64', '--self-contained', 'true',
            '-p:PublishSingleFile=true', '-o', $outputRoot)
        if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) {
            throw 'The published executable was not produced.'
        }
        $hash = (Get-FileHash -LiteralPath $executable -Algorithm SHA256).Hash.ToLowerInvariant()
        "$hash  Guanacaste3D.Desktop.exe" |
            Set-Content -LiteralPath (Join-Path $outputRoot 'SHA256SUMS.txt') -Encoding Ascii
        Write-Host "Portable executable: $executable" -ForegroundColor Green
    }

    if ($SmokeLaunch) {
        if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) {
            throw 'Smoke launch requires a published executable; omit -SkipPackage.'
        }
        $process = Start-Process -FilePath $executable -PassThru
        try {
            $deadline = (Get-Date).AddSeconds(20)
            do {
                Start-Sleep -Milliseconds 500
                $process.Refresh()
            } while (-not $process.HasExited -and
                [string]::IsNullOrEmpty($process.MainWindowTitle) -and
                (Get-Date) -lt $deadline)
            if ($process.HasExited) {
                throw "Guanacaste 3D exited during startup with code $($process.ExitCode)."
            }
            if ([string]::IsNullOrEmpty($process.MainWindowTitle)) {
                throw 'Guanacaste 3D did not show its main window within 20 seconds.'
            }
            Write-Host "Smoke launch passed: '$($process.MainWindowTitle)'" -ForegroundColor Green
        }
        finally {
            if (-not $process.HasExited) { Stop-Process -Id $process.Id -Force }
        }
    }
}
finally {
    Pop-Location
}
