# =============================================================================
# ZH Technologies
# Progress Dashboard v5
# Complete Dashboard Runner v2
# =============================================================================

$ErrorActionPreference = "Stop"


$ScriptRoot = $PSScriptRoot


Write-Host ""
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host " ZH Dashboard Full Analyzer v2"
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host ""



function Run-Script($name)
{

    Write-Host ""
    Write-Host "Running $name" -ForegroundColor Yellow


    powershell `
    -ExecutionPolicy Bypass `
    -File (Join-Path $ScriptRoot $name)


    if($LASTEXITCODE -ne 0)
    {
        throw "$name failed"
    }

}



Run-Script "analyze-backend.ps1"

Run-Script "analyze-frontend.ps1"

Run-Script "analyze-tests.ps1"

Run-Script "analyze-docs.ps1"

Run-Script "analyze-architecture.ps1"

Run-Script "analyze-dependencies.ps1"

Run-Script "analyze-module-health.ps1"

Run-Script "build-dashboard-v2.ps1"

Run-Script "render-dashboard-v2.ps1"



Write-Host ""
Write-Host "==============================================" -ForegroundColor Green
Write-Host " Dashboard completed successfully"
Write-Host "==============================================" -ForegroundColor Green