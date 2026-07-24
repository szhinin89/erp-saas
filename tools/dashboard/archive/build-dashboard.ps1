# =============================================================================
# ZH Technologies
# Progress Dashboard v4
# Dashboard Builder v1.0
# =============================================================================

$ErrorActionPreference = "Stop"


$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path

$DataRoot = Join-Path $ProjectRoot "docs\ProgressDashboard\data"


Write-Host ""
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host " Dashboard Builder v1.0"
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host ""



function Read-Json($file)
{
    $path = Join-Path $DataRoot $file

    if(!(Test-Path $path))
    {
        throw "Missing data file: $file"
    }

    return Get-Content $path -Raw | ConvertFrom-Json
}



$backend = Read-Json "backend-analysis.json"

$frontend = Read-Json "frontend-analysis.json"

$tests = Read-Json "tests-analysis.json"

$docs = Read-Json "docs-analysis.json"



$model = [ordered]@{


    Generated = Get-Date -Format "yyyy-MM-dd HH:mm:ss"


    Backend = $backend


    Frontend = $frontend


    Tests = $tests


    Documentation = $docs


    Summary = [ordered]@{

        Projects = $backend.ProjectCount

        Modules = $backend.ModuleCount

        FrontendFiles = $frontend.Statistics.SourceFiles

        BackendTests = $tests.Backend.TestFiles

        FrontendTests = $tests.Frontend.TestFiles

        ADRs = $docs.Documentation.ADRCount
    }

}



$output = Join-Path $DataRoot "dashboard-model.json"



$model |
ConvertTo-Json -Depth 50 |
Set-Content $output -Encoding UTF8



Write-Host ""
Write-Host "Dashboard Model generated successfully." -ForegroundColor Green

Write-Host ""

Write-Host "Summary" -ForegroundColor Yellow

Write-Host "Projects : $($model.Summary.Projects)"
Write-Host "Modules  : $($model.Summary.Modules)"
Write-Host "Frontend : $($model.Summary.FrontendFiles) files"
Write-Host "Tests    : $($model.Summary.BackendTests + $model.Summary.FrontendTests)"
Write-Host "ADR      : $($model.Summary.ADRs)"