# =============================================================================
# ZH Technologies
# Generate Mermaid Diagram
# =============================================================================

$ErrorActionPreference = "Stop"

$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$DashboardRoot = Join-Path $ProjectRoot "docs\ProgressDashboard"
$DataFolder = Join-Path $DashboardRoot "data"
$DiagramFolder = Join-Path $DashboardRoot "diagrams\mermaid"

if (!(Test-Path $DiagramFolder)) {
    New-Item -ItemType Directory -Force -Path $DiagramFolder | Out-Null
}

$modelPath = Join-Path $DataFolder "project-model.json"

if (!(Test-Path $modelPath)) {
    throw "No existe project-model.json"
}

$model = Get-Content $modelPath -Raw | ConvertFrom-Json

$output = @()
$output += "mindmap"
$output += "  root(($($model.Project)))"

foreach($layer in $model.Layers)
{
    $output += "    $($layer.Name)"

    foreach($domain in $layer.Domains)
    {
        $output += "      $($domain.Name)"
    }
}

$output |
Set-Content (Join-Path $DiagramFolder "ERP-Master.mmd") -Encoding UTF8

Write-Host ""
Write-Host "Mermaid diagram generated successfully." -ForegroundColor Green