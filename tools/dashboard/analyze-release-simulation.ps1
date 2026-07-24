# =============================================================================
# ZH Technologies
# Release Simulation Analyzer ("what-if" calculator)
#
# Ejecuta escenarios hipoteticos usando EXACTAMENTE las mismas formulas ya
# usadas en produccion (calculate-engineering-score.ps1 para el peso del
# Overall, render-dashboard.ps1 para Production Readiness / bandas de riesgo,
# architecture-progress.json para el peso real de Etapas/Fases del mapa
# maestro). Nunca escribe sobre los JSON reales -- esto es una simulacion de
# solo lectura que produce un archivo nuevo (release-simulation.json).
#
# IMPORTANTE: Engineering Score (calidad de codigo) y ERP Completion (avance
# de PROGRESS.html) son dos familias de metricas independientes en este
# pipeline. Completar una etapa/fase de PROGRESS.html NO mueve el Engineering
# Score, y mejorar Security/Quality NO mueve el % de PROGRESS.html. Cada
# escenario reporta unicamente la familia de metricas que realmente afecta,
# para no insinuar una relacion causal que no existe en los datos.
# =============================================================================

$ErrorActionPreference = "Stop"

$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$DataRoot = Join-Path $ProjectRoot "docs\ProgressDashboard\data"
$Output = Join-Path $DataRoot "release-simulation.json"

Write-Host ""
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host " Release Simulation Analyzer"
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host ""

function LoadJson($file)
{
    $path = Join-Path $DataRoot $file
    if(!(Test-Path $path)) { throw "Missing file: $path" }
    return (Get-Content $path -Raw | ConvertFrom-Json)
}

$model = LoadJson "dashboard-model-v12.json"
$score = $model.EngineeringScore
$architectureProgress = LoadJson "architecture-progress.json"

Write-Host "Baseline Engineering Score: $($score.Overall)% -- Baseline ERP Completion: $($architectureProgress.global.pct)%"


# =============================================================================
# Ported formulas (must match calculate-engineering-score.ps1 / render-dashboard.ps1 exactly)
# =============================================================================

function Get-OverallScore($architectureVal, $moduleHealthVal, $qualityVal, $securityVal, $dependenciesVal)
{
    # Weights copied verbatim from tools/dashboard/calculate-engineering-score.ps1
    $overall = ($moduleHealthVal * 0.30) + ($architectureVal * 0.20) + ($qualityVal * 0.20) + ($securityVal * 0.20) + ($dependenciesVal * 0.10)
    if($overall -lt 0) { $overall = 0 }
    if($overall -gt 100) { $overall = 100 }
    return [math]::Round($overall, 2)
}

function Get-ProductionReadiness($architectureVal, $moduleHealthVal, $qualityVal, $securityVal, $dependenciesVal)
{
    # Formula copied verbatim from render-dashboard.ps1 ($productionReadiness)
    return [math]::Round((($architectureVal + $moduleHealthVal + $qualityVal + $securityVal + $dependenciesVal) / 5), 2)
}

function Get-ProductionStatus($readiness)
{
    # Bands copied verbatim from render-dashboard.ps1 ($productionStatus)
    if($readiness -lt 80) { return "NOT READY" }
    elseif($readiness -lt 90) { return "NEEDS REVIEW" }
    else { return "READY" }
}

function Get-RiskLevelSim($value, $isFindingsCount)
{
    # Copied verbatim from render-dashboard.ps1 (Get-RiskLevel)
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

function Get-RiskRankSim($level)
{
    switch($level) { "CRITICAL" { 4 }; "HIGH" { 3 }; "MEDIUM" { 2 }; default { 1 } }
}

function Get-OverallRisk($architectureVal, $securityVal, $qualityVal, $criticalFindingsVal)
{
    $levels = @(
        (Get-RiskLevelSim $architectureVal $false),
        (Get-RiskLevelSim $securityVal $false),
        (Get-RiskLevelSim $qualityVal $false),
        (Get-RiskLevelSim $criticalFindingsVal $true)
    )
    $best = "LOW"
    $bestRank = 0
    foreach($lvl in $levels)
    {
        $rank = Get-RiskRankSim $lvl
        if($rank -gt $bestRank) { $bestRank = $rank; $best = $lvl }
    }
    return $best
}

function Build-EngineeringScenario($name, $architectureVal, $moduleHealthVal, $qualityVal, $securityVal, $dependenciesVal, $criticalFindingsVal, $basis)
{
    $newOverall = Get-OverallScore $architectureVal $moduleHealthVal $qualityVal $securityVal $dependenciesVal
    $newReadiness = Get-ProductionReadiness $architectureVal $moduleHealthVal $qualityVal $securityVal $dependenciesVal
    $newStatus = Get-ProductionStatus $newReadiness
    $newRisk = Get-OverallRisk $architectureVal $securityVal $qualityVal $criticalFindingsVal

    return [ordered]@{
        scenario = $name
        basis = $basis
        inputs = [ordered]@{ architecture = $architectureVal; moduleHealth = $moduleHealthVal; quality = $qualityVal; security = $securityVal; dependencies = $dependenciesVal; criticalFindings = $criticalFindingsVal }
        engineeringScoreOverall = [ordered]@{ baseline = $score.Overall; simulated = $newOverall; delta = [math]::Round($newOverall - $score.Overall, 2) }
        productionReadiness = [ordered]@{ baseline = $baselineReadiness; simulated = $newReadiness; delta = [math]::Round($newReadiness - $baselineReadiness, 2) }
        productionStatus = [ordered]@{ baseline = $baselineStatus; simulated = $newStatus }
        overallRisk = [ordered]@{ baseline = $baselineRisk; simulated = $newRisk }
    }
}


# =============================================================================
# Baseline (current real values)
# =============================================================================

$technicalDebt = $model.TechnicalDebt
$baselineReadiness = Get-ProductionReadiness $score.Architecture $score.ModuleHealth $score.Quality $score.Security $score.Dependencies
$baselineStatus = Get-ProductionStatus $baselineReadiness
$baselineRisk = Get-OverallRisk $score.Architecture $score.Security $score.Quality $technicalDebt.CriticalFindings

Write-Host "Baseline Production Readiness: $baselineReadiness% ($baselineStatus) -- Overall Risk: $baselineRisk"


# =============================================================================
# Scenario 1 & 2: Security -> 80, Quality -> 85 (Engineering Score family)
# =============================================================================

$scenarioSecurity80 = Build-EngineeringScenario `
    "Security reaches 80%" $score.Architecture $score.ModuleHealth $score.Quality 80 $score.Dependencies $technicalDebt.CriticalFindings `
    "dashboard-model-v12.json EngineeringScore.Security set to 80, all other components held at current real values"

$scenarioQuality85 = Build-EngineeringScenario `
    "Quality reaches 85%" $score.Architecture $score.ModuleHealth 85 $score.Security $score.Dependencies $technicalDebt.CriticalFindings `
    "dashboard-model-v12.json EngineeringScore.Quality set to 85, all other components held at current real values"

$scenarioCombined = Build-EngineeringScenario `
    "Security 80% + Quality 85% combined" $score.Architecture $score.ModuleHealth 85 80 $score.Dependencies $technicalDebt.CriticalFindings `
    "Both Security=80 and Quality=85 applied simultaneously to EngineeringScore components"

Write-Host "Engineering scenarios computed: Security->80, Quality->85, combined"


# =============================================================================
# Scenarios 3-5: Stage/Phase completion (ERP Completion family)
# =============================================================================

function Get-ErpCompletionScenario($name, $stageName, $phaseName, $basis)
{
    $totalTasksAll = [double]$architectureProgress.global.totalTasks
    $doneAll = [double]$architectureProgress.global.done

    $targetStage = $architectureProgress.stages | Where-Object { $_.name -eq $stageName } | Select-Object -First 1
    if($null -eq $targetStage) { throw "Stage not found in architecture-progress.json: $stageName" }

    if($phaseName)
    {
        $targetPhase = $targetStage.phases | Where-Object { $_.name -eq $phaseName } | Select-Object -First 1
        if($null -eq $targetPhase) { throw "Phase not found: $phaseName (stage $stageName)" }
        $remaining = [double]$targetPhase.totalTasks - [double]$targetPhase.done
        $scopeLabel = "$stageName / $phaseName"
    }
    else
    {
        $remaining = [double]$targetStage.totalTasks - [double]$targetStage.done
        $scopeLabel = $stageName
    }

    $newDoneAll = $doneAll + $remaining
    $newGlobalPct = [math]::Round(($newDoneAll / $totalTasksAll) * 100, 2)

    return [ordered]@{
        scenario = $name
        basis = $basis
        scope = $scopeLabel
        remainingTasksCompleted = $remaining
        erpCompletion = [ordered]@{
            baseline = $architectureProgress.global.pct
            simulated = $newGlobalPct
            delta = [math]::Round($newGlobalPct - $architectureProgress.global.pct, 2)
        }
        note = "Engineering Score / Production Readiness / Overall Risk are NOT affected by this scenario -- they measure code quality, not PROGRESS.html feature completion (separate metric families in this pipeline)."
    }
}

$scenarioAccounting = Get-ErpCompletionScenario `
    "Accounting (Contabilidad) phase completed" "Futuro" "Contabilidad (Accounting)" `
    "architecture-progress.json stage 'Futuro' phase 'Contabilidad (Accounting)': totalTasks=$(($architectureProgress.stages | Where-Object {$_.name -eq 'Futuro'}).phases | Where-Object {$_.name -eq 'Contabilidad (Accounting)'} | Select-Object -ExpandProperty totalTasks), done set to totalTasks"

$scenarioFiscal = Get-ErpCompletionScenario `
    "Fiscal / SRI stage completed" "Fiscal / SRI" $null `
    "architecture-progress.json stage 'Fiscal / SRI': totalTasks=$(($architectureProgress.stages | Where-Object {$_.name -eq 'Fiscal / SRI'}).totalTasks), done set to totalTasks"

$scenarioSales = Get-ErpCompletionScenario `
    "Sales (Ventas / Sales Invoice) phase reaches 100%" "Operaciones" "Ventas (Sales Invoice)" `
    "architecture-progress.json stage 'Operaciones' phase 'Ventas (Sales Invoice)': totalTasks=$(($architectureProgress.stages | Where-Object {$_.name -eq 'Operaciones'}).phases | Where-Object {$_.name -eq 'Ventas (Sales Invoice)'} | Select-Object -ExpandProperty totalTasks), done set to totalTasks"

Write-Host "ERP completion scenarios computed: Accounting, Fiscal/SRI, Sales"


# =============================================================================
# Output
# =============================================================================

$result = [ordered]@{
    generated = (Get-Date -Format "yyyy-MM-dd HH:mm:ss")
    source = "dashboard-model-v12.json (EngineeringScore, TechnicalDebt), architecture-progress.json (stage/phase weighted completion)"
    disclaimer = "This is a read-only simulation. No real JSON file is modified. Formulas are copied verbatim from calculate-engineering-score.ps1 and render-dashboard.ps1 -- see 'method' below."
    method = [ordered]@{
        overallScoreWeights = "ModuleHealth*0.30 + Architecture*0.20 + Quality*0.20 + Security*0.20 + Dependencies*0.10 (tools/dashboard/calculate-engineering-score.ps1)"
        productionReadiness = "average of the 5 EngineeringScore components (tools/dashboard/render-dashboard.ps1)"
        productionStatusBands = "<80 NOT READY, <90 NEEDS REVIEW, else READY (tools/dashboard/render-dashboard.ps1)"
        riskBands = "score-based: <40 CRITICAL, <70 HIGH, <90 MEDIUM, else LOW; findings-based (critical findings count): >40 CRITICAL, >20 HIGH, >=10 MEDIUM, else LOW (tools/dashboard/render-dashboard.ps1 Get-RiskLevel)"
        erpCompletionRecalc = "(sum of done tasks across all stages, with the simulated stage/phase's done = its totalTasks) / architecture-progress.json global.totalTasks * 100"
    }
    baseline = [ordered]@{
        engineeringScoreOverall = $score.Overall
        productionReadiness = $baselineReadiness
        productionStatus = $baselineStatus
        overallRisk = $baselineRisk
        erpCompletion = $architectureProgress.global.pct
    }
    scenarios = @($scenarioSecurity80, $scenarioQuality85, $scenarioCombined, $scenarioAccounting, $scenarioFiscal, $scenarioSales)
}

$result | ConvertTo-Json -Depth 10 | Out-File $Output -Encoding utf8

Write-Host ""
Write-Host "Release simulation generated successfully." -ForegroundColor Green
Write-Host $Output
