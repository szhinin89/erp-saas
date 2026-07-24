# =============================================================================
# ZH Technologies
# Progress Dashboard v6
# Migration Analyzer v1.0
# =============================================================================

$ErrorActionPreference = "Stop"


$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path

$InfrastructureRoot = Join-Path $ProjectRoot "backend\src\ERP.Infrastructure"

$DataRoot = Join-Path $ProjectRoot "docs\ProgressDashboard\data"


Write-Host ""
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host " Migration Analyzer v1.0"
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host ""



$migrationsFolder = Join-Path $InfrastructureRoot "Migrations"



if(!(Test-Path $migrationsFolder))
{
    throw "Migrations folder not found"
}



$files =
Get-ChildItem `
-Path $migrationsFolder `
-Recurse `
-File `
-Filter "*.cs"



$migrations = @()



foreach($file in $files)
{

    $migrations += [ordered]@{

        Name = $file.BaseName

        SizeKB = [math]::Round(
            $file.Length / 1KB,
            2
        )

        Modified =
        $file.LastWriteTime.ToString(
            "yyyy-MM-dd HH:mm:ss"
        )

    }

}



$result = [ordered]@{


Generated =
Get-Date -Format "yyyy-MM-dd HH:mm:ss"



Summary = [ordered]@{

    TotalFiles = $migrations.Count

}



Migrations = $migrations



}



$output =
Join-Path $DataRoot "migration-analysis.json"



$result |
ConvertTo-Json -Depth 30 |
Set-Content $output -Encoding UTF8



Write-Host ""

Write-Host "Migration analysis generated successfully." -ForegroundColor Green

Write-Host ""

Write-Host "Migration Files : $($migrations.Count)"