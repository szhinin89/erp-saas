# =============================================================================
# ZH Technologies
# Progress Dashboard v4
# Dashboard Builder
# =============================================================================

$ErrorActionPreference = "Stop"

$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$DashboardRoot = Join-Path $ProjectRoot "docs\ProgressDashboard"
$DataFolder = Join-Path $DashboardRoot "data"

Write-Host ""
Write-Host "==================================================" -ForegroundColor Cyan
Write-Host " Progress Dashboard Builder"
Write-Host "==================================================" -ForegroundColor Cyan
Write-Host ""

function Read-JsonArray($fileName)
{
    $path = Join-Path $DataFolder $fileName

    if (!(Test-Path $path))
    {
        return @()
    }

    $content = Get-Content $path -Raw

    if ([string]::IsNullOrWhiteSpace($content))
    {
        return @()
    }

    $obj = $content | ConvertFrom-Json

    if ($obj -is [System.Array])
    {
        return $obj
    }

    return @($obj)
}

$layers    = Read-JsonArray "layers.json"
$domains   = Read-JsonArray "domains.json"
$modules   = Read-JsonArray "modules.json"
$processes = Read-JsonArray "processes.json"
$features  = Read-JsonArray "features.json"
$tasks     = Read-JsonArray "tasks.json"

$metrics = @{
    Generated = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")

    Statistics = @{
        Layers    = $layers.Count
        Domains   = $domains.Count
        Modules   = $modules.Count
        Processes = $processes.Count
        Features  = $features.Count
        Tasks     = $tasks.Count
    }

    Dashboard = @{
        Status = "Building"
        Version = "4.0"
    }
}

$metrics |
    ConvertTo-Json -Depth 10 |
    Set-Content (Join-Path $DataFolder "metrics.json") -Encoding UTF8

Write-Host ""
Write-Host "Dashboard Metrics Generated" -ForegroundColor Green
Write-Host ""

$metrics.Statistics | Format-Table