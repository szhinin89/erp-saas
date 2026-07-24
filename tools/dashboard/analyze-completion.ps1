# =============================================================================
# ZH Technologies
# ERP Completion Intelligence Analyzer
#
# Responde: "Que falta para terminar el ERP?"
#
# No recalcula nada que ya exista. Unicamente lee y correlaciona:
#   architecture-progress.json  -> avance real por Etapa/Fase (PROGRESS.html)
#   dashboard-model-v12.json    -> EngineeringScore, QualityGate, Security,
#                                   TechnicalDebt (ya calculados)
#   model-health.json           -> integridad del modelo de conocimiento
#   modules.json                -> salud por modulo
#   features.json                -> features por modulo y su estado
#   processes.json               -> procesos de negocio y su verificacion
#   tasks.json                   -> tareas de ingenieria ya detectadas
#
# Salida: docs/ProgressDashboard/data/completion-intelligence.json
#
# Toda conclusion se deriva de datos ya existentes. Si falta informacion se
# indica explicitamente en vez de inventar un valor.
# =============================================================================

$ErrorActionPreference = "Stop"

$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$DataRoot = Join-Path $ProjectRoot "docs\ProgressDashboard\data"
$Output = Join-Path $DataRoot "completion-intelligence.json"

Write-Host ""
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host " ERP Completion Intelligence Analyzer"
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

$architectureProgress = LoadJson "architecture-progress.json"
$model = LoadJson "dashboard-model-v12.json"
$modelHealth = LoadJson "model-health.json"
$modulesData = @(LoadJson "modules.json")
$featuresData = @(LoadJson "features.json")
$processesData = @(LoadJson "processes.json")
$tasksData = @(LoadJson "tasks.json")

$score = $model.EngineeringScore
$gate = $model.QualityGate
$security = $model.Security
$technicalDebt = $model.TechnicalDebt

Write-Host "JSON loaded successfully" -ForegroundColor Green

# =============================================================================
# Base metrics (reused, not recalculated)
# =============================================================================

$erpCompletion = [double]$architectureProgress.global.pct
$engineeringHealth = [double]$score.Overall

$architectureHealth =
[math]::Round(
    (([double]$score.Architecture + [double]$modelHealth.integrityScore) / 2),
    2
)

$avg5 =
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

$productionReadiness = "READY"

if($avg5 -lt 80)
{
    $productionReadiness = "NOT READY"
}
elseif($avg5 -lt 90)
{
    $productionReadiness = "NEEDS REVIEW"
}

Write-Host "ERP Completion: $erpCompletion%"
Write-Host "Engineering Health: $engineeringHealth%"
Write-Host "Architecture Health: $architectureHealth%"
Write-Host "Production Readiness: $productionReadiness"

# =============================================================================
# Stage-level gaps (reusa architecture-progress.json -- no recalcula PROGRESS.html)
# =============================================================================

$stages = @($architectureProgress.stages)
$incompleteStages = @($stages | Where-Object { [double]$_.pct -lt 90 } | Sort-Object { [double]$_.pct })
$nearCompleteStages = @($stages | Where-Object { [double]$_.pct -ge 80 -and [double]$_.pct -lt 100 } | Sort-Object { [double]$_.pct } -Descending)

$pendingItems = @($architectureProgress.pending)
$nextStepsItems = @($architectureProgress.nextSteps)

$phasesWithOutstandingWork =
@(
    @($pendingItems | ForEach-Object { "$($_.stage)||$($_.phase)" }) +
    @($nextStepsItems | ForEach-Object { "$($_.stage)||$($_.phase)" })
) | Select-Object -Unique

$estimatedRemainingAreas = $phasesWithOutstandingWork.Count

# Fases con muy pocos items pendientes (nextSteps) -> candidatas a quick win
$nextStepsByPhase = $nextStepsItems | Group-Object -Property { "$($_.stage)||$($_.phase)" }
$smallRemainingPhases = @($nextStepsByPhase | Where-Object { $_.Count -le 2 })

# =============================================================================
# Process / feature verification gaps
# =============================================================================

$unverifiedProcessSteps = @()

foreach($process in $processesData)
{
    foreach($step in $process.steps)
    {
        if($step.status -ne "verified")
        {
            $unverifiedProcessSteps += "$($process.process) / $($step.name)"
        }
    }
}

$pendingFeatures = @()

foreach($moduleEntry in $featuresData)
{
    foreach($feature in $moduleEntry.features)
    {
        if($feature.status -ne "implemented")
        {
            $pendingFeatures += "$($moduleEntry.module) / $($feature.name) ($($feature.status))"
        }
    }
}

# =============================================================================
# Critical Gaps (solo problemas reales, con evidencia)
# =============================================================================

$criticalGaps = @()

if([double]$score.Security -lt 70)
{
    $criticalGaps += "Security Score bajo: $($score.Security)% (Secrets detectados: $($security.SecretsFound), endpoints anonimos: $($security.AnonymousDetected))"
}

if([double]$score.Quality -lt 70)
{
    $criticalGaps += "Quality Score bajo: $($score.Quality)%"
}

if([int]$technicalDebt.CriticalFindings -gt 20)
{
    $criticalGaps += "Technical Debt alta: $($technicalDebt.CriticalFindings) critical findings (TODO: $($technicalDebt.TODO), FIXME: $($technicalDebt.FIXME))"
}

if([int]$modelHealth.brokenReferences -gt 0 -or [int]$modelHealth.missingEvidence -gt 0 -or [double]$modelHealth.integrityScore -lt 90)
{
    $criticalGaps += "Model Integrity incompleta: integrityScore $($modelHealth.integrityScore)%, brokenReferences $($modelHealth.brokenReferences), missingEvidence $($modelHealth.missingEvidence)"
}

if($unverifiedProcessSteps.Count -gt 0)
{
    $criticalGaps += "$($unverifiedProcessSteps.Count) pasos de proceso sin mapear/verificar: $($unverifiedProcessSteps -join '; ')"
}

if($pendingFeatures.Count -gt 0)
{
    $criticalGaps += "$($pendingFeatures.Count) features pendientes: $($pendingFeatures -join '; ')"
}

foreach($stage in $incompleteStages)
{
    $criticalGaps += "Etapa incompleta: $($stage.name) al $($stage.pct)% ($($stage.done) / $($stage.totalTasks) tareas)"
}

if($criticalGaps.Count -eq 0)
{
    $criticalGaps += "No se detectaron gaps criticos con los umbrales actuales"
}

Write-Host "Critical Gaps: $($criticalGaps.Count)"

# =============================================================================
# Overall Status (calculo objetivo, sin valores fijos)
# =============================================================================

$realCriticalGapCount = @($criticalGaps | Where-Object { $_ -ne "No se detectaron gaps criticos con los umbrales actuales" }).Count

$overallStatus = "READY"

if($realCriticalGapCount -ge 4 -or $engineeringHealth -lt 50)
{
    $overallStatus = "NOT READY"
}
elseif($realCriticalGapCount -ge 2 -or $engineeringHealth -lt 70)
{
    $overallStatus = "AT RISK"
}
elseif($erpCompletion -lt 90)
{
    $overallStatus = "IN PROGRESS"
}
elseif($erpCompletion -lt 100)
{
    $overallStatus = "NEAR COMPLETION"
}

Write-Host "Overall Status: $overallStatus"

# =============================================================================
# Recommended Order (prioridad automatica, solo pasos aplicables)
# =============================================================================

$recommendedOrder = @()
$stepNumber = 1

if([double]$score.Security -lt 70)
{
    $recommendedOrder += "$stepNumber. Resolver problemas de seguridad (Security Score $($score.Security)%, $($security.SecretsFound) secretos, $($security.AnonymousDetected) endpoints anonimos)"
    $stepNumber++
}

if([int]$technicalDebt.CriticalFindings -gt 20)
{
    $recommendedOrder += "$stepNumber. Reducir deuda tecnica ($($technicalDebt.CriticalFindings) critical findings, $($technicalDebt.TODO) TODO, $($technicalDebt.FIXME) FIXME)"
    $stepNumber++
}

if($nextStepsItems.Count -gt 0)
{
    $topPartialStages = ($incompleteStages | Select-Object -First 3 | ForEach-Object { "$($_.name) ($($_.pct)%)" }) -join ", "
    $recommendedOrder += "$stepNumber. Completar fases parcialmente implementadas: $($nextStepsItems.Count) items abiertos, priorizar $topPartialStages"
    $stepNumber++
}

if($pendingItems.Count -gt 0)
{
    $pendingStages = ($pendingItems | Select-Object -Property stage -Unique | ForEach-Object { $_.stage }) -join ", "
    $recommendedOrder += "$stepNumber. Completar fases sin iniciar ($($pendingItems.Count) items en: $pendingStages)"
    $stepNumber++
}

if([int]$modelHealth.unmappedItems -gt 0 -or [int]$modelHealth.brokenReferences -gt 0 -or [int]$modelHealth.missingEvidence -gt 0)
{
    $recommendedOrder += "$stepNumber. Revalidar el modelo ($($modelHealth.unmappedItems) items sin mapear, integrityScore $($modelHealth.integrityScore)%)"
    $stepNumber++
}

if($recommendedOrder.Count -eq 0)
{
    $recommendedOrder += "1. No hay acciones prioritarias pendientes segun los datos actuales"
}

Write-Host "Recommended Order: $($recommendedOrder.Count) steps"

# =============================================================================
# Quick Wins (mejoras pequenas de alto impacto, solo si hay evidencia)
# =============================================================================

$quickWins = @()

if([int]$technicalDebt.HACK -gt 0)
{
    $quickWins += "Revisar $($technicalDebt.HACK) marcador(es) HACK"
}

if([int]$technicalDebt.NotImplemented -gt 0)
{
    $quickWins += "Completar $($technicalDebt.NotImplemented) ruta(s) de codigo NotImplementedException"
}

if([int]$technicalDebt.FIXME -gt 0)
{
    $quickWins += "Resolver $($technicalDebt.FIXME) marcador(es) FIXME"
}

if([int]$technicalDebt.TODO -gt 0)
{
    $quickWins += "Triaje y reduccion de $($technicalDebt.TODO) marcador(es) TODO"
}

$unmappedModuleNames = @($modulesData | Where-Object { $_.domainId -eq "unmapped" } | ForEach-Object { $_.id })

if($unmappedModuleNames.Count -gt 0)
{
    $quickWins += "Registrar dominio para $($unmappedModuleNames.Count) modulo(s) sin dominio asignado: $($unmappedModuleNames -join ', ')"
}

foreach($stage in $nearCompleteStages)
{
    $quickWins += "Completar fase cerca del 100%: $($stage.name) esta al $($stage.pct)%"
}

foreach($group in $smallRemainingPhases)
{
    $parts = $group.Name -split '\|\|'
    $quickWins += "Fase con pocos items restantes: $($parts[1]) ($($parts[0])) - $($group.Count) item(s) pendientes"
}

if($quickWins.Count -eq 0)
{
    $quickWins += "No se detectaron quick wins evidentes con los datos actuales"
}

Write-Host "Quick Wins: $($quickWins.Count)"

# =============================================================================
# Next Milestone (derivado de la etapa in-scope menos avanzada)
# =============================================================================

$inScopeIncompleteStages = @($incompleteStages | Where-Object { $_.name -ne "Futuro" })

if($inScopeIncompleteStages.Count -gt 0)
{
    $targetStage = $inScopeIncompleteStages[0]
    $nextMilestone = "Complete remaining $($targetStage.name) phases ($($targetStage.pct)% done, $($targetStage.done) / $($targetStage.totalTasks) tareas)"
}
elseif($incompleteStages.Count -gt 0)
{
    $targetStage = $incompleteStages[0]
    $nextMilestone = "Advance $($targetStage.name) roadmap ($($targetStage.pct)% done)"
}
else
{
    $nextMilestone = "All tracked stages report 100% -- validate PROGRESS.html for newly added scope"
}

Write-Host "Next Milestone: $nextMilestone"

# =============================================================================
# Output
# =============================================================================

$result = [ordered]@{
    generated = (Get-Date -Format "yyyy-MM-dd HH:mm:ss")
    source = "architecture-progress.json, dashboard-model-v12.json, model-health.json, modules.json, features.json, processes.json, tasks.json"
    erpCompletion = $erpCompletion
    engineeringHealth = $engineeringHealth
    architectureHealth = $architectureHealth
    productionReadiness = $productionReadiness
    overallStatus = $overallStatus
    criticalGaps = $criticalGaps
    recommendedOrder = $recommendedOrder
    quickWins = $quickWins
    nextMilestone = $nextMilestone
    estimatedRemainingAreas = $estimatedRemainingAreas
}

$result | ConvertTo-Json -Depth 6 | Out-File $Output -Encoding utf8

Write-Host ""
Write-Host "Completion intelligence generated successfully." -ForegroundColor Green
Write-Host $Output
