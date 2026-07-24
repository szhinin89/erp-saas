# =============================================================================
# ZH Technologies
# Progress Dashboard v4
# Tests Analyzer v1.0
# =============================================================================

$ErrorActionPreference = "Stop"


$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path


$BackendRoot = Join-Path $ProjectRoot "backend\src"
$FrontendRoot = Join-Path $ProjectRoot "frontend"

$DashboardData = Join-Path $ProjectRoot "docs\ProgressDashboard\data"



Write-Host ""
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host " Tests Analyzer v1.0"
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host ""



if (!(Test-Path $DashboardData)) {
    New-Item -ItemType Directory -Path $DashboardData | Out-Null
}



function Count-TestFiles($path) {

    if (!(Test-Path $path)) {
        return 0
    }


    return (
        Get-ChildItem `
        -Path $path `
        -Recurse `
        -File `
        -Include *.cs,*.ts,*.tsx |
        Where-Object {

            $_.Name -match "Test|Tests|Spec|spec|test"

        }
    ).Count
}



$backendTests = @()



if(Test-Path $BackendRoot)
{

    Get-ChildItem $BackendRoot -Directory |
    Where-Object {

        $_.Name -like "*.Tests"

    }|
    ForEach-Object {


        $files = Count-TestFiles $_.FullName


        $backendTests += [ordered]@{

            Project = $_.Name

            Path = $_.FullName

            TestFiles = $files
        }
    }
}



$frontendTests = Count-TestFiles $FrontendRoot



$backendFileCount = 0

foreach($test in $backendTests)
{
    $backendFileCount += $test.TestFiles
}



$result = [ordered]@{


    Generated = Get-Date -Format "yyyy-MM-dd HH:mm:ss"


    Backend = [ordered]@{

        TestProjects = $backendTests.Count

        TestFiles = $backendFileCount

        Projects = $backendTests
    }


    Frontend = [ordered]@{

        TestFiles = $frontendTests
    }


}



$output = Join-Path $DashboardData "tests-analysis.json"



$result |
ConvertTo-Json -Depth 20 |
Set-Content $output -Encoding UTF8



Write-Host ""
Write-Host "Backend Test Projects : $($backendTests.Count)" -ForegroundColor Green
Write-Host "Backend Test Files    : $backendFileCount"
Write-Host "Frontend Test Files   : $frontendTests"

Write-Host ""
Write-Host "tests-analysis.json generated successfully." -ForegroundColor Green