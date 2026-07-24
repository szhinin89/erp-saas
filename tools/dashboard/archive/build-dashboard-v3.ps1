# =============================================================================
# ZH Technologies
# Progress Dashboard v6
# Dashboard Builder v3
# =============================================================================

$ErrorActionPreference = "Stop"


$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path

$DataRoot = Join-Path $ProjectRoot "docs\ProgressDashboard\data"



Write-Host ""
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host " Dashboard Builder v3"
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host ""



function Load-Json($file)
{

    $path = Join-Path $DataRoot $file

    if(!(Test-Path $path))
    {
        throw "Missing $file"
    }

    return Get-Content $path -Raw | ConvertFrom-Json
}



$backend =
Load-Json "backend-analysis.json"


$frontend =
Load-Json "frontend-analysis.json"


$tests =
Load-Json "tests-analysis.json"


$docs =
Load-Json "docs-analysis.json"


$architecture =
Load-Json "architecture-analysis.json"


$health =
Load-Json "module-health.json"


$dependencies =
Load-Json "dependency-analysis.json"


$git =
Load-Json "git-analysis.json"


$database =
Load-Json "database-analysis.json"


$api =
Load-Json "api-analysis.json"


$migrations =
Load-Json "migration-analysis.json"



$healthAverage =
(
    $health.Modules |
    Measure-Object Score -Average
).Average



$model = [ordered]@{


Generated =
Get-Date -Format "yyyy-MM-dd HH:mm:ss"



Summary = [ordered]@{


Projects =
$backend.ProjectCount


Modules =
$backend.Modules.Count


FrontendFiles =
$frontend.Files


HealthAverage =
[Math]::Round($healthAverage,2)


DependencyIssues =
$dependencies.Summary.Total


APIEndpoints =
$api.API.Endpoints


Migrations =
$migrations.Summary.TotalFiles


}



Backend = $backend

Frontend = $frontend

Tests = $tests

Documentation = $docs

Architecture = $architecture

ModuleHealth = $health

Dependencies = $dependencies

Git = $git

Database = $database

API = $api

Migrations = $migrations



}



$output =
Join-Path $DataRoot "dashboard-model.json"



$model |
ConvertTo-Json -Depth 60 |
Set-Content $output -Encoding UTF8



Write-Host ""

Write-Host "Dashboard Model v3 generated successfully." -ForegroundColor Green

Write-Host ""

Write-Host "Health       : $($model.Summary.HealthAverage)%"

Write-Host "API Endpoints: $($model.Summary.APIEndpoints)"

Write-Host "Migrations   : $($model.Summary.Migrations)"