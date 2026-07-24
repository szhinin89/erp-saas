# =============================================================================
# ZH Technologies
# Progress Dashboard v4
# Architecture Analyzer v1.0
# =============================================================================

$ErrorActionPreference = "Stop"


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



function Count-CSFiles($path)
{
    if(Test-Path $path)
    {
        return (
            Get-ChildItem `
            -Path $path `
            -Recurse `
            -File `
            -Filter *.cs |
            Measure-Object
        ).Count
    }

    return 0
}



function Search-Code($path,$pattern)
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



$domain = Join-Path $BackendRoot "ERP.Domain"

$application = Join-Path $BackendRoot "ERP.Application"

$infrastructure = Join-Path $BackendRoot "ERP.Infrastructure"

$api = Join-Path $BackendRoot "ERP.API"



$violations = @()



$domainEF = Search-Code $domain "Microsoft.EntityFrameworkCore"

$domainAsp = Search-Code $domain "Microsoft.AspNetCore"



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
    Files = Count-CSFiles $domain
}


Application = @{

    Files = Count-CSFiles $application

    Commands = Search-Code $application "Command"

    Queries = Search-Code $application "Query"

    Validators = Search-Code $application "Validator"
}



Infrastructure = @{

    Files = Count-CSFiles $infrastructure

    DbContext = Search-Code $infrastructure "DbContext"

    Repository = Search-Code $infrastructure "Repository"
}



API = @{

    Files = Count-CSFiles $api

    Controllers = Search-Code $api "Controller"
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