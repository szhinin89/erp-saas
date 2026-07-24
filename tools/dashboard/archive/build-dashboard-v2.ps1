# =============================================================================
# ZH Technologies
# Progress Dashboard v5
# Dashboard Builder v2
# =============================================================================

$ErrorActionPreference = "Stop"


$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path

$DataRoot = Join-Path $ProjectRoot "docs\ProgressDashboard\data"



Write-Host ""
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host " Dashboard Builder v2"
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host ""



function Load-Json($file)
{

    $path = Join-Path $DataRoot $file


    if(!(Test-Path $path))
    {
        throw "Missing file: $file"
    }


    return Get-Content $path -Raw | ConvertFrom-Json
}



$backend = Load-Json "backend-analysis.json"

$frontend = Load-Json "frontend-analysis.json"

$tests = Load-Json "tests-analysis.json"

$docs = Load-Json "docs-analysis.json"

$architecture = Load-Json "architecture-analysis.json"

$health = Load-Json "module-health.json"

$dependencies = Load-Json "dependency-analysis.json"



$avgHealth = (
    $health.Modules |
    Measure-Object Score -Average
).Average



$model = [ordered]@{


Generated = Get-Date -Format "yyyy-MM-dd HH:mm:ss"



Summary = [ordered]@{

    Projects = $backend.ProjectCount

    Modules = $backend.Modules.Count

    FrontendFiles = $frontend.Files

    Tests =
        $tests.Backend.TestFiles +
        $tests.Frontend.TestFiles

    ADRs = $docs.Documentation.ADRCount

    ModuleHealthAverage = [math]::Round($avgHealth,2)

    DependencyViolations =
        $dependencies.Summary.Total
}



Backend = $backend


Frontend = $frontend


Tests = $tests


Documentation = $docs


Architecture = $architecture


ModuleHealth = $health


Dependencies = $dependencies


}



$output = Join-Path $DataRoot "dashboard-model.json"



$model |
ConvertTo-Json -Depth 50 |
Set-Content $output -Encoding UTF8



Write-Host ""
Write-Host "Dashboard model v2 generated." -ForegroundColor Green

Write-Host ""

Write-Host "Health Average : $($model.Summary.ModuleHealthAverage)%"

Write-Host "Dependency Issues : $($model.Summary.DependencyViolations)"