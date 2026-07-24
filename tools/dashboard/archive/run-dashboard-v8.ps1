# =============================================================================
# ZH Technologies
# Progress Dashboard v8
# Full Engineering Pipeline
# =============================================================================

$ErrorActionPreference = "Stop"


$ScriptRoot = $PSScriptRoot



Write-Host ""
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host " ZH ERP Engineering Dashboard v8"
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host ""



function Run-Step($script)
{

    Write-Host ""
    Write-Host "Running $script" -ForegroundColor Yellow


    powershell `
    -ExecutionPolicy Bypass `
    -File (Join-Path $ScriptRoot $script)


    if($LASTEXITCODE -ne 0)
    {
        throw "$script failed"
    }

}



# ERP Analysis

Run-Step "analyze-backend.ps1"

Run-Step "analyze-frontend.ps1"

Run-Step "analyze-tests.ps1"

Run-Step "analyze-docs.ps1"

Run-Step "analyze-architecture.ps1"

Run-Step "analyze-dependencies.ps1"

Run-Step "analyze-module-health.ps1"

Run-Step "analyze-database.ps1"

Run-Step "analyze-api.ps1"

Run-Step "analyze-migrations.ps1"



# History

Run-Step "snapshot-dashboard.ps1"

Run-Step "compare-dashboard.ps1"



# Intelligence

Run-Step "health-score.ps1"



# Dashboard

Run-Step "build-dashboard-v7.ps1"

Run-Step "render-dashboard-v7.ps1"



# Reports

Run-Step "export-dashboard-report.ps1"



Write-Host ""

Write-Host "==============================================" -ForegroundColor Green
Write-Host " Dashboard v8 completed successfully"
Write-Host "==============================================" -ForegroundColor Green


Write-Host ""

Write-Host "Generated:"
Write-Host ""

Write-Host "Dashboard:"
Write-Host "docs\ProgressDashboard\index.html"

Write-Host ""

Write-Host "Reports:"
Write-Host "docs\ProgressDashboard\reports"