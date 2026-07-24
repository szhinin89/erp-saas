# =============================================================================
# ZH Technologies
# Progress Dashboard v12.5
# Engineering Dashboard Renderer
# =============================================================================

$ErrorActionPreference = "Stop"

# =============================================================================
# Paths
# =============================================================================

$ProjectRoot =
(
    Resolve-Path (
        Join-Path $PSScriptRoot "..\.."
    )
).Path

$DashboardRoot =
Join-Path $ProjectRoot "docs\ProgressDashboard"

$DataRoot =
Join-Path $DashboardRoot "data"

$HistoryRoot =
Join-Path $DashboardRoot "history"

$Output =
Join-Path $DashboardRoot "index.html"

# =============================================================================
# Banner
# =============================================================================

Write-Host ""
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host " Dashboard Renderer v12.5"
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host ""

# =============================================================================
# Helper
# =============================================================================

function Load-Json
{
    param(
        [string]$File
    )

    $path =
    Join-Path $DataRoot $File

    if(!(Test-Path $path))
    {
        throw "$File not found."
    }

    return (
        Get-Content $path -Raw |
        ConvertFrom-Json
    )
}

# =============================================================================
# Load dashboard model
# =============================================================================

$model =
Load-Json "dashboard-model-v12.json"

# =============================================================================
# Root Objects
# =============================================================================

$score =
$model.EngineeringScore

$gate =
$model.QualityGate

$health =
$model.Health

$architecture =
$model.Architecture

$dependencies =
$model.Dependencies

$security =
$model.Security

$technicalDebt =
$model.TechnicalDebt

$trend =
$model.Trend

# =============================================================================
# Critical Findings
# =============================================================================

$criticalFindings =
$technicalDebt.CriticalFindings

# =============================================================================
# Engineering Maturity
# =============================================================================

$maturity = "Initial"

if($score.Overall -ge 90)
{
    $maturity = "Optimized"
}
elseif($score.Overall -ge 80)
{
    $maturity = "Advanced"
}
elseif($score.Overall -ge 60)
{
    $maturity = "Controlled"
}
elseif($score.Overall -ge 40)
{
    $maturity = "Developing"
}
else
{
    $maturity = "Initial"
}

# =============================================================================
# Dependency Status
# =============================================================================

$dependencyStatus = "PASS"

if($dependencies.Summary.Total -gt 0)
{
    $dependencyStatus = "FAIL"
}

# =============================================================================
# Security Status
# =============================================================================

$securityStatus = "GREEN"

if(
    $security.SecretsFound -gt 0 `
    -or
    $security.AnonymousDetected -gt 0
)
{
    $securityStatus = "RED"
}
elseif($security.Warnings -gt 0)
{
    $securityStatus = "YELLOW"
}

# =============================================================================
# Technical Debt Status
# =============================================================================

$debtStatus = "GREEN"

if($technicalDebt.CriticalFindings -gt 20)
{
    $debtStatus = "RED"
}
elseif($technicalDebt.CriticalFindings -gt 0)
{
    $debtStatus = "YELLOW"
}

# =============================================================================
# Quality Gate Lists
# =============================================================================

$warnings = @()

foreach($item in $gate.Warnings)
{
    $warnings += "<li>$item</li>"
}

$strengths = @()

foreach($item in $gate.Strengths)
{
    $strengths += "<li>$item</li>"
}

# =============================================================================
# Roadmap
# =============================================================================

$roadmap = @()

if($score.Security -lt 70)
{
    $roadmap +=
    "<li><b>Security Hardening</b><br/>Security score: $($score.Security)%</li>"
}

if($score.Quality -lt 70)
{
    $roadmap +=
    "<li><b>Quality Improvement</b><br/>Quality score: $($score.Quality)%</li>"
}

if($technicalDebt.CriticalFindings -gt 0)
{
    $roadmap +=
    "<li><b>Technical Debt Reduction</b><br/>Critical findings: $($technicalDebt.CriticalFindings)</li>"
}

if($dependencies.Summary.Total -gt 0)
{
    $roadmap +=
    "<li><b>Dependency Cleanup</b><br/>Violations: $($dependencies.Summary.Total)</li>"
}

if($roadmap.Count -eq 0)
{
    $roadmap +=
    "<li>No priority actions detected.</li>"
}

# =============================================================================
# Module Health Table
# =============================================================================

$moduleRows = @()

foreach($module in $health.value)
{
    $moduleRows +=
    "<tr>
        <td>$($module.Module)</td>
        <td>$($module.Score)%</td>
    </tr>"
}
# =============================================================================
# Engineering Trend
# =============================================================================

$trendRows = @()

$currentTrendScore = 0
$previousTrendScore = 0
$trendDifference = 0
$trendStatus = "Stable"

if($trend.History.Count -gt 0)
{
    $currentTrendScore =
    [double]$trend.History[-1].Score

    $previousTrendScore =
    $currentTrendScore

    if($trend.History.Count -gt 1)
    {
        $previousTrendScore =
        [double]$trend.History[-2].Score
    }

    $trendDifference =
    [math]::Round(
        $currentTrendScore - $previousTrendScore,
        2
    )

    if($trendDifference -gt 0)
    {
        $trendStatus = "Improving ↑"
    }
    elseif($trendDifference -lt 0)
    {
        $trendStatus = "Declining ↓"
    }

    foreach($snapshot in $trend.History)
    {
        $trendRows +=
@"
<tr>
<td>$($snapshot.Date)</td>
<td>$($snapshot.Score)%</td>
</tr>
"@
    }
}

# =============================================================================
# Architecture Panel
# =============================================================================

$architectureHtml =
@"
<div class='card'>

<h2>Architecture Overview</h2>

<table>

<tr>
<th>Layer</th>
<th>Files</th>
</tr>

<tr>
<td>Domain</td>
<td>$($architecture.Layers.Domain.Files)</td>
</tr>

<tr>
<td>Application</td>
<td>$($architecture.Layers.Application.Files)</td>
</tr>

<tr>
<td>Infrastructure</td>
<td>$($architecture.Layers.Infrastructure.Files)</td>
</tr>

<tr>
<td>API</td>
<td>$($architecture.Layers.API.Files)</td>
</tr>

</table>

</div>
"@

# =============================================================================
# Dependency Panel
# =============================================================================

$dependencyHtml =
@"
<div class='card'>

<h2>Dependency Governance</h2>

<h1>$dependencyStatus</h1>

<p>
Violations:
<b>$($dependencies.Summary.Total)</b>
</p>

</div>
"@

# =============================================================================
# Security Panel
# =============================================================================

$securityHtml =
@"
<div class='card'>

<h2>
Security Analysis ($securityStatus)
</h2>

<table>

<tr>
<td>Files Analyzed</td>
<td>$($security.FilesAnalyzed)</td>
</tr>

<tr>
<td>Secrets Found</td>
<td>$($security.SecretsFound)</td>
</tr>

<tr>
<td>Anonymous Access</td>
<td>$($security.AnonymousDetected)</td>
</tr>

<tr>
<td>Connection Strings</td>
<td>$($security.ConnectionStringsFound)</td>
</tr>

<tr>
<td>Warnings</td>
<td>$($security.Warnings)</td>
</tr>

</table>

</div>
"@

# =============================================================================
# Technical Debt Panel
# =============================================================================

$technicalDebtHtml =
@"
<div class='card'>

<h2>
Technical Debt ($debtStatus)
</h2>

<table>

<tr>
<td>TODO</td>
<td>$($technicalDebt.TODO)</td>
</tr>

<tr>
<td>FIXME</td>
<td>$($technicalDebt.FIXME)</td>
</tr>

<tr>
<td>HACK</td>
<td>$($technicalDebt.HACK)</td>
</tr>

<tr>
<td>Not Implemented</td>
<td>$($technicalDebt.NotImplemented)</td>
</tr>

<tr>
<td>Critical Findings</td>
<td>$($technicalDebt.CriticalFindings)</td>
</tr>

</table>

</div>
"@

# =============================================================================
# Trend Panel
# =============================================================================

$trendHtml =
@"

<div class='card'>

<h2>
Engineering Trend
</h2>

<p>
Snapshots:
<b>$($trend.Snapshots)</b>
</p>

<p>
Previous Score:
<b>$previousTrendScore%</b>
</p>

<p>
Current Score:
<b>$currentTrendScore%</b>
</p>

<p>
Difference:
<b>$trendDifference%</b>
</p>

<h3>
$trendStatus
</h3>

<table>

<tr>
<th>Date</th>
<th>Score</th>
</tr>

$($trendRows -join "`n")

</table>

</div>

"@