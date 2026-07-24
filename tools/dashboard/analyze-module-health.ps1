# =============================================================================
# ZH Technologies
# Progress Dashboard v4
# Module Health Analyzer v1.0
# =============================================================================

$ErrorActionPreference = "Stop"


$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path

$DataRoot = Join-Path $ProjectRoot "docs\ProgressDashboard\data"


Write-Host ""
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host " Module Health Analyzer v1.0"
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



$backend = Load-Json "backend-analysis.json"

$frontend = Load-Json "frontend-analysis.json"

$tests = Load-Json "tests-analysis.json"



$modules = @()



foreach($module in $backend.Modules)
{

    $name = $module.Name


    $score = 0


    $domain = $module.Layers -contains "Domain"

    $application = $module.Layers -contains "Application"


    if($domain -or $application)
    {
        $score += 40
    }


    $frontendExists =
        $frontend.Modules -contains $name.ToLower()



    if($frontendExists)
    {
        $score += 25
    }



    $testExists =
        $tests.Backend.Projects.Count -gt 0



    if($testExists)
    {
        $score += 25
    }



    $modules += [ordered]@{

        Name = $name

        Domain = $domain

        Application = $application

        Frontend = $frontendExists

        Score = $score
    }

}



$result = [ordered]@{


Generated = Get-Date -Format "yyyy-MM-dd HH:mm:ss"


Modules = $modules



}



$output = Join-Path $DataRoot "module-health.json"



$result |
ConvertTo-Json -Depth 20 |
Set-Content $output -Encoding UTF8



Write-Host ""

Write-Host "Modules analyzed : $($modules.Count)" -ForegroundColor Green

foreach($m in $modules)
{
    Write-Host "$($m.Name) : $($m.Score)%"
}



Write-Host ""

Write-Host "module-health.json generated successfully." -ForegroundColor Green