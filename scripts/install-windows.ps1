<#
.SYNOPSIS
Publishes and installs Guanacaste 3D for the current Windows user.

.DESCRIPTION
Builds the framework-dependent win-x64 bundle inside the repository, copies it
to the current user's local Programs directory, creates a Start Menu shortcut,
and registers the native .g3 project file association. The script only stops a
running Guanacaste 3D process when its executable path belongs to that install.
#>
[CmdletBinding()]
param(
    [switch]$SkipBuild
)

$startingLocation = Get-Location
$scriptRoot = Split-Path -Parent $PSCommandPath
$projectRoot = Split-Path -Parent $scriptRoot
$publishRoot = Join-Path $projectRoot 'artifacts\win-x64'
$installRoot = Join-Path $env:LOCALAPPDATA 'Programs\Guanacaste 3D'
$installedExecutable = Join-Path $installRoot 'Guanacaste3D.Desktop.exe'

try {
    Set-Location -LiteralPath $projectRoot
    if (-not $SkipBuild) {
        dotnet publish src/Guanacaste3D.Desktop/Guanacaste3D.Desktop.csproj `
            -c Release -r win-x64 --self-contained false -o $publishRoot
        if ($LASTEXITCODE -ne 0) {
            throw "Guanacaste 3D publishing failed with exit code $LASTEXITCODE."
        }
    }
    $publishedExecutable = Join-Path $publishRoot 'Guanacaste3D.Desktop.exe'
    if (-not (Test-Path -LiteralPath $publishedExecutable)) {
        throw "The published Guanacaste 3D executable was not found."
    }

    $ownedProcesses = Get-CimInstance Win32_Process -Filter "Name='Guanacaste3D.Desktop.exe'" |
        Where-Object { $_.ExecutablePath -eq $installedExecutable }
    foreach ($ownedProcess in $ownedProcesses) {
        Stop-Process -Id $ownedProcess.ProcessId -Force
        # Stop-Process returns before the process releases its file locks;
        # copying immediately left a half-updated install (Copy-Item IOException).
        Wait-Process -Id $ownedProcess.ProcessId -Timeout 15 -ErrorAction SilentlyContinue
    }

    New-Item -ItemType Directory -Path $installRoot -Force | Out-Null
    # Loaded DLLs can stay locked briefly after the process exits (loader or
    # antivirus handles), so retry a bounded number of times before failing.
    for ($attempt = 1; ; $attempt++) {
        try {
            Get-ChildItem -LiteralPath $publishRoot -Force |
                Copy-Item -Destination $installRoot -Recurse -Force -ErrorAction Stop
            break
        }
        catch [System.IO.IOException] {
            if ($attempt -ge 20) { throw }
            Start-Sleep -Milliseconds 500
        }
    }

    $shortcutPath = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\Guanacaste 3D.lnk'
    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($shortcutPath)
    $shortcut.TargetPath = $installedExecutable
    $shortcut.WorkingDirectory = $installRoot
    $shortcut.Description = 'Guanacaste 3D BIM authoring application'
    $shortcut.Save()

    $extensionKey = 'HKCU:\Software\Classes\.g3'
    $projectTypeKey = 'HKCU:\Software\Classes\Guanacaste3D.Project'
    New-Item -Path $extensionKey -Force | Out-Null
    Set-Item -Path $extensionKey -Value 'Guanacaste3D.Project'
    New-Item -Path $projectTypeKey -Force | Out-Null
    Set-Item -Path $projectTypeKey -Value 'Guanacaste 3D Project'
    New-Item -Path "$projectTypeKey\DefaultIcon" -Force | Out-Null
    Set-Item -Path "$projectTypeKey\DefaultIcon" -Value "`"$installedExecutable`",0"
    New-Item -Path "$projectTypeKey\shell\open\command" -Force | Out-Null
    Set-Item -Path "$projectTypeKey\shell\open\command" -Value "`"$installedExecutable`" `"%1`""

    Write-Output "Installed Guanacaste 3D at $installedExecutable"
    Write-Output "Registered .g3 as the native Guanacaste 3D project format."
}
finally {
    Set-Location -LiteralPath $startingLocation
}
