# =============================================================================
# ZH Technologies
# Progress Dashboard v6
# Migration Analyzer v1.0
# =============================================================================

$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "source-population.ps1")


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



$migrationsScope = "backend/src/ERP.Infrastructure/Migrations"

$files =
@(Get-SourceFiles $ProjectRoot @($migrationsScope) @("*.cs"))

Register-SourcePopulation $ProjectRoot "analyze-migrations" @($migrationsScope) @("*.cs") $files



$migrations = @()



foreach($file in $files)
{

    $migrations += [ordered]@{

        Name = $file.BaseName

        SizeKB = [math]::Round(
            $file.Length / 1KB,
            2
        )

        # Fecha del ultimo commit del archivo: la fecha local no es reproducible.
        Modified =
        $(
            $relative = (Get-SourceRelativePath $ProjectRoot $file.FullName).Replace("\", "/")
            $committed = (Invoke-PopulationGit $ProjectRoot "log -1 --format=%cd ""--date=format:%Y-%m-%d %H:%M:%S"" -- ""$relative""").Trim()
            if($committed) { $committed } else { "sin commit" }
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