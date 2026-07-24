# =============================================================================
# ZH Technologies
# Progress Dashboard v6
# API Analyzer v1.0
# =============================================================================

$ErrorActionPreference = "Stop"


$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path

$ApiRoot = Join-Path $ProjectRoot "backend\src\ERP.API"

$DataRoot = Join-Path $ProjectRoot "docs\ProgressDashboard\data"


Write-Host ""
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host " API Analyzer v1.0"
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host ""



if(!(Test-Path $ApiRoot))
{
    throw "API project not found"
}



function Count-Code($pattern)
{
    return (
        Select-String `
        -Path "$ApiRoot\**\*.cs" `
        -Pattern $pattern `
        -SimpleMatch `
        -ErrorAction SilentlyContinue
    ).Count
}



$controllers =
Count-Code "Controller"



$httpMethods =
@(
    "HttpGet",
    "HttpPost",
    "HttpPut",
    "HttpDelete",
    "HttpPatch"
)



$endpoints = 0


foreach($method in $httpMethods)
{
    $endpoints += Count-Code $method
}



$authorize =
Count-Code "Authorize"



$swagger =
Count-Code "Swagger"



$middleware =
Count-Code "Middleware"



$result = [ordered]@{


Generated =
Get-Date -Format "yyyy-MM-dd HH:mm:ss"



API = [ordered]@{


Controllers = $controllers


Endpoints = $endpoints


AuthorizeAttributes = $authorize


SwaggerReferences = $swagger


MiddlewareReferences = $middleware


}



}



$output =
Join-Path $DataRoot "api-analysis.json"



$result |
ConvertTo-Json -Depth 20 |
Set-Content $output -Encoding UTF8



Write-Host ""

Write-Host "API analysis generated successfully." -ForegroundColor Green

Write-Host ""

Write-Host "Controllers          : $controllers"

Write-Host "Endpoints            : $endpoints"

Write-Host "Authorize            : $authorize"

Write-Host "Swagger              : $swagger"

Write-Host "Middleware           : $middleware"