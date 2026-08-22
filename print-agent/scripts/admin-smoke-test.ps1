<#
.SYNOPSIS
Exercises the /api/admin/* bootstrap + wizard flow end to end.

.DESCRIPTION
Intended for a freshly installed, not-yet-configured instance (SetupCompleted=false,
loopback binding) so /api/admin/* is reachable without a key. Regenerates the API key,
lists Windows printers, configures the first one found, runs a test print, and completes
setup - after which /api/admin/* starts requiring the returned key.
#>
param(
    [string]$BaseUrl = "http://127.0.0.1:9817"
)

$ErrorActionPreference = "Stop"

Write-Host "GET /api/admin/status (pre-setup, no key)"
$status = Invoke-RestMethod -Method Get -Uri "$BaseUrl/api/admin/status"
$status | Format-List

if ($status.setupCompleted) {
    throw "Setup is already completed on this instance. This script is meant for a fresh, unconfigured install."
}

Write-Host "`nPOST /api/admin/apikey/regenerate"
$keyResult = Invoke-RestMethod -Method Post -Uri "$BaseUrl/api/admin/apikey/regenerate"
$apiKey = $keyResult.apiKey
Write-Host "Generated API key (store this securely): $apiKey"
$headers = @{ "X-ZH-PrintAgent-Key" = $apiKey }

Write-Host "`nGET /api/admin/printers/windows"
$detected = Invoke-RestMethod -Method Get -Uri "$BaseUrl/api/admin/printers/windows" -Headers $headers
$detected | Format-Table

if (-not $detected -or $detected.Count -eq 0) {
    throw "No Windows printers detected. Connect/install a printer before running this script."
}

$chosen = $detected[0]
Write-Host "`nPUT /api/admin/printers (choosing '$($chosen.name)')"
$printerPayload = @(
    @{ name = $chosen.name; driver = "windows-raw"; enabled = $true; isDefault = $true; paperWidthMm = 80 }
) | ConvertTo-Json -Depth 4 -AsArray

Invoke-RestMethod -Method Put -Uri "$BaseUrl/api/admin/printers" -Headers $headers -ContentType "application/json" -Body $printerPayload

Write-Host "`nPOST /api/admin/printers/$($chosen.name)/test-print"
Invoke-RestMethod -Method Post -Uri "$BaseUrl/api/admin/printers/$([uri]::EscapeDataString($chosen.name))/test-print" -Headers $headers

Write-Host "`nPOST /api/admin/setup/complete"
Invoke-RestMethod -Method Post -Uri "$BaseUrl/api/admin/setup/complete" -Headers $headers

Write-Host "`nGET /api/admin/status without a key (should now fail with 401)"
try {
    Invoke-RestMethod -Method Get -Uri "$BaseUrl/api/admin/status" -ErrorAction Stop
    throw "Expected /api/admin/status to require the API key after setup completed."
} catch {
    Write-Host "As expected: $($_.Exception.Message)"
}

Write-Host "`nGET /api/admin/status with the key"
Invoke-RestMethod -Method Get -Uri "$BaseUrl/api/admin/status" -Headers $headers | Format-List

Write-Host "`nGET /api/admin/queue"
Invoke-RestMethod -Method Get -Uri "$BaseUrl/api/admin/queue" -Headers $headers | Format-List
