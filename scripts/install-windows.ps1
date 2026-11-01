<#
.SYNOPSIS
Publishes and installs Guanacaste 3D for the current Windows user.

.DESCRIPTION
Builds the framework-dependent win-x64 bundle inside the repository, copies it
to the current user's local Programs directory, creates a Start Menu shortcut,
and registers native .g3d project, .g3df family, and .g3dt template associations. The script only stops a
running Guanacaste 3D process when its executable path belongs to that install.
#>
[CmdletBinding()]
param(
    [switch]$SkipBuild,
    [switch]$EnableThumbnails
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
    # The executable embeds the brand icon (ApplicationIcon); pointing at it keeps the Start menu tile in sync.
    $shortcut.IconLocation = "$installedExecutable,0"
    $shortcut.Save()

    $nativeFileTypes = @(
        @{ Extension = '.g3d'; Type = 'Guanacaste3D.Project'; Description = 'Guanacaste 3D Project' },
        @{ Extension = '.g3df'; Type = 'Guanacaste3D.Family'; Description = 'Guanacaste 3D Family' },
        @{ Extension = '.g3dx'; Type = 'Guanacaste3D.Exchange'; Description = 'Guanacaste 3D Exchange' },
        @{ Extension = '.g3'; Type = 'Guanacaste3D.Project'; Description = 'Guanacaste 3D Project' },
        @{ Extension = '.g3dt'; Type = 'Guanacaste3D.Template'; Description = 'Guanacaste 3D Template' }
    )
    foreach ($nativeFileType in $nativeFileTypes) {
        $extensionKey = "HKCU:\Software\Classes\$($nativeFileType.Extension)"
        $documentTypeKey = "HKCU:\Software\Classes\$($nativeFileType.Type)"
        New-Item -Path $extensionKey -Force | Out-Null
        Set-Item -Path $extensionKey -Value $nativeFileType.Type
        New-Item -Path $documentTypeKey -Force | Out-Null
        Set-Item -Path $documentTypeKey -Value $nativeFileType.Description
        New-Item -Path "$documentTypeKey\DefaultIcon" -Force | Out-Null
        Set-Item -Path "$documentTypeKey\DefaultIcon" -Value "`"$installedExecutable`",0"
        New-Item -Path "$documentTypeKey\shell\open\command" -Force | Out-Null
        Set-Item -Path "$documentTypeKey\shell\open\command" -Value "`"$installedExecutable`" `"%1`""
    }

    if ($EnableThumbnails) {
        & (Join-Path $scriptRoot 'register-thumbnails.ps1') -ProviderDirectory (Join-Path $publishRoot 'thumbnail-handler') -AllowInProcess
    }
    else {
        # Older releases enabled an in-process Explorer handler automatically. Remove it on safe-default upgrades.
        & (Join-Path $scriptRoot 'register-thumbnails.ps1') -Unregister
        Write-Output 'Shell thumbnails are disabled by default for Explorer safety; pass -EnableThumbnails to opt in.'
    }

    Write-Output "Installed Guanacaste 3D at $installedExecutable"
    Write-Output 'Registered .g3d projects, .g3df families, and .g3dt templates.'
}
finally {
    Set-Location -LiteralPath $startingLocation
}
