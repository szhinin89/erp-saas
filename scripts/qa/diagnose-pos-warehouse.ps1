[CmdletBinding()]
param(
    [switch]$UseRunningServers,
    [string]$Url = 'http://localhost:5173/sales',
    [string]$CdpEndpoint = '',
    [string]$OutputDirectory = '',
    [int]$LoginTimeoutSeconds = 300
)

$ErrorActionPreference = 'Stop'
if (-not $UseRunningServers) {
    throw 'Specify -UseRunningServers. This diagnostic never starts or stops application servers.'
}
$workspace = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$diagnostic = Join-Path $workspace 'frontend/e2e/diagnostics/pos-warehouse.mjs'
if (-not $OutputDirectory) {
    $OutputDirectory = Join-Path $workspace ('reports/pos-warehouse/' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
}
$arguments = @($diagnostic, '--url', $Url, '--output', $OutputDirectory, '--login-timeout', [string]$LoginTimeoutSeconds)
if ($CdpEndpoint) { $arguments += @('--cdp', $CdpEndpoint) }
& node @arguments
if ($LASTEXITCODE -eq 1) {
    Write-Warning "Overflow or displacement detected. See $OutputDirectory/diagnostic.json"
    exit 1
}
if ($LASTEXITCODE -ne 0) { throw "Diagnostic could not finish (exit code $LASTEXITCODE). See $OutputDirectory" }
