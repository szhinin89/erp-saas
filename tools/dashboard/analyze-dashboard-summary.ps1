# =============================================================================
# ZH Technologies
# Dashboard Summary Analyzer
#
# Consolida TODA la logica de calculo/decision que antes vivia dentro de
# render-dashboard.ps1 (Engineering Confidence, bandas de riesgo, Production
# Decision, Quality Gate Detail, Security Posture, Technical Debt Trend,
# Release Recommendation, Production Gate, Roadmap, Trend, banderas
# ejecutivas). El renderer deja de calcular nada de esto -- solo lee
# dashboard-summary.json y formatea HTML.
#
# Fuente: dashboard-model-v12.json (EngineeringScore, QualityGate,
# Architecture, Dependencies, Security, TechnicalDebt, Trend, Health) y
# completion-intelligence.json (para reutilizar, no reinventar, la banda ya
# publicada de Production Readiness). Ningun valor se inventa: cada campo de
# salida es o bien copiado 1:1 de un campo real, o una formula aritmetica /
# umbral ya documentado aqui explicitamente.
# =============================================================================

$ErrorActionPreference = "Stop"

$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$DataRoot = Join-Path $ProjectRoot "docs\ProgressDashboard\data"
$Output = Join-Path $DataRoot "dashboard-summary.json"

Write-Host ""
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host " Dashboard Summary Analyzer"
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host ""

function LoadJson($file)
{
    $path = Join-Path $DataRoot $file
    if(!(Test-Path $path)) { throw "Missing file: $path (run its analyzer first)" }
    return (Get-Content $path -Raw | ConvertFrom-Json)
}

$model = LoadJson "dashboard-model-v12.json"
$completionIntelligence = LoadJson "completion-intelligence.json"

$score = $model.EngineeringScore
$gate = $model.QualityGate
$architecture = $model.Architecture
$dependencies = $model.Dependencies
$security = $model.Security
$technicalDebt = $model.TechnicalDebt

Write-Host "Loaded dashboard-model-v12.json + completion-intelligence.json"


# -----------------------------------------------------------------------------
# Security / Technical Debt simple status (GREEN/RED)
# -----------------------------------------------------------------------------

$securityStatus = "GREEN"
if($security.SecretsFound -gt 0 -or $security.AnonymousDetected -gt 0) { $securityStatus = "RED" }

$debtStatus = "GREEN"
if($technicalDebt.CriticalFindings -gt 20) { $debtStatus = "RED" }


# -----------------------------------------------------------------------------
# Production Readiness (numero crudo) -- misma formula que ya usa
# internamente analyze-completion.ps1 (avg5) para derivar su banda
# publicada; ese analizador solo expone la banda (string), no el numero, por
# lo que se recalcula aqui UNICAMENTE el promedio aritmetico. La banda
# (READY/NEEDS REVIEW/NOT READY) se reutiliza tal cual de
# completion-intelligence.json -- no se reinventan los umbrales.
# -----------------------------------------------------------------------------

$productionReadinessScore =
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

$productionStatus = $completionIntelligence.productionReadiness


# -----------------------------------------------------------------------------
# Maturity
# -----------------------------------------------------------------------------

$maturity = "Initial"
if($score.Overall -ge 90) { $maturity = "Optimized" }
elseif($score.Overall -ge 80) { $maturity = "Advanced" }
elseif($score.Overall -ge 60) { $maturity = "Controlled" }
elseif($score.Overall -ge 40) { $maturity = "Developing" }

Write-Host "Maturity=$maturity Security=$securityStatus Debt=$debtStatus ProductionReadiness=$productionReadinessScore% ($productionStatus)"


# -----------------------------------------------------------------------------
# Engineering Confidence Score (pesos documentados)
# -----------------------------------------------------------------------------

$confidenceWeights = @{ Architecture = 0.20; ModuleHealth = 0.20; Quality = 0.20; Security = 0.25; Dependencies = 0.15 }

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


# -----------------------------------------------------------------------------
# Risk Classification (umbrales documentados)
# -----------------------------------------------------------------------------

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
    switch($level) { "CRITICAL" { 4 }; "HIGH" { 3 }; "MEDIUM" { 2 }; default { 1 } }
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
    if($rank -gt $overallRiskRank) { $overallRiskRank = $rank; $overallRisk = $level }
}

Write-Host "Risk Assessment: Architecture=$architectureRisk Security=$securityRisk Quality=$qualityRisk Debt=$technicalDebtRisk Overall=$overallRisk"


# -----------------------------------------------------------------------------
# Production Decision + Blockers
# -----------------------------------------------------------------------------

$productionDecision = "READY"
if($confidenceScore -lt 70) { $productionDecision = "NOT READY" }
elseif($confidenceScore -lt 90) { $productionDecision = "NEEDS REVIEW" }

$decisionBlockers = @()
if($score.Security -lt 70) { $decisionBlockers += "Security blockers - Security score $($score.Security)%" }
if($score.Quality -lt 70) { $decisionBlockers += "Quality blockers - Quality score $($score.Quality)%" }
if($technicalDebt.CriticalFindings -gt 20) { $decisionBlockers += "Debt blockers - $($technicalDebt.CriticalFindings) critical findings" }
if($decisionBlockers.Count -eq 0) { $decisionBlockers += "No blockers detected" }

Write-Host "Production Decision: $productionDecision"


# -----------------------------------------------------------------------------
# Recommended Next Actions (simples, basadas en umbrales -- distintas y mas
# basicas que recommendations.json, que ya cita evidencia detallada; se
# preservan aqui para no perder la seccion visual existente)
# -----------------------------------------------------------------------------

$quickRecommendations = @()
if($security.SecretsFound -gt 0) { $quickRecommendations += "Remove detected secrets ($($security.SecretsFound) found)" }
if($score.Quality -lt 70) { $quickRecommendations += "Improve automated tests and code quality (current: $($score.Quality)%)" }
if($technicalDebt.CriticalFindings -gt 20) { $quickRecommendations += "Reduce critical findings (currently $($technicalDebt.CriticalFindings))" }
if($security.AnonymousDetected -gt 0) { $quickRecommendations += "Review security configuration ($($security.AnonymousDetected) anonymous access points detected)" }
if($quickRecommendations.Count -eq 0) { $quickRecommendations += "No immediate actions required" }

Write-Host "Quick Recommendations: $($quickRecommendations.Count)"


# -----------------------------------------------------------------------------
# Quality Gate Detail
# -----------------------------------------------------------------------------

$testScores = @($model.Health.value | ForEach-Object { [double]$_.Tests })
$testAverage = 0
if($testScores.Count -gt 0) { $testAverage = [math]::Round((($testScores | Measure-Object -Sum).Sum / $testScores.Count), 2) }

$testStatus = "RED"
if($testAverage -ge 80) { $testStatus = "GREEN" }
elseif($testAverage -ge 60) { $testStatus = "YELLOW" }

$buildStatus = "N/A - Not tracked in current JSON model"
$coverageStatus = "N/A - Coverage integration pending"

$staticAnalysisViolations = 0
if($null -ne $architecture -and $null -ne $architecture.Violations) { $staticAnalysisViolations += @($architecture.Violations).Count }
if($null -ne $dependencies -and $null -ne $dependencies.Violations) { $staticAnalysisViolations += @($dependencies.Violations).Count }

$staticAnalysisStatus = "PASS"
if($staticAnalysisViolations -gt 0) { $staticAnalysisStatus = "FAIL" }

Write-Host "Quality Gate Detail: Test=$testStatus($testAverage%) Static=$staticAnalysisStatus"


# -----------------------------------------------------------------------------
# Security Posture (Maturity Level)
# -----------------------------------------------------------------------------

if([double]$score.Security -lt 40) { $securityMaturityLevel = "Level 1"; $securityMaturityLabel = "Critical" }
elseif([double]$score.Security -lt 70) { $securityMaturityLevel = "Level 2"; $securityMaturityLabel = "Developing" }
elseif([double]$score.Security -lt 90) { $securityMaturityLevel = "Level 3"; $securityMaturityLabel = "Controlled" }
else { $securityMaturityLevel = "Level 4"; $securityMaturityLabel = "Mature" }


# -----------------------------------------------------------------------------
# Technical Debt Trend
# -----------------------------------------------------------------------------

$debtTrendStatus = "Stable"
$debtTrendDetail = "No historical data available"

if($null -ne $technicalDebt.History -and @($technicalDebt.History).Count -gt 1)
{
    $debtHistory = @($technicalDebt.History)
    $currentDebtVal = [double]$debtHistory[-1].CriticalFindings
    $previousDebtVal = [double]$debtHistory[-2].CriticalFindings
    $debtChange = $currentDebtVal - $previousDebtVal

    if($debtChange -lt 0) { $debtTrendStatus = "Improving" }
    elseif($debtChange -gt 0) { $debtTrendStatus = "Declining" }
    else { $debtTrendStatus = "Stable" }

    $debtTrendDetail = "Previous: $previousDebtVal | Current: $currentDebtVal | Change: $debtChange"
}


# -----------------------------------------------------------------------------
# Release Recommendation
# -----------------------------------------------------------------------------

$releaseSummary = "Approved for release"
$releaseActions = @("Proceed with standard release checklist")

if($productionDecision -eq "NEEDS REVIEW")
{
    $releaseSummary = "Conditional release - pending review"
    $releaseActions = @("Resolve open blockers before scheduling release", "Re-run Engineering Confidence Score after fixes")
}
elseif($productionDecision -eq "NOT READY")
{
    $releaseSummary = "Release blocked"
    $releaseActions = @("Address all blockers listed below", "Do not schedule a release until Confidence Score reaches 70%+")
}


# -----------------------------------------------------------------------------
# Roadmap (Intelligent, umbrales simples -- distinto de recommendations.json)
# -----------------------------------------------------------------------------

$roadmap = @()
if($score.Security -lt 70) { $roadmap += [ordered]@{ order = 1; title = "Security Hardening"; detail = "Score: $($score.Security)%"; priority = "CRITICAL" } }
if($score.Quality -lt 70) { $roadmap += [ordered]@{ order = 2; title = "Quality Improvement"; detail = "Score: $($score.Quality)%"; priority = "HIGH" } }
if($technicalDebt.CriticalFindings -gt 0) { $roadmap += [ordered]@{ order = 3; title = "Technical Debt Reduction"; detail = "Findings: $($technicalDebt.CriticalFindings)"; priority = "HIGH" } }


# -----------------------------------------------------------------------------
# Production Gate (pass/fail)
# -----------------------------------------------------------------------------

$productionGate = @()
if($score.Security -lt 70) { $productionGate += [ordered]@{ gate = "Security Gate"; status = "FAILED"; detail = "Score $($score.Security)%" } }
if($score.Quality -lt 70) { $productionGate += [ordered]@{ gate = "Quality Gate"; status = "FAILED"; detail = "Score $($score.Quality)%" } }
if($technicalDebt.CriticalFindings -gt 20) { $productionGate += [ordered]@{ gate = "Technical Debt Gate"; status = "FAILED"; detail = "$($technicalDebt.CriticalFindings) critical findings" } }
if($productionGate.Count -eq 0) { $productionGate += [ordered]@{ gate = "All Gates"; status = "PASSED"; detail = "All production gates passed" } }


# -----------------------------------------------------------------------------
# Trend (current/previous/change/status)
# -----------------------------------------------------------------------------

$currentTrend = 0
$previousTrend = 0
$trendChange = 0
$trendStatus = "No Data"

if($null -ne $model.Trend -and $model.Trend.History.Count -gt 0)
{
    $currentTrend = [double]$model.Trend.History[-1].Score
    $previousTrend = $currentTrend
    if($model.Trend.History.Count -gt 1) { $previousTrend = [double]$model.Trend.History[-2].Score }

    $trendChange = [math]::Round(($currentTrend - $previousTrend), 2)

    $trendStatus = "Stable"
    if($trendChange -gt 0) { $trendStatus = "Improving" }
    elseif($trendChange -lt 0) { $trendStatus = "Declining" }
}

Write-Host "Trend: Current=$currentTrend Previous=$previousTrend Change=$trendChange Status=$trendStatus"


# -----------------------------------------------------------------------------
# Executive Summary flags
# -----------------------------------------------------------------------------

$securityHealth = "GOOD"
if($score.Security -lt 70) { $securityHealth = "NEEDS ATTENTION" }

$qualityHealth = "GOOD"
if($score.Quality -lt 70) { $qualityHealth = "NEEDS ATTENTION" }

$debtHealth = "LOW"
if($technicalDebt.CriticalFindings -gt 20) { $debtHealth = "HIGH" }


# =============================================================================
# Output
# =============================================================================

$result = [ordered]@{
    generated = (Get-Date -Format "yyyy-MM-dd HH:mm:ss")
    source = "dashboard-model-v12.json, completion-intelligence.json"
    method = [ordered]@{
        productionReadinessScore = "avg(Architecture, ModuleHealth, Quality, Security, Dependencies) -- same formula analyze-completion.ps1 uses internally to derive its published band"
        productionStatus = "Reused verbatim from completion-intelligence.json.productionReadiness (READY/NEEDS REVIEW/NOT READY) -- not recomputed"
        maturityBands = ">=90 Optimized, >=80 Advanced, >=60 Controlled, >=40 Developing, else Initial (on EngineeringScore.Overall)"
        confidenceWeights = "Architecture 0.20, ModuleHealth 0.20, Quality 0.20, Security 0.25, Dependencies 0.15"
        riskBands = "score-based: <40 CRITICAL, <70 HIGH, <90 MEDIUM, else LOW; findings-based: >40 CRITICAL, >20 HIGH, >=10 MEDIUM, else LOW"
        productionDecisionBands = "confidenceScore <70 NOT READY, <90 NEEDS REVIEW, else READY"
    }
    securityStatus = $securityStatus
    debtStatus = $debtStatus
    productionReadinessScore = $productionReadinessScore
    productionStatus = $productionStatus
    maturity = $maturity
    confidenceScore = $confidenceScore
    risk = [ordered]@{
        architectureRisk = $architectureRisk
        securityRisk = $securityRisk
        qualityRisk = $qualityRisk
        technicalDebtRisk = $technicalDebtRisk
        overallRisk = $overallRisk
    }
    productionDecision = $productionDecision
    decisionBlockers = $decisionBlockers
    quickRecommendations = $quickRecommendations
    qualityGateDetail = [ordered]@{
        testAverage = $testAverage
        testStatus = $testStatus
        buildStatus = $buildStatus
        coverageStatus = $coverageStatus
        staticAnalysisViolations = $staticAnalysisViolations
        staticAnalysisStatus = $staticAnalysisStatus
    }
    securityPosture = [ordered]@{
        level = $securityMaturityLevel
        label = $securityMaturityLabel
    }
    debtTrend = [ordered]@{
        status = $debtTrendStatus
        detail = $debtTrendDetail
    }
    releaseRecommendation = [ordered]@{
        summary = $releaseSummary
        actions = $releaseActions
    }
    roadmap = $roadmap
    productionGate = $productionGate
    trend = [ordered]@{
        current = $currentTrend
        previous = $previousTrend
        change = $trendChange
        status = $trendStatus
    }
    executiveFlags = [ordered]@{
        securityHealth = $securityHealth
        qualityHealth = $qualityHealth
        debtHealth = $debtHealth
    }
}

$result | ConvertTo-Json -Depth 10 | Out-File $Output -Encoding utf8

Write-Host ""
Write-Host "Dashboard summary generated successfully." -ForegroundColor Green
Write-Host $Output
