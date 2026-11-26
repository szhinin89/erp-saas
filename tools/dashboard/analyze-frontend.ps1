# =============================================================================
# ZH Technologies
# Progress Dashboard v4
# Frontend Analyzer v1.0
# =============================================================================

$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "source-population.ps1")

$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path

$FrontendRoot = Join-Path $ProjectRoot "frontend\src"
$DashboardData = Join-Path $ProjectRoot "docs\ProgressDashboard\data"


Write-Host ""
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host " Frontend Analyzer v1.0"
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host ""


if (!(Test-Path $FrontendRoot)) {
    throw "Frontend folder not found: $FrontendRoot"
}


if (!(Test-Path $DashboardData)) {
    New-Item -ItemType Directory -Path $DashboardData | Out-Null
}



$frontendScope = "frontend/src"


function Count-Files($folder, $pattern) {

    return @(Get-SourceFiles $ProjectRoot @("$frontendScope/$folder") @($pattern)).Count
}



function Get-Folders($folder) {

    return @(Get-SourceDirectories $ProjectRoot "$frontendScope/$folder" |
        Select-Object -ExpandProperty Name)
}



$sourceFiles = @(Get-SourceFiles $ProjectRoot @($frontendScope) @("*.ts", "*.tsx"))

Register-SourcePopulation $ProjectRoot "analyze-frontend" @($frontendScope) @("*.ts", "*.tsx") $sourceFiles



$pages = Count-Files "pages" "*.tsx"
$components = Count-Files "components" "*.tsx"
$hooks = Count-Files "hooks" "*.ts"
$services = Count-Files "services" "*.ts"
$stores = Count-Files "stores" "*.ts"
$routes = Count-Files "routes" "*.ts"



$moduleFolders = @()


$moduleCandidates = @(
    "features",
    "modules"
)


foreach($folder in $moduleCandidates) {

    $moduleFolders += Get-Folders $folder
}



$result = [ordered]@{

    Generated = Get-Date -Format "yyyy-MM-dd HH:mm:ss"

    FrontendRoot = Get-SourceRelativePath $ProjectRoot $FrontendRoot

    Statistics = [ordered]@{

        SourceFiles = $sourceFiles.Count

        Pages = $pages

        Components = $components

        Hooks = $hooks

        Services = $services

        Stores = $stores

        Routes = $routes
    }


    Modules = $moduleFolders | Sort-Object -Unique
}



$output = Join-Path $DashboardData "frontend-analysis.json"


$result |
ConvertTo-Json -Depth 20 |
Set-Content $output -Encoding UTF8



Write-Host ""
Write-Host "Frontend Files : $($sourceFiles.Count)" -ForegroundColor Green
Write-Host "Pages          : $pages"
Write-Host "Components     : $components"
Write-Host "Hooks          : $hooks"
Write-Host "Services       : $services"
Write-Host "Stores         : $stores"
Write-Host "Routes         : $routes"


Write-Host ""

if($moduleFolders.Count -gt 0) {

    Write-Host "Modules detected:" -ForegroundColor Yellow

    foreach($module in ($moduleFolders | Sort-Object -Unique)) {

        Write-Host "   - $module"
    }
}


Write-Host ""
Write-Host "frontend-analysis.json generated successfully." -ForegroundColor Green