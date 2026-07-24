# =============================================================================
# ZH Technologies
# Critical Path & Impact Analysis Analyzer
#
# Responde: "Si completo el modulo X, que se desbloquea?" y "Que depende de
# este modulo, y que riesgo implica tocarlo?"
#
# Lee unicamente datos ya calculados por otros analizadores:
#   modules.json          -> score real por modulo (proxy de completitud)
#   dependencies.json      -> grafo real de dependencias (analyze-module-graph.ps1)
#   impact.json             -> features/processes/risk reales por modulo
#   tasks.json              -> tareas reales con evidencia de archivo
#
# No se inventa ninguna relacion: "desbloquea" se define estrictamente como
# "es dependiente directo o transitivo en el grafo real de dependencias".
# No existe informacion de velocidad/esfuerzo en el pipeline, por lo que este
# analizador NUNCA estima tiempos ni fechas -- solo cuenta relaciones reales.
# =============================================================================

$ErrorActionPreference = "Stop"

$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$DataRoot = Join-Path $ProjectRoot "docs\ProgressDashboard\data"
$Output = Join-Path $DataRoot "critical-path.json"

Write-Host ""
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host " Critical Path & Impact Analysis Analyzer"
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host ""

function LoadJson($file)
{
    $path = Join-Path $DataRoot $file
    if(!(Test-Path $path)) { throw "Missing file: $path (run its analyzer first)" }
    return (Get-Content $path -Raw | ConvertFrom-Json)
}

$modulesData = @(LoadJson "modules.json")
$dependencyGraph = LoadJson "dependencies.json"
$impactData = LoadJson "impact.json"
$tasksData = @(LoadJson "tasks.json")

$nodesByModule = @{}
foreach($n in $dependencyGraph.nodes) { $nodesByModule[$n.id] = $n }

$impactByModule = @{}
foreach($domain in $impactData.domains)
{
    foreach($m in $domain.modules)
    {
        $impactByModule[$m.name] = [ordered]@{ domain = $domain.domain; features = $m.features; processes = $m.processes; risk = $m.risk }
    }
}

Write-Host "Loaded: $($modulesData.Count) modules, $($dependencyGraph.edges.Count) edges, $($impactByModule.Keys.Count) impact profiles"


# =============================================================================
# Reverse-edge BFS: direct + transitive dependents of a module
# =============================================================================

function Get-TransitiveDependents($moduleId)
{
    $result = New-Object System.Collections.Generic.List[string]
    $seen = @{}
    $queue = New-Object System.Collections.Generic.Queue[string]

    $direct = @()
    if($nodesByModule.ContainsKey($moduleId)) { $direct = @($nodesByModule[$moduleId].dependedOnBy) }

    foreach($d in $direct) { $queue.Enqueue($d) }

    while($queue.Count -gt 0)
    {
        $current = $queue.Dequeue()
        if($seen.ContainsKey($current)) { continue }
        $seen[$current] = $true
        $result.Add($current)

        if($nodesByModule.ContainsKey($current))
        {
            foreach($next in @($nodesByModule[$current].dependedOnBy))
            {
                if(-not $seen.ContainsKey($next)) { $queue.Enqueue($next) }
            }
        }
    }

    return @($result)
}


# =============================================================================
# Impact profile per module (Fase 3 - Impact Analysis)
# =============================================================================

$totalFeaturePoints = [double]$impactData.coverage.totalFeaturePoints

$moduleImpact = @()

foreach($m in $modulesData)
{
    $id = $m.id
    $directDependents = @()
    $fanIn = 0
    $fanOut = 0
    if($nodesByModule.ContainsKey($id))
    {
        $directDependents = @($nodesByModule[$id].dependedOnBy)
        $fanIn = $nodesByModule[$id].fanIn
        $fanOut = $nodesByModule[$id].fanOut
    }

    $transitiveDependents = Get-TransitiveDependents $id

    $ownFeatures = 0
    if($impactByModule.ContainsKey($id)) { $ownFeatures = [double]$impactByModule[$id].features }

    $dependentFeatures = 0
    $dependentProcessNames = New-Object System.Collections.Generic.List[string]
    foreach($dep in $transitiveDependents)
    {
        if($impactByModule.ContainsKey($dep))
        {
            $dependentFeatures += [double]$impactByModule[$dep].features
            foreach($p in @($impactByModule[$dep].processes)) { $dependentProcessNames.Add($p.name) }
        }
    }

    $impactedFeaturePoints = $ownFeatures + $dependentFeatures
    $percentOfErp = 0
    if($totalFeaturePoints -gt 0) { $percentOfErp = [math]::Round(($impactedFeaturePoints / $totalFeaturePoints) * 100, 2) }

    $riskOfModifying = "UNKNOWN"
    if($impactByModule.ContainsKey($id)) { $riskOfModifying = $impactByModule[$id].risk }

    $relatedTasks = @($tasksData | Where-Object { @($_.evidence) -match [regex]::Escape("/$id/") } | ForEach-Object { $_.task })

    $moduleImpact += [ordered]@{
        module = $id
        score = $m.score
        fanIn = $fanIn
        fanOut = $fanOut
        directDependents = @($directDependents | Select-Object -Unique | Sort-Object)
        transitiveDependents = @($transitiveDependents | Select-Object -Unique | Sort-Object)
        transitiveDependentCount = @($transitiveDependents | Select-Object -Unique).Count
        dependentProcesses = @($dependentProcessNames | Select-Object -Unique | Sort-Object)
        ownFeatures = $ownFeatures
        impactedFeaturePoints = $impactedFeaturePoints
        percentOfErp = $percentOfErp
        riskOfModifying = $riskOfModifying
        relatedTasks = $relatedTasks
    }
}

Write-Host "Impact profile computed for $($moduleImpact.Count) modules"


# =============================================================================
# Critical Path (Fase 2): incomplete modules ranked by unblock potential
# =============================================================================

$incompleteModules = @($moduleImpact | Where-Object { [double]$_.score -lt 100 })
$rankedPath = @($incompleteModules | Sort-Object -Property @{Expression={$_.transitiveDependentCount};Descending=$true}, @{Expression={$_.score};Descending=$false})

$criticalPath = @()
$order = 1
foreach($entry in $rankedPath)
{
    $criticalPath += [ordered]@{
        order = $order
        module = $entry.module
        currentScore = $entry.score
        directUnblocks = $entry.directDependents.Count
        transitiveUnblocks = $entry.transitiveDependentCount
        unlockedProcesses = $entry.dependentProcesses.Count
        rationale = "Completing '$($entry.module)' (score $($entry.score)%) removes it as a dependency blocker for $($entry.transitiveDependentCount) module(s) reachable via the real dependency graph (dependencies.json), touching $($entry.dependentProcesses.Count) distinct process(es) (impact.json)."
    }
    $order++
}

Write-Host "Critical path computed: $($criticalPath.Count) incomplete modules ranked"


# =============================================================================
# Output
# =============================================================================

$result = [ordered]@{
    generated = (Get-Date -Format "yyyy-MM-dd HH:mm:ss")
    source = "modules.json (completion proxy = module score), dependencies.json (real dependency graph), impact.json (features/processes/risk), tasks.json (evidence-matched tasks)"
    disclaimer = "No effort/velocity data exists in this pipeline, so no completion dates or durations are estimated here -- only real relationship counts (dependents, features, processes)."
    method = [ordered]@{
        completionProxy = "A module is considered 'incomplete' when modules.json score < 100. This score is the existing composite (architecture/tests/documentation/backend/frontend) quality score -- not a literal feature-completion percentage."
        unblocks = "direct = modules.dependedOnBy in dependencies.json; transitive = BFS over reverse edges of the real dependency graph"
        percentOfErp = "(own features + transitive dependents' features) / impact.json coverage.totalFeaturePoints * 100"
        rankingRule = "Incomplete modules sorted by transitiveUnblocks (desc), then by current score (asc) as tiebreak"
    }
    moduleImpact = $moduleImpact
    criticalPath = $criticalPath
}

$result | ConvertTo-Json -Depth 10 | Out-File $Output -Encoding utf8

Write-Host ""
Write-Host "Critical path & impact analysis generated successfully." -ForegroundColor Green
Write-Host $Output
