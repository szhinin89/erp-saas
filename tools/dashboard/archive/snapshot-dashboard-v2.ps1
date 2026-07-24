# =============================================================================
# ZH Technologies
# Progress Dashboard v11
# Snapshot Engine v2.0
# =============================================================================

$ErrorActionPreference = "Stop"


$ProjectRoot =
(Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path


$DataRoot =
Join-Path $ProjectRoot "docs\ProgressDashboard\data"


$HistoryRoot =
Join-Path $ProjectRoot "docs\ProgressDashboard\history"



Write-Host ""
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host " Snapshot Engine v2.0"
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host ""



if(!(Test-Path $HistoryRoot))
{
    New-Item `
    -ItemType Directory `
    $HistoryRoot |
    Out-Null
}



function LoadJson($file)
{
    $path =
    Join-Path $DataRoot $file


    if(Test-Path $path)
    {
        return Get-Content $path -Raw |
        ConvertFrom-Json
    }


    return $null
}



$dashboard =
LoadJson "dashboard-model-v10.json"


$engineering =
LoadJson "engineering-score.json"


$health =
LoadJson "health-score.json"


$security =
LoadJson "security-analysis.json"


$technicalDebt =
LoadJson "technical-debt.json"



$timestamp =
Get-Date -Format "yyyy-MM-dd-HHmm"



$fileName =
"dashboard-$timestamp-v2.json"



$output =
Join-Path $HistoryRoot $fileName



$snapshot =
[ordered]@{

Generated =
Get-Date -Format "yyyy-MM-dd HH:mm:ss"


EngineeringScore =
$engineering


Health =
$health


Security =
$security


TechnicalDebt =
$technicalDebt


Dashboard =
$dashboard


}



$snapshot |
ConvertTo-Json -Depth 100 |
Set-Content `
$output `
-Encoding UTF8



$count =
(Get-ChildItem `
$HistoryRoot `
-Filter "*-v2.json").Count



Write-Host ""

Write-Host "Snapshot created successfully." -ForegroundColor Green

Write-Host ""

Write-Host "File:"
Write-Host $output

Write-Host ""

Write-Host "Total v2 snapshots:"
Write-Host $count