# =============================================================================
# ZH Technologies
# Progress Dashboard â€” Engineering Health Report Renderer
# Canonical entry point (consolidated from the v13 -> v21 lineage)
#
# Historial completo de versiones anteriores (v2 .. v21, incluyendo backups
# y variantes "final"): tools/dashboard/archive/. Este archivo es la UNICA
# version activa; no crear render-dashboard-vNN.ps1 nuevos â€” evolucionar
# este archivo directamente y, si se quiere conservar un punto de referencia
# grande, copiar un snapshot a archive/ antes de un cambio mayor.
#
# Secciones (en orden de aparicion en el HTML generado):
#   Executive Summary, Architecture & Domains, Business Capability Map
#   (incluye Tasks), Architecture Risk Map (incluye Engineering Risk
#   Coverage), Engineering Confidence, Risk Assessment, Production Decision,
#   Recommended Next Actions, Production Gate Decision, Quality Gate Detail,
#   Security Posture, Technical Debt Trend, Release Recommendation,
#   Engineering Score, Quality Gate, Engineering Maturity, Production
#   Readiness, Security Analysis, Technical Debt, Engineering Trend,
#   Module Health, Engineering Roadmap.
#
# PROGRESS.html (mapa maestro, en la raiz del repo) NO se modifica. Este
# reporte solo referencia esa portada mediante un enlace relativo.
#
# Prerequisitos (correr antes de este script para refrescar los datos):
#   tools/dashboard/analyze-modules.ps1     -> modules.json
#   tools/dashboard/analyze-features.ps1    -> features.json
#   tools/dashboard/analyze-processes.ps1   -> processes.json
#   tools/dashboard/analyze-tasks.ps1       -> tasks.json
#   tools/dashboard/analyze-impact.ps1      -> impact.json
#   tools/dashboard/analyze-completion.ps1  -> completion-intelligence.json
#
# Ver tools/dashboard/README.md para el flujo de datos completo.
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
Write-Host " ZH Engineering Dashboard Renderer"
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



# =============================================================================
# Dashboard Summary (calculado por tools/dashboard/analyze-dashboard-summary.ps1)
#
# TODA la logica de calculo/decision (Engineering Confidence, bandas de
# riesgo, Production Decision, Quality Gate Detail, Security Posture,
# Technical Debt Trend, Release Recommendation, Production Gate, Roadmap,
# Trend, banderas ejecutivas) vive en ese analizador. El renderer solo lee
# los campos ya resueltos y arma el HTML -- ningun umbral, peso ni formula se
# evalua aqui.
# =============================================================================

$dashboardSummary = LoadJson "dashboard-summary.json"

$securityStatus = $dashboardSummary.securityStatus
$debtStatus = $dashboardSummary.debtStatus
$productionReadiness = $dashboardSummary.productionReadinessScore
$productionStatus = $dashboardSummary.productionStatus
$maturity = $dashboardSummary.maturity
$confidenceScore = $dashboardSummary.confidenceScore

$architectureRisk = $dashboardSummary.risk.architectureRisk
$securityRisk = $dashboardSummary.risk.securityRisk
$qualityRisk = $dashboardSummary.risk.qualityRisk
$technicalDebtRisk = $dashboardSummary.risk.technicalDebtRisk
$overallRisk = $dashboardSummary.risk.overallRisk

$productionDecision = $dashboardSummary.productionDecision
$decisionBlockersHtml = @($dashboardSummary.decisionBlockers | ForEach-Object { "<li>$_</li>" })
$recommendationsHtml = @($dashboardSummary.quickRecommendations | ForEach-Object { $i = 1 } { "<li>$i. $_</li>"; $i++ })

$testAverage = $dashboardSummary.qualityGateDetail.testAverage
$testStatus = $dashboardSummary.qualityGateDetail.testStatus
$buildStatus = $dashboardSummary.qualityGateDetail.buildStatus
$coverageStatus = $dashboardSummary.qualityGateDetail.coverageStatus
$staticAnalysisViolations = $dashboardSummary.qualityGateDetail.staticAnalysisViolations
$staticAnalysisStatus = $dashboardSummary.qualityGateDetail.staticAnalysisStatus

$securityMaturityLevel = $dashboardSummary.securityPosture.level
$securityMaturityLabel = $dashboardSummary.securityPosture.label

$debtTrendStatus = $dashboardSummary.debtTrend.status
$debtTrendDetail = $dashboardSummary.debtTrend.detail

$releaseSummary = $dashboardSummary.releaseRecommendation.summary
$releaseActions = @($dashboardSummary.releaseRecommendation.actions | ForEach-Object { "<li>$_</li>" })

Write-Host ""
Write-Host "Dashboard summary loaded" -ForegroundColor Green
Write-Host "Maturity : $maturity"
Write-Host "Security : $securityStatus"
Write-Host "Debt     : $debtStatus"
Write-Host "Engineering Confidence: $confidenceScore%"
Write-Host "Risk Assessment: Architecture=$architectureRisk Security=$securityRisk Quality=$qualityRisk Debt=$technicalDebtRisk Overall=$overallRisk"
Write-Host "Production Decision: $productionDecision"
Write-Host "Recommendations :" $recommendationsHtml.Count
Write-Host "Quality Gate Detail: Build=$buildStatus Test=$testStatus($testAverage%) Coverage=$coverageStatus Static=$staticAnalysisStatus"
Write-Host "Security Posture: $securityMaturityLevel $securityMaturityLabel"
Write-Host "Technical Debt Trend: $debtTrendStatus ($debtTrendDetail)"
Write-Host "Release Recommendation: $releaseSummary"


# =============================================================================
# Architecture & Domains
# =============================================================================

# Conecta el modelo de arquitectura real (erp.json / layers.json / domains.json)
# con los modulos reales de Health.value, ya clasificados por dominio en
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
# Architecture Risk Map
# =============================================================================

# Lee impact.json (calculado por analyze-impact.ps1) y agrega por dominio:
# total de modulos, total de features, total de procesos distintos, y el
# riesgo mas alto entre sus modulos. El renderer no inventa ni recalcula
# riesgo -- solo agrega lo que el analizador ya determino con evidencia real.

function Get-DomainRiskRank($level)
{
    switch($level)
    {
        "CRITICAL" { return 4 }
        "HIGH"     { return 3 }
        "MEDIUM"   { return 2 }
        default    { return 1 }
    }
}


$impactData = LoadJson "impact.json"
$impactDomains = @($impactData.domains)

$riskMapHtml = @()

foreach($domainImpact in $impactDomains)
{
    $modules = @($domainImpact.modules)

    $moduleCount = $modules.Count
    $featureCount = (($modules | Measure-Object -Property features -Sum).Sum)

    $processNames = @()

    foreach($module in $modules)
    {
        $processNames += @($module.processes | ForEach-Object { $_.name })
    }

    $processCount = @($processNames | Select-Object -Unique).Count

    $domainRisk = "LOW"

    foreach($module in $modules)
    {
        if((Get-DomainRiskRank $module.risk) -gt (Get-DomainRiskRank $domainRisk))
        {
            $domainRisk = $module.risk
        }
    }

    $riskMapHtml +=
    "<tr><td>$($domainImpact.domain)</td><td>$moduleCount</td><td>$featureCount</td><td>$processCount</td><td>$domainRisk</td></tr>"
}


Write-Host "Architecture Risk Map: $($impactDomains.Count) domains, Risk Coverage $($impactData.coverage.percentage)%"


# =============================================================================
# Model Health
# =============================================================================

# Lee model-health.json (calculado por tools/dashboard/validate-dashboard-model.ps1)
# -- el renderer no valida nada por su cuenta, solo presenta lo que el
# validador ya determino sobre la integridad referencial y completitud del
# modelo de conocimiento (modules/domains/features/processes/tasks).

$modelHealth = LoadJson "model-health.json"

$modelHealthStatus = "GREEN"

if($modelHealth.brokenReferences -gt 0)
{
    $modelHealthStatus = "RED"
}
elseif($modelHealth.integrityScore -lt 90 -or $modelHealth.missingEvidence -gt 0)
{
    $modelHealthStatus = "YELLOW"
}


Write-Host "Model Health: Integrity=$($modelHealth.integrityScore)% Broken=$($modelHealth.brokenReferences) MissingEvidence=$($modelHealth.missingEvidence) Unmapped=$($modelHealth.unmappedItems) Status=$modelHealthStatus"


# =============================================================================
# Architecture Progress
# =============================================================================

# Lee architecture-progress.json (calculado por
# tools/dashboard/analyze-progress-map.ps1 a partir de PROGRESS.html). El
# renderer no vuelve a leer PROGRESS.html ni reinterpreta su estructura --
# solo presenta lo que el analizador ya extrajo. PROGRESS.html usa "Etapas"
# como unidad organizativa (no "Dominios" -- ese es un modelo distinto, ver
# domains.json), asi que esta seccion muestra avance por Etapa, fiel a la
# estructura real del mapa maestro.

$architectureProgress = LoadJson "architecture-progress.json"

$progressLayersHtml = @()

foreach($layer in $architectureProgress.layers)
{
    $progressLayersHtml += "<tr><td>$($layer.label)</td><td>$($layer.pct)%</td><td>$($layer.status)</td></tr>"
}


$progressStagesHtml = @()

foreach($stage in $architectureProgress.stages)
{
    $progressStagesHtml += "<tr><td>$($stage.name)</td><td>$($stage.done) / $($stage.totalTasks)</td><td>$($stage.pct)%</td></tr>"
}


$progressCompletedSample = @($architectureProgress.completed | Select-Object -First 15 | ForEach-Object { "<li>[$($_.stage)] $($_.name)</li>" })

if($architectureProgress.completed.Count -gt 15)
{
    $progressCompletedSample += "<li>&hellip; and $($architectureProgress.completed.Count - 15) more (see architecture-progress.json)</li>"
}


$progressPendingHtml = @($architectureProgress.pending | ForEach-Object { "<li>[$($_.stage) / $($_.phase)] $($_.name)</li>" })

if($progressPendingHtml.Count -eq 0)
{
    $progressPendingHtml = @("<li>No fully-unstarted phases pending</li>")
}


$progressNextStepsHtml = @($architectureProgress.nextSteps | ForEach-Object { "<li>[$($_.stage) / $($_.phase)] $($_.name)</li>" })

if($progressNextStepsHtml.Count -eq 0)
{
    $progressNextStepsHtml = @("<li>No open next steps</li>")
}


Write-Host "Architecture Progress: Global=$($architectureProgress.global.pct)% Stages=$($architectureProgress.stages.Count) Completed=$($architectureProgress.completed.Count) Pending=$($architectureProgress.pending.Count) NextSteps=$($architectureProgress.nextSteps.Count)"

# =============================================================================
# ERP Completion Intelligence
# =============================================================================

# Lee completion-intelligence.json (calculado por
# tools/dashboard/analyze-completion.ps1). El renderer no recalcula ninguna
# conclusion aqui -- solo presenta lo que el analizador ya derivo a partir de
# architecture-progress.json, dashboard-model-v12.json, model-health.json,
# modules.json, features.json, processes.json y tasks.json.

$completionIntelligence = LoadJson "completion-intelligence.json"

$criticalGapsHtml = @($completionIntelligence.criticalGaps | ForEach-Object { "<li>$_</li>" })
$recommendedOrderHtml = @($completionIntelligence.recommendedOrder | ForEach-Object { "<li>$_</li>" })
$quickWinsHtml = @($completionIntelligence.quickWins | ForEach-Object { "<li>$_</li>" })

Write-Host "ERP Completion Intelligence: ErpCompletion=$($completionIntelligence.erpCompletion)% OverallStatus=$($completionIntelligence.overallStatus) CriticalGaps=$($completionIntelligence.criticalGaps.Count) QuickWins=$($completionIntelligence.quickWins.Count)"


# =============================================================================
# Navigation Map (calculado por tools/dashboard/analyze-navigation-map.ps1).
# TODA relacion Layer->Stage, Layer->coreModule->modulo real, Layer->Domains,
# Layer->estadisticas de Database/Frontend/Backend ya viene resuelta en este
# JSON. El renderer NUNCA decide estas relaciones -- solo las presenta.
# =============================================================================

$navigationMap = LoadJson "navigation-map.json"
$navLayersById = @{}
foreach($navLayer in $navigationMap.layers) { $navLayersById[$navLayer.id] = $navLayer }

Write-Host "Navigation Map loaded: $($navigationMap.layers.Count) layer records"


# =============================================================================
# Explorer Index (calculado por tools/dashboard/analyze-explorer-index.ps1).
# Backend consolidado de Module Health, Impact Explorer, Risk-of-change,
# indice inverso Archivo->Feature/Process/Task->Module->Domain, Vista
# Ejecutiva y busqueda global. El renderer NO recalcula nada de esto -- solo
# indexa una vez (hashtable por id de modulo, por archivo) y formatea.
# =============================================================================

$explorerIndex = LoadJson "explorer-index.json"

# Indice unico, construido una sola vez -- evita recorrer $explorerIndex.modules
# repetidamente cada vez que se necesita el perfil de un modulo.
$explorerModuleById = @{}
foreach($mp in $explorerIndex.modules) { $explorerModuleById[$mp.id] = $mp }

# Indice archivo -> referencias (para la navegacion inversa Archivo->Feature/
# Process/Task->Module->Domain, Fase 5). Tambien construido una sola vez.
$reverseFileIndexByPath = @{}
foreach($rf in $explorerIndex.reverseFileIndex) { $reverseFileIndexByPath[$rf.file] = $rf.referencedBy }

Write-Host "Explorer Index loaded: $($explorerIndex.modules.Count) module profiles indexed, $($explorerIndex.reverseFileIndex.Count) files in reverse index, $($explorerIndex.searchEntries.Count) search entries"

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


# =============================================================================
# Presentation helpers (pure formatting -- no new data, no new relations)
# =============================================================================

# -----------------------------------------------------------------------------
# Paleta reducida a 4 estados semanticos (informacion / completado / pendiente
# / error). Todas las funciones de color de este archivo devuelven
# exclusivamente uno de estos 4 valores -- ningun degradado, ningun color
# adicional (el morado/cian de sesiones anteriores fueron eliminados).
# -----------------------------------------------------------------------------
$colorInfo = "#2563eb"
$colorDone = "#16a34a"
$colorPending = "#d97706"
$colorError = "#dc2626"

function Get-RiskBandColor($band)
{
    switch($band)
    {
        "Safe"          { return $colorDone }
        "Low Risk"      { return $colorInfo }
        "Medium Risk"   { return $colorPending }
        "High Risk"     { return $colorError }
        "Critical"      { return $colorError }
        default         { return $colorInfo }
    }
}

function Get-ScoreColor($value)
{
    $v = [double]$value
    if($v -ge 80) { return $colorDone }
    elseif($v -ge 60) { return $colorPending }
    else { return $colorError }
}

function Get-CountColor($value, $amberAt, $redAt)
{
    $v = [double]$value
    if($v -ge $redAt) { return $colorError }
    elseif($v -ge $amberAt) { return $colorPending }
    else { return $colorDone }
}

function Get-RiskColor($level)
{
    switch($level)
    {
        "LOW"      { return $colorDone }
        "MEDIUM"   { return $colorPending }
        "HIGH"     { return $colorError }
        "CRITICAL" { return $colorError }
        default    { return $colorInfo }
    }
}

function Get-RiskChartScore($level)
{
    switch($level)
    {
        "LOW"      { return 95 }
        "MEDIUM"   { return 70 }
        "HIGH"     { return 40 }
        "CRITICAL" { return 15 }
        default    { return 50 }
    }
}

function ConvertTo-SafeId($text)
{
    $safe = [regex]::Replace([string]$text, "[^a-zA-Z0-9\-]", "-")
    return $safe.ToLower()
}

function HtmlEncodeText($text)
{
    return [System.Net.WebUtility]::HtmlEncode([string]$text)
}

function Build-Gauge($pct, $color, $size, $labelSmall)
{
    $r = 52
    $circ = [math]::Round(2 * [math]::PI * $r, 2)
    $offset = [math]::Round($circ - ($circ * [double]$pct / 100), 2)

    return @"
<div class='gauge' style='width:$($size)px;height:$($size)px'>
<svg viewBox='0 0 120 120' width='$size' height='$size'>
<circle cx='60' cy='60' r='$r' fill='none' stroke='#e2e8f0' stroke-width='10'/>
<circle cx='60' cy='60' r='$r' fill='none' stroke='$color' stroke-width='10' stroke-linecap='round' stroke-dasharray='$circ' stroke-dashoffset='$offset' transform='rotate(-90 60 60)'/>
</svg>
<div class='gpct'>$pct<small>$labelSmall</small></div>
</div>
"@
}

function Build-RadarSvg($axes)
{
    $cx = 130
    $cy = 130
    $maxR = 95
    $n = $axes.Count
    $angleStep = 360.0 / $n
    $points = @()
    $axisLines = @()
    $labels = @()

    for($i = 0; $i -lt $n; $i++)
    {
        $angleDeg = -90 + ($i * $angleStep)
        $rad = $angleDeg * [math]::PI / 180

        $val = [double]$axes[$i].value
        if($val -gt 100) { $val = 100 }
        if($val -lt 0) { $val = 0 }

        $r = $maxR * ($val / 100)
        $x = [math]::Round($cx + ($r * [math]::Cos($rad)), 1)
        $y = [math]::Round($cy + ($r * [math]::Sin($rad)), 1)
        $points += "$x,$y"

        $ax = [math]::Round($cx + ($maxR * [math]::Cos($rad)), 1)
        $ay = [math]::Round($cy + ($maxR * [math]::Sin($rad)), 1)
        $axisLines += "<line x1='$cx' y1='$cy' x2='$ax' y2='$ay' stroke='#e2e8f0' stroke-width='1'/>"

        $lx = [math]::Round($cx + (($maxR + 22) * [math]::Cos($rad)), 1)
        $ly = [math]::Round($cy + (($maxR + 22) * [math]::Sin($rad)), 1)
        $anchor = "middle"
        if($lx -gt ($cx + 5)) { $anchor = "start" }
        elseif($lx -lt ($cx - 5)) { $anchor = "end" }
        $labels += "<text x='$lx' y='$ly' font-size='10' fill='var(--muted)' text-anchor='$anchor'>$($axes[$i].label) ($([math]::Round($val,1)))</text>"
    }

    $ringsHtml = @()
    foreach($ringPct in @(25, 50, 75, 100))
    {
        $ringsHtml += "<circle cx='$cx' cy='$cy' r='$([math]::Round($maxR * $ringPct / 100,1))' fill='none' stroke='#eef2f6' stroke-width='1'/>"
    }

    $polygon = $points -join " "

    return @"
<svg viewBox='0 0 260 260' width='100%' height='260' class='radar-svg'>
$($ringsHtml -join "`n")
$($axisLines -join "`n")
<polygon points='$polygon' fill='rgba(37,99,235,.25)' stroke='#2563eb' stroke-width='2'/>
$($labels -join "`n")
</svg>
"@
}

function Build-Sparkline($historyItems)
{
    $count = @($historyItems).Count

    if($count -lt 2)
    {
        return "<p class='muted-note'>No hay suficiente historial para graficar (se requieren 2+ snapshots).</p>"
    }

    $scores = @($historyItems | ForEach-Object { [double]$_.Score })
    $min = ($scores | Measure-Object -Minimum).Minimum
    $max = ($scores | Measure-Object -Maximum).Maximum
    if($max -eq $min) { $max = $min + 1 }

    $w = 320
    $h = 60
    $step = $w / ($count - 1)

    $pts = @()
    for($i = 0; $i -lt $count; $i++)
    {
        $x = [math]::Round($i * $step, 1)
        $y = [math]::Round($h - (($scores[$i] - $min) / ($max - $min) * $h), 1)
        $pts += "$x,$y"
    }

    return @"
<svg viewBox='0 0 $w $h' width='100%' height='$h' class='sparkline-svg'>
<polyline points='$($pts -join " ")' fill='none' stroke='#2563eb' stroke-width='2'/>
</svg>
"@
}

function Build-StageTimeline($stages)
{
    $nodes = @()

    foreach($stage in $stages)
    {
        $color = Get-ScoreColor $stage.pct
        $safeId = ConvertTo-SafeId $stage.name

        $nodes +=
@"
<div class='tl-node' onclick="document.getElementById('stagedetail-$safeId').open=true;document.getElementById('stagedetail-$safeId').scrollIntoView({behavior:'smooth'});">
<div class='tl-dot' style='background:$color'>$($stage.pct)%</div>
<div class='tl-label'>$($stage.name)</div>
<div class='tl-sub'>$($stage.done) / $($stage.totalTasks)</div>
</div>
"@
    }

    return "<div class='timeline'>$($nodes -join "<div class='tl-line'></div>")</div>"
}

function Build-RowHeatmap($items)
{
    $cellsHtml = @()

    foreach($item in $items)
    {
        $cellsHtml +=
@"
<div class='heat-cell' style='background:$($item.color)' title='$($item.label): $($item.value)'>
<div class='heat-v'>$($item.value)</div>
<div class='heat-l'>$($item.label)</div>
</div>
"@
    }

    return "<div class='heat-row'>$($cellsHtml -join "`n")</div>"
}

function Build-ModuleHeatmap($modulesForHeatmap)
{
    $metricCols = @("architecture", "tests", "documentation", "backend", "frontend")
    $headerCells = ($metricCols | ForEach-Object { "<div class='mh-col-h'>$_</div>" }) -join ""

    $rowsHtml = @()

    foreach($m in $modulesForHeatmap)
    {
        $cells = @()
        foreach($col in $metricCols)
        {
            $val = [double]$m.$col
            $cells += "<div class='heat-mini' style='background:$(Get-ScoreColor $val)' title='$($m.id) - $col : $val'>$val</div>"
        }

        $rowsHtml +=
        "<div class='mh-row'><div class='mh-name'>$($m.id)</div>$($cells -join '')</div>"
    }

    return @"
<div class='module-heatmap'>
<div class='mh-row mh-head'><div class='mh-name'></div>$headerCells</div>
$($rowsHtml -join "`n")
</div>
"@
}

function Build-RiskHeatmap($domainsImpact)
{
    $levels = @("LOW", "MEDIUM", "HIGH", "CRITICAL")
    $headerCells = ($levels | ForEach-Object { "<div class='mh-col-h'>$_</div>" }) -join ""

    $rowsHtml = @()

    foreach($domainImpact in $domainsImpact)
    {
        $modulesInDomain = @($domainImpact.modules)
        $cells = @()

        foreach($level in $levels)
        {
            $matchCount = @($modulesInDomain | Where-Object { $_.risk -eq $level }).Count
            $color = "#eef2f6"
            if($matchCount -gt 0) { $color = Get-RiskColor $level }
            $cellText = ''
            if($matchCount -gt 0) { $cellText = $matchCount }
            $cells += "<div class='heat-mini' style='background:$color' title='$($domainImpact.domain) - $level : $matchCount module(s)'>$cellText</div>"
        }

        $rowsHtml += "<div class='mh-row'><div class='mh-name'>$($domainImpact.domain)</div>$($cells -join '')</div>"
    }

    return @"
<div class='module-heatmap'>
<div class='mh-row mh-head'><div class='mh-name'></div>$headerCells</div>
$($rowsHtml -join "`n")
</div>
"@
}


# ==============================
# Roadmap (leido de dashboard-summary.json -- ningun umbral se evalua aqui)
# ==============================

$roadmapHtml = @($dashboardSummary.roadmap | ForEach-Object {
    "<li><b>$($_.order). $($_.title)</b><br/>$($_.detail)<br/>Priority: $($_.priority)</li>"
})

if($roadmapHtml.Count -eq 0)
{
    $roadmapHtml += "<li>No production blockers detected</li>"
}


Write-Host "Roadmap :" $roadmapHtml.Count
# =============================================================================
# Trend Data (historial = presentacion de model.Trend.History ya existente;
# current/previous/change/status vienen de dashboard-summary.json)
# =============================================================================

$trendHistoryHtml = @()

if($null -ne $model.Trend -and $model.Trend.History.Count -gt 0)
{
    foreach($item in $model.Trend.History)
    {
        $trendHistoryHtml += "<tr><td>$($item.Date)</td><td>$($item.Score)%</td></tr>"
    }
}

$currentTrend = $dashboardSummary.trend.current
$previousTrend = $dashboardSummary.trend.previous
$trendChange = $dashboardSummary.trend.change
$trendStatus = $dashboardSummary.trend.status

Write-Host "Trend prepared"
Write-Host "Current :" $currentTrend
Write-Host "Previous:" $previousTrend
Write-Host "Change  :" $trendChange
Write-Host "Status  :" $trendStatus

# =============================================================================
# Production Gate (leido de dashboard-summary.json)
# =============================================================================

$productionGate = @($dashboardSummary.productionGate | ForEach-Object {
    $tag = if($_.status -eq "PASSED") { "[PASSED]" } else { "[FAILED]" }
    "<li>$tag $($_.gate) - $($_.detail)</li>"
})

Write-Host "ProductionGate Items:" $productionGate.Count

# =============================================================================
# Executive KPI Strip (7 KPIs -- values already computed above, zero new data)
# =============================================================================

$businessCapabilityPct = [double]$impactData.coverage.percentage

function Build-Kpi($label, $value, $suffix, $color, $badge, $pct)
{
    $barPct = [math]::Round([double]$pct, 1)
    if($barPct -gt 100) { $barPct = 100 }
    if($barPct -lt 0) { $barPct = 0 }

    return @"
<div class='kpi-tile'>
<div class='kpi-top'>
<span class='kpi-label'>$label</span>
<span class='badge' style='background:$color;color:#fff'>$badge</span>
</div>
<div class='kpi-value' style='color:$color'>$value$suffix</div>
<div class='kpi-bar'><div class='kpi-bar-fill' style='width:$barPct%;background:$color'></div></div>
</div>
"@
}

$kpiEngineeringScore = Build-Kpi "Engineering Score" $score.Overall "%" (Get-ScoreColor $score.Overall) $maturity $score.Overall
$kpiErpCompletion = Build-Kpi "ERP Completion" $completionIntelligence.erpCompletion "%" (Get-ScoreColor $completionIntelligence.erpCompletion) $completionIntelligence.overallStatus $completionIntelligence.erpCompletion
$kpiArchitectureHealth = Build-Kpi "Architecture Health" $completionIntelligence.architectureHealth "%" (Get-ScoreColor $completionIntelligence.architectureHealth) "Stable" $completionIntelligence.architectureHealth
$kpiProductionReadiness = Build-Kpi "Production Readiness" $productionReadiness "%" (Get-ScoreColor $productionReadiness) $productionStatus $productionReadiness
$kpiOverallRisk = Build-Kpi "Overall Risk" $overallRisk "" (Get-RiskColor $overallRisk) $overallRisk (Get-RiskChartScore $overallRisk)
$kpiModelIntegrity = Build-Kpi "Model Integrity" $modelHealth.integrityScore "%" (Get-ScoreColor $modelHealth.integrityScore) $modelHealthStatus $modelHealth.integrityScore
$kpiBusinessCapability = Build-Kpi "Business Capability" $businessCapabilityPct "%" (Get-ScoreColor $businessCapabilityPct) "$($impactData.coverage.mappedFeaturePoints)/$($impactData.coverage.totalFeaturePoints)" $businessCapabilityPct

$kpiStripHtml = @"
<div class='kpi-strip'>
$kpiEngineeringScore
$kpiErpCompletion
$kpiArchitectureHealth
$kpiProductionReadiness
$kpiOverallRisk
$kpiModelIntegrity
$kpiBusinessCapability
</div>
"@

Write-Host "KPI Strip built (7 tiles)"


# =============================================================================
# Search Index -- sourced ENTIRELY from explorer-index.json.searchEntries
# (single consolidated source computed by analyze-explorer-index.ps1: Module,
# Domain, Feature, Process, Task, File). The renderer only adds a `target`
# per entry to know which UI action to trigger -- no new relation, no
# recomputation, purely a presentation mapping over an already-built list.
# =============================================================================

$searchEntries = @($explorerIndex.searchEntries | ForEach-Object {
    $entry = $_
    $target = "roadmap"
    $kind = "scroll"

    switch($entry.type)
    {
        "Module"  { $target = $entry.moduleId; $kind = "module-panel" }
        "Feature" { $target = $entry.moduleId; $kind = "module-panel" }
        "Process" { $target = $entry.moduleId; $kind = "module-panel" }
        "Domain"  { $target = "business-capability"; $kind = "group" }
        "Task"    { $target = "roadmap"; $kind = "group" }
        "File"    { $target = $entry.label; $kind = "file-panel" }
    }

    [ordered]@{ type = $entry.type; label = $entry.label; target = $target; kind = $kind }
})

$searchIndexJson = ($searchEntries | ConvertTo-Json -Depth 3 -Compress)

Write-Host "Search index built: $($searchEntries.Count) entries (source: explorer-index.json)"


# =============================================================================
# Architecture Explorer tree (Domain -> Module -> Feature -> Process -> Task -> File)
# Solo relaciones ya presentes en modules.json/features.json/processes.json/
# tasks.json. Un task se asocia a un modulo unicamente si su evidencia real
# (ruta de archivo) contiene el nombre del modulo -- coincidencia textual
# sobre datos ya existentes, no una relacion nueva inventada.
# =============================================================================

$explorerNodesHtml = @()

foreach($domain in $domains)
{
    $domainModules = @($modulesData | Where-Object { $_.domainId -eq $domain.id })

    if($domainModules.Count -eq 0)
    {
        $explorerNodesHtml +=
        "<details class='tree-node'><summary>$($domain.name) <span class='tree-count'>0 modules</span></summary><div class='tree-empty'>No modules mapped yet</div></details>"
        continue
    }

    $moduleNodesHtml = @()

    foreach($m in $domainModules)
    {
        $safeModuleId = ConvertTo-SafeId $m.id

        $featureEntry = $featuresData | Where-Object { $_.module -eq $m.id } | Select-Object -First 1
        $featureNodesHtml = @()

        if($null -ne $featureEntry -and $featureEntry.features.Count -gt 0)
        {
            foreach($feature in $featureEntry.features)
            {
                $evidenceHtml = ($feature.evidence | ForEach-Object { "<li class='tree-file'>$_</li>" }) -join ""
                $featureNodesHtml +=
                "<details class='tree-node'><summary>$($feature.name) <span class='pill pill-d'>$($feature.status)</span></summary><ul class='tree-files'>$evidenceHtml</ul></details>"
            }
        }
        else
        {
            $reason = if($null -ne $featureEntry -and $featureEntry.reason) { $featureEntry.reason } else { "No features mapped yet" }
            $featureNodesHtml += "<div class='tree-empty'>$reason</div>"
        }

        $processNodesHtml = @()
        foreach($process in $processesData)
        {
            foreach($step in $process.steps)
            {
                if($step.module -eq $m.id)
                {
                    $stepPill = if($step.status -eq "verified") { "pill-d" } else { "pill-n" }
                    $stepEvidence = @()
                    if($null -ne $step.evidence) { $stepEvidence = @($step.evidence) }
                    $stepEvidenceHtml = ($stepEvidence | ForEach-Object { "<li class='tree-file'>$_</li>" }) -join ""
                    $processNodesHtml +=
                    "<details class='tree-node'><summary>$($process.process) / $($step.name) <span class='pill $stepPill'>$($step.status)</span></summary><ul class='tree-files'>$stepEvidenceHtml</ul></details>"
                }
            }
        }
        if($processNodesHtml.Count -eq 0)
        {
            $processNodesHtml += "<div class='tree-empty'>No process steps evidenced for this module</div>"
        }

        $moduleTaskMatches = @($tasksData | Where-Object { @($_.evidence) -match [regex]::Escape("/$($m.id)/") })
        $taskNodesHtml = @()
        foreach($task in $moduleTaskMatches)
        {
            $taskNodesHtml += "<li class='tree-task'>[$($task.priority)] $($task.task)</li>"
        }
        if($taskNodesHtml.Count -eq 0)
        {
            $taskNodesHtml += "<li class='tree-empty-li'>No tasks with evidence pointing at this module</li>"
        }

        $moduleNodesHtml +=
@"
<details class='tree-node' id='mod-$safeModuleId'>
<summary>$($m.id) <span class='pill' style='background:$(Get-ScoreColor $m.score);color:#fff'>$($m.score)%</span></summary>
<div class='tree-group-label'>Features</div>
$($featureNodesHtml -join "`n")
<div class='tree-group-label'>Process steps</div>
$($processNodesHtml -join "`n")
<div class='tree-group-label'>Related tasks (evidence-matched)</div>
<ul class='tree-files'>$($taskNodesHtml -join "")</ul>
</details>
"@
    }

    $explorerNodesHtml +=
    "<details class='tree-node'><summary>$($domain.name) <span class='tree-count'>$($domainModules.Count) modules</span></summary>$($moduleNodesHtml -join "`n")</details>"
}

$explorerTreeHtml = "<div class='explorer-tree'><details class='tree-node' open><summary>ERP</summary>$($explorerNodesHtml -join "`n")</details></div>"

Write-Host "Architecture Explorer tree built: $($domains.Count) domains"


# =============================================================================
# Architecture Map (interactive SVG -- Stage boxes colored by completion %)
# =============================================================================

$stageBoxesHtml = @()
$stageDetailsHtml = @()
$boxX = 10

foreach($stage in $architectureProgress.stages)
{
    $safeStageId = ConvertTo-SafeId $stage.name
    $boxColor = Get-ScoreColor $stage.pct
    $boxWidth = 170

    $stageBoxesHtml +=
@"
<g class='arch-map-node' onclick="document.getElementById('stagedetail-$safeStageId').open=true;document.getElementById('stagedetail-$safeStageId').scrollIntoView({behavior:'smooth'});">
<rect x='$boxX' y='10' width='$boxWidth' height='64' rx='10' fill='$boxColor' fill-opacity='0.15' stroke='$boxColor' stroke-width='2'/>
<text x='$($boxX + $boxWidth/2)' y='34' text-anchor='middle' font-size='12' font-weight='700' fill='var(--text)'>$($stage.name)</text>
<text x='$($boxX + $boxWidth/2)' y='54' text-anchor='middle' font-size='16' font-weight='800' fill='$boxColor'>$($stage.pct)%</text>
<text x='$($boxX + $boxWidth/2)' y='68' text-anchor='middle' font-size='9' fill='var(--muted)'>$($stage.done)/$($stage.totalTasks) tasks</text>
</g>
"@

    $boxX += ($boxWidth + 16)

    $phaseRowsHtml = @()
    foreach($phase in $stage.phases)
    {
        $pillClass = "pill-$($phase.statusLetter)"
        $phaseRowsHtml +=
@"
<div class='phase-row'>
<div class='phase-bar-track'><div class='phase-bar-fill' style='width:$($phase.pct)%;background:$(Get-ScoreColor $phase.pct)'></div></div>
<div class='phase-info'>
<span class='phase-name'>$($phase.name)</span>
<span class='pill $pillClass'>$($phase.statusLetter)</span>
<span class='phase-pct'>$($phase.pct)% ($($phase.done)/$($phase.totalTasks))</span>
</div>
<div class='phase-desc'>$($phase.description)</div>
</div>
"@
    }

    $stageDetailsHtml +=
@"
<details class='card-section' id='stagedetail-$safeStageId'>
<summary>$($stage.name) &mdash; $($stage.pct)% ($($stage.done)/$($stage.totalTasks) tasks)</summary>
$($phaseRowsHtml -join "`n")
</details>
"@
}

$archMapWidth = $boxX
$archMapSvgHtml = "<svg viewBox='0 0 $archMapWidth 84' width='100%' height='84' class='arch-map-svg'>$($stageBoxesHtml -join "`n")</svg>"

Write-Host "Architecture Map built: $($architectureProgress.stages.Count) stage nodes"


# =============================================================================
# Heatmaps (SVG, no images, no libraries)
# =============================================================================

$engineeringScoreHeatmap = Build-RowHeatmap @(
    @{ label = "Architecture"; value = $score.Architecture; color = (Get-ScoreColor $score.Architecture) },
    @{ label = "ModuleHealth"; value = $score.ModuleHealth; color = (Get-ScoreColor $score.ModuleHealth) },
    @{ label = "Quality"; value = $score.Quality; color = (Get-ScoreColor $score.Quality) },
    @{ label = "Security"; value = $score.Security; color = (Get-ScoreColor $score.Security) },
    @{ label = "Dependencies"; value = $score.Dependencies; color = (Get-ScoreColor $score.Dependencies) }
)

$securityHeatmap = Build-RowHeatmap @(
    @{ label = "Secrets"; value = $security.SecretsFound; color = (Get-CountColor $security.SecretsFound 1 10) },
    @{ label = "Anonymous"; value = $security.AnonymousDetected; color = (Get-CountColor $security.AnonymousDetected 1 10) },
    @{ label = "ConnStrings"; value = $security.ConnectionStringsFound; color = (Get-CountColor $security.ConnectionStringsFound 1 5) }
)

$technicalDebtHeatmap = Build-RowHeatmap @(
    @{ label = "TODO"; value = $technicalDebt.TODO; color = (Get-CountColor $technicalDebt.TODO 50 300) },
    @{ label = "FIXME"; value = $technicalDebt.FIXME; color = (Get-CountColor $technicalDebt.FIXME 5 20) },
    @{ label = "HACK"; value = $technicalDebt.HACK; color = (Get-CountColor $technicalDebt.HACK 1 5) },
    @{ label = "NotImpl"; value = $technicalDebt.NotImplemented; color = (Get-CountColor $technicalDebt.NotImplemented 1 5) },
    @{ label = "Critical"; value = $technicalDebt.CriticalFindings; color = (Get-CountColor $technicalDebt.CriticalFindings 10 20) }
)

$architectureLayersHeatmap = Build-RowHeatmap (
    @($architectureProgress.layers | ForEach-Object { @{ label = $_.label; value = $_.pct; color = (Get-ScoreColor $_.pct) } })
)

$moduleHealthHeatmapHtml = Build-ModuleHeatmap $modulesData
$riskHeatmapHtml = Build-RiskHeatmap $impactDomains

Write-Host "Heatmaps built: Engineering Score, Security, Technical Debt, Architecture Layers, Module Health, Risk"


# =============================================================================
# Radar chart (Architecture / Quality / Security / Technical Debt /
# Production Readiness / Engineering Health)
# =============================================================================

$radarSvgHtml = Build-RadarSvg @(
    @{ label = "Architecture"; value = $score.Architecture },
    @{ label = "Quality"; value = $score.Quality },
    @{ label = "Security"; value = $score.Security },
    @{ label = "Tech Debt"; value = (Get-RiskChartScore $technicalDebtRisk) },
    @{ label = "Prod. Readiness"; value = $productionReadiness },
    @{ label = "Eng. Health"; value = $score.Overall }
)

Write-Host "Radar chart built (6 axes)"


# =============================================================================
# Timeline (Stages -> Phases -> Completion -> Remaining work)
# =============================================================================

$stageTimelineHtml = Build-StageTimeline $architectureProgress.stages
$sparklineHtml = Build-Sparkline $model.Trend.History

Write-Host "Timeline and sparkline built"


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


$securityHealth = $dashboardSummary.executiveFlags.securityHealth
$qualityHealth = $dashboardSummary.executiveFlags.qualityHealth
$debtHealth = $dashboardSummary.executiveFlags.debtHealth

Write-Host "Executive summary flags: Security=$securityHealth Quality=$qualityHealth Debt=$debtHealth"


# =============================================================================
# SECTION: Executive Summary
# =============================================================================

$execSummaryHtml = @"
<section id='exec-summary' class='panel' data-group='roadmap'>
<h2>Executive Summary</h2>
<div class='panel-grid-2'>
<div class='sub-card'>
<h3>Status</h3>
<p class='big-status' style='color:$(Get-ScoreColor $score.Overall)'>$($completionIntelligence.overallStatus)</p>
<p>Engineering Score: <b>$($score.Overall)%</b> &middot; Maturity: <b>$maturity</b></p>
<p>Security: <span class='pill $(if($securityHealth -eq "GOOD"){"pill-d"}else{"pill-n"})'>$securityHealth</span>
Quality: <span class='pill $(if($qualityHealth -eq "GOOD"){"pill-d"}else{"pill-n"})'>$qualityHealth</span>
Technical Debt: <span class='pill $(if($debtHealth -eq "LOW"){"pill-d"}else{"pill-n"})'>$debtHealth</span></p>
<p>Engineering Confidence: <b>$confidenceScore%</b> &middot; Overall Risk: <b style='color:$(Get-RiskColor $overallRisk)'>$overallRisk</b></p>
</div>
<div class='sub-card'>
<h3>Production Decision</h3>
<p class='big-status' style='color:$(Get-ScoreColor $productionReadiness)'>$productionDecision</p>
<h4>Blockers</h4>
<ul>$($decisionBlockersHtml -join "`n")</ul>
<h4>Gate checks</h4>
<ul>$($productionGate -join "`n")</ul>
</div>
</div>
<details class='card-section'>
<summary>Recommended Next Actions ($($recommendationsHtml.Count))</summary>
<ul>$($recommendationsHtml -join "`n")</ul>
</details>
</section>
"@


# =============================================================================
# SECTION: ERP Completion Intelligence
# =============================================================================

$erpCompletionHtml = @"
<section id='erp-completion' class='panel' data-group='roadmap'>
<h2>ERP Completion Intelligence</h2>
<div class='panel-grid-2'>
<div class='sub-card center'>
$(Build-Gauge $completionIntelligence.erpCompletion (Get-ScoreColor $completionIntelligence.erpCompletion) 160 "%")
<p>ERP Completion &mdash; fuente: architecture-progress.json (PROGRESS.html)</p>
<p>Overall Status: <b>$($completionIntelligence.overallStatus)</b> &middot; Production Readiness: <b>$($completionIntelligence.productionReadiness)</b></p>
<p>Architecture Health: $($completionIntelligence.architectureHealth)% &middot; Engineering Health: $($completionIntelligence.engineeringHealth)%</p>
<p>Estimated Remaining Areas: <b>$($completionIntelligence.estimatedRemainingAreas)</b></p>
<h4>Next Milestone</h4>
<p>$($completionIntelligence.nextMilestone)</p>
</div>
<div class='sub-card'>
<h3>Critical Gaps ($($completionIntelligence.criticalGaps.Count))</h3>
<ul>$(($criticalGapsHtml | Select-Object -First 3) -join "`n")</ul>
<details class='card-section'><summary>Show all critical gaps</summary><ul>$($criticalGapsHtml -join "`n")</ul></details>
<h3>Recommended Order</h3>
<ol>$($recommendedOrderHtml -join "`n")</ol>
<details class='card-section'><summary>Quick Wins ($($completionIntelligence.quickWins.Count))</summary><ul>$($quickWinsHtml -join "`n")</ul></details>
</div>
</div>
<p class='muted-note'>Fuente: tools/dashboard/analyze-completion.ps1 -&gt; completion-intelligence.json. Ninguna conclusion se recalcula en el renderer.</p>
</section>
"@


# =============================================================================
# SECTION: Architecture (Map + Explorer + Domains/Layers heatmap)
# =============================================================================

$architectureHtml = @"
<section id='architecture' class='panel' data-group='business-capability'>
<h2>Architecture</h2>
<p><a href='../../PROGRESS.html' class='ext-link'>&#9664; Ver Mapa Maestro de Arquitectura (PROGRESS.html)</a> &middot; ERP: $($erpInfo.name) v$($erpInfo.version) - $($erpInfo.status)</p>

<h3>Architecture Map</h3>
<p class='muted-note'>Click a stage to jump to its phase detail.</p>
$archMapSvgHtml
$($stageDetailsHtml -join "`n")

<h3>Architecture Layers</h3>
$architectureLayersHeatmap

<h3>Domains &rarr; Modules</h3>
<table class='sortable'>
<tr><th>Domain</th><th>Modules</th><th>Avg Score</th><th>Module List</th></tr>
$($domainsHtml -join "`n")
</table>
<p>Unmapped Modules: $unmappedNames</p>

<h3>Architecture Explorer (Domain &rarr; Module &rarr; Feature &rarr; Process &rarr; Task &rarr; File)</h3>
<p class='muted-note'>Tree built exclusively from modules.json / features.json / processes.json / tasks.json. No relation is invented â€” collapsed nodes with no evidence say so explicitly.</p>
$explorerTreeHtml
</section>
"@


# =============================================================================
# SECTION: Engineering Score
# =============================================================================

$engineeringScoreHtml = @"
<section id='engineering-score' class='panel' data-group='engineering'>
<h2>Engineering Score</h2>
<div class='panel-grid-2'>
<div class='sub-card center'>
$radarSvgHtml
</div>
<div class='sub-card'>
$engineeringScoreHeatmap
<p>Overall: <b>$($score.Overall)%</b> &middot; Confidence Score: <b>$confidenceScore%</b> &middot; Maturity: <b>$maturity</b></p>
<h4>Risk Assessment</h4>
<p>Architecture: <span style='color:$(Get-RiskColor $architectureRisk)'>$architectureRisk</span> &middot;
Security: <span style='color:$(Get-RiskColor $securityRisk)'>$securityRisk</span> &middot;
Quality: <span style='color:$(Get-RiskColor $qualityRisk)'>$qualityRisk</span> &middot;
Technical Debt: <span style='color:$(Get-RiskColor $technicalDebtRisk)'>$technicalDebtRisk</span> &middot;
Overall: <b style='color:$(Get-RiskColor $overallRisk)'>$overallRisk</b></p>
</div>
</div>
<details class='card-section'>
<summary>Quality Gate &mdash; $($gate.Status)</summary>
<p>Build: $buildStatus &middot; Tests: $testStatus (avg $testAverage%) &middot; Coverage: $coverageStatus &middot; Static Analysis: $staticAnalysisStatus ($staticAnalysisViolations violations)</p>
<h4>Warnings</h4><ul>$($warningsHtml -join "`n")</ul>
<h4>Strengths</h4><ul>$($strengthsHtml -join "`n")</ul>
</details>
<details class='card-section'>
<summary>Module Health ($($modulesData.Count) modules)</summary>
$moduleHealthHeatmapHtml
<table class='sortable'>
<tr><th>Module</th><th>Score</th></tr>
$($modulesHtml -join "`n")
</table>
</details>
</section>
"@


# =============================================================================
# SECTION: Business Capability
# =============================================================================

$businessCapabilityHtml = @"
<section id='business-capability' class='panel' data-group='business-capability'>
<h2>Business Capability</h2>
<p>Domain &rarr; Modules &rarr; Features &rarr; Processes. Business Capability KPI = Engineering Risk Coverage: <b>$($impactData.coverage.percentage)%</b> ($($impactData.coverage.mappedFeaturePoints) / $($impactData.coverage.totalFeaturePoints) capability points mapped to a verified process).</p>
<table class='sortable'>
<tr><th>Domain</th><th>Modules</th><th>Features</th><th>Processes</th></tr>
$($capabilityMapHtml -join "`n")
</table>
<details class='card-section'>
<summary>Tracked Tasks ($($tasksData.Count))</summary>
<ul>$($tasksSummaryHtml -join "`n")</ul>
</details>
</section>
"@


# =============================================================================
# SECTION: Architecture Progress (Timeline + Etapas/Fases detalladas)
# =============================================================================

$architectureProgressSectionHtml = @"
<section id='architecture-progress' class='panel' data-group='business-capability'>
<h2>Architecture Progress</h2>
<p><a href='../../PROGRESS.html' class='ext-link'>&#9664; Ver Mapa Maestro de Arquitectura (PROGRESS.html)</a></p>
<p>Global (PROGRESS.html): <b>$($architectureProgress.global.pct)%</b> ($($architectureProgress.global.done) / $($architectureProgress.global.totalTasks) tareas ponderadas) &mdash; extraido de $($architectureProgress.source)</p>
$stageTimelineHtml
<details class='card-section'>
<summary>Avance por Etapa (tabla)</summary>
<table class='sortable'>
<tr><th>Etapa</th><th>Tareas</th><th>Progreso</th></tr>
$($progressStagesHtml -join "`n")
</table>
</details>
<details class='card-section'>
<summary>Componentes Completados ($($architectureProgress.completed.Count))</summary>
<ul>$($progressCompletedSample -join "`n")</ul>
</details>
<details class='card-section'>
<summary>Pendientes &mdash; fases no iniciadas ($($architectureProgress.pending.Count))</summary>
<ul>$($progressPendingHtml -join "`n")</ul>
</details>
<details class='card-section'>
<summary>Proximos Pasos &mdash; fases en progreso ($($architectureProgress.nextSteps.Count))</summary>
<ul>$($progressNextStepsHtml -join "`n")</ul>
</details>
</section>
"@


# =============================================================================
# SECTION: Security
# =============================================================================

$securitySectionHtml = @"
<section id='security' class='panel' data-group='security'>
<h2>Security</h2>
<div class='panel-grid-2'>
<div class='sub-card center'>
$(Build-Gauge $score.Security (Get-ScoreColor $score.Security) 140 "%")
<p>$securityMaturityLevel &mdash; $securityMaturityLabel</p>
</div>
<div class='sub-card'>
$securityHeatmap
<p>Files analyzed: $($security.FilesAnalyzed)</p>
<p>Secrets detected: $($security.SecretsFound) &middot; Anonymous findings: $($security.AnonymousDetected) &middot; Connection Strings: $($security.ConnectionStringsFound)</p>
<p>Security Risk: <b style='color:$(Get-RiskColor $securityStatus)'>$securityStatus</b></p>
</div>
</div>
</section>
"@


# =============================================================================
# SECTION: Technical Debt
# =============================================================================

$technicalDebtSectionHtml = @"
<section id='technical-debt' class='panel' data-group='engineering'>
<h2>Technical Debt</h2>
$technicalDebtHeatmap
<p>Risk Level: <b style='color:$(Get-RiskColor $debtHealth)'>$debtHealth</b> &middot; Trend: $debtTrendStatus ($debtTrendDetail)</p>
<p>Critical Findings: $($technicalDebt.CriticalFindings) &middot; TODO: $($technicalDebt.TODO) &middot; FIXME: $($technicalDebt.FIXME) &middot; HACK: $($technicalDebt.HACK) &middot; Not Implemented: $($technicalDebt.NotImplemented)</p>
<details class='card-section'>
<summary>Related tasks (source: tasks.json)</summary>
<ul>$(($tasksData | Where-Object { $_.category -eq "Technical Debt" } | ForEach-Object { "<li>[$($_.priority)] $($_.task)</li>" }) -join "`n")</ul>
</details>
</section>
"@


# =============================================================================
# SECTION: Trend
# =============================================================================

$trendSectionHtml = @"
<section id='trend' class='panel' data-group='engineering'>
<h2>Engineering Trend</h2>
$sparklineHtml
<p>Snapshots: $($model.Trend.Snapshots) &middot; Previous: $previousTrend% &middot; Current: $currentTrend% &middot; Change: $trendChange% &middot; Status: $trendStatus</p>
<details class='card-section'>
<summary>History table</summary>
<table><tr><th>Date</th><th>Score</th></tr>$($trendHistoryHtml -join "`n")</table>
</details>
</section>
"@


# =============================================================================
# SECTION: Roadmap
# =============================================================================

$roadmapSectionHtml = @"
<section id='roadmap' class='panel' data-group='roadmap'>
<h2>Engineering Roadmap</h2>
<ul>$($roadmapHtml -join "`n")</ul>
<h3>Release Recommendation</h3>
<p class='big-status' style='color:$(Get-ScoreColor $productionReadiness)'>$releaseSummary</p>
<p>Based on Production Decision: $productionDecision</p>
<h4>Required Actions</h4>
<ul>$($releaseActions -join "`n")</ul>
</section>
"@


# =============================================================================
# SECTION: Model Health
# =============================================================================

$modelHealthSectionHtml = @"
<section id='model-health' class='panel' data-group='engineering'>
<h2>Model Health</h2>
<div class='panel-grid-2'>
<div class='sub-card center'>
$(Build-Gauge $modelHealth.integrityScore (Get-ScoreColor $modelHealth.integrityScore) 140 "%")
<p>Status: <b>$modelHealthStatus</b></p>
</div>
<div class='sub-card'>
<p>Broken References: $($modelHealth.brokenReferences) &middot; Missing Evidence: $($modelHealth.missingEvidence) &middot; Unmapped Items: $($modelHealth.unmappedItems) &middot; Total Reference Checks: $($modelHealth.totalReferenceChecks)</p>
<details class='card-section'>
<summary>Unmapped items detail</summary>
<ul>$(($modelHealth.details.unmappedItems | ForEach-Object { "<li>$_</li>" }) -join "`n")</ul>
</details>
<p class='muted-note'>Source: tools/dashboard/validate-dashboard-model.ps1 -&gt; model-health.json</p>
</div>
</div>
</section>
"@


# =============================================================================
# SECTION: Risk Assessment (Risk Map heatmap + table)
# =============================================================================

$riskAssessmentSectionHtml = @"
<section id='risk-assessment' class='panel' data-group='production'>
<h2>Risk Assessment</h2>
$riskHeatmapHtml
<table class='sortable'>
<tr><th>Domain</th><th>Modules</th><th>Features</th><th>Processes</th><th>Risk</th></tr>
$($riskMapHtml -join "`n")
</table>
<p class='muted-note'>Engineering Risk Coverage: $($impactData.coverage.percentage)% (impact.json)</p>
</section>
"@


# =============================================================================
# SECTION: Production Decision (Gate detail, already summarized in Exec Summary)
# =============================================================================

$productionDecisionSectionHtml = @"
<section id='production-decision' class='panel' data-group='production'>
<h2>Production Decision</h2>
<p class='big-status' style='color:$(Get-ScoreColor $productionReadiness)'>$productionDecision</p>
<p>Production Readiness: <b>$productionReadiness%</b> ($productionStatus)</p>
<h4>Blockers</h4>
<ul>$($decisionBlockersHtml -join "`n")</ul>
<h4>Gate checks</h4>
<ul>$($productionGate -join "`n")</ul>
</section>
"@


# =============================================================================
# Architecture Intelligence data (Fases 1-8 -- new analyzers, never modifying
# any existing JSON, analyzer, or the general page structure above). Each of
# these files is produced by a NEW analyzer script:
#   analyze-module-graph.ps1        -> dependencies.json
#   analyze-critical-path.ps1       -> critical-path.json
#   analyze-release-simulation.ps1  -> release-simulation.json
#   analyze-recommendations.ps1     -> recommendations.json
# =============================================================================

$dependencyGraph = LoadJson "dependencies.json"
$criticalPathData = LoadJson "critical-path.json"
$releaseSimulation = LoadJson "release-simulation.json"
$recommendationsData = LoadJson "recommendations.json"

Write-Host "Architecture Intelligence data loaded: $($dependencyGraph.nodes.Count) nodes, $($criticalPathData.criticalPath.Count) critical-path entries, $($releaseSimulation.scenarios.Count) simulation scenarios, $($recommendationsData.recommendations.Count) recommendations"


# =============================================================================
# SECTION: Dependency Graph & Engineering Metrics (Fases 1 + 7)
# =============================================================================

$depNodesByModule = @{}
foreach($n in $dependencyGraph.nodes) { $depNodesByModule[$n.id] = $n }


# -----------------------------------------------------------------------------
# Dependency Explorer graph view -- pure SVG, no external libraries. Layout is
# a deterministic circle (angle = 360/N per node, real array order); this is
# a geometric presentation formula, not a new data relation -- every edge
# drawn comes straight from dependencies.json.edges. Node color reuses
# explorer-index.json's already-computed changeRisk band when available.
# Click a node -> highlights its direct edges and opens its module panel
# (openModulePanel, already wired) so the user can keep drilling
# module -> its dependency -> that dependency's dependency, chained.
# -----------------------------------------------------------------------------

function Build-DependencyGraphSvg($nodes, $edges)
{
    $n = $nodes.Count
    if($n -eq 0) { return "<p class='muted-note'>No modules to graph.</p>" }

    $cx = 320
    $cy = 320
    $r = 260
    $angleStep = 360.0 / $n
    $pointById = @{}

    for($i = 0; $i -lt $n; $i++)
    {
        $angleRad = ($i * $angleStep) * [math]::PI / 180
        $x = [math]::Round($cx + ($r * [math]::Cos($angleRad)), 1)
        $y = [math]::Round($cy + ($r * [math]::Sin($angleRad)), 1)
        $pointById[$nodes[$i].id] = [ordered]@{ x = $x; y = $y }
    }

    $edgeLines = @($edges | ForEach-Object {
        if($pointById.ContainsKey($_.from) -and $pointById.ContainsKey($_.to))
        {
            $p1 = $pointById[$_.from]; $p2 = $pointById[$_.to]
            "<line x1='$($p1.x)' y1='$($p1.y)' x2='$($p2.x)' y2='$($p2.y)' stroke='#cbd5e1' stroke-width='1' class='dep-edge' data-from='$($_.from)' data-to='$($_.to)'/>"
        }
    })

    $nodeCircles = @($nodes | ForEach-Object {
        $p = $pointById[$_.id]
        $color = $colorInfo
        if($explorerModuleById.ContainsKey($_.id)) { $color = Get-RiskBandColor $explorerModuleById[$_.id].changeRisk.band }
        $labelAnchor = if($p.x -gt ($cx + 20)) { "start" } elseif($p.x -lt ($cx - 20)) { "end" } else { "middle" }
        $labelDx = if($labelAnchor -eq "start") { 8 } elseif($labelAnchor -eq "end") { -8 } else { 0 }
@"
<g class='dep-graph-node' data-module='$($_.id)' onclick="highlightDepNode('$($_.id)')">
<circle cx='$($p.x)' cy='$($p.y)' r='7' fill='$color' stroke='#fff' stroke-width='1.5'/>
<text x='$($p.x + $labelDx)' y='$($p.y - 10)' font-size='9' text-anchor='$labelAnchor' fill='var(--text)'>$($_.id)</text>
</g>
"@
    })

    return @"
<svg viewBox='0 0 640 640' width='100%' height='640' class='dep-graph-svg' id='depGraphSvg'>
$($edgeLines -join "`n")
$($nodeCircles -join "`n")
</svg>
<div class='dep-graph-legend'>
<span><i style='background:$colorDone'></i> Completado / Safe</span>
<span><i style='background:$colorInfo'></i> Informacion / Low Risk</span>
<span><i style='background:$colorPending'></i> Pendiente / Medium Risk</span>
<span><i style='background:$colorError'></i> Error / High-Critical Risk</span>
</div>
<p class='muted-note'>Click a node to highlight its direct dependencies/dependents and jump to its Module level in Arquitectura (chained drill-down: click a listed dependency there to re-center on it).</p>
"@
}

$dependencyGraphSvgHtml = Build-DependencyGraphSvg $dependencyGraph.nodes $dependencyGraph.edges

$depMetricsRowsHtml = @($dependencyGraph.nodes | Sort-Object -Property {$_.coupling} -Descending | ForEach-Object {
    $cohesionText = "n/a"
    if($null -ne $_.cohesionApprox) { $cohesionText = $_.cohesionApprox }
    "<tr><td>$($_.id)</td><td>$($_.fanIn)</td><td>$($_.fanOut)</td><td>$($_.coupling)</td><td>$($_.instability)</td><td>$cohesionText</td><td>$($_.busFactor)</td></tr>"
})

$cyclesHtml = @($dependencyGraph.cycles | ForEach-Object { "<li>$_</li>" })
if($cyclesHtml.Count -eq 0) { $cyclesHtml = @("<li>No circular dependencies detected</li>") }

$dependencyGraphHtml = @"
<section id='dependency-graph' class='panel' data-group='engineering'>
<h2>Dependency Graph &amp; Engineering Metrics</h2>
<p class='muted-note'>Source: tools/dashboard/analyze-module-graph.ps1 -&gt; dependencies.json. Edges are 'using ERP.*' references between real module folders, kept only when the referenced name matches a real modules.json id. $($dependencyGraph.method.fanIn) / $($dependencyGraph.method.instability)</p>
<h3>Dependency Explorer (graph view)</h3>
$dependencyGraphSvgHtml
<p>Central modules (highest coupling): <b>$($dependencyGraph.centralModules -join ', ')</b></p>
<p>Critical modules (highest fan-in): <b>$($dependencyGraph.criticalModules -join ', ')</b></p>
<p>Isolated modules (fanIn=0 and fanOut=0): <b>$(if($dependencyGraph.isolatedModules.Count -gt 0){$dependencyGraph.isolatedModules -join ', '}else{'None'})</b></p>
<p>Max dependency depth: <b>$($dependencyGraph.maxDependencyDepth)</b> &middot; Circular dependencies: <b>$($dependencyGraph.cycles.Count)</b></p>
<details class='card-section'>
<summary>Circular dependency chains ($($dependencyGraph.cycles.Count))</summary>
<ul>$($cyclesHtml -join "`n")</ul>
</details>
<details class='card-section' open>
<summary>Engineering Metrics per module (Fan-In / Fan-Out / Coupling / Instability / Cohesion approx. / Bus Factor)</summary>
<table class='sortable'>
<tr><th>Module</th><th>Fan-In (Ca)</th><th>Fan-Out (Ce)</th><th>Coupling</th><th>Instability</th><th>Cohesion approx.</th><th>Bus Factor</th></tr>
$($depMetricsRowsHtml -join "`n")
</table>
</details>
</section>
"@


# =============================================================================
# SECTION: Dependency Explorer (Fase 6) -- Module -> Dependencies -> Dependents
# -> Impact -> Risk -> Files -> Evidence. Reuses dependencies.json +
# critical-path.json (moduleImpact) exclusively -- no new relation invented.
# =============================================================================

$impactByModuleForExplorer = @{}
foreach($mi in $criticalPathData.moduleImpact) { $impactByModuleForExplorer[$mi.module] = $mi }

$depExplorerNodesHtml = @()

foreach($n in ($dependencyGraph.nodes | Sort-Object -Property {$_.id}))
{
    $safeId = ConvertTo-SafeId $n.id
    $impact = $null
    if($impactByModuleForExplorer.ContainsKey($n.id)) { $impact = $impactByModuleForExplorer[$n.id] }

    $depsHtml = "<div class='tree-empty'>No dependencies (fanOut=0)</div>"
    if($n.dependsOn.Count -gt 0) { $depItems = ($n.dependsOn | ForEach-Object { "<li>$_</li>" }) -join ''; $depsHtml = "<ul class='tree-files'>$depItems</ul>" }

    $dependentsHtml = "<div class='tree-empty'>No dependents (fanIn=0)</div>"
    if($n.dependedOnBy.Count -gt 0) { $dependentItems = ($n.dependedOnBy | ForEach-Object { "<li>$_</li>" }) -join ''; $dependentsHtml = "<ul class='tree-files'>$dependentItems</ul>" }

    $impactHtml = "<div class='tree-empty'>No impact profile computed</div>"
    $riskHtml = "<div class='tree-empty'>No risk profile computed</div>"
    $filesHtml = "<div class='tree-empty'>No related tasks with file evidence</div>"

    if($null -ne $impact)
    {
        $impactHtml = "<p>Transitive dependents: <b>$($impact.transitiveDependentCount)</b> &middot; Impacted feature points: <b>$($impact.impactedFeaturePoints)</b> &middot; % of ERP (by feature points): <b>$($impact.percentOfErp)%</b></p>"
        $riskHtml = "<p>Risk of modifying this module: <b style='color:$(Get-RiskColor $impact.riskOfModifying)'>$($impact.riskOfModifying)</b> (source: impact.json)</p>"
        if($impact.relatedTasks.Count -gt 0)
        {
            $relatedTaskItems = ($impact.relatedTasks | ForEach-Object { "<li>$_</li>" }) -join ''
            $filesHtml = "<ul class='tree-files'>$relatedTaskItems</ul>"
        }
    }

    $depExplorerNodesHtml +=
@"
<details class='tree-node' id='depmod-$safeId'>
<summary>$($n.id) <span class='pill' style='background:#2563eb;color:#fff'>coupling $($n.coupling)</span></summary>
<div class='tree-group-label'>Dependencies (this module depends on)</div>
$depsHtml
<div class='tree-group-label'>Dependents (depend on this module)</div>
$dependentsHtml
<div class='tree-group-label'>Impact</div>
$impactHtml
<div class='tree-group-label'>Risk</div>
$riskHtml
<div class='tree-group-label'>Related tasks (evidence-matched, source: tasks.json)</div>
$filesHtml
</details>
"@
}

$dependencyExplorerHtml = @"
<section id='dependency-explorer' class='panel' data-group='business-capability'>
<h2>Dependency Explorer</h2>
<p class='muted-note'>Module &rarr; Dependencies &rarr; Dependents &rarr; Impact &rarr; Risk &rarr; Evidence. Built exclusively from dependencies.json and critical-path.json.</p>
<div class='explorer-tree'>
$($depExplorerNodesHtml -join "`n")
</div>
</section>
"@


# =============================================================================
# SECTION: Critical Path & Impact Analysis (Fases 2 + 3)
# =============================================================================

$criticalPathRowsHtml = @($criticalPathData.criticalPath | Select-Object -First 15 | ForEach-Object {
    "<tr><td>$($_.order)</td><td>$($_.module)</td><td>$($_.currentScore)%</td><td>$($_.directUnblocks)</td><td>$($_.transitiveUnblocks)</td><td>$($_.unlockedProcesses)</td></tr>"
})

$criticalPathHtml = @"
<section id='critical-path' class='panel' data-group='business-capability'>
<h2>Critical Path &amp; Impact Analysis</h2>
<p class='muted-note'>$($criticalPathData.disclaimer)</p>
<p class='muted-note'>Completion proxy: $($criticalPathData.method.completionProxy)</p>
<table class='sortable'>
<tr><th>Order</th><th>Module</th><th>Score</th><th>Direct Unblocks</th><th>Transitive Unblocks</th><th>Processes Unlocked</th></tr>
$($criticalPathRowsHtml -join "`n")
</table>
<details class='card-section'>
<summary>Show all $($criticalPathData.criticalPath.Count) modules ranked</summary>
<ul>$(($criticalPathData.criticalPath | ForEach-Object { "<li>$($_.rationale)</li>" }) -join "`n")</ul>
</details>
</section>
"@


# =============================================================================
# SECTION: Release Simulation (Fase 4)
# =============================================================================

$simulationRowsHtml = @()
foreach($s in $releaseSimulation.scenarios)
{
    if($null -ne $s.engineeringScoreOverall)
    {
        $simulationRowsHtml += "<tr><td>$($s.scenario)</td><td>Engineering</td><td>Prod. Readiness: $($s.productionReadiness.baseline)%</td><td>$($s.productionReadiness.simulated)%</td><td>$($s.productionReadiness.delta)</td><td>$($s.productionStatus.baseline) &rarr; $($s.productionStatus.simulated)</td></tr>"
    }
    else
    {
        $simulationRowsHtml += "<tr><td>$($s.scenario)</td><td>ERP Completion</td><td>$($s.erpCompletion.baseline)%</td><td>$($s.erpCompletion.simulated)%</td><td>$($s.erpCompletion.delta)</td><td>scope: $($s.scope)</td></tr>"
    }
}

$releaseSimulationHtml = @"
<section id='release-simulation' class='panel' data-group='production'>
<h2>Release Simulation</h2>
<p class='muted-note'>$($releaseSimulation.disclaimer)</p>
<table class='sortable'>
<tr><th>Scenario</th><th>Metric family</th><th>Baseline</th><th>Simulated</th><th>Delta</th><th>Status change / scope</th></tr>
$($simulationRowsHtml -join "`n")
</table>
<p class='muted-note'>Formulas (verbatim from calculate-engineering-score.ps1 / render-dashboard.ps1): $($releaseSimulation.method.overallScoreWeights)</p>
</section>
"@


# =============================================================================
# SECTION: Architecture Recommendations (Fase 5)
# =============================================================================

$recommendationCardsHtml = @($recommendationsData.recommendations | ForEach-Object {
@"
<div class='sub-card'>
<h4>$($_.title)</h4>
<p>$($_.text)</p>
<p class='muted-note'>Justified by: $($_.justifiedBy -join ' | ')</p>
</div>
"@
})

$recommendationsSectionHtml = @"
<section id='recommendations' class='panel' data-group='roadmap'>
<h2>Architecture Recommendations</h2>
<p class='muted-note'>$($recommendationsData.rule)</p>
$($recommendationCardsHtml -join "`n")
</section>
"@


# =============================================================================
# SECTION: Executive Dashboard (Fase 8) -- single-screen synthesis, no repeats
# =============================================================================

$biggestOpportunity = $criticalPathData.criticalPath | Select-Object -First 1
$bestSimulation = $releaseSimulation.scenarios | Where-Object { $null -ne $_.productionReadiness } | Sort-Object -Property {$_.productionReadiness.delta} -Descending | Select-Object -First 1

$top10RiskRows = @($explorerIndex.executive.top10Risk | ForEach-Object { "<tr><td>$($_.module)</td><td>$($_.riskScore)</td><td style='color:$(Get-RiskBandColor $_.band)'>$($_.band)</td></tr>" })
$top10DebtRows = @($explorerIndex.executive.top10Debt | ForEach-Object { "<tr><td>$($_.module)</td><td>$($_.largeFilesCount)</td></tr>" })
$top10LowScoreRows = @($explorerIndex.executive.top10LowScore | ForEach-Object { "<tr><td>$($_.module)</td><td>$($_.score)%</td></tr>" })
$execBlockersHtml = @($explorerIndex.executive.blockers | ForEach-Object { "<li>$_</li>" })
if($execBlockersHtml.Count -eq 0) { $execBlockersHtml = @("<li>No blockers detected (completion-intelligence.json)</li>") }

$execDashboardHtml = @"
<section id='executive-dashboard' class='panel' data-group='roadmap'>
<h2>Executive Dashboard (Vista Ejecutiva)</h2>
<p class='muted-note'>Fuente: explorer-index.json.executive (analyze-explorer-index.ps1) -- sin recalculo, solo cross-references de datos ya publicados.</p>
<div class='panel-grid-2'>
<div class='sub-card'>
<h4>Engineering Score</h4><p class='big-status' style='color:$(Get-ScoreColor $explorerIndex.executive.engineeringScoreOverall)'>$($explorerIndex.executive.engineeringScoreOverall)%</p>
<h4>ERP Completion</h4><p>$($explorerIndex.executive.erpCompletion)%</p>
<h4>Production Decision</h4><p>$($explorerIndex.executive.productionDecision)% ($($explorerIndex.executive.overallStatus))</p>
<h4>Overall Risk</h4><p style='color:$(Get-RiskColor $overallRisk)'>$overallRisk</p>
<h4>Next milestone</h4><p>$($explorerIndex.executive.nextMilestone)</p>
<h4>Estimated remaining time</h4>
<p class='muted-note'>Not computable -- this pipeline has no velocity/effort/time-tracking data. Showing a duration here would be invented, not derived.</p>
<h4>Current blockers</h4><ul>$($execBlockersHtml -join "`n")</ul>
</div>
<div class='sub-card'>
<h4>Top 10 modules by change risk</h4>
<table class='sortable'><tr><th>Module</th><th>Risk Score</th><th>Band</th></tr>$($top10RiskRows -join '')</table>
<h4>Top 10 modules by technical debt (large files)</h4>
<table class='sortable'><tr><th>Module</th><th>Large Files</th></tr>$($top10DebtRows -join '')</table>
<h4>Top 10 modules by lowest score</h4>
<table class='sortable'><tr><th>Module</th><th>Score</th></tr>$($top10LowScoreRows -join '')</table>
</div>
</div>
<p class='muted-note'>Biggest opportunity: completing '<b>$($biggestOpportunity.module)</b>' unblocks $($biggestOpportunity.transitiveUnblocks) module(s) transitively (critical-path.json)$(if($null -ne $bestSimulation){" &middot; best simulated gain: '$($bestSimulation.scenario)' (+$($bestSimulation.productionReadiness.delta) pts Production Readiness)"})</p>
</section>
"@


# =============================================================================
# Architectural Explorer -- HOME view (interactive diagram + contextual panel)
#
# El diagrama reutiliza EXCLUSIVAMENTE navigation-map.json (calculado por
# analyze-navigation-map.ps1) para toda relacion Layer->Stage/coreModule/
# Domain/Database/Frontend. El renderer solo formatea; no decide ninguna
# relacion aqui.
# =============================================================================


function Build-FileLink($rawPath, $displayText)
{
    $safeFile = $rawPath -replace '\\', '\\\\' -replace "'", "\'"
    return "<li class='tree-file file-link' onclick=""pushLevel('file','$safeFile','$safeFile')"">$displayText</li>"
}

# Module Health + Impact Explorer + Change Risk, todo en UN solo panel
# consolidado (no se agregan tarjetas nuevas) -- 100% leido de
# explorer-index.json, cero joins/calculos en el renderer.
function Build-ModulePanel($moduleId)
{
    if(-not $explorerModuleById.ContainsKey($moduleId)) { return "<div class='tree-empty'>Module '$moduleId' not found in explorer-index.json</div>" }
    $mp = $explorerModuleById[$moduleId]

    $featuresHtml = "<span class='muted-note'>No features mapped</span>"
    if($mp.featureNames.Count -gt 0) { $featuresHtml = ($mp.featureNames | ForEach-Object { "<li>$_</li>" }) -join '' }

    $processesHtml = "<span class='muted-note'>No processes mapped</span>"
    if($mp.processes.Count -gt 0) { $processesHtml = ($mp.processes | ForEach-Object { "<li>$($_.process) / $($_.step) ($($_.status))</li>" }) -join '' }

    $depsHtml = "<span class='muted-note'>None</span>"
    if($mp.dependencies.dependsOn.Count -gt 0) { $depsHtml = ($mp.dependencies.dependsOn | ForEach-Object { $critical = if($mp.dependencies.criticalDependencies -contains $_){" <span class='pill pill-n'>critical</span>"}else{""}; "<li><span class='module-open-link' onclick=""pushLevel('module','$_','$_')"">$_ &rarr;</span>$critical</li>" }) -join '' }

    $dependentsHtml = "<span class='muted-note'>None</span>"
    if($mp.dependencies.dependedOnBy.Count -gt 0) { $dependentsHtml = ($mp.dependencies.dependedOnBy | ForEach-Object { "<li><span class='module-open-link' onclick=""pushLevel('module','$_','$_')"">$_ &rarr;</span></li>" }) -join '' }

    $cyclesHtmlPanel = ""
    if($mp.dependencies.cyclesInvolved.Count -gt 0) { $cyclesHtmlPanel = "<h5>Circular dependencies involving this module</h5><ul>$(($mp.dependencies.cyclesInvolved | ForEach-Object { "<li>$_</li>" }) -join '')</ul>" }

    $largeFilesHtml = "<span class='muted-note'>None</span>"
    if($mp.debt.largeFiles.Count -gt 0) { $largeFilesHtml = ($mp.debt.largeFiles | ForEach-Object { Build-FileLink $_.file "$($_.file) ($($_.lines) lines)" }) -join '' }

    $secretsHtml = "<span class='muted-note'>None</span>"
    if($mp.security.secretFiles.Count -gt 0) { $secretsHtml = ($mp.security.secretFiles | ForEach-Object { Build-FileLink $_ $_ }) -join '' }

    $dependentFeaturesHtml = "<span class='muted-note'>None</span>"
    if($mp.impact.dependentFeatures.Count -gt 0) { $dependentFeaturesHtml = "<details><summary>$($mp.impact.dependentFeatures.Count) feature(s) in dependent modules</summary><ul>$(($mp.impact.dependentFeatures | ForEach-Object { "<li>$_</li>" }) -join '')</ul></details>" }

    $dependentProcessesHtml = "<span class='muted-note'>None</span>"
    if($mp.impact.dependentProcesses.Count -gt 0) { $dependentProcessesHtml = ($mp.impact.dependentProcesses | ForEach-Object { "<li>$_</li>" }) -join '' }

    $relatedTasksHtml = "<span class='muted-note'>None</span>"
    if($mp.impact.relatedTasks.Count -gt 0) { $relatedTasksHtml = ($mp.impact.relatedTasks | ForEach-Object { "<li>$_</li>" }) -join '' }

    $riskColor = Get-RiskBandColor $mp.changeRisk.band

    return @"
<div class='module-panel'>
<div class='module-panel-head'>
<span class='pill' style='background:$(Get-ScoreColor $mp.score);color:#fff'>$($mp.score)%</span>
<b>$moduleId</b>
<span class='muted-note'>domain: $($mp.domainName) &middot; last evidence: $($mp.lastEvidenceDate) &middot; bus factor: $($mp.busFactor)</span>
<span class='pill' style='background:$riskColor;color:#fff;margin-left:auto' title='$($mp.changeRisk.formula)'>$($mp.changeRisk.band) ($($mp.changeRisk.score))</span>
</div>
<div class='panel-grid-2'>
<div>
<h5>Module Health &mdash; Architecture / Tests / Docs / Backend / Frontend</h5>
<p>$($mp.architecture)% / $($mp.tests)% / $($mp.documentation)% / $($mp.backend)% / $($mp.frontend)%</p>
<h5>Features ($($mp.featuresCount))</h5><ul>$featuresHtml</ul>
<h5>Processes ($($mp.processesCount))</h5><ul>$processesHtml</ul>
<h5>Large files (Technical Debt)</h5><ul>$largeFilesHtml</ul>
<h5>Secrets detected</h5><ul>$secretsHtml</ul>
<p class='muted-note'>$($mp.debt.note)</p>
</div>
<div>
<h5>Dependencies (depends on) &mdash; coupling $($mp.dependencies.coupling), instability $($mp.dependencies.instability)</h5><ul>$depsHtml</ul>
<h5>Dependents (depended on by)</h5><ul>$dependentsHtml</ul>
$cyclesHtmlPanel
<h5>If modified: modules that may break ($($mp.impact.transitiveDependentCount) transitive dependents)</h5>
$dependentFeaturesHtml
<h5>Processes that use this module</h5><ul>$dependentProcessesHtml</ul>
<h5>Related tasks</h5><ul>$relatedTasksHtml</ul>
<h5>Risk of modifying (impact.json) / % of ERP</h5><p style='color:$(Get-RiskColor $mp.impact.riskOfModifying)'>$($mp.impact.riskOfModifying)</p><p>$($mp.impact.percentOfErp)% of ERP feature points</p>
</div>
</div>
</div>
"@
}

# Navegacion inversa (Fase 5): Archivo -> Feature/Process/Task -> Modulo ->
# Dominio -> Arquitectura. 100% leido de explorer-index.json.reverseFileIndex,
# indexado una sola vez ($reverseFileIndexByPath).
function Build-ReverseFilePanel($filePath)
{
    if(-not $reverseFileIndexByPath.ContainsKey($filePath))
    {
        return "<div class='module-panel'><p class='muted-note'>No reverse references found for '$filePath' in features.json/processes.json/tasks.json.</p></div>"
    }

    $refs = $reverseFileIndexByPath[$filePath]
    $rowsHtml = @($refs | ForEach-Object {
        $moduleLink = if($_.module) { "<span class='module-open-link' onclick=""pushLevel('module','$($_.module)','$($_.module)')"">$($_.module) &rarr;</span>" } else { "<span class='muted-note'>unmatched</span>" }
        $domainText = if($_.domainName) { $_.domainName } else { "n/a" }
        "<tr><td>$($_.type)</td><td>$($_.label)</td><td>$moduleLink</td><td>$domainText</td></tr>"
    })

    return @"
<div class='module-panel'>
<div class='module-panel-head'><b>Reverse navigation</b><span class='muted-note'>$filePath</span></div>
<p class='muted-note'>File &rarr; Feature/Process/Task &rarr; Module &rarr; Domain &rarr; Architecture (click a module to jump to its level; use the breadcrumb to reach its Domain/Layer).</p>
<table><tr><th>Type</th><th>Feature/Process/Task</th><th>Module</th><th>Domain</th></tr>$($rowsHtml -join '')</table>
</div>
"@
}

# ----- Nivel: Feature / Proceso (evidencia = archivos reales) -----

function Build-FeatureLevel($moduleId, $featureName)
{
    $featureEntry = $featuresData | Where-Object { $_.module -eq $moduleId } | Select-Object -First 1
    $feature = $null
    if($null -ne $featureEntry) { $feature = $featureEntry.features | Where-Object { $_.name -eq $featureName } | Select-Object -First 1 }

    if($null -eq $feature) { return "<p class='muted-note'>Feature not found.</p>" }

    $fileItems = @($feature.evidence | ForEach-Object { Build-FileLink $_ $_ })

    return @"
<p><span class='pill pill-d'>$($feature.status)</span></p>
<h5>Evidence files</h5>
<ul class='tree-files'>$($fileItems -join '')</ul>
"@
}

function Build-ProcessLevel($processName, $stepName, $status, $evidence)
{
    $pillClass = if($status -eq "verified") { "pill-d" } else { "pill-n" }
    $fileItems = @()
    if($null -ne $evidence) { $fileItems = @($evidence | ForEach-Object { Build-FileLink $_ $_ }) }
    if($fileItems.Count -eq 0) { $fileItems = @("<li class='tree-empty-li'>No evidence recorded</li>") }

    return @"
<p><span class='pill $pillClass'>$status</span></p>
<h5>Evidence files</h5>
<ul class='tree-files'>$($fileItems -join '')</ul>
"@
}


# -----------------------------------------------------------------------------
# Big interactive diagram -- same 6 rows as PROGRESS.html's own architecture
# diagram, nodes = navigation-map.json layers (pct/status already resolved by
# the analyzer). Colors reuse Get-ScoreColor (0 -> red, same threshold as
# every other score in this dashboard).
# -----------------------------------------------------------------------------

function Get-LayerBoxColor($navLayer)
{
    if($navLayer.status -eq "not_started") { return $colorPending }
    return (Get-ScoreColor $navLayer.pct)
}

function Build-DiagramNode($layerId, $extraClass)
{
    $navLayer = $navLayersById[$layerId]
    $color = Get-LayerBoxColor $navLayer
    return @"
<div class='diagram-node $extraClass' id='diagramnode-$layerId' onclick="selectLayer('$layerId')" style='border-color:$color'>
<div class='diagram-node-title'>$($navLayer.label)</div>
<div class='diagram-node-pct' style='color:$color'>$($navLayer.pct)%</div>
</div>
"@
}

$diagramHtml = @"
<div class='diagram-row'>
$(Build-DiagramNode "web" "")
$(Build-DiagramNode "mobile" "diagram-node-dim")
$(Build-DiagramNode "chat" "diagram-node-dim")
</div>
<div class='diagram-conn'>&#9660;</div>
<div class='diagram-row'>
$(Build-DiagramNode "intelligence" "diagram-node-wide")
</div>
<div class='diagram-conn'>&#9660;</div>
<div class='diagram-row'>
$(Build-DiagramNode "ai-assistant" "diagram-node-dim")
$(Build-DiagramNode "ai-analyst" "diagram-node-dim")
$(Build-DiagramNode "ai-automation" "diagram-node-dim")
</div>
<div class='diagram-conn'>&#9660;</div>
<div class='diagram-row'>
$(Build-DiagramNode "core" "diagram-node-wide")
</div>
<div class='diagram-conn'>&#9660;</div>
<div class='diagram-row'>
$(Build-DiagramNode "db" "")
$(Build-DiagramNode "data-warehouse" "diagram-node-dim")
</div>
<div class='diagram-conn'>&#9660;</div>
<div class='diagram-row'>
$(Build-DiagramNode "ai-advanced" "diagram-node-dim")
</div>
"@

# Compact persistent strip (always visible outside the Architecture home
# view, so the user never loses architectural context)
$compactStripHtml = "<div class='compact-strip'>" + (($navigationMap.layers | ForEach-Object {
    $c = Get-LayerBoxColor $_
    "<div class='compact-chip' onclick=""showGroup('architecture');selectLayer('$($_.id)')"" style='border-color:$c;color:$c' title='$($_.label)'>$($_.pct)%</div>"
}) -join "") + "</div>"


# -----------------------------------------------------------------------------
# HIERARCHICAL LEVELS: Arquitectura -> Capa -> Dominio -> Modulo ->
# Feature/Proceso -> Archivo/Evidencia.
#
# Cada nivel se precomputa como una tarjeta-resumen (id + % + estado), NUNCA
# el detalle completo. El detalle (features, dependencias, riesgo, archivos)
# solo aparece cuando el usuario sigue navegando un nivel mas -- nada de esto
# se recalcula: cada tarjeta solo formatea campos que ya vienen de
# navigation-map.json / explorer-index.json / features.json / processes.json.
# -----------------------------------------------------------------------------

function Build-SummaryCard($id, $label, $pct, $sub, $onclick)
{
    $color = Get-ScoreColor $pct
    return @"
<div class='level-card' onclick="$onclick">
<div class='level-card-pct' style='color:$color'>$pct%</div>
<div class='level-card-label'>$label</div>
<div class='level-card-sub'>$sub</div>
</div>
"@
}

# ----- Nivel: Capa (children = Domains | CoreModules | Database | gap) -----

$layerLevelMap = [ordered]@{}

foreach($navLayer in $navigationMap.layers)
{
    $cards = @()

    if($navLayer.domains.Count -gt 0)
    {
        foreach($dom in $navLayer.domains)
        {
            $domModules = @($modulesData | Where-Object { $_.domainId -eq $dom.id })
            $avgPct = 0
            if($domModules.Count -gt 0) { $avgPct = [math]::Round((($domModules | Measure-Object -Property score -Average).Average)) }
            $cards += Build-SummaryCard $dom.id $dom.name $avgPct "$($domModules.Count) modulo(s)" "pushLevel('domain','$($dom.id)','$($dom.name)')"
        }
    }
    elseif($navLayer.coreModules.Count -gt 0)
    {
        foreach($cm in $navLayer.coreModules)
        {
            if($cm.realModuleVerified)
            {
                $cards += Build-SummaryCard $cm.realModuleId $cm.name $cm.pct "modulo: $($cm.realModuleId)" "pushLevel('module','$($cm.realModuleId)','$($cm.name)')"
            }
            else
            {
                $cards += "<div class='level-card level-card-disabled'><div class='level-card-pct' style='color:$(Get-ScoreColor $cm.pct)'>$($cm.pct)%</div><div class='level-card-label'>$($cm.name)</div><div class='level-card-sub'>$($cm.gapReason)</div></div>"
            }
        }
    }
    elseif($null -ne $navLayer.databaseStats)
    {
        $ds = $navLayer.databaseStats
        $cards += "<div class='level-card level-card-disabled'><div class='level-card-pct' style='color:$(Get-ScoreColor $navLayer.pct)'>$($navLayer.pct)%</div><div class='level-card-label'>Database</div><div class='level-card-sub'>$($ds.DbSets) tables &middot; $($ds.Migrations) migrations &middot; $($ds.Repositories) repos</div></div>"
        if($null -ne $navLayer.migrations)
        {
            $migItems = ($navLayer.migrations.mostRecent | ForEach-Object { "<li>$($_.name) &mdash; $($_.modified)</li>" }) -join ''
            $cards += "<div class='level-card level-card-disabled' style='text-align:left'><div class='level-card-label'>Migraciones recientes</div><ul class='tree-files'>$migItems</ul></div>"
        }
    }
    elseif($navLayer.aiBackendScaffold.Count -gt 0)
    {
        foreach($proj in $navLayer.aiBackendScaffold)
        {
            $cards += "<div class='level-card level-card-disabled'><div class='level-card-label'>$($proj.project)</div><div class='level-card-sub'>$($proj.moduleCount) modulo(s) &mdash; $($navLayer.gapReason)</div></div>"
        }
    }
    else
    {
        $cards += "<div class='level-card level-card-disabled'><div class='level-card-pct' style='color:$(Get-ScoreColor $navLayer.pct)'>$($navLayer.pct)%</div><div class='level-card-label'>$($navLayer.label)</div><div class='level-card-sub'>$($navLayer.gapReason)</div></div>"
    }

    $layerLevelMap[$navLayer.id] = "<p class='muted-note'>$($navLayer.note)</p><div class='level-grid'>$($cards -join "`n")</div>"
}

# ----- Nivel: Dominio (children = Modules) -----

$domainLevelMap = [ordered]@{}

foreach($dom in $domains)
{
    $domModules = @($modulesData | Where-Object { $_.domainId -eq $dom.id })
    $cards = @($domModules | ForEach-Object { Build-SummaryCard $_.id $_.id $_.score "modulo" "pushLevel('module','$($_.id)','$($_.id)')" })
    if($cards.Count -eq 0) { $cards = @("<div class='level-card level-card-disabled'><div class='level-card-label'>Sin modulos mapeados</div></div>") }
    $domainLevelMap[$dom.id] = "<div class='level-grid'>$($cards -join "`n")</div>"
}

# ----- Nivel: Modulo (children = Features / Processes, resumen) -----

$moduleSummaryMap = [ordered]@{}
$featureLevelMap = [ordered]@{}
$processLevelMap = [ordered]@{}

function ConvertTo-LevelKey($text) { return [regex]::Replace([string]$text, "[^a-zA-Z0-9]", "_") }
function ConvertTo-JsSafe($text) { return ([string]$text) -replace "\\", "\\\\" -replace "'", "\'" }

foreach($mp in $explorerIndex.modules)
{
    $riskColor = Get-RiskBandColor $mp.changeRisk.band

    $featureChips = "<span class='muted-note'>Sin features</span>"
    if($mp.featureNames.Count -gt 0)
    {
        $featureChips = ($mp.featureNames | ForEach-Object {
            $key = "$($mp.id)::$_"
            $safeKey = ConvertTo-JsSafe $key
            $safeLabel = ConvertTo-JsSafe $_
            $featureLevelMap[$key] = Build-FeatureLevel $mp.id $_
            "<span class='chip' onclick=""pushLevel('feature','$safeKey','$safeLabel')"">$_</span>"
        }) -join ''
    }

    $processChips = "<span class='muted-note'>Sin procesos</span>"
    if($mp.processes.Count -gt 0)
    {
        $processChips = ($mp.processes | ForEach-Object {
            $key = "$($mp.id)::$($_.process)::$($_.step)"
            $safeKey = ConvertTo-JsSafe $key
            $safeLabel = ConvertTo-JsSafe "$($_.process) / $($_.step)"
            $processLevelMap[$key] = Build-ProcessLevel $_.process $_.step $_.status $_.evidence
            "<span class='chip' onclick=""pushLevel('process','$safeKey','$safeLabel')"">$($_.process) / $($_.step)</span>"
        }) -join ''
    }

    $moduleSummaryMap[$mp.id] = @"
<div class='level-summary-head'>
<div class='level-card-pct' style='color:$(Get-ScoreColor $mp.score)'>$($mp.score)%</div>
<div><b>$($mp.id)</b><br/><span class='muted-note'>$($mp.domainName) &middot; last evidence $($mp.lastEvidenceDate)</span></div>
<span class='pill' style='background:$riskColor;color:#fff' title='$($mp.changeRisk.formula)'>$($mp.changeRisk.band)</span>
</div>
<h5>Features ($($mp.featuresCount))</h5><div class='chip-row'>$featureChips</div>
<h5>Processes ($($mp.processesCount))</h5><div class='chip-row'>$processChips</div>
<details class='card-section'><summary>Ver ficha tecnica completa (dependencias, deuda, seguridad, impacto)</summary>$(Build-ModulePanel $mp.id)</details>
"@
}

Write-Host "Hierarchical levels built: $($layerLevelMap.Keys.Count) layer levels, $($domainLevelMap.Keys.Count) domain levels, $($moduleSummaryMap.Keys.Count) module summaries, $($featureLevelMap.Keys.Count) feature levels, $($processLevelMap.Keys.Count) process levels"

# Module panel container (filled client-side by pre-rendered per-module HTML,
# embedded as a JSON object -- built server-side once, never re-scanned).
# JSON serialization handles all escaping (quotes/backticks/newlines) safely.
# NOTE: the old flat "MODULE_PANELS" (one full ficha per module, always
# built) was retired -- the full ficha is now embedded lazily inside
# $moduleSummaryMap's <details> ("Ver ficha tecnica completa"), so it is only
# rendered once, at the Module level, not duplicated into a separate map.

# File panels (reverse navigation, Fase 5) -- precomputed once for every file
# present in explorer-index.json.reverseFileIndex.
$filePanelsMap = [ordered]@{}
foreach($rf in $explorerIndex.reverseFileIndex) { $filePanelsMap[$rf.file] = Build-ReverseFilePanel $rf.file }
$filePanelsJson = ($filePanelsMap | ConvertTo-Json -Depth 4 -Compress)

$layerLevelJson = ($layerLevelMap | ConvertTo-Json -Depth 4 -Compress)
$domainLevelJson = ($domainLevelMap | ConvertTo-Json -Depth 4 -Compress)
$moduleSummaryJson = ($moduleSummaryMap | ConvertTo-Json -Depth 6 -Compress)
$featureLevelJson = ($featureLevelMap | ConvertTo-Json -Depth 4 -Compress)
$processLevelJson = ($processLevelMap | ConvertTo-Json -Depth 4 -Compress)

Write-Host "Architecture Explorer home built: 11 diagram nodes, $($filePanelsMap.Keys.Count) file panels, $($layerLevelMap.Keys.Count) layer levels, $($domainLevelMap.Keys.Count) domain levels, $($moduleSummaryMap.Keys.Count) module levels"


# =============================================================================
# PAGE SHELL: Sidebar, Topbar, CSS, JS
# =============================================================================

$sidebarHtml = @"
<nav class='sidebar' id='sidebar'>
<div class='brand'>ZH ERP<br/><small>Architecture Explorer</small></div>
<a href='#' class='nav-link' data-nav='architecture' onclick="showGroup('architecture');return false;">Arquitectura</a>
<a href='#' class='nav-link' data-nav='architecture' onclick="showGroup('architecture');selectLayer('core');return false;">ERP Core</a>
<a href='#' class='nav-link' data-nav='architecture' onclick="showGroup('architecture');selectLayer('web');return false;">Frontend</a>
<a href='#' class='nav-link' data-nav='architecture' onclick="showGroup('architecture');selectLayer('db');return false;">Database</a>
<a href='#' class='nav-link' data-nav='business-capability' onclick="showGroup('business-capability');return false;">Business Capability</a>
<a href='#' class='nav-link' data-nav='engineering' onclick="showGroup('engineering');return false;">Engineering</a>
<a href='#' class='nav-link' data-nav='security' onclick="showGroup('security');return false;">Security</a>
<a href='#' class='nav-link' data-nav='production' onclick="showGroup('production');return false;">Production</a>
<a href='#' class='nav-link' data-nav='roadmap' onclick="showGroup('roadmap');return false;">Roadmap</a>
<div class='sidebar-footer'>
<a href='../../PROGRESS.html' class='ext-link'>&#8599; PROGRESS.html</a>
<a href='DASHBOARD-CONTRACT.md' class='ext-link'>&#8599; DASHBOARD-CONTRACT</a>
</div>
</nav>
"@

$topbarHtml = @"
<header class='topbar'>
<div class='topbar-title'>ZH Technologies &mdash; ERP Engineering Dashboard</div>
<div class='topbar-search'>
<input type='text' id='searchBox' placeholder='Buscar modulo, feature, proceso o tarea...' autocomplete='off'/>
<div id='searchResults' class='search-results'></div>
</div>
<div class='topbar-actions'>
<span class='topbar-status' style='color:$(Get-ScoreColor $score.Overall)'>$($completionIntelligence.overallStatus)</span>
<button id='themeToggle' class='theme-toggle' title='Toggle light/dark theme'>&#9788;/&#9790;</button>
</div>
</header>
"@

$footerHtml = @"
<footer class='footer'>
<p>Generated: $($model.Generated) &middot; Data sources: dashboard-model-v12.json, architecture-progress.json, model-health.json, modules.json, features.json, processes.json, tasks.json, impact.json, completion-intelligence.json</p>
<p>Regenerate: <code>tools/dashboard/render-dashboard.ps1</code> &middot; Master architecture map: <a href='../../PROGRESS.html'>PROGRESS.html</a> (not modified by this pipeline)</p>
</footer>
"@

$cssHtml = @"
<style>
:root{
  --bg:#f0f2f5;--card:#fff;--text:#1e293b;--muted:#64748b;--border:#e2e8f0;
  /* Paleta reducida a 4 estados: informacion, completado, pendiente, error. */
  --green:#16a34a;--blue:#2563eb;--yellow:#d97706;--red:#dc2626;
  --shadow:0 1px 3px rgba(0,0,0,.06);--shadow-md:0 4px 8px rgba(0,0,0,.08);
  --R:10px;--RL:14px;--sidebar-w:240px;
  --green-bg:#f0fdf4;--green-bd:#bbf7d0;--yellow-bg:#fffbeb;--yellow-bd:#fde68a;--red-bg:#fef2f2;--red-bd:#fecaca;--blue-bg:#eff6ff;--blue-bd:#bfdbfe;
}
:root[data-theme="dark"]{
  --bg:#0b1220;--card:#111827;--text:#e5e7eb;--muted:#94a3b8;--border:#1f2937;
  --shadow:0 1px 3px rgba(0,0,0,.4);--shadow-md:0 4px 10px rgba(0,0,0,.5);
  --green-bg:#052e16;--green-bd:#14532d;--yellow-bg:#422006;--yellow-bd:#713f12;--red-bg:#450a0a;--red-bd:#7f1d1d;--blue-bg:#0c2a4d;--blue-bd:#1d4ed8;
}
@media (prefers-color-scheme: dark){ :root:not([data-theme="light"]){
  --bg:#0b1220;--card:#111827;--text:#e5e7eb;--muted:#94a3b8;--border:#1f2937;
}}
*{box-sizing:border-box}
body{font-family:'Segoe UI',Arial,sans-serif;background:var(--bg);color:var(--text);margin:0;padding:0;font-size:14px}
a{color:var(--blue)}
.topbar{position:sticky;top:0;z-index:20;display:flex;align-items:center;gap:16px;background:var(--card);border-bottom:1px solid var(--border);padding:10px 20px;box-shadow:var(--shadow)}
.topbar-title{font-weight:800;font-size:15px;white-space:nowrap}
.topbar-search{position:relative;flex:1;max-width:420px}
.topbar-search input{width:100%;padding:8px 12px;border-radius:8px;border:1px solid var(--border);background:var(--bg);color:var(--text)}
.search-results{position:absolute;top:100%;left:0;right:0;background:var(--card);border:1px solid var(--border);border-radius:8px;box-shadow:var(--shadow-md);max-height:280px;overflow-y:auto;display:none;z-index:30}
.search-results.show{display:block}
.search-result-item{padding:8px 12px;font-size:12px;cursor:pointer;border-bottom:1px solid var(--border)}
.search-result-item:hover{background:var(--bg)}
.search-result-type{font-size:9px;text-transform:uppercase;color:var(--muted);font-weight:700}
.topbar-actions{display:flex;align-items:center;gap:12px;margin-left:auto}
.topbar-status{font-weight:700;font-size:12px;text-transform:uppercase}
.theme-toggle{border:1px solid var(--border);background:var(--bg);color:var(--text);border-radius:8px;padding:6px 10px;cursor:pointer}
.layout{display:flex;align-items:flex-start}
.sidebar{position:sticky;top:53px;width:var(--sidebar-w);flex-shrink:0;height:calc(100vh - 53px);overflow-y:auto;background:var(--card);border-right:1px solid var(--border);padding:16px 0}
.brand{padding:0 16px 14px;font-weight:800;font-size:13px;border-bottom:1px solid var(--border);margin-bottom:8px}
.brand small{color:var(--muted);font-weight:600}
.nav-link{display:block;padding:9px 16px;font-size:12.5px;color:var(--text);text-decoration:none;border-left:3px solid transparent}
.nav-link:hover{background:var(--bg)}
.nav-link.active{border-left-color:var(--blue);color:var(--blue);font-weight:700;background:var(--bg)}
.sidebar-footer{margin-top:16px;padding:12px 16px 0;border-top:1px solid var(--border)}
.ext-link{display:block;font-size:11px;margin-bottom:6px}
.main{flex:1;min-width:0;padding:26px 30px 70px}
.kpi-strip{display:grid;grid-template-columns:repeat(7,1fr);gap:10px;margin-bottom:18px}
.kpi-tile{background:var(--card);border:1px solid var(--border);border-radius:var(--R);padding:12px;box-shadow:var(--shadow)}
.kpi-top{display:flex;justify-content:space-between;align-items:center;margin-bottom:6px}
.kpi-label{font-size:10px;color:var(--muted);text-transform:uppercase;font-weight:700;letter-spacing:.3px}
.badge{font-size:8px;padding:2px 6px;border-radius:8px;font-weight:700;text-transform:uppercase}
.kpi-value{font-size:22px;font-weight:800;line-height:1.1}
.kpi-bar{height:5px;background:var(--border);border-radius:3px;margin-top:8px;overflow:hidden}
.kpi-bar-fill{height:100%}
.panel{background:var(--card);border:1px solid var(--border);border-radius:var(--RL);box-shadow:var(--shadow-md);padding:28px 30px;margin-bottom:26px;scroll-margin-top:64px}
.panel h2{margin:0 0 12px;font-size:18px}
.panel h3{font-size:13px;margin:14px 0 8px;color:var(--muted);text-transform:uppercase;letter-spacing:.4px}
.panel h4{font-size:12px;margin:10px 0 6px}
.panel-grid-2{display:grid;grid-template-columns:1fr 1fr;gap:18px}
.sub-card{background:var(--bg);border:1px solid var(--border);border-radius:var(--R);padding:14px}
.sub-card.center{text-align:center}
.big-status{font-size:26px;font-weight:800;margin:4px 0}
.muted-note{color:var(--muted);font-size:11.5px}
table{width:100%;border-collapse:collapse;margin-top:6px}
td,th{padding:8px 10px;border-bottom:1px solid var(--border);text-align:left;font-size:12.5px}
th{cursor:pointer;user-select:none;color:var(--muted);text-transform:uppercase;font-size:10.5px}
ul,ol{margin:6px 0;padding-left:20px;font-size:12.5px}
li{margin-bottom:4px}
.card-section{border:1px solid var(--border);border-radius:var(--R);padding:10px 14px;margin-top:10px;background:var(--bg)}
.card-section summary{cursor:pointer;font-weight:700;font-size:12.5px}
.pill{padding:2px 9px;border-radius:14px;font-size:9px;font-weight:700;text-transform:uppercase}
.pill-d{background:var(--green-bg);color:var(--green);border:1px solid var(--green-bd)}
.pill-p{background:var(--yellow-bg);color:var(--yellow);border:1px solid var(--yellow-bd)}
.pill-n{background:var(--red-bg);color:var(--red);border:1px solid var(--red-bd)}
.pill-f{background:var(--blue-bg);color:var(--blue);border:1px solid var(--blue-bd)}
.gauge{position:relative;margin:0 auto}
.gauge .gpct{position:absolute;inset:0;display:flex;align-items:center;justify-content:center;font-size:22px;font-weight:800}
.gauge .gpct small{font-size:11px;margin-left:2px;color:var(--muted)}
.heat-row{display:flex;gap:8px;flex-wrap:wrap;margin:8px 0}
.heat-cell{color:#fff;border-radius:8px;padding:8px 12px;text-align:center;min-width:70px}
.heat-v{font-size:16px;font-weight:800}
.heat-l{font-size:9px;text-transform:uppercase}
.module-heatmap{overflow-x:auto;margin:8px 0}
.mh-row{display:grid;grid-template-columns:140px repeat(auto-fit,minmax(60px,1fr));gap:3px;align-items:center;margin-bottom:3px}
.mh-head .mh-col-h{font-size:9px;text-transform:uppercase;color:var(--muted);text-align:center;font-weight:700}
.mh-name{font-size:11px;font-weight:600;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
.heat-mini{color:#fff;font-size:10px;text-align:center;border-radius:4px;padding:4px 0;font-weight:700}
.explorer-tree{margin-top:8px}
.tree-node{border:1px solid var(--border);border-radius:8px;padding:6px 10px;margin:4px 0;background:var(--bg)}
.tree-node summary{cursor:pointer;font-size:12.5px;font-weight:600}
.tree-count{color:var(--muted);font-size:10px;margin-left:6px}
.tree-empty,.tree-empty-li{color:var(--muted);font-size:11px;padding:4px 0}
.tree-group-label{font-size:9.5px;text-transform:uppercase;color:var(--muted);font-weight:700;margin:8px 0 4px}
.tree-files{list-style:none;padding-left:6px;font-size:11px;color:var(--muted)}
.tree-file{font-family:Consolas,monospace;font-size:10.5px}
.tree-task{font-size:11px}
.arch-map-svg{margin:8px 0}
.arch-map-node{cursor:pointer}
.arch-map-node:hover rect{filter:brightness(1.08)}
.phase-row{padding:8px 0;border-bottom:1px solid var(--border)}
.phase-bar-track{height:6px;background:var(--border);border-radius:3px;overflow:hidden;margin-bottom:4px}
.phase-bar-fill{height:100%}
.phase-info{display:flex;align-items:center;gap:8px;font-size:12px}
.phase-name{font-weight:700}
.phase-pct{margin-left:auto;color:var(--muted);font-size:11px}
.phase-desc{font-size:11px;color:var(--muted);margin-top:2px}
.timeline{display:flex;align-items:center;overflow-x:auto;padding:14px 4px}
.tl-node{display:flex;flex-direction:column;align-items:center;min-width:110px;cursor:pointer}
.tl-dot{width:52px;height:52px;border-radius:50%;color:#fff;display:flex;align-items:center;justify-content:center;font-weight:800;font-size:12px}
.tl-label{font-size:11px;font-weight:700;margin-top:6px;text-align:center}
.tl-sub{font-size:10px;color:var(--muted)}
.tl-line{flex:0 0 30px;height:2px;background:var(--border);margin-top:-30px}
.radar-svg,.sparkline-svg{display:block;margin:0 auto}
.footer{padding:20px 22px;color:var(--muted);font-size:11px;border-top:1px solid var(--border)}
.footer code{background:var(--bg);padding:1px 5px;border-radius:4px}
@media(max-width:1279px){
  .kpi-strip{grid-template-columns:repeat(3,1fr)}
  .panel-grid-2{grid-template-columns:1fr}
}
@media(max-width:1023px){
  :root{--sidebar-w:56px}
  .sidebar .nav-link{font-size:0;padding:10px 0;text-align:center}
  .sidebar .nav-link::first-letter{font-size:13px}
  .brand small,.sidebar-footer{display:none}
  .kpi-strip{grid-template-columns:repeat(2,1fr)}
  .topbar-title{display:none}
}

/* ===== Architecture Explorer (home view + diagram + contextual panel) ===== */
.compact-strip{display:flex;gap:6px;flex-wrap:wrap;margin-bottom:14px;padding:8px;background:var(--card);border:1px solid var(--border);border-radius:var(--R)}
.compact-chip{border:2px solid;border-radius:14px;padding:3px 9px;font-size:10px;font-weight:800;cursor:pointer;background:var(--bg)}
.compact-chip:hover{filter:brightness(1.1)}
#view-architecture{display:block}
#view-architecture.hidden{display:none}
.explorer-grid{display:grid;grid-template-columns:1.4fr 1fr;gap:26px;align-items:start}
@media(max-width:1279px){.explorer-grid{grid-template-columns:1fr}}
.diagram-wrap{background:var(--card);border:1px solid var(--border);border-radius:var(--RL);box-shadow:var(--shadow-md);padding:22px;min-height:520px}
.diagram-row{display:flex;justify-content:center;gap:16px;margin-bottom:6px;flex-wrap:wrap}
.diagram-conn{text-align:center;color:var(--muted);font-size:14px;margin:2px 0}
.diagram-node{border:2px solid;border-radius:var(--R);background:var(--bg);padding:12px 16px;min-width:120px;text-align:center;cursor:pointer;transition:.15s}
.diagram-node:hover{transform:translateY(-2px);box-shadow:var(--shadow-md)}
.diagram-node.selected{box-shadow:0 0 0 3px rgba(37,99,235,.35)}
.diagram-node-wide{min-width:260px}
.diagram-node-dim{opacity:.6}
.diagram-node-title{font-size:12px;font-weight:700}
.diagram-node-pct{font-size:18px;font-weight:800;margin-top:2px}
.context-panel{background:var(--card);border:1px solid var(--border);border-radius:var(--RL);box-shadow:var(--shadow-md);padding:28px;min-height:520px;max-height:80vh;overflow-y:auto}
.module-panel h5{font-size:11.5px;text-transform:uppercase;color:var(--muted);margin:16px 0 6px;letter-spacing:.3px}
.module-panel-head{display:flex;align-items:center;gap:10px;margin-bottom:14px;font-size:14px}

/* ===== Breadcrumb + hierarchical level cards (Arquitectura -> Capa ->
   Dominio -> Modulo -> Feature/Proceso -> Archivo). Detalle solo aparece al
   seguir navegando -- cada nivel muestra tarjetas resumen (id + %), nunca
   el detalle completo del siguiente nivel. ===== */
.breadcrumb-bar{font-size:12px;margin-bottom:18px;min-height:18px;color:var(--muted)}
.breadcrumb-bar:empty{display:none}
.crumb{cursor:pointer;font-weight:600}
.crumb:hover{color:var(--blue);text-decoration:underline}
.crumb-root{font-weight:700}
.crumb-sep{margin:0 6px;color:var(--border)}
.level-heading{font-size:11.5px;text-transform:uppercase;color:var(--muted);letter-spacing:.4px;margin-bottom:16px}
.level-grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(150px,1fr));gap:16px}
.level-card{background:var(--bg);border:1px solid var(--border);border-radius:var(--R);padding:18px 16px;text-align:center;cursor:pointer;transition:.15s}
.level-card:hover{transform:translateY(-2px);box-shadow:var(--shadow-md);border-color:var(--blue)}
.level-card-disabled{cursor:default}
.level-card-disabled:hover{transform:none;box-shadow:none;border-color:var(--border)}
.level-card-pct{font-size:24px;font-weight:800}
.level-card-label{font-size:12.5px;font-weight:700;margin-top:6px}
.level-card-sub{font-size:10.5px;color:var(--muted);margin-top:4px}
.level-summary-head{display:flex;align-items:center;gap:14px;margin-bottom:18px;padding-bottom:18px;border-bottom:1px solid var(--border)}
.chip-row{display:flex;flex-wrap:wrap;gap:8px;margin-bottom:6px}
.chip{background:var(--blue-bg);color:var(--blue);border:1px solid var(--blue-bd);border-radius:16px;padding:5px 12px;font-size:11.5px;cursor:pointer}
.chip:hover{filter:brightness(0.95)}
.module-open-link{color:var(--blue);cursor:pointer;font-size:10.5px;font-weight:700}
.file-link{color:var(--blue);cursor:pointer;text-decoration:underline}
.dep-graph-svg{display:block;margin:0 auto;max-width:100%}
.dep-graph-node{cursor:pointer}
.dep-graph-node:hover circle{filter:brightness(1.15)}
.dep-graph-legend{display:flex;gap:14px;flex-wrap:wrap;margin-top:10px;font-size:11px;color:var(--muted)}
.dep-graph-legend span{display:inline-flex;align-items:center;gap:4px}
.dep-graph-legend i{width:10px;height:10px;border-radius:50%;display:inline-block}
.groups-view{display:none}
.groups-view.active{display:block}
</style>
"@

$jsHtml = @"
<script>
var SEARCH_INDEX = $searchIndexJson;

(function(){
  var root = document.documentElement;
  var stored = localStorage.getItem('zh-dashboard-theme');
  if(stored){ root.setAttribute('data-theme', stored); }
  var btn = document.getElementById('themeToggle');
  if(btn){
    btn.addEventListener('click', function(){
      var current = root.getAttribute('data-theme') === 'dark' ? 'light' : 'dark';
      root.setAttribute('data-theme', current);
      localStorage.setItem('zh-dashboard-theme', current);
    });
  }
})();

(function(){
  var box = document.getElementById('searchBox');
  var results = document.getElementById('searchResults');
  if(!box || !results) return;
  box.addEventListener('input', function(){
    var q = box.value.trim().toLowerCase();
    if(q.length < 2){ results.classList.remove('show'); results.innerHTML=''; return; }
    var matches = SEARCH_INDEX.filter(function(e){ return e.label.toLowerCase().indexOf(q) !== -1; }).slice(0, 25);
    if(matches.length === 0){ results.innerHTML = '<div class=search-result-item>No matches</div>'; }
    else{
      results.innerHTML = matches.map(function(e){
        return '<div class="search-result-item" data-target="'+e.target+'" data-kind="'+e.kind+'"><span class="search-result-type">'+e.type+'</span><br/>'+e.label+'</div>';
      }).join('');
    }
    results.classList.add('show');
  });
  results.addEventListener('click', function(ev){
    var item = ev.target.closest('.search-result-item');
    if(!item || !item.dataset.target) return;
    var target = item.dataset.target;
    var kind = item.dataset.kind;

    if(kind === 'module-panel'){
      showGroup('architecture');
      pushLevel('module', target, target);
    } else if(kind === 'file-panel'){
      showGroup('architecture');
      pushLevel('file', target, target);
    } else if(kind === 'group'){
      showGroup(target);
    } else {
      var el = document.getElementById(target);
      if(el){
        var groupAncestor = el.closest('.groups-view');
        if(groupAncestor){ showGroup(groupAncestor.getAttribute('data-group-view')); }
        if(el.tagName === 'DETAILS') el.open = true;
        el.scrollIntoView({behavior:'smooth', block:'center'});
      }
    }
    results.classList.remove('show');
    box.value='';
  });
  document.addEventListener('click', function(ev){
    if(!results.contains(ev.target) && ev.target !== box){ results.classList.remove('show'); }
  });
})();

var FILE_PANELS = $filePanelsJson;
var LAYER_LEVELS = $layerLevelJson;
var DOMAIN_LEVELS = $domainLevelJson;
var MODULE_SUMMARIES = $moduleSummaryJson;
var FEATURE_LEVELS = $featureLevelJson;
var PROCESS_LEVELS = $processLevelJson;

function showGroup(name){
  var links = document.querySelectorAll('.nav-link');
  links.forEach(function(l){ l.classList.remove('active'); });
  var activeLinks = document.querySelectorAll('.nav-link[data-nav="' + name + '"]');
  activeLinks.forEach(function(l){ l.classList.add('active'); });

  var archView = document.getElementById('view-architecture');
  var groupViews = document.querySelectorAll('.groups-view');

  if(name === 'architecture'){
    archView.classList.remove('hidden');
    groupViews.forEach(function(g){ g.classList.remove('active'); });
    resetBreadcrumb();
  } else {
    archView.classList.add('hidden');
    groupViews.forEach(function(g){
      if(g.getAttribute('data-group-view') === name) g.classList.add('active');
      else g.classList.remove('active');
    });
  }
}

// ============================================================================
// BREADCRUMB-DRIVEN HIERARCHICAL NAVIGATION
// Arquitectura -> Capa -> Dominio -> Modulo -> Feature/Proceso -> Archivo
// El diagrama principal (11 nodos) permanece SIEMPRE visible y persistente;
// solo el panel lateral cambia de nivel. Ningun nivel muestra el detalle
// completo del siguiente -- solo tarjetas resumen con % hasta que el usuario
// decide seguir navegando.
// ============================================================================

var breadcrumbStack = [];

var LEVEL_LABELS = { layer: 'Capa', domain: 'Dominio', module: 'Modulo', feature: 'Feature', process: 'Proceso', file: 'Archivo' };

function resetBreadcrumb(){
  breadcrumbStack = [];
  renderBreadcrumb();
  var content = document.getElementById('levelContent');
  if(content) content.innerHTML = '';
  document.querySelectorAll('.diagram-node').forEach(function(el){ el.classList.remove('selected'); });
}

function pushLevel(level, id, label){
  breadcrumbStack.push({level:level, id:id, label:label});
  renderBreadcrumb();
  renderCurrentLevel();

  if(level === 'layer'){
    document.querySelectorAll('.diagram-node').forEach(function(el){ el.classList.remove('selected'); });
    var node = document.getElementById('diagramnode-' + id);
    if(node) node.classList.add('selected');
  }

  var content = document.getElementById('levelContent');
  if(content) content.scrollIntoView({behavior:'smooth', block:'nearest'});
}

function popToIndex(i){
  breadcrumbStack = breadcrumbStack.slice(0, i + 1);
  renderBreadcrumb();
  renderCurrentLevel();
}

function renderBreadcrumb(){
  var bar = document.getElementById('breadcrumbBar');
  if(!bar) return;

  var crumbs = ['<span class="crumb crumb-root" onclick="resetBreadcrumb()">Arquitectura</span>'];
  breadcrumbStack.forEach(function(entry, i){
    crumbs.push('<span class="crumb-sep">&rsaquo;</span>');
    crumbs.push('<span class="crumb" onclick="popToIndex(' + i + ')">' + entry.label + '</span>');
  });
  bar.innerHTML = crumbs.join('');
}

function renderCurrentLevel(){
  var content = document.getElementById('levelContent');
  if(!content) return;
  if(breadcrumbStack.length === 0){ content.innerHTML = ''; return; }

  var top = breadcrumbStack[breadcrumbStack.length - 1];
  var html = '<p class="muted-note">No data available for this level.</p>';

  if(top.level === 'layer'){ html = LAYER_LEVELS[top.id] || html; }
  else if(top.level === 'domain'){ html = DOMAIN_LEVELS[top.id] || html; }
  else if(top.level === 'module'){ html = MODULE_SUMMARIES[top.id] || html; }
  else if(top.level === 'feature'){ html = FEATURE_LEVELS[top.id] || html; }
  else if(top.level === 'process'){ html = PROCESS_LEVELS[top.id] || html; }
  else if(top.level === 'file'){ html = FILE_PANELS[top.id] || html; }

  content.innerHTML = '<div class="level-heading">' + (LEVEL_LABELS[top.level] || '') + ': <b>' + top.label + '</b></div>' + html;
}

function selectLayer(layerId){
  var navLabelEl = document.getElementById('diagramnode-' + layerId);
  var label = navLabelEl ? navLabelEl.querySelector('.diagram-node-title').textContent : layerId;
  resetBreadcrumb();
  pushLevel('layer', layerId, label);
}

// Legacy aliases kept so evidence panels generated for other sections
// (Dependency Explorer graph, search results, reverse-file navigation)
// keep working through the same single breadcrumb-driven panel.
function openModulePanel(moduleId){ pushLevel('module', moduleId, moduleId); }
function openFilePanel(filePath){ pushLevel('file', filePath, filePath); }

function highlightDepNode(moduleId){
  var svg = document.getElementById('depGraphSvg');
  if(svg){
    svg.querySelectorAll('.dep-edge').forEach(function(line){
      var isDirect = line.dataset.from === moduleId || line.dataset.to === moduleId;
      line.setAttribute('stroke', isDirect ? '#2563eb' : '#cbd5e1');
      line.setAttribute('stroke-width', isDirect ? '2' : '1');
    });
  }
  showGroup('architecture');
  resetBreadcrumb();
  pushLevel('module', moduleId, moduleId);
}

document.addEventListener('DOMContentLoaded', function(){
  showGroup('architecture');
});

(function(){
  document.querySelectorAll('table.sortable').forEach(function(table){
    var headers = table.querySelectorAll('th');
    headers.forEach(function(th, idx){
      th.addEventListener('click', function(){
        var tbody = table.querySelectorAll('tr').length > 1 ? table : null;
        if(!tbody) return;
        var rows = Array.prototype.slice.call(table.querySelectorAll('tr')).slice(1);
        var asc = th.dataset.asc !== 'true';
        headers.forEach(function(h){ delete h.dataset.asc; });
        th.dataset.asc = asc;
        rows.sort(function(a, b){
          var av = a.children[idx] ? a.children[idx].innerText.trim() : '';
          var bv = b.children[idx] ? b.children[idx].innerText.trim() : '';
          var an = parseFloat(av.replace('%','')); var bn = parseFloat(bv.replace('%',''));
          if(!isNaN(an) && !isNaN(bn)) return asc ? an-bn : bn-an;
          return asc ? av.localeCompare(bv) : bv.localeCompare(av);
        });
        rows.forEach(function(r){ table.appendChild(r); });
      });
    });
  });
})();
</script>
"@

# =============================================================================
# HTML RENDER
# =============================================================================

$html = @(
"<!DOCTYPE html>",
"<html>",
"<head>",
"<meta charset='utf-8'/>",
"<meta name='viewport' content='width=device-width, initial-scale=1.0'/>",
"<title>ZH Technologies ERP Engineering Dashboard</title>",
$cssHtml,
"</head>",
"<body>",
$topbarHtml,
"<div class='layout'>",
$sidebarHtml,
"<main class='main'>",
$kpiStripHtml,
$compactStripHtml,

"<div id='view-architecture'>",
"<div class='explorer-grid'>",
"<div class='diagram-wrap'>",
$diagramHtml,
"</div>",
"<div class='context-panel'>",
"<div id='breadcrumbBar' class='breadcrumb-bar'></div>",
"<div id='levelContent' class='level-content'><p class='muted-note'>Selecciona un bloque del diagrama para explorar: Arquitectura &rarr; Capa &rarr; Dominio &rarr; Modulo &rarr; Feature/Proceso &rarr; Archivo.</p></div>",
"</div>",
"</div>",
"</div>",

"<div class='groups-view' data-group-view='business-capability'>",
$architectureHtml,
$businessCapabilityHtml,
$architectureProgressSectionHtml,
$dependencyExplorerHtml,
$criticalPathHtml,
"</div>",

"<div class='groups-view' data-group-view='engineering'>",
$engineeringScoreHtml,
$dependencyGraphHtml,
$technicalDebtSectionHtml,
$trendSectionHtml,
$modelHealthSectionHtml,
"</div>",

"<div class='groups-view' data-group-view='security'>",
$securitySectionHtml,
"</div>",

"<div class='groups-view' data-group-view='production'>",
$productionDecisionSectionHtml,
$releaseSimulationHtml,
$riskAssessmentSectionHtml,
"</div>",

"<div class='groups-view' data-group-view='roadmap'>",
$execSummaryHtml,
$erpCompletionHtml,
$roadmapSectionHtml,
$recommendationsSectionHtml,
$execDashboardHtml,
"</div>",

$footerHtml,
"</main>",
"</div>",
$jsHtml,
"</body>",
"</html>"
) -join "`n"


$html | Out-File $Output -Encoding utf8



Write-Host ""
Write-Host "Dashboard generated successfully." -ForegroundColor Green
Write-Host $Output
