# =============================================================================
# ZH Technologies
# Architecture Recommendations Analyzer (deterministic rule engine)
#
# Genera recomendaciones citando EXACTAMENTE los numeros que las justifican.
# Ninguna recomendacion es texto generico: cada una referencia un campo real
# de un JSON ya calculado (dependencies.json, critical-path.json,
# release-simulation.json, completion-intelligence.json, dashboard-model-v12.json,
# model-health.json). Si una condicion no se cumple con los datos actuales, la
# regla correspondiente simplemente no dispara -- no se rellena con relleno.
# =============================================================================

$ErrorActionPreference = "Stop"

$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$DataRoot = Join-Path $ProjectRoot "docs\ProgressDashboard\data"
$Output = Join-Path $DataRoot "recommendations.json"

Write-Host ""
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host " Architecture Recommendations Analyzer"
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host ""

function LoadJson($file)
{
    $path = Join-Path $DataRoot $file
    if(!(Test-Path $path)) { throw "Missing file: $path (run its analyzer first)" }
    return (Get-Content $path -Raw | ConvertFrom-Json)
}

$model = LoadJson "dashboard-model-v12.json"
$modelHealth = LoadJson "model-health.json"
$completionIntelligence = LoadJson "completion-intelligence.json"
$dependencyGraph = LoadJson "dependencies.json"
$criticalPathData = LoadJson "critical-path.json"
$releaseSimulation = LoadJson "release-simulation.json"

$score = $model.EngineeringScore
$technicalDebt = $model.TechnicalDebt

Write-Host "Loaded 6 source JSON files"

$recommendations = @()


# -----------------------------------------------------------------------------
# Rule 1: Technical debt gate blocker
# -----------------------------------------------------------------------------

if([int]$technicalDebt.CriticalFindings -gt 20)
{
    $reduceBy = [int]$technicalDebt.CriticalFindings - 20
    $recommendations += [ordered]@{
        title = "Reduce critical findings below the production gate threshold"
        text = "Reducing $reduceBy of the $($technicalDebt.CriticalFindings) current critical findings (to 20 or fewer) removes the Technical Debt production gate blocker, which currently forces Production Decision = NOT READY / NEEDS REVIEW."
        justifiedBy = @("dashboard-model-v12.json: TechnicalDebt.CriticalFindings = $($technicalDebt.CriticalFindings)", "render-dashboard.ps1 threshold: CriticalFindings > 20 fails the Technical Debt gate")
    }
}


# -----------------------------------------------------------------------------
# Rule 2: Security / Quality simulated impact on Production Readiness
# -----------------------------------------------------------------------------

foreach($scenario in $releaseSimulation.scenarios)
{
    if($null -ne $scenario.engineeringScoreOverall -and [double]$scenario.productionReadiness.delta -gt 0)
    {
        $recommendations += [ordered]@{
            title = "$($scenario.scenario) raises Production Readiness"
            text = "$($scenario.scenario) moves Production Readiness from $($scenario.productionReadiness.baseline)% to $($scenario.productionReadiness.simulated)% (+$($scenario.productionReadiness.delta) pts), changing status from '$($scenario.productionStatus.baseline)' to '$($scenario.productionStatus.simulated)'."
            justifiedBy = @("release-simulation.json scenario '$($scenario.scenario)'", "Formula: $($releaseSimulation.method.productionReadiness)")
        }
    }
}

foreach($scenario in $releaseSimulation.scenarios)
{
    if($null -ne $scenario.erpCompletion -and [double]$scenario.erpCompletion.delta -gt 0)
    {
        $recommendations += [ordered]@{
            title = "$($scenario.scenario) advances ERP Completion"
            text = "Completing $($scenario.scope) ($($scenario.remainingTasksCompleted) remaining weighted tasks) raises ERP Completion (PROGRESS.html) from $($scenario.erpCompletion.baseline)% to $($scenario.erpCompletion.simulated)% (+$($scenario.erpCompletion.delta) pts)."
            justifiedBy = @("release-simulation.json scenario '$($scenario.scenario)'", "architecture-progress.json scope: $($scenario.scope)")
        }
    }
}


# -----------------------------------------------------------------------------
# Rule 3: Critical path -- module with highest transitive unblock count
# -----------------------------------------------------------------------------

$topPath = $criticalPathData.criticalPath | Select-Object -First 1

if($null -ne $topPath -and [int]$topPath.transitiveUnblocks -gt 0)
{
    $recommendations += [ordered]@{
        title = "Prioritize '$($topPath.module)' in the dependency graph"
        text = "'$($topPath.module)' has the highest transitive unblock count in the real dependency graph: completing it removes it as a dependency blocker for $($topPath.transitiveUnblocks) module(s), touching $($topPath.unlockedProcesses) distinct verified process(es)."
        justifiedBy = @("critical-path.json: module='$($topPath.module)', transitiveUnblocks=$($topPath.transitiveUnblocks), unlockedProcesses=$($topPath.unlockedProcesses)", "dependencies.json real dependency graph (edges evidenced by 'using ERP.*' references)")
    }
}


# -----------------------------------------------------------------------------
# Rule 4: Modules that unblock verified processes directly
# -----------------------------------------------------------------------------

$processUnblockers = @($criticalPathData.criticalPath | Where-Object { [int]$_.unlockedProcesses -gt 0 } | Select-Object -First 3)

foreach($entry in $processUnblockers)
{
    if($entry.module -eq $topPath.module) { continue }
    $recommendations += [ordered]@{
        title = "'$($entry.module)' unblocks $($entry.unlockedProcesses) process(es)"
        text = "Completing '$($entry.module)' (current score $($entry.currentScore)%) unblocks $($entry.directUnblocks) direct dependent module(s) and $($entry.transitiveUnblocks) transitive dependent module(s), covering $($entry.unlockedProcesses) verified business process(es)."
        justifiedBy = @("critical-path.json: module='$($entry.module)', directUnblocks=$($entry.directUnblocks), transitiveUnblocks=$($entry.transitiveUnblocks), unlockedProcesses=$($entry.unlockedProcesses)")
    }
}


# -----------------------------------------------------------------------------
# Rule 5: Circular dependencies detected
# -----------------------------------------------------------------------------

if(@($dependencyGraph.cycles).Count -gt 0)
{
    $recommendations += [ordered]@{
        title = "Resolve $($dependencyGraph.cycles.Count) circular dependency chain(s)"
        text = "The real dependency graph contains $($dependencyGraph.cycles.Count) circular reference chain(s) (e.g. $($dependencyGraph.cycles[0])), which increases coupling and blocks a clean layering between the affected modules."
        justifiedBy = @("dependencies.json: cycles = $($dependencyGraph.cycles -join ' | ')")
    }
}


# -----------------------------------------------------------------------------
# Rule 6: Single-author modules (Bus Factor = 1) that are also highly coupled
# -----------------------------------------------------------------------------

$riskyBusFactorModules = @($dependencyGraph.nodes | Where-Object { [int]$_.busFactor -le 1 -and [int]$_.coupling -ge 10 } | Sort-Object -Property {$_.coupling} -Descending | Select-Object -First 3)

foreach($node in $riskyBusFactorModules)
{
    $recommendations += [ordered]@{
        title = "'$($node.id)' is a single-author bottleneck with high coupling"
        text = "'$($node.id)' has Bus Factor = $($node.busFactor) (single known contributor via git history) and coupling = $($node.coupling) (fanIn=$($node.fanIn), fanOut=$($node.fanOut)). Knowledge concentration on a highly-coupled module is a real continuity risk."
        justifiedBy = @("dependencies.json: node id='$($node.id)', busFactor=$($node.busFactor), coupling=$($node.coupling)")
    }
}


# -----------------------------------------------------------------------------
# Rule 7: Model integrity gaps
# -----------------------------------------------------------------------------

if([int]$modelHealth.unmappedItems -gt 0)
{
    $recommendations += [ordered]@{
        title = "Assign a domain to $($modelHealth.unmappedItems) unmapped catalog item(s)"
        text = "model-health.json lists $($modelHealth.unmappedItems) unmapped item(s) (modules with domainId='unmapped' or zero features mapped). Resolving them raises Model Integrity confidence for downstream analysis (critical path, impact analysis)."
        justifiedBy = @("model-health.json: unmappedItems=$($modelHealth.unmappedItems)")
    }
}


# -----------------------------------------------------------------------------
# Rule 8: Existing critical gaps from completion-intelligence.json (top 2, avoid duplication)
# -----------------------------------------------------------------------------

$topGaps = @($completionIntelligence.criticalGaps | Select-Object -First 2)
foreach($gap in $topGaps)
{
    if($gap -eq "No se detectaron gaps criticos con los umbrales actuales") { continue }
    $recommendations += [ordered]@{
        title = "Critical gap already tracked: $gap"
        text = "This gap is already computed by analyze-completion.ps1 and remains open: $gap"
        justifiedBy = @("completion-intelligence.json: criticalGaps")
    }
}

Write-Host "Recommendations generated: $($recommendations.Count)"


# =============================================================================
# Output
# =============================================================================

$result = [ordered]@{
    generated = (Get-Date -Format "yyyy-MM-dd HH:mm:ss")
    source = "dashboard-model-v12.json, model-health.json, completion-intelligence.json, dependencies.json, critical-path.json, release-simulation.json"
    rule = "Deterministic rule engine -- every recommendation cites the exact JSON field(s) that triggered it in 'justifiedBy'. No generic text, no invented relationships. A rule that finds no matching condition produces zero recommendations for that rule."
    recommendations = @($recommendations)
}

$result | ConvertTo-Json -Depth 10 | Out-File $Output -Encoding utf8

Write-Host ""
Write-Host "Architecture recommendations generated successfully." -ForegroundColor Green
Write-Host $Output
