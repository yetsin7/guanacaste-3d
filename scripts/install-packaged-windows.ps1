<#
.SYNOPSIS
Installs a prebuilt Guanacaste 3D package without requiring a compiler or source checkout.
#>
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$source = Join-Path $PSScriptRoot 'application'
$installRoot = Join-Path $env:LOCALAPPDATA 'Programs\Guanacaste 3D'
$executable = Join-Path $installRoot 'Guanacaste3D.Desktop.exe'
if (-not (Test-Path -LiteralPath (Join-Path $source 'Guanacaste3D.Desktop.exe'))) { throw 'The application payload is missing. Extract the entire ZIP before installing.' }
$running = Get-CimInstance Win32_Process -Filter "Name='Guanacaste3D.Desktop.exe'" | Where-Object { $_.ExecutablePath -eq $executable }
if ($running) { throw 'Save your project and close Guanacaste 3D before installing this update.' }
& (Join-Path $PSScriptRoot 'ensure-desktop-runtime.ps1') -PackageDirectory $PSScriptRoot
New-Item -ItemType Directory -Path $installRoot -Force | Out-Null
Get-ChildItem -LiteralPath $source -Force | Copy-Item -Destination $installRoot -Recurse -Force
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut((Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\Guanacaste 3D.lnk'))
$shortcut.TargetPath = $executable
$shortcut.WorkingDirectory = $installRoot
$shortcut.IconLocation = "$executable,0"
$shortcut.Save()
$types = @{ '.g3d' = 'Project'; '.g3' = 'Project'; '.g3dt' = 'Template'; '.g3df' = 'Family'; '.g3dx' = 'Exchange' }
foreach ($extension in $types.Keys) {
    $type = 'Guanacaste3D.' + $types[$extension]
    $extensionKey = "HKCU:\Software\Classes\$extension"
    $typeKey = "HKCU:\Software\Classes\$type"
    New-Item -Path $extensionKey -Force | Out-Null
    Set-Item -Path $extensionKey -Value $type
    New-Item -Path "$typeKey\DefaultIcon" -Force | Out-Null
    Set-Item -Path "$typeKey\DefaultIcon" -Value "`"$executable`",0"
    New-Item -Path "$typeKey\shell\open\command" -Force | Out-Null
    Set-Item -Path "$typeKey\shell\open\command" -Value "`"$executable`" `"%1`""
}
& (Join-Path $PSScriptRoot 'register-thumbnails.ps1') -ProviderDirectory (Join-Path $source 'thumbnail-handler')
Write-Output "Installed Guanacaste 3D at $executable."
