# =============================================================================
# ZH Technologies
# Progress Dashboard v4
# Architecture Analyzer v1.0
# =============================================================================

$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "source-population.ps1")


$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path

$BackendRoot = Join-Path $ProjectRoot "backend\src"

$DashboardData = Join-Path $ProjectRoot "docs\ProgressDashboard\data"


Write-Host ""
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host " Architecture Analyzer v1.0"
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host ""



if(!(Test-Path $BackendRoot))
{
    throw "Backend not found"
}



function Get-LayerFiles($scope)
{
    return @(Get-SourceFiles $ProjectRoot @($scope) @("*.cs"))
}



$layerScopes = @(
    "backend/src/ERP.Domain",
    "backend/src/ERP.Application",
    "backend/src/ERP.Infrastructure",
    "backend/src/ERP.API"
)

$domain = @(Get-LayerFiles $layerScopes[0])

$application = @(Get-LayerFiles $layerScopes[1])

$infrastructure = @(Get-LayerFiles $layerScopes[2])

$api = @(Get-LayerFiles $layerScopes[3])

Register-SourcePopulation $ProjectRoot "analyze-architecture" $layerScopes @("*.cs") ($domain + $application + $infrastructure + $api)



$violations = @()



$domainEF = @(Find-ForbiddenImports $domain "Microsoft.EntityFrameworkCore").Count

$domainAsp = @(Find-ForbiddenImports $domain "Microsoft.AspNetCore").Count



if($domainEF -gt 0)
{
    $violations += @{
        Layer="Domain"
        Rule="EF Core dependency"
        Count=$domainEF
    }
}



if($domainAsp -gt 0)
{
    $violations += @{
        Layer="Domain"
        Rule="ASP.NET dependency"
        Count=$domainAsp
    }
}



$result = [ordered]@{


Generated = Get-Date -Format "yyyy-MM-dd HH:mm:ss"



Layers = [ordered]@{


Domain = @{
    Files = $domain.Count
}


Application = @{

    Files = $application.Count

    Commands = Measure-SourceLines $application "Command"

    Queries = Measure-SourceLines $application "Query"

    Validators = Measure-SourceLines $application "Validator"
}



Infrastructure = @{

    Files = $infrastructure.Count

    DbContext = Measure-SourceLines $infrastructure "DbContext"

    Repository = Measure-SourceLines $infrastructure "Repository"
}



API = @{

    Files = $api.Count

    Controllers = Measure-SourceLines $api "Controller"
}


}



Violations = $violations

}



$output = Join-Path $DashboardData "architecture-analysis.json"



$result |
ConvertTo-Json -Depth 30 |
Set-Content $output -Encoding UTF8



Write-Host ""
Write-Host "Architecture analysis generated." -ForegroundColor Green

Write-Host ""

Write-Host "Domain Files        : $($result.Layers.Domain.Files)"
Write-Host "Application Commands : $($result.Layers.Application.Commands)"
Write-Host "Application Queries  : $($result.Layers.Application.Queries)"
Write-Host "Infrastructure DB   : $($result.Layers.Infrastructure.DbContext)"
Write-Host "API Controllers     : $($result.Layers.API.Controllers)"
Write-Host "Violations          : $($violations.Count)"