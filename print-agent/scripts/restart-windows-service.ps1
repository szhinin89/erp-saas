param(
    [string]$ServiceName = "ZHPrintAgent"
)

$ErrorActionPreference = "Stop"

$existing = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if (-not $existing) {
    throw "Service '$ServiceName' does not exist. Run install-windows-service.ps1 first."
}

Restart-Service -Name $ServiceName -Force
Get-Service -Name $ServiceName
