# =============================================================================
# ZH Technologies
# Progress Dashboard v4
# Dependency Analyzer v1.0
# =============================================================================

$ErrorActionPreference = "Stop"


$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path

$BackendRoot = Join-Path $ProjectRoot "backend\src"

$DataRoot = Join-Path $ProjectRoot "docs\ProgressDashboard\data"


Write-Host ""
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host " Dependency Analyzer v1.0"
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host ""



$rules = @{

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



foreach($project in $rules.Keys)
{

    $path = Join-Path $BackendRoot $project


    if(Test-Path $path)
    {

        foreach($rule in $rules[$project])
        {

            $matches = Select-String `
                -Path "$path\**\*.cs" `
                -Pattern $rule `
                -SimpleMatch `
                -ErrorAction SilentlyContinue


            foreach($match in $matches)
            {

                $violations += [ordered]@{

                    Project = $project

                    Rule = "Forbidden dependency"

                    Reference = $rule

                    File = $match.Path.Replace($ProjectRoot,"")

                }

            }

        }

    }

}



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