# =============================================================================
# ZH Technologies
# Progress Dashboard v9
# Intelligence Model Builder
# =============================================================================

$ErrorActionPreference = "Stop"


$ProjectRoot =
(Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path


$DataRoot =
Join-Path $ProjectRoot "docs\ProgressDashboard\data"



Write-Host ""
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host " Dashboard Builder v9"
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host ""



function LoadJson($file)
{
    return Get-Content `
    (Join-Path $DataRoot $file) `
    -Raw |
    ConvertFrom-Json
}



$model =
LoadJson "dashboard-model-v7.json"



$debt =
LoadJson "technical-debt.json"



$security =
LoadJson "security-analysis.json"



$result = [ordered]@{

Generated =
Get-Date -Format "yyyy-MM-dd HH:mm:ss"


Summary =
$model.Summary


Architecture =
$model.Architecture


API =
$model.API


Database =
$model.Database


Migrations =
$model.Migrations


HealthScore =
$model.HealthScore


SnapshotDiff =
$model.SnapshotDiff



Quality =
[ordered]@{

TechnicalDebt =
$debt


Security =
$security

}


}



$output =
Join-Path `
$DataRoot `
"dashboard-model-v9.json"



$result |
ConvertTo-Json -Depth 100 |
Set-Content `
$output `
-Encoding UTF8



Write-Host ""

Write-Host "Dashboard Model v9 generated successfully." -ForegroundColor Green

Write-Host ""

Write-Host "Health Modules :" $model.HealthScore.Count

Write-Host "TODO           :" $debt.TODO

Write-Host "Security Issues:" $security.Warnings