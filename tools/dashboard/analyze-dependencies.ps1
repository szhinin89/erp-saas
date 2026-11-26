# =============================================================================
# ZH Technologies
# Progress Dashboard v4
# Dependency Analyzer v1.0
# =============================================================================

$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "source-population.ps1")


$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path

$BackendRoot = Join-Path $ProjectRoot "backend\src"

$DataRoot = Join-Path $ProjectRoot "docs\ProgressDashboard\data"


Write-Host ""
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host " Dependency Analyzer v1.0"
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host ""



$rules = [ordered]@{

    "ERP.Domain" = @(
        "Microsoft.EntityFrameworkCore",
        "Microsoft.AspNetCore",
        "Npgsql"
    )

    "ERP.Application" = @(
        "Microsoft.EntityFrameworkCore",
        "Npgsql",
        "Microsoft.AspNetCore.Http"
    )

}



$violations = @()



$analyzedScopes = @()
$analyzedFiles = @()

foreach($project in $rules.Keys)
{

    $scope = "backend/src/$project"

    $files = @(Get-SourceFiles $ProjectRoot @($scope) @("*.cs"))

    $analyzedScopes += $scope
    $analyzedFiles += $files


    foreach($rule in $rules[$project])
    {

        foreach($match in @(Find-ForbiddenImports $files $rule))
        {

            $violations += [ordered]@{

                Project = $project

                Rule = "Forbidden dependency"

                Reference = $rule

                File = Get-SourceRelativePath $ProjectRoot $match.File.FullName

            }

        }

    }

}

Register-SourcePopulation $ProjectRoot "analyze-dependencies" $analyzedScopes @("*.cs") $analyzedFiles



$result = [ordered]@{


Generated = Get-Date -Format "yyyy-MM-dd HH:mm:ss"


Violations = $violations


Summary = [ordered]@{

    Total = $violations.Count

}


}



$output = Join-Path $DataRoot "dependency-analysis.json"



$result |
ConvertTo-Json -Depth 30 |
Set-Content $output -Encoding UTF8



Write-Host ""

Write-Host "Dependency analysis generated." -ForegroundColor Green

Write-Host "Violations : $($violations.Count)"