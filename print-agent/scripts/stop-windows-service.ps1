param(
    [string]$ServiceName = "ZHPrintAgent"
)

$ErrorActionPreference = "Stop"

$existing = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if (-not $existing) {
    throw "Service '$ServiceName' does not exist."
}

if ($existing.Status -eq "Stopped") {
    Write-Host "Service '$ServiceName' is already stopped."
} else {
    Stop-Service -Name $ServiceName -Force
}

Get-Service -Name $ServiceName
