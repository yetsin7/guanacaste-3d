<#
.SYNOPSIS
    Compila, comprueba y publica Guanacaste 3D en un equipo Windows x64 con .NET 10 SDK.
.DESCRIPTION
    Genera una aplicación portable auto-contenida; NO crea instalador MSI/MSIX.
.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\scripts\verify-windows.ps1
.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\scripts\verify-windows.ps1 -Launch
#>
[CmdletBinding()]
param(
    [switch]$Launch,
    [switch]$SkipPackage
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'No se encontró dotnet. Instala el SDK .NET 10 y vuelve a intentarlo.'
}

function Invoke-Dotnet {
    param([Parameter(Mandatory = $true)][string[]]$Arguments)
    Write-Host ('> dotnet ' + ($Arguments -join ' ')) -ForegroundColor Cyan
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw ("El comando dotnet terminó con código " + $LASTEXITCODE)
    }
}

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Push-Location $repositoryRoot
try {
    Invoke-Dotnet -Arguments @('restore', 'Guanacaste3D.slnx')
    Invoke-Dotnet -Arguments @('build', 'Guanacaste3D.slnx', '-c', 'Release', '--no-restore')
    Invoke-Dotnet -Arguments @(
        'run', '--project', 'tests/Guanacaste3D.Checks/Guanacaste3D.Checks.csproj',
        '-c', 'Release', '--no-build', '--no-restore'
    )
    Write-Host 'Compilación y comprobaciones del núcleo completadas.' -ForegroundColor Green

    if (-not $SkipPackage) {
        $output = Join-Path $repositoryRoot 'dist/win-x64'
        Invoke-Dotnet -Arguments @(
            'publish', 'src/Guanacaste3D.Desktop/Guanacaste3D.Desktop.csproj',
            '-c', 'Release', '-r', 'win-x64', '--self-contained', 'true',
            '-p:PublishSingleFile=true', '-o', $output
        )
        $executable = Join-Path $output 'Guanacaste3D.Desktop.exe'
        if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) {
            throw 'No se generó el ejecutable esperado.'
        }
        $hash = (Get-FileHash -LiteralPath $executable -Algorithm SHA256).Hash.ToLowerInvariant()
        "$hash  Guanacaste3D.Desktop.exe" |
            Set-Content -LiteralPath (Join-Path $output 'SHA256SUMS.txt') -Encoding Ascii
        Write-Host ("Ejecutable portable: " + $executable) -ForegroundColor Green
        Write-Host 'El paquete no es un instalador MSI/MSIX.'
    }

    if ($Launch) {
        if ($SkipPackage) {
            Invoke-Dotnet -Arguments @(
                'run', '--project', 'src/Guanacaste3D.Desktop/Guanacaste3D.Desktop.csproj',
                '-c', 'Release', '--no-build', '--no-restore'
            )
        }
        else {
            Start-Process -FilePath $executable
        }
    }
}
finally {
    Pop-Location
}
