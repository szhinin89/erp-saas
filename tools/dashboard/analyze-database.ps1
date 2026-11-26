# =============================================================================
# ZH Technologies
# Progress Dashboard v6
# Database Analyzer v1.0
# =============================================================================

$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "source-population.ps1")


$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path

$InfrastructureRoot = Join-Path $ProjectRoot "backend\src\ERP.Infrastructure"

$DataRoot = Join-Path $ProjectRoot "docs\ProgressDashboard\data"


Write-Host ""
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host " Database Analyzer v1.0"
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host ""



if(!(Test-Path $InfrastructureRoot))
{
    throw "Infrastructure project not found"
}



$infrastructureFiles =
@(Get-SourceFiles $ProjectRoot @("backend/src/ERP.Infrastructure") @("*.cs"))

Register-SourcePopulation $ProjectRoot "analyze-database" @("backend/src/ERP.Infrastructure") @("*.cs") $infrastructureFiles



$dbContext =
Measure-SourceLines $infrastructureFiles "DbContext"



$dbSets =
Measure-SourceLines $infrastructureFiles "DbSet<"



$configurations =
Measure-SourceLines $infrastructureFiles "IEntityTypeConfiguration"



$repositories =
Measure-SourceLines $infrastructureFiles "Repository"



$migrations =
@(Get-SourceFiles $ProjectRoot @("backend/src/ERP.Infrastructure/Migrations") @("*.cs")).Count



$result = [ordered]@{


Generated =
Get-Date -Format "yyyy-MM-dd HH:mm:ss"



Database = [ordered]@{


DbContext = $dbContext


DbSets = $dbSets


EntityConfigurations = $configurations


Repositories = $repositories


Migrations = $migrations


}



}



$output =
Join-Path $DataRoot "database-analysis.json"



$result |
ConvertTo-Json -Depth 20 |
Set-Content $output -Encoding UTF8



Write-Host ""

Write-Host "Database analysis generated successfully." -ForegroundColor Green

Write-Host ""

Write-Host "DbContext          : $dbContext"

Write-Host "DbSets             : $dbSets"

Write-Host "Configurations     : $configurations"

Write-Host "Repositories       : $repositories"

Write-Host "Migration Files    : $migrations"