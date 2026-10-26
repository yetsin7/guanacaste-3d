<#
.SYNOPSIS
Runs local REA static inspection against a built Guanacaste assembly.
.DESCRIPTION
Requires the official rea-agents 6.7.0 npm package in artifacts/rea-tooling.
Uses an explicitly supplied Node runtime or the Codex bundled runtime. Does
not register MCP servers, execute target assemblies, or configure native providers.
#>
[CmdletBinding()]
param(
    [string]$NodeExecutable,
    [string]$AssemblyPath,
    [string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$cliPath = Join-Path $projectRoot 'artifacts/rea-tooling/node_modules/rea-agents/scripts/rea.mjs'
if (-not (Test-Path -LiteralPath $cliPath)) {
    throw 'Install the official rea-agents@6.7.0 package into artifacts/rea-tooling first.'
}
if (-not $NodeExecutable) {
    $runtimeRoot = Join-Path $env:LOCALAPPDATA 'OpenAI/Codex/runtimes/cua_node'
    $runtime = Get-ChildItem -LiteralPath $runtimeRoot -Directory -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($runtime) { $NodeExecutable = Join-Path $runtime.FullName 'bin/node.exe' }
    else { $NodeExecutable = (Get-Command node -ErrorAction Stop).Source }
}
if (-not $AssemblyPath) {
    $AssemblyPath = Join-Path $projectRoot 'src/Guanacaste3D.Core/bin/Release/net10.0/Guanacaste3D.Core.dll'
}
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $projectRoot 'artifacts/rea-evidence' }
$AssemblyPath = (Resolve-Path -LiteralPath $AssemblyPath).Path
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
foreach ($operation in @('inspect-managed-artifact', 'inspect-managed-members')) {
    $arguments = @($cliPath, $operation, $AssemblyPath, '--format', 'json', '--full-output')
    $evidence = & $NodeExecutable @arguments
    if ($LASTEXITCODE -ne 0) { throw "REA $operation failed with exit code $LASTEXITCODE." }
    $parsedEvidence = $evidence | ConvertFrom-Json
    if (-not $parsedEvidence.ok) { throw "REA $operation returned an unsuccessful evidence envelope." }
    $evidence | Set-Content -LiteralPath (Join-Path $OutputDirectory "$operation.json") -Encoding utf8
}
[ordered]@{
    assembly = $AssemblyPath
    sha256 = (Get-FileHash -LiteralPath $AssemblyPath -Algorithm SHA256).Hash
    nodeVersion = (& $NodeExecutable --version)
    reaVersion = '6.7.0'
    inspection = 'Static metadata only; not runtime or visual validation.'
} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $OutputDirectory 'manifest.json') -Encoding utf8
Write-Host "REA evidence saved to $OutputDirectory"
