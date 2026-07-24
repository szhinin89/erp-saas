# =============================================================================
# ZH Technologies
# Progress Dashboard Final Runner v1.0
# =============================================================================

$ErrorActionPreference = "Stop"


$DashboardRoot =
$PSScriptRoot



Write-Host ""
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host " ZH Dashboard Final Pipeline"
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host ""



function Run-Script($name)
{

    Write-Host ""
    Write-Host "Running $name" -ForegroundColor Yellow
    Write-Host ""


    powershell `
    -ExecutionPolicy Bypass `
    -File (Join-Path $DashboardRoot $name)


    if($LASTEXITCODE -ne 0)
    {
        throw "$name failed"
    }

}



$scripts = @(

"analyze-backend.ps1"

"analyze-frontend.ps1"

"analyze-tests.ps1"

"analyze-architecture.ps1"

"analyze-dependencies.ps1"

"analyze-database.ps1"

"analyze-migrations.ps1"

"analyze-technical-debt.ps1"

"analyze-security.ps1"

"analyze-module-health.ps1"

"health-score.ps1"

"calculate-engineering-score.ps1"

"Manage-EngineeringHistory.ps1"

"quality-gate.ps1"

"build-dashboard-v12.ps1"

"analyze-progress-map.ps1"

"analyze-modules.ps1"

"analyze-features.ps1"

"analyze-processes.ps1"

"analyze-tasks.ps1"

"validate-dashboard-model.ps1"

"analyze-impact.ps1"

"analyze-completion.ps1"

"analyze-module-graph.ps1"

"analyze-critical-path.ps1"

"analyze-release-simulation.ps1"

"analyze-recommendations.ps1"

"analyze-navigation-map.ps1"

"analyze-dashboard-summary.ps1"

"analyze-explorer-index.ps1"

"render-dashboard.ps1"

)



foreach($script in $scripts)
{
    Run-Script $script
}



Write-Host ""

Write-Host "==============================================" -ForegroundColor Green
Write-Host " Dashboard completed successfully"
Write-Host "==============================================" -ForegroundColor Green

Write-Host ""

Write-Host "Open:"
Write-Host "docs\ProgressDashboard\index.html"