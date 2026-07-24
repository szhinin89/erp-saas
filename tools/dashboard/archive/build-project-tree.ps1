# =============================================================================
# ZH Technologies
# Progress Dashboard v4
# Build Project Tree
# =============================================================================

$ErrorActionPreference = "Stop"

$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$DashboardRoot = Join-Path $ProjectRoot "docs\ProgressDashboard"
$DataFolder = Join-Path $DashboardRoot "data"

Write-Host ""
Write-Host "==================================================" -ForegroundColor Cyan
Write-Host " Building Project Tree"
Write-Host "==================================================" -ForegroundColor Cyan
Write-Host ""

$modelFile = Join-Path $DataFolder "project-model.json"

if (!(Test-Path $modelFile))
{
    throw "project-model.json no existe."
}

$model = Get-Content $modelFile -Raw | ConvertFrom-Json

$tree = @()

foreach($layer in $model.Layers)
{
    $layerNode = [ordered]@{
        Id = $layer.Id
        Name = $layer.Name
        Type = "Layer"
        Children = @()
    }

    foreach($domain in $layer.Domains)
    {
        $layerNode.Children += [ordered]@{
            Id = $domain.Id
            Name = $domain.Name
            Type = "Domain"
            Progress = 0
            Maturity = 0
            Status = "Pending"
            Children = @()
        }
    }

    $tree += $layerNode
}

$output = @{
    Generated = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    Root = "ZH ERP SaaS"
    Tree = $tree
}

$output |
ConvertTo-Json -Depth 30 |
Set-Content (Join-Path $DataFolder "project-tree.json") -Encoding UTF8

Write-Host ""
Write-Host "Project Tree generated successfully." -ForegroundColor Green
Write-Host ""