# =============================================================================
# ZH Technologies
# Progress Dashboard v12
# Dashboard Builder v12
# =============================================================================

$ErrorActionPreference = "Stop"


$ProjectRoot =
(Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path


$DataRoot =
Join-Path $ProjectRoot "docs\ProgressDashboard\data"



Write-Host ""
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host " Dashboard Builder v12"
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



$model =
[ordered]@{


Generated =
Get-Date -Format "yyyy-MM-dd HH:mm:ss"


EngineeringScore =
LoadJson "engineering-score.json"


QualityGate =
LoadJson "quality-gate.json"


Trend =
LoadJson "engineering-trend.json"


Health =
LoadJson "health-score.json"


Security =
LoadJson "security-analysis.json"


TechnicalDebt =
LoadJson "technical-debt.json"


Architecture =
LoadJson "architecture-analysis.json"


Dependencies =
LoadJson "dependency-analysis.json"


}



$output =
Join-Path `
$DataRoot `
"dashboard-model-v12.json"



$model |
ConvertTo-Json -Depth 100 |
Set-Content `
$output `
-Encoding UTF8



Write-Host ""

Write-Host "Dashboard Model v12 generated successfully." -ForegroundColor Green

Write-Host ""

Write-Host "Engineering Score :" `
$model.EngineeringScore.Overall "%"

Write-Host "Quality Status    :" `
$model.QualityGate.Status

Write-Host ""

Write-Host "Output:"
Write-Host $output