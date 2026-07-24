# =============================================================================
# ZH Technologies
# Progress Dashboard v7
# Snapshot Compare Engine v1.0
# =============================================================================

$ErrorActionPreference = "Stop"


$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path


$HistoryRoot = Join-Path $ProjectRoot "docs\ProgressDashboard\history"

$DataRoot = Join-Path $ProjectRoot "docs\ProgressDashboard\data"



Write-Host ""
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host " Snapshot Compare Engine v1.0"
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host ""



$snapshots =
Get-ChildItem `
$HistoryRoot `
-Filter "*.json" |
Sort-Object Name



if($snapshots.Count -lt 2)
{
    Write-Host ""
    Write-Host "Need at least 2 snapshots." -ForegroundColor Yellow
    Write-Host "Current snapshots: $($snapshots.Count)"
    exit
}



$previous =
Get-Content `
$snapshots[-2].FullName `
-Raw |
ConvertFrom-Json



$current =
Get-Content `
$snapshots[-1].FullName `
-Raw |
ConvertFrom-Json




function Get-Delta($old,$new)
{
    if($null -eq $old)
    {
        $old = 0
    }

    if($null -eq $new)
    {
        $new = 0
    }

    return $new - $old
}



$result = [ordered]@{


Generated =
Get-Date -Format "yyyy-MM-dd HH:mm:ss"



From =
$snapshots[-2].Name



To =
$snapshots[-1].Name



Changes = [ordered]@{


Projects =
Get-Delta `
$previous.Summary.Projects `
$current.Summary.Projects



Modules =
Get-Delta `
$previous.Summary.Modules `
$current.Summary.Modules



FrontendFiles =
Get-Delta `
$previous.Summary.FrontendFiles `
$current.Summary.FrontendFiles



Health =
Get-Delta `
$previous.Summary.HealthAverage `
$current.Summary.HealthAverage



APIEndpoints =
Get-Delta `
$previous.Summary.APIEndpoints `
$current.Summary.APIEndpoints



Migrations =
Get-Delta `
$previous.Summary.Migrations `
$current.Summary.Migrations



}



}



$output =
Join-Path `
$DataRoot `
"dashboard-diff.json"



$result |
ConvertTo-Json -Depth 20 |
Set-Content `
$output `
-Encoding UTF8



Write-Host ""

Write-Host "Comparison generated successfully." -ForegroundColor Green

Write-Host ""

Write-Host "From:"
Write-Host $result.From

Write-Host ""

Write-Host "To:"
Write-Host $result.To

Write-Host ""

Write-Host "Changes:"
$result.Changes