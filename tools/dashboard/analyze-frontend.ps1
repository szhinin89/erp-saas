# =============================================================================
# ZH Technologies
# Progress Dashboard v4
# Frontend Analyzer v1.0
# =============================================================================

$ErrorActionPreference = "Stop"

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



function Count-Files($path, $pattern) {

    if (Test-Path $path) {

        return (
            Get-ChildItem `
            -Path $path `
            -Recurse `
            -File `
            -Include $pattern `
            -ErrorAction SilentlyContinue
        ).Count
    }

    return 0
}



function Get-Folders($path) {

    if (Test-Path $path) {

        return (
            Get-ChildItem `
            -Path $path `
            -Directory |
            Sort-Object Name |
            Select-Object -ExpandProperty Name
        )
    }

    return @()
}



$ignored = @(
    "node_modules",
    "dist",
    ".vite"
)



$sourceFiles = Get-ChildItem `
    -Path $FrontendRoot `
    -Recurse `
    -File `
    -Include *.ts,*.tsx |
    Where-Object {

        foreach($item in $ignored) {

            if ($_.FullName -like "*\$item\*") {
                return $false
            }
        }

        return $true
    }



$pagesPath = Join-Path $FrontendRoot "pages"
$componentsPath = Join-Path $FrontendRoot "components"
$hooksPath = Join-Path $FrontendRoot "hooks"
$servicesPath = Join-Path $FrontendRoot "services"
$storesPath = Join-Path $FrontendRoot "stores"
$routesPath = Join-Path $FrontendRoot "routes"



$pages = Count-Files $pagesPath "*.tsx"
$components = Count-Files $componentsPath "*.tsx"
$hooks = Count-Files $hooksPath "*.ts"
$services = Count-Files $servicesPath "*.ts"
$stores = Count-Files $storesPath "*.ts"
$routes = Count-Files $routesPath "*.ts"



$moduleFolders = @()


$moduleCandidates = @(
    "features",
    "modules"
)


foreach($folder in $moduleCandidates) {

    $target = Join-Path $FrontendRoot $folder

    if(Test-Path $target) {

        $moduleFolders += Get-Folders $target
    }
}



$result = [ordered]@{

    Generated = Get-Date -Format "yyyy-MM-dd HH:mm:ss"

    FrontendRoot = $FrontendRoot

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