# =============================================================================
# ZH Technologies
# Progress Dashboard v6
# Full Dashboard Runner v3
# =============================================================================

$ErrorActionPreference = "Stop"


$ScriptRoot = $PSScriptRoot


Write-Host ""
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host " ZH ERP Engineering Dashboard v3"
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



Run-Step "analyze-backend.ps1"

Run-Step "analyze-frontend.ps1"

Run-Step "analyze-tests.ps1"

Run-Step "analyze-docs.ps1"


Run-Step "analyze-architecture.ps1"

Run-Step "analyze-dependencies.ps1"

Run-Step "analyze-module-health.ps1"


Run-Step "analyze-git.ps1"

Run-Step "analyze-database.ps1"

Run-Step "analyze-api.ps1"

Run-Step "analyze-migrations.ps1"



Run-Step "build-dashboard-v3.ps1"

Run-Step "render-dashboard-v3.ps1"



Write-Host ""

Write-Host "==============================================" -ForegroundColor Green
Write-Host " Dashboard generated successfully"
Write-Host "==============================================" -ForegroundColor Green

Write-Host ""

Write-Host "Open:"
Write-Host "docs\ProgressDashboard\index.html"