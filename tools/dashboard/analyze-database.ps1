# =============================================================================
# ZH Technologies
# Progress Dashboard v6
# Database Analyzer v1.0
# =============================================================================

$ErrorActionPreference = "Stop"


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



function Count-Files($path,$filter)
{
    if(Test-Path $path)
    {
        return (
            Get-ChildItem `
            -Path $path `
            -Recurse `
            -File `
            -Filter $filter |
            Measure-Object
        ).Count
    }

    return 0
}



function Search-Count($path,$pattern)
{
    if(Test-Path $path)
    {
        return (
            Select-String `
            -Path "$path\**\*.cs" `
            -Pattern $pattern `
            -SimpleMatch `
            -ErrorAction SilentlyContinue
        ).Count
    }

    return 0
}



$dbContext =
Search-Count $InfrastructureRoot "DbContext"



$dbSets =
Search-Count $InfrastructureRoot "DbSet<"



$configurations =
Search-Count $InfrastructureRoot "IEntityTypeConfiguration"



$repositories =
Search-Count $InfrastructureRoot "Repository"



$migrationsPath =
Join-Path $InfrastructureRoot "Migrations"



$migrations = 0

if(Test-Path $migrationsPath)
{
    $migrations =
    Count-Files $migrationsPath "*.cs"
}



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