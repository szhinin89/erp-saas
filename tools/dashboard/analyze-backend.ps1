# =============================================================================
# ZH Technologies
# Progress Dashboard v4
# Backend Analyzer v1.1
# =============================================================================

$ErrorActionPreference = "Stop"

$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$BackendRoot = Join-Path $ProjectRoot "backend\src"
$DashboardData = Join-Path $ProjectRoot "docs\ProgressDashboard\data"

Write-Host ""
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host " Backend Analyzer v1.1"
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host ""

if (!(Test-Path $BackendRoot)) {
    throw "Backend folder not found: $BackendRoot"
}

if (!(Test-Path $DashboardData)) {
    New-Item -ItemType Directory -Path $DashboardData | Out-Null
}


function Get-ProjectType($name) {

    if ($name -eq "TestResults") {
        return "Generated"
    }

    if ($name -like "*.Tests") {
        return "Test"
    }

    return "Production"
}


function Get-Layer($name) {

    switch -Wildcard ($name) {

        "ERP.Domain" {
            return "Domain"
        }

        "ERP.Application" {
            return "Application"
        }

        "ERP.Infrastructure" {
            return "Infrastructure"
        }

        "ERP.API" {
            return "API"
        }

        default {
            return "Other"
        }
    }
}


$projects = @()
$moduleRegistry = @{}

$ignored = @(
    "bin",
    "obj",
    "TestResults"
)


Get-ChildItem $BackendRoot -Directory |
Where-Object {
    $ignored -notcontains $_.Name
}|
ForEach-Object {

    $projectName = $_.Name
    $layer = Get-Layer $projectName
    $type = Get-ProjectType $projectName


    $project = [ordered]@{
        Name = $projectName
        Path = $_.FullName
        Layer = $layer
        Type = $type
        Modules = @()
    }


    $modulesFolder = Join-Path $_.FullName "Modules"


    if (Test-Path $modulesFolder) {

        Get-ChildItem $modulesFolder -Directory |
        Sort-Object Name |
        ForEach-Object {

            $moduleName = $_.Name


            $module = [ordered]@{
                Name = $moduleName
                Path = $_.FullName
            }


            $project.Modules += $module


            if (!$moduleRegistry.ContainsKey($moduleName)) {

                $moduleRegistry[$moduleName] = [ordered]@{
                    Name = $moduleName
                    Layers = @()
                    Projects = @()
                }
            }


            $moduleRegistry[$moduleName].Layers += $layer
            $moduleRegistry[$moduleName].Projects += $projectName
        }
    }


    $projects += $project
}



$modules = @()

foreach ($entry in $moduleRegistry.Values) {

    $modules += [ordered]@{
        Name = $entry.Name
        Layers = $entry.Layers | Sort-Object -Unique
        Projects = $entry.Projects | Sort-Object -Unique
    }
}



$result = [ordered]@{

    Generated = Get-Date -Format "yyyy-MM-dd HH:mm:ss"

    BackendRoot = $BackendRoot

    ProjectCount = $projects.Count

    ModuleCount = $modules.Count

    Projects = $projects

    Modules = $modules
}



$output = Join-Path $DashboardData "backend-analysis.json"


$result |
ConvertTo-Json -Depth 30 |
Set-Content $output -Encoding UTF8



Write-Host ""
Write-Host "Projects Found : $($projects.Count)" -ForegroundColor Green
Write-Host "Modules Found  : $($modules.Count)" -ForegroundColor Green


foreach($p in $projects)
{
    Write-Host ""
    Write-Host "$($p.Name)" -ForegroundColor Yellow
    Write-Host "Layer : $($p.Layer)"
    Write-Host "Type  : $($p.Type)"
    Write-Host "Modules : $($p.Modules.Count)"

    foreach($m in $p.Modules)
    {
        Write-Host "   - $($m.Name)"
    }
}


Write-Host ""
Write-Host "backend-analysis.json generated successfully." -ForegroundColor Green