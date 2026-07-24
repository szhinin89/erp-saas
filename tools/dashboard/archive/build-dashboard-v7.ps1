# =============================================================================
# ZH Technologies
# Progress Dashboard v7
# Dashboard Builder v7
# =============================================================================

$ErrorActionPreference = "Stop"


$ProjectRoot =
(Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path


$DataRoot =
Join-Path $ProjectRoot "docs\ProgressDashboard\data"



Write-Host ""
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host " Dashboard Builder v7"
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host ""



function LoadJson($file)
{
    $path = Join-Path $DataRoot $file

    if(!(Test-Path $path))
    {
        throw "Missing $file"
    }

    return Get-Content $path -Raw | ConvertFrom-Json
}



$base =
LoadJson "dashboard-model.json"


$health =
LoadJson "health-score.json"


$diffPath =
Join-Path $DataRoot "dashboard-diff.json"



$diff = $null

if(Test-Path $diffPath)
{
    $diff =
    Get-Content $diffPath -Raw |
    ConvertFrom-Json
}



$model = [ordered]@{


Generated =
Get-Date -Format "yyyy-MM-dd HH:mm:ss"



Summary =
$base.Summary



Backend =
$base.Backend


Frontend =
$base.Frontend


Tests =
$base.Tests


Documentation =
$base.Documentation


Architecture =
$base.Architecture


Dependencies =
$base.Dependencies


API =
$base.API


Database =
$base.Database


Migrations =
$base.Migrations



HealthScore =
$health



SnapshotDiff =
$diff



}



$output =
Join-Path $DataRoot "dashboard-model-v7.json"



$model |
ConvertTo-Json -Depth 80 |
Set-Content $output -Encoding UTF8



Write-Host ""

Write-Host "Dashboard Model v7 generated successfully." -ForegroundColor Green

Write-Host ""

Write-Host "Health Scores : $($health.Count)"

if($diff)
{
    Write-Host "Snapshot Diff : INCLUDED"
}
else
{
    Write-Host "Snapshot Diff : NONE"
}