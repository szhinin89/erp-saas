# =============================================================================
# ZH Technologies
# Progress Dashboard v13
# Engineering Dashboard Renderer
# =============================================================================

$ErrorActionPreference = "Stop"


$ProjectRoot =
(Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path


$DataRoot =
Join-Path $ProjectRoot "docs\ProgressDashboard\data"


$Output =
Join-Path $ProjectRoot "docs\ProgressDashboard\index.html"



Write-Host ""
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host " Dashboard Renderer v13"
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host ""



function LoadJson($file)
{
    $path = Join-Path $DataRoot $file

    if(!(Test-Path $path))
    {
        throw "Missing file: $path"
    }

    Get-Content $path -Raw | ConvertFrom-Json
}



$model = LoadJson "dashboard-model-v12.json"

$score = $model.EngineeringScore
$gate = $model.QualityGate
$architecture = $model.Architecture
$dependencies = $model.Dependencies

$security = $model.Security
$technicalDebt = $model.TechnicalDebt

$criticalFindings = $technicalDebt.CriticalFindings


Write-Host "JSON loaded successfully" -ForegroundColor Green



# -----------------------------
# Security
# -----------------------------

$securityStatus = "GREEN"


if(
    $security.SecretsFound -gt 0 -or
    $security.AnonymousDetected -gt 0
)
{
    $securityStatus = "RED"
}



# -----------------------------
# Technical Debt
# -----------------------------

$debtStatus = "GREEN"


if($technicalDebt.CriticalFindings -gt 20)
{
    $debtStatus = "RED"
}



# -----------------------------
# Maturity
# -----------------------------

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



Write-Host ""
Write-Host "Calculations completed" -ForegroundColor Green
Write-Host "Maturity : $maturity"
Write-Host "Security : $securityStatus"
Write-Host "Debt     : $debtStatus"
# =============================================================================
# HTML DATA BUILD
# =============================================================================


# -----------------------------
# Quality Gate Lists
# -----------------------------

$warningsHtml = @()

foreach($item in $gate.Warnings)
{
    $warningsHtml += "<li>$item</li>"
}



$strengthsHtml = @()

foreach($item in $gate.Strengths)
{
    $strengthsHtml += "<li>$item</li>"
}



# -----------------------------
# Module Health
# -----------------------------

$modulesHtml = @()


foreach($module in $model.Health.value)
{
    $modulesHtml += 
    "<tr><td>$($module.Module)</td><td>$($module.Score)%</td></tr>"
}



# -----------------------------
# Roadmap
# -----------------------------

$roadmapHtml = @()


if($score.Security -lt 70)
{
    $roadmapHtml += 
    "<li>Security Hardening - Score $($score.Security)%</li>"
}


if($score.Quality -lt 70)
{
    $roadmapHtml +=
    "<li>Quality Improvement - Score $($score.Quality)%</li>"
}


if($technicalDebt.CriticalFindings -gt 0)
{
    $roadmapHtml +=
    "<li>Technical Debt Reduction - $($technicalDebt.CriticalFindings) findings</li>"
}

if($roadmapHtml.Count -eq 0)
{
    $roadmapHtml +=
    "<li>No priority actions detected</li>"
}


Write-Host ""
Write-Host "HTML data prepared" -ForegroundColor Green
Write-Host "Modules :" $modulesHtml.Count
Write-Host "Roadmap :" $roadmapHtml.Count
# =============================================================================
# Trend Data
# =============================================================================

$trendHistoryHtml = @()


if($null -ne $model.Trend -and $model.Trend.History.Count -gt 0)
{
    foreach($item in $model.Trend.History)
    {
        $trendHistoryHtml += "<tr><td>$($item.Date)</td><td>$($item.Score)%</td></tr>"
    }


    $currentTrend = [double]$model.Trend.History[-1].Score


    $previousTrend = $currentTrend


    if($model.Trend.History.Count -gt 1)
    {
        $previousTrend = [double]$model.Trend.History[-2].Score
    }


    $trendChange =
    [math]::Round(
        ($currentTrend - $previousTrend),
        2
    )


    $trendStatus = "Stable"


    if($trendChange -gt 0)
    {
        $trendStatus = "Improving"
    }
    elseif($trendChange -lt 0)
    {
        $trendStatus = "Declining"
    }
}
else
{
    $currentTrend = 0
    $previousTrend = 0
    $trendChange = 0
    $trendStatus = "No Data"
}


Write-Host "Trend prepared"
Write-Host "Current :" $currentTrend
Write-Host "Previous:" $previousTrend
Write-Host "Change  :" $trendChange
Write-Host "Status  :" $trendStatus

# =============================================================================
# Executive Summary
# =============================================================================

$securityHealth = "GOOD"

if($score.Security -lt 70)
{
    $securityHealth = "NEEDS ATTENTION"
}


$qualityHealth = "GOOD"

if($score.Quality -lt 70)
{
    $qualityHealth = "NEEDS ATTENTION"
}


$debtHealth = "LOW"

if($technicalDebt.CriticalFindings -gt 20)
{
    $debtHealth = "HIGH"
}



$summaryHtml = @(
"<div class='card'>",
"<h2>ERP Engineering Health</h2>",
"<div class='score'>$($score.Overall)%</div>",
"<p>Architecture: EXCELLENT</p>",
"<p>Security: $securityHealth</p>",
"<p>Quality: $qualityHealth</p>",
"<p>Technical Debt: $debtHealth</p>",
"</div>"
) -join "`n"

# =============================================================================
# HTML RENDER
# =============================================================================


$html = @(
"<!DOCTYPE html>",
"<html>",
"<head>",
"<title>ZH Technologies ERP Engineering Dashboard v13</title>",

"<style>",
"body { font-family: Segoe UI, Arial; background:#f4f6f8; margin:40px; }",
".card { background:white; padding:25px; margin-bottom:20px; border-radius:12px; box-shadow:0 3px 10px #ccc; }",
".score { font-size:60px; font-weight:bold; }",
"table { width:100%; border-collapse:collapse; }",
"td,th { padding:10px; border-bottom:1px solid #ddd; }",
"</style>",

"</head>",
"<body>",
$summaryHtml,

"<h1>ZH Technologies ERP Engineering Dashboard v13</h1>",


"<div class='card'>",
"<h2>Engineering Score</h2>",
"<div class='score'>$($score.Overall)%</div>",
"<p>Architecture: $($score.Architecture)%</p>",
"<p>Module Health: $($score.ModuleHealth)%</p>",
"<p>Quality: $($score.Quality)%</p>",
"<p>Security: $($score.Security)%</p>",
"<p>Dependencies: $($score.Dependencies)%</p>",
"</div>",



"<div class='card'>",
"<h2>Quality Gate</h2>",
"<h1>$($gate.Status)</h1>",
"<h3>Warnings</h3>",
"<ul>",
($warningsHtml -join "`n"),
"</ul>",
"<h3>Strengths</h3>",
"<ul>",
($strengthsHtml -join "`n"),
"</ul>",
"</div>",



"<div class='card'>",
"<h2>Engineering Maturity</h2>",
"<div class='score'>$maturity</div>",
"</div>",



"<div class='card'>",
"<h2>Security Analysis - $securityStatus</h2>",
"<p>Files analyzed: $($security.FilesAnalyzed)</p>",
"<p>Secrets detected: $($security.SecretsFound)</p>",
"<p>Anonymous findings: $($security.AnonymousDetected)</p>",
"<p>Connection Strings: $($security.ConnectionStringsFound)</p>",
"<p>Security Risk: $securityStatus</p>",
"</div>",



"<div class='card'>",
"<h2>Technical Debt - $debtStatus</h2>",
"<p>Risk Level: $debtHealth</p>",
"<p>Critical Findings: $($technicalDebt.CriticalFindings)</p>",
"<p>TODO: $($technicalDebt.TODO)</p>",
"<p>FIXME: $($technicalDebt.FIXME)</p>",
"<p>HACK: $($technicalDebt.HACK)</p>",
"<p>Not Implemented: $($technicalDebt.NotImplemented)</p>",
"</div>",

"<div class='card'>",
"<h2>Engineering Trend</h2>",
"<p>Snapshots: $($model.Trend.Snapshots)</p>",
"<p>Previous Score: $previousTrend%</p>",
"<p>Current Score: $currentTrend%</p>",
"<p>Change: $trendChange%</p>",
"<p>Status: $trendStatus</p>",
"<h3>History</h3>",
"<table>",
"<tr><th>Date</th><th>Score</th></tr>",
($trendHistoryHtml -join "`n"),
"</table>",
"</div>",

"<div class='card'>",
"<h2>Module Health</h2>",
"<table>",
"<tr><th>Module</th><th>Score</th></tr>",
($modulesHtml -join "`n"),
"</table>",
"</div>",



"<div class='card'>",
"<h2>Engineering Roadmap</h2>",
"<ul>",
($roadmapHtml -join "`n"),
"</ul>",
"</div>",


"</body>",
"</html>"
) -join "`n"



$html | Set-Content $Output -Encoding UTF8



Write-Host ""
Write-Host "Dashboard v13 generated successfully." -ForegroundColor Green
Write-Host $Output