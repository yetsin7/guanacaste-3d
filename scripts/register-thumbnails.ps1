<#
.SYNOPSIS
Registers or removes the current user's native-document thumbnail provider.
.DESCRIPTION
Copies the published provider into a content-addressed directory so updates never
overwrite a DLL loaded by a Shell client. Does not restart Explorer or change
the user's thumbnail visibility preferences. Requires the .NET 10 Desktop runtime.
#>
[CmdletBinding()]
param(
    [string]$ProviderDirectory,
    [switch]$Unregister
)
$ErrorActionPreference = 'Stop'
$providerClass = '{2175B67E-0798-4A06-B104-4F1B6E9C42D8}'
$thumbnailInterface = '{E357FCCD-A995-4576-B01F-234630154E96}'
$classesRoot = 'HKCU:\Software\Classes'
$classKey = "$classesRoot\CLSID\$providerClass"
$associationPaths = @('.g3d', '.g3dt', '.g3df', '.g3dx', 'Guanacaste3D.Project', 'Guanacaste3D.Template',
    'Guanacaste3D.Family', 'Guanacaste3D.Exchange', 'SystemFileAssociations\.g3d', 'SystemFileAssociations\.g3dt',
    'SystemFileAssociations\.g3df', 'SystemFileAssociations\.g3dx')

if ($Unregister) {
    foreach ($extension in $associationPaths) {
        $handlerKey = "$classesRoot\$extension\shellex\$thumbnailInterface"
        if ((Test-Path $handlerKey) -and (Get-Item $handlerKey).GetValue('') -eq $providerClass) {
            Remove-Item -LiteralPath $handlerKey -Force
        }
    }
    if (Test-Path "$classKey\InprocServer32") { Remove-Item -LiteralPath "$classKey\InprocServer32" -Force }
    if (Test-Path $classKey) { Remove-Item -LiteralPath $classKey -Force }
    if (Test-Path "$classesRoot\AppID\$providerClass") { Remove-Item -LiteralPath "$classesRoot\AppID\$providerClass" -Force }
}
else {
    if (-not [Environment]::Is64BitProcess) { throw 'Thumbnail registration requires a 64-bit PowerShell process.' }
    $providerFiles = @('Guanacaste3D.Thumbnails.comhost.dll', 'Guanacaste3D.Thumbnails.dll',
        'Guanacaste3D.Thumbnails.deps.json', 'Guanacaste3D.Thumbnails.runtimeconfig.json', 'Guanacaste3D.Core.dll')
    $fingerprints = foreach ($file in $providerFiles) {
        $source = Join-Path $ProviderDirectory $file
        if (-not (Test-Path -LiteralPath $source)) { throw "The thumbnail provider file is missing: $source" }
        (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
    }
    $algorithm = [System.Security.Cryptography.SHA256]::Create()
    try { $digest = $algorithm.ComputeHash([Text.Encoding]::UTF8.GetBytes(($fingerprints -join ''))) }
    finally { $algorithm.Dispose() }
    $version = ([BitConverter]::ToString($digest) -replace '-', '').Substring(0, 24)
    $destination = Join-Path $env:LOCALAPPDATA "Programs\Guanacaste 3D Thumbnails\$version"
    New-Item -ItemType Directory -Path $destination -Force | Out-Null
    foreach ($file in $providerFiles) {
        $target = Join-Path $destination $file
        if (-not (Test-Path -LiteralPath $target)) { Copy-Item -LiteralPath (Join-Path $ProviderDirectory $file) -Destination $target }
    }
    New-Item -Path "$classKey\InprocServer32" -Force | Out-Null
    Set-Item -Path "$classKey\InprocServer32" -Value (Join-Path $destination $providerFiles[0])
    New-ItemProperty -Path "$classKey\InprocServer32" -Name ThreadingModel -Value 'Both' -Force | Out-Null
    # Per-user COM activation is unavailable to the restricted thumbnail surrogate on supported test machines.
    # Stream access remains read-only; Windows requests extraction on its thumbnail worker threads.
    New-ItemProperty -Path $classKey -Name DisableProcessIsolation -Value 1 -PropertyType DWord -Force | Out-Null
    foreach ($extension in $associationPaths) {
        $handlerKey = "$classesRoot\$extension\shellex\$thumbnailInterface"
        New-Item -Path $handlerKey -Force | Out-Null
        Set-Item -Path $handlerKey -Value $providerClass
    }
}

if (-not ('GuanacasteThumbnailNotification' -as [type])) {
    Add-Type @'
using System;
using System.Runtime.InteropServices;
/// <summary>Notifies the Shell to reload file-type associations without restarting Explorer.</summary>
public static class GuanacasteThumbnailNotification {
    /// <summary>Broadcasts an association change to invalidate cached handler registrations.</summary>
    [DllImport("shell32.dll")]
    public static extern void SHChangeNotify(uint eventId, uint flags, IntPtr first, IntPtr second);
}
'@
}
[GuanacasteThumbnailNotification]::SHChangeNotify(0x08000000, 0, [IntPtr]::Zero, [IntPtr]::Zero)
Write-Output 'Updated native document thumbnail registration for the current user.'
