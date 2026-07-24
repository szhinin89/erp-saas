# =============================================================================
# ZH Technologies
# Progress Dashboard v20
# Engineering Health Report Renderer
#
# Evoluciona v19 (sin modificarlo) agregando la seccion "Business Capability
# Map": Dominio -> Modulos -> Features -> Procesos, usando exclusivamente
# datos ya resueltos por los analizadores (nada se infiere en el renderer).
#
# PROGRESS.html (mapa maestro, en la raiz del repo) NO se modifica. Este
# reporte solo referencia esa portada mediante un enlace relativo.
#
# Prerequisitos (correr antes de este script):
#   tools/dashboard/analyze-modules.ps1    -> modules.json
#   tools/dashboard/analyze-features.ps1   -> features.json
#   tools/dashboard/analyze-processes.ps1  -> processes.json
#   tools/dashboard/analyze-tasks.ps1      -> tasks.json
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
Write-Host " Dashboard Renderer v20"
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host ""



function LoadJson($file)
{
    $path = Join-Path $DataRoot $file

    if(!(Test-Path $path))
    {
        throw "Missing file: $path"
    }

    return (Get-Content $path -Raw | ConvertFrom-Json)
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
# Quality Gate Detail
# =============================================================================

# NOTE: dashboard-model-v12.json does not currently expose dedicated
# Build/Test/Coverage/StaticAnalysis fields. Test status is derived from the
# per-module "Tests" scores already present in Health.value. Build and
# Coverage remain explicit placeholders until the model exposes real data —
# this keeps v18/v19 compatible with the current JSON without inventing values.

$testScores = @($model.Health.value | ForEach-Object { [double]$_.Tests })

if($testScores.Count -gt 0)
{
    $testAverage =
    [math]::Round(
        (($testScores | Measure-Object -Sum).Sum / $testScores.Count),
        2
    )
}
else
{
    $testAverage = 0
}


$testStatus = "RED"

if($testAverage -ge 80)
{
    $testStatus = "GREEN"
}
elseif($testAverage -ge 60)
{
    $testStatus = "YELLOW"
}


$buildStatus = "N/A - Not tracked in current JSON model"

$coverageStatus = "N/A - Coverage integration pending"


$staticAnalysisViolations = 0

if($null -ne $architecture -and $null -ne $architecture.Violations)
{
    $staticAnalysisViolations += @($architecture.Violations).Count
}

if($null -ne $dependencies -and $null -ne $dependencies.Violations)
{
    $staticAnalysisViolations += @($dependencies.Violations).Count
}


$staticAnalysisStatus = "PASS"

if($staticAnalysisViolations -gt 0)
{
    $staticAnalysisStatus = "FAIL"
}


Write-Host "Quality Gate Detail: Build=$buildStatus Test=$testStatus($testAverage%) Coverage=$coverageStatus Static=$staticAnalysisStatus"


# =============================================================================
# Security Posture (Maturity Level)
# =============================================================================

if([double]$score.Security -lt 40)
{
    $securityMaturityLevel = "Level 1"
    $securityMaturityLabel = "Critical"
}
elseif([double]$score.Security -lt 70)
{
    $securityMaturityLevel = "Level 2"
    $securityMaturityLabel = "Developing"
}
elseif([double]$score.Security -lt 90)
{
    $securityMaturityLevel = "Level 3"
    $securityMaturityLabel = "Controlled"
}
else
{
    $securityMaturityLevel = "Level 4"
    $securityMaturityLabel = "Mature"
}


Write-Host "Security Posture: $securityMaturityLevel $securityMaturityLabel"


# =============================================================================
# Technical Debt Trend
# =============================================================================

# NOTE: the current JSON model does not persist a historical series of
# CriticalFindings (only the latest snapshot value is available). If a future
# model version adds TechnicalDebt.History, this block compares the last two
# entries automatically; otherwise it reports a graceful fallback instead of
# fabricating historical data.

$debtTrendStatus = "Stable"
$debtTrendDetail = "No historical data available"

if(
    $null -ne $technicalDebt.History -and
    @($technicalDebt.History).Count -gt 1
)
{
    $debtHistory = @($technicalDebt.History)

    $currentDebt = [double]$debtHistory[-1].CriticalFindings
    $previousDebt = [double]$debtHistory[-2].CriticalFindings

    $debtChange = $currentDebt - $previousDebt

    if($debtChange -lt 0)
    {
        $debtTrendStatus = "Improving"
    }
    elseif($debtChange -gt 0)
    {
        $debtTrendStatus = "Declining"
    }
    else
    {
        $debtTrendStatus = "Stable"
    }

    $debtTrendDetail = "Previous: $previousDebt | Current: $currentDebt | Change: $debtChange"
}


Write-Host "Technical Debt Trend: $debtTrendStatus ($debtTrendDetail)"


# =============================================================================
# Release Recommendation
# =============================================================================

$releaseSummary = "Approved for release"
$releaseActions = @("<li>Proceed with standard release checklist</li>")

if($productionDecision -eq "NEEDS REVIEW")
{
    $releaseSummary = "Conditional release - pending review"
    $releaseActions = @("<li>Resolve open blockers before scheduling release</li>", "<li>Re-run Engineering Confidence Score after fixes</li>")
}
elseif($productionDecision -eq "NOT READY")
{
    $releaseSummary = "Release blocked"
    $releaseActions = @("<li>Address all blockers listed below</li>", "<li>Do not schedule a release until Confidence Score reaches 70%+</li>")
}


Write-Host "Release Recommendation: $releaseSummary"


# =============================================================================
# Architecture & Domains
# =============================================================================

# Conecta el modelo de arquitectura real (erp.json / layers.json / domains.json)
# con los 29 modulos reales de Health.value, ya clasificados por dominio en
# modules.json (generado por tools/dashboard/analyze-modules.ps1). No se
# inventa ninguna relacion aqui: solo se lee lo que analyze-modules.ps1 ya
# resolvio.

$erpInfo = LoadJson "erp.json"
$layers = @(LoadJson "layers.json")
$domains = @(LoadJson "domains.json")
$modulesData = @(LoadJson "modules.json")


$layersHtml = @()

foreach($layer in ($layers | Sort-Object order))
{
    $layersHtml += "<li>$($layer.order). $($layer.name)</li>"
}


$domainsHtml = @()

foreach($domain in $domains)
{
    $domainModules = @($modulesData | Where-Object { $_.domainId -eq $domain.id })

    $moduleCount = $domainModules.Count

    if($moduleCount -gt 0)
    {
        $domainAvg =
        [math]::Round(
            (($domainModules | Measure-Object -Property score -Sum).Sum / $moduleCount),
            2
        )

        $moduleNames = ($domainModules | ForEach-Object { $_.id }) -join ", "
    }
    else
    {
        $domainAvg = 0
        $moduleNames = "No modules mapped yet"
    }

    $domainsHtml +=
    "<tr><td>$($domain.name)</td><td>$moduleCount</td><td>$domainAvg%</td><td>$moduleNames</td></tr>"
}


$unmappedModules = @($modulesData | Where-Object { $_.domainId -eq "unmapped" })
$unmappedNames = ($unmappedModules | ForEach-Object { $_.id }) -join ", "

if([string]::IsNullOrEmpty($unmappedNames))
{
    $unmappedNames = "None"
}


Write-Host "Architecture: $($layers.Count) layers, $($domains.Count) domains, $($modulesData.Count) modules ($($unmappedModules.Count) unmapped)"


# =============================================================================
# Business Capability Map
# =============================================================================

# Dominio -> Modulos -> Features -> Procesos. No se infiere nada aqui: solo
# se lee lo que analyze-features.ps1 y analyze-processes.ps1 ya resolvieron
# contra el codigo real. Un dominio sin features/procesos mapeados lo dice
# explicitamente en vez de inventar contenido para llenar la tarjeta.

$featuresData = @(LoadJson "features.json")
$processesData = @(LoadJson "processes.json")
$tasksData = @(LoadJson "tasks.json")


$tasksByPriority = $tasksData | Group-Object -Property priority | Sort-Object Name

$tasksSummaryHtml = @()

foreach($group in $tasksByPriority)
{
    $tasksSummaryHtml += "<li>$($group.Name): $($group.Count)</li>"
}

if($tasksSummaryHtml.Count -eq 0)
{
    $tasksSummaryHtml += "<li>No tasks tracked yet</li>"
}


$capabilityMapHtml = @()

foreach($domain in $domains)
{
    $domainModuleIds = @($modulesData | Where-Object { $_.domainId -eq $domain.id } | ForEach-Object { $_.id })

    if($domainModuleIds.Count -eq 0)
    {
        $capabilityMapHtml +=
        "<tr><td>$($domain.name)</td><td>No modules mapped yet</td><td>-</td><td>-</td></tr>"
        continue
    }

    $moduleList = $domainModuleIds -join ", "

    $domainFeatureNames = @()

    foreach($moduleId in $domainModuleIds)
    {
        $moduleFeatureEntry = $featuresData | Where-Object { $_.module -eq $moduleId } | Select-Object -First 1

        if($null -ne $moduleFeatureEntry -and $moduleFeatureEntry.features.Count -gt 0)
        {
            $domainFeatureNames += @($moduleFeatureEntry.features | ForEach-Object { $_.name })
        }
    }

    if($domainFeatureNames.Count -gt 0)
    {
        $featureList = ($domainFeatureNames | Select-Object -Unique) -join ", "
    }
    else
    {
        $featureList = "No features mapped yet"
    }

    $domainProcessNames = @()

    foreach($process in $processesData)
    {
        $touchesDomain = @($process.steps | Where-Object { $domainModuleIds -contains $_.module -and $_.status -eq "verified" })

        if($touchesDomain.Count -gt 0)
        {
            $domainProcessNames += $process.process
        }
    }

    if($domainProcessNames.Count -gt 0)
    {
        $processList = ($domainProcessNames | Select-Object -Unique) -join ", "
    }
    else
    {
        $processList = "No processes mapped yet"
    }

    $capabilityMapHtml +=
    "<tr><td>$($domain.name)</td><td>$moduleList</td><td>$featureList</td><td>$processList</td></tr>"
}


Write-Host "Business Capability Map: $($domains.Count) domains, $($featuresData.Count) feature entries, $($processesData.Count) processes"

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
# Quality Gate Detail HTML
# =============================================================================

$qualityGateDetailHtml = @"
<div class='card'>
<h2>Quality Gate Detail</h2>
<p>Build Status: $buildStatus</p>
<p>Test Status: $testStatus (avg $testAverage%)</p>
<p>Coverage: $coverageStatus</p>
<p>Static Analysis: $staticAnalysisStatus ($staticAnalysisViolations violations)</p>
</div>
"@


# =============================================================================
# Security Posture HTML
# =============================================================================

$securityPostureHtml = @"
<div class='card'>
<h2>Security Posture</h2>
<div class='score'>$securityMaturityLevel - $securityMaturityLabel</div>
<p>Security Score: $($score.Security)%</p>
<p>Secrets detected: $($security.SecretsFound)</p>
<p>Anonymous findings: $($security.AnonymousDetected)</p>
<p>Connection Strings: $($security.ConnectionStringsFound)</p>
</div>
"@


# =============================================================================
# Technical Debt Trend HTML
# =============================================================================

$technicalDebtTrendHtml = @"
<div class='card'>
<h2>Technical Debt Trend</h2>
<div class='score'>$debtTrendStatus</div>
<p>$debtTrendDetail</p>
<p>Current Critical Findings: $($technicalDebt.CriticalFindings)</p>
</div>
"@


# =============================================================================
# Release Recommendation HTML
# =============================================================================

$releaseRecommendationHtml = @"
<div class='card'>
<h2>Release Recommendation</h2>
<div class='score'>$releaseSummary</div>
<p>Based on Production Decision: $productionDecision</p>
<h3>Reasons</h3>
<ul>
$($decisionBlockersHtml -join "`n")
</ul>
<h3>Required Actions</h3>
<ul>
$($releaseActions -join "`n")
</ul>
</div>
"@


# =============================================================================
# Architecture & Domains HTML
# =============================================================================

$architectureDomainsHtml = @"
<div class='card'>
<h2>Architecture &amp; Domains</h2>
<p><a href='../../PROGRESS.html'>&#9664; Ver Mapa Maestro de Arquitectura (PROGRESS.html)</a></p>
<p>ERP: $($erpInfo.name) v$($erpInfo.version) - $($erpInfo.status)</p>
<h3>Layers</h3>
<ul>
$($layersHtml -join "`n")
</ul>
<h3>Domains -&gt; Modules</h3>
<table>
<tr><th>Domain</th><th>Modules</th><th>Avg Score</th><th>Module List</th></tr>
$($domainsHtml -join "`n")
</table>
<h3>Unmapped Modules</h3>
<p>$unmappedNames</p>
</div>
"@


# =============================================================================
# Business Capability Map HTML
# =============================================================================

$capabilityMapCardHtml = @"
<div class='card'>
<h2>Business Capability Map</h2>
<p>Domain &rarr; Modules &rarr; Features &rarr; Processes</p>
<table>
<tr><th>Domain</th><th>Modules</th><th>Features</th><th>Processes</th></tr>
$($capabilityMapHtml -join "`n")
</table>
<h3>Tasks</h3>
<p>$($tasksData.Count) tracked tasks (source: tasks.json)</p>
<ul>
$($tasksSummaryHtml -join "`n")
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
"<title>ZH Technologies ERP Engineering Dashboard v20</title>",

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
$architectureDomainsHtml,
$capabilityMapCardHtml,
$confidenceHtml,
$riskAssessmentHtml,
$productionDecisionHtml,
$recommendationsCardHtml,
$productionGateHtml,
$qualityGateDetailHtml,
$securityPostureHtml,
$technicalDebtTrendHtml,
$releaseRecommendationHtml,
"<h1>ZH Technologies ERP Engineering Dashboard v20</h1>",


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
Write-Host "Dashboard v20 generated successfully." -ForegroundColor Green
Write-Host $Output
