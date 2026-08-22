param(
    [string]$ServiceName = "ZHPrintAgent"
)

$ErrorActionPreference = "Stop"

$existing = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if (-not $existing) {
    throw "Service '$ServiceName' does not exist. Run install-windows-service.ps1 first."
}

if ($existing.Status -eq "Running") {
    Write-Host "Service '$ServiceName' is already running."
} else {
    Start-Service -Name $ServiceName
}

Get-Service -Name $ServiceName
