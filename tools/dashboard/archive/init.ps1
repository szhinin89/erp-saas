# =============================================================================
# ZH Technologies
# Dashboard Init
# =============================================================================

$ErrorActionPreference = "Stop"

$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$DashboardRoot = Join-Path $ProjectRoot "docs\ProgressDashboard"
$DataFolder = Join-Path $DashboardRoot "data"

Write-Host ""
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host " Progress Dashboard v4 Initialization"
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host ""

if (!(Test-Path $DataFolder)) {
    throw "No existe la carpeta: $DataFolder. Ejecute primero create-dashboard-engine.ps1"
}

$StateFile = Join-Path $DataFolder "dashboard-state.json"

$State = @{
    Project = "ZH ERP SaaS"
    Version = "4.0"
    Created = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")

    Dashboard = @{
        Status = "Initialized"
        Progress = 0
        ProductionReadiness = 0
        Maturity = 0
    }

    Statistics = @{
        Layers = 0
        Domains = 0
        Modules = 0
        Processes = 0
        Features = 0
        Tasks = 0
    }

    LastBuild = $null
}

$State |
    ConvertTo-Json -Depth 10 |
    Set-Content $StateFile -Encoding UTF8

Write-Host ""
Write-Host "Dashboard initialized successfully." -ForegroundColor Green
Write-Host "File:"
Write-Host $StateFile -ForegroundColor Yellow
Write-Host ""