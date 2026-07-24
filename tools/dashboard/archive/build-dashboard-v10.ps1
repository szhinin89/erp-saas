# =============================================================================
# ZH Technologies
# Progress Dashboard v10
# Dashboard Builder v10
# =============================================================================

$ErrorActionPreference = "Stop"


$ProjectRoot =
(Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path


$DataRoot =
Join-Path $ProjectRoot "docs\ProgressDashboard\data"


$Output =
Join-Path $DataRoot "dashboard-model-v10.json"



Write-Host ""
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host " Dashboard Builder v10"
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host ""



function LoadJson($file)
{
    $path = Join-Path $DataRoot $file

    if(Test-Path $path)
    {
        return Get-Content $path -Raw |
        ConvertFrom-Json
    }

    return $null
}



$backend =
LoadJson "backend-analysis.json"


$frontend =
LoadJson "frontend-analysis.json"


$tests =
LoadJson "tests-analysis.json"


$health =
LoadJson "health-score.json"


$security =
LoadJson "security-analysis.json"


$technicalDebt =
LoadJson "technical-debt.json"


$engineering =
LoadJson "engineering-score.json"


$dashboard =
[ordered]@{


Generated =
Get-Date -Format "yyyy-MM-dd HH:mm:ss"



EngineeringScore =
$engineering



Backend =
$backend



Frontend =
$frontend



Tests =
$tests



Health =
$health



Security =
$security



TechnicalDebt =
$technicalDebt



}



$dashboard |
ConvertTo-Json -Depth 50 |
Set-Content `
$Output `
-Encoding UTF8



Write-Host ""

Write-Host "Dashboard Model v10 generated successfully." -ForegroundColor Green

Write-Host ""

if($engineering)
{
    Write-Host "Engineering Score :" `
    $engineering.Overall "%"
}

Write-Host "Output:"
Write-Host $Output