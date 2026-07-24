# =============================================================================
# ZH Technologies
# Progress Dashboard v17
# Engineering Health Report Renderer
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
Write-Host " Dashboard Renderer v17"
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

# ==============================
# Production Readiness
# ==============================

$productionReadiness =
[math]::Round(
(
    [double]$score.Architecture +
    [double]$score.ModuleHealth +
    [double]$score.Quality +
    [double]$score.Security +
    [double]$score.Dependencies
) / 5,
2
)


$productionStatus = "READY"


if($productionReadiness -lt 80)
{
    $productionStatus = "NOT READY"
}
elseif($productionReadiness -lt 90)
{
    $productionStatus = "NEEDS REVIEW"
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
# Engineering Confidence Score
# =============================================================================

$confidenceWeights = @{
    Architecture = 0.20
    ModuleHealth = 0.20
    Quality      = 0.20
    Security     = 0.25
    Dependencies = 0.15
}


$confidenceScore =
[math]::Round(
(
    ([double]$score.Architecture * $confidenceWeights.Architecture) +
    ([double]$score.ModuleHealth * $confidenceWeights.ModuleHealth) +
    ([double]$score.Quality * $confidenceWeights.Quality) +
    ([double]$score.Security * $confidenceWeights.Security) +
    ([double]$score.Dependencies * $confidenceWeights.Dependencies)
),
2
)


Write-Host "Engineering Confidence: $confidenceScore%"


# =============================================================================
# Risk Classification
# =============================================================================

function Get-RiskLevel($value, $isFindingsCount)
{
    if($isFindingsCount)
    {
        if($value -gt 40) { return "CRITICAL" }
        elseif($value -gt 20) { return "HIGH" }
        elseif($value -ge 10) { return "MEDIUM" }
        else { return "LOW" }
    }
    else
    {
        if($value -lt 40) { return "CRITICAL" }
        elseif($value -lt 70) { return "HIGH" }
        elseif($value -lt 90) { return "MEDIUM" }
        else { return "LOW" }
    }
}


function Get-RiskRank($level)
{
    switch($level)
    {
        "CRITICAL" { 4 }
        "HIGH"     { 3 }
        "MEDIUM"   { 2 }
        default    { 1 }
    }
}


$architectureRisk = Get-RiskLevel ([double]$score.Architecture) $false
$securityRisk = Get-RiskLevel ([double]$score.Security) $false
$qualityRisk = Get-RiskLevel ([double]$score.Quality) $false
$technicalDebtRisk = Get-RiskLevel ([double]$technicalDebt.CriticalFindings) $true


$riskLevels = @($architectureRisk, $securityRisk, $qualityRisk, $technicalDebtRisk)

$overallRisk = "LOW"
$overallRiskRank = 0

foreach($level in $riskLevels)
{
    $rank = Get-RiskRank $level

    if($rank -gt $overallRiskRank)
    {
        $overallRiskRank = $rank
        $overallRisk = $level
    }
}


Write-Host "Risk Assessment: Architecture=$architectureRisk Security=$securityRisk Quality=$qualityRisk Debt=$technicalDebtRisk Overall=$overallRisk"


# =============================================================================
# Production Decision (Extended)
# =============================================================================

$productionDecision = "READY"

if($confidenceScore -lt 70)
{
    $productionDecision = "NOT READY"
}
elseif($confidenceScore -lt 90)
{
    $productionDecision = "NEEDS REVIEW"
}


$decisionBlockersHtml = @()


if($score.Security -lt 70)
{
    $decisionBlockersHtml +=
    "<li>Security blockers - Security score $($score.Security)%</li>"
}


if($score.Quality -lt 70)
{
    $decisionBlockersHtml +=
    "<li>Quality blockers - Quality score $($score.Quality)%</li>"
}


if($technicalDebt.CriticalFindings -gt 20)
{
    $decisionBlockersHtml +=
    "<li>Debt blockers - $($technicalDebt.CriticalFindings) critical findings</li>"
}


if($decisionBlockersHtml.Count -eq 0)
{
    $decisionBlockersHtml +=
    "<li>No blockers detected</li>"
}


Write-Host "Production Decision: $productionDecision"


# =============================================================================
# Recommended Next Actions
# =============================================================================

$recommendationsHtml = @()
$recommendationIndex = 1


if($security.SecretsFound -gt 0)
{
    $recommendationsHtml +=
    "<li>$recommendationIndex. Remove detected secrets ($($security.SecretsFound) found)</li>"
    $recommendationIndex++
}


if($score.Quality -lt 70)
{
    $recommendationsHtml +=
    "<li>$recommendationIndex. Improve automated tests and code quality (current: $($score.Quality)%)</li>"
    $recommendationIndex++
}


if($technicalDebt.CriticalFindings -gt 20)
{
    $recommendationsHtml +=
    "<li>$recommendationIndex. Reduce critical findings (currently $($technicalDebt.CriticalFindings))</li>"
    $recommendationIndex++
}


if($security.AnonymousDetected -gt 0)
{
    $recommendationsHtml +=
    "<li>$recommendationIndex. Review security configuration ($($security.AnonymousDetected) anonymous access points detected)</li>"
    $recommendationIndex++
}


if($recommendationsHtml.Count -eq 0)
{
    $recommendationsHtml +=
    "<li>No immediate actions required</li>"
}


Write-Host "Recommendations :" $recommendationsHtml.Count

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


# ==============================
# Roadmap Intelligent
# ==============================

$roadmapHtml = @()


if($score.Security -lt 70)
{
    $roadmapHtml +=
    "<li>
    <b>1. Security Hardening</b><br/>
    Score: $($score.Security)%<br/>
    Priority: CRITICAL
    </li>"
}


if($score.Quality -lt 70)
{
    $roadmapHtml +=
    "<li>
    <b>2. Quality Improvement</b><br/>
    Score: $($score.Quality)%<br/>
    Priority: HIGH
    </li>"
}


if($technicalDebt.CriticalFindings -gt 0)
{
    $roadmapHtml +=
    "<li>
    <b>3. Technical Debt Reduction</b><br/>
    Findings: $($technicalDebt.CriticalFindings)<br/>
    Priority: HIGH
    </li>"
}


if($roadmapHtml.Count -eq 0)
{
    $roadmapHtml +=
    "<li>No production blockers detected</li>"
}


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
# Production Gate
# =============================================================================

$productionGate = @()

Write-Host "ProductionGate Items:" $productionGate.Count

if($score.Security -lt 70)
{
    $productionGate +=
    "<li>[FAILED] Security Gate - Score $($score.Security)%</li>"
}


if($score.Quality -lt 70)
{
    $productionGate +=
    "<li>[FAILED] Quality Gate - Score $($score.Quality)%</li>"
}


if($technicalDebt.CriticalFindings -gt 20)
{
    $productionGate +=
    "<li>[FAILED] Technical Debt Gate - $($technicalDebt.CriticalFindings) critical findings</li>"
}


if($productionGate.Count -eq 0)
{
    $productionGate +=
    "<li>[PASSED] All production gates passed</li>"
}

# =============================================================================
# Production Gate HTML
# =============================================================================

$productionGateHtml = @"
<div class='card'>
<h2>Production Gate Decision</h2>
<ul>
$($productionGate -join "`n")
</ul>
</div>
"@

Write-Host "ProductionGate Final:" $productionGate.Count


# =============================================================================
# Engineering Confidence HTML
# =============================================================================

$confidenceHtml = @"
<div class='card'>
<h2>Engineering Confidence</h2>
<div class='score'>$confidenceScore%</div>
<p>Architecture: $($score.Architecture)%</p>
<p>Module Health: $($score.ModuleHealth)%</p>
<p>Quality: $($score.Quality)%</p>
<p>Security: $($score.Security)%</p>
<p>Dependencies: $($score.Dependencies)%</p>
<p>Confidence Score: $confidenceScore%</p>
</div>
"@


# =============================================================================
# Risk Assessment HTML
# =============================================================================

$riskAssessmentHtml = @"
<div class='card'>
<h2>Risk Assessment</h2>
<p>Architecture Risk: $architectureRisk</p>
<p>Security Risk: $securityRisk</p>
<p>Quality Risk: $qualityRisk</p>
<p>Technical Debt Risk: $technicalDebtRisk</p>
<p>Overall Risk: $overallRisk</p>
</div>
"@


# =============================================================================
# Production Decision HTML
# =============================================================================

$productionDecisionHtml = @"
<div class='card'>
<h2>Production Decision</h2>
<div class='score'>$productionDecision</div>
<h3>Blockers</h3>
<ul>
$($decisionBlockersHtml -join "`n")
</ul>
</div>
"@


# =============================================================================
# Recommendations HTML
# =============================================================================

$recommendationsCardHtml = @"
<div class='card'>
<h2>Recommended Next Actions</h2>
<ul>
$($recommendationsHtml -join "`n")
</ul>
</div>
"@

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
"<h2>Executive Summary</h2>",
"<div class='score'>$($score.Overall)%</div>",
"<p>Architecture: $score.Architecture</p>",
"<p>Security: $securityHealth</p>",
"<p>Quality: $qualityHealth</p>",
"<p>Technical Debt: $debtHealth</p>",
"<p>Status: $productionStatus</p>",
"<p>Engineering Confidence: $confidenceScore%</p>",
"<p>Overall Risk: $overallRisk</p>",
"</div>"
) -join "`n"

# =============================================================================
# HTML RENDER
# =============================================================================

$html = @(
"<!DOCTYPE html>",
"<html>",
"<head>",
"<title>ZH Technologies ERP Engineering Dashboard v17</title>",

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
$confidenceHtml,
$riskAssessmentHtml,
$productionDecisionHtml,
$recommendationsCardHtml,
$productionGateHtml,
"<h1>ZH Technologies ERP Engineering Dashboard v17</h1>",


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
"<h2>Production Readiness</h2>",
"<div class='score'>$productionReadiness%</div>",
"<p>Status: $productionStatus</p>",
"<p>Architecture: $($score.Architecture)%</p>",
"<p>Module Health: $($score.ModuleHealth)%</p>",
"<p>Quality: $($score.Quality)%</p>",
"<p>Security: $($score.Security)%</p>",
"<p>Dependencies: $($score.Dependencies)%</p>",
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
"</html>"
) -join "`n"


$html | Out-File $Output -Encoding utf8



Write-Host ""
Write-Host "Dashboard v17 generated successfully." -ForegroundColor Green
Write-Host $Output
