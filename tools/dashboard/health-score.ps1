# =============================================================================
# ZH Technologies
# Progress Dashboard v7
# Health Scoring Engine v1.0
# =============================================================================

$ErrorActionPreference = "Stop"


$ProjectRoot =
(Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path


$DataRoot =
Join-Path $ProjectRoot "docs\ProgressDashboard\data"



Write-Host ""
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host " Health Scoring Engine v1.0"
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host ""



function LoadJson($file)
{

    return Get-Content `
    (Join-Path $DataRoot $file) `
    -Raw |
    ConvertFrom-Json

}



$modules =
LoadJson "module-health.json"



$tests =
LoadJson "tests-analysis.json"



$result = @()



foreach($module in $modules.Modules)
{


    $architecture = 90

    $backend = $module.Score


    $frontend = 70


    $documentation = 70


    $testScore = 80



    $final =
    (
        ($architecture * 0.30) +
        ($testScore * 0.25) +
        ($documentation * 0.15) +
        ($backend * 0.15) +
        ($frontend * 0.15)
    )



    $result += [ordered]@{

        Module = $module.Name

        Architecture = $architecture

        Tests = $testScore

        Documentation = $documentation

        Backend = $backend

        Frontend = $frontend

        Score =
        [math]::Round($final,2)

    }


}



$output =
Join-Path $DataRoot "health-score.json"



$result |
ConvertTo-Json -Depth 20 |
Set-Content $output -Encoding UTF8



Write-Host ""

Write-Host "Health scoring generated successfully." -ForegroundColor Green

Write-Host ""

Write-Host "Modules scored:"
Write-Host $result.Count