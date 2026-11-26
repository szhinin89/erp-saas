# =============================================================================
# ZH Technologies
# Progress Dashboard v4
# Tests Analyzer v1.0
# =============================================================================

$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "source-population.ps1")


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



$testInclude = @("*.cs", "*.ts", "*.tsx")
$testFiles = @{}

function Count-TestFiles($scope) {

    $selected = @(Get-SourceFiles $ProjectRoot @($scope) $testInclude |
        Where-Object {

            $_.Name -match "Test|Tests|Spec|spec|test"

        })

    foreach($file in $selected) { $testFiles[$file.FullName] = $file }

    return $selected.Count
}



$backendTests = @()



Get-SourceDirectories $ProjectRoot "backend/src" |
Where-Object {

    $_.Name -like "*.Tests"

}|
ForEach-Object {


    $files = Count-TestFiles "backend/src/$($_.Name)"


    $backendTests += [ordered]@{

        Project = $_.Name

        Path = Get-SourceRelativePath $ProjectRoot $_.FullName

        TestFiles = $files
    }
}



$frontendTests = Count-TestFiles "frontend"

Register-SourcePopulation $ProjectRoot "analyze-tests" @("backend/src", "frontend") $testInclude @($testFiles.Values)



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