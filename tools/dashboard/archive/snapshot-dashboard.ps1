# =============================================================================
# ZH Technologies
# Progress Dashboard v7
# Snapshot Engine v1.0
# =============================================================================

$ErrorActionPreference = "Stop"


$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path


$DataRoot = Join-Path $ProjectRoot "docs\ProgressDashboard\data"

$HistoryRoot = Join-Path $ProjectRoot "docs\ProgressDashboard\history"



Write-Host ""
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host " Snapshot Engine v1.0"
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host ""



$modelFile = Join-Path $DataRoot "dashboard-model.json"



if(!(Test-Path $modelFile))
{
    throw "dashboard-model.json not found. Run dashboard builder first."
}



if(!(Test-Path $HistoryRoot))
{
    New-Item `
    -ItemType Directory `
    -Path $HistoryRoot `
    | Out-Null
}



$timestamp =
Get-Date -Format "yyyy-MM-dd-HHmm"



$historyFile =
Join-Path `
$HistoryRoot `
"dashboard-$timestamp.json"



Copy-Item `
$modelFile `
$historyFile



$historyCount =
(
    Get-ChildItem `
    $HistoryRoot `
    -Filter "*.json"
).Count



Write-Host ""

Write-Host "Snapshot created successfully." -ForegroundColor Green

Write-Host ""

Write-Host "File:"
Write-Host $historyFile

Write-Host ""

Write-Host "Total snapshots:"
Write-Host $historyCount