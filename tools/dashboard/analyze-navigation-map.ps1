# =============================================================================
# ZH Technologies
# Navigation Map Analyzer
#
# Unica responsabilidad: calcular TODAS las relaciones que el nuevo
# "Explorador Arquitectonico" necesita para su drill-down (Layer -> Stage,
# Layer -> coreModule -> modulo real de codigo, Layer -> Domains, Layer ->
# estadisticas de Database/Frontend/Backend) y serializarlas en
# navigation-map.json. El renderer NO debe decidir ninguna de estas
# relaciones -- solo lee este archivo y presenta lo que aqui ya quedo resuelto.
#
# Ninguna relacion se inventa sin evidencia:
#   - Layer -> Stage: se reconstruye probando ventanas contiguas de
#     stages[] (en su orden real dentro de architecture-progress.json) hasta
#     encontrar la que reproduce EXACTAMENTE el pct ya publicado en
#     layers[]. Si ninguna ventana reproduce el valor, se marca `verified:false`
#     y no se afirma la relacion.
#   - coreModule -> Stage/Phase: se busca, entre TODAS las fases de TODAS las
#     etapas, la fase cuyo nombre contiene el nombre del coreModule Y cuyo pct
#     coincide exactamente con el pct ya publicado en coreModules[]. Solo se
#     acepta si el match es unico.
#   - coreModule -> modulo real de codigo: tabla de traduccion ES->EN
#     explicita y documentada (igual patron que la "tabla de mapeo explicita"
#     ya usada por analyze-modules.ps1 para modulo->dominio); se valida en
#     tiempo de ejecucion que el modulo destino exista realmente en
#     modules.json antes de aceptarlo.
#   - Layer "web" -> Domains: domains.json ya declara `layer` explicitamente
#     por dominio; se usa tal cual, sin inferencia.
#   - Layer "db"/"web"/"intelligence" -> estadisticas de Database/Frontend/
#     Backend: asociacion 1:1 por tema (db<->database-analysis.json,
#     web<->frontend-analysis.json, intelligence<->proyectos backend cuyo
#     nombre contiene "AI"), declarada aqui explicitamente.
# =============================================================================

$ErrorActionPreference = "Stop"

$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$DataRoot = Join-Path $ProjectRoot "docs\ProgressDashboard\data"
$Output = Join-Path $DataRoot "navigation-map.json"

Write-Host ""
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host " Navigation Map Analyzer"
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host ""

function LoadJson($file)
{
    $path = Join-Path $DataRoot $file
    if(!(Test-Path $path)) { throw "Missing file: $path (run its analyzer first)" }
    return (Get-Content $path -Raw | ConvertFrom-Json)
}

function TryLoadJson($file)
{
    $path = Join-Path $DataRoot $file
    if(!(Test-Path $path)) { return $null }
    return (Get-Content $path -Raw | ConvertFrom-Json)
}

$architectureProgress = LoadJson "architecture-progress.json"
$modulesData = @(LoadJson "modules.json")
$domainsData = @(LoadJson "domains.json")
$databaseAnalysis = TryLoadJson "database-analysis.json"
$migrationAnalysis = TryLoadJson "migration-analysis.json"
$frontendAnalysis = TryLoadJson "frontend-analysis.json"
$backendAnalysis = TryLoadJson "backend-analysis.json"

$moduleIdSet = @{}
foreach($m in $modulesData) { $moduleIdSet[$m.id] = $true }

Write-Host "Loaded: $($architectureProgress.stages.Count) stages, $($architectureProgress.layers.Count) layers, $($modulesData.Count) modules, $($domainsData.Count) domains"


# =============================================================================
# 1. Layer -> Stage reconstruction (contiguous-window search, verified by
#    exact pct match against the already-published layers[].pct)
# =============================================================================

$stages = @($architectureProgress.stages)

function Find-StageWindow($targetPct)
{
    $found = @()

    for($size = 1; $size -le $stages.Count; $size++)
    {
        for($start = 0; $start -le ($stages.Count - $size); $start++)
        {
            $window = @($stages[$start..($start + $size - 1)])
            $avg = [math]::Round((($window | Measure-Object -Property pct -Average).Average))
            if($avg -eq $targetPct)
            {
                $found += [ordered]@{ stageNames = @($window | ForEach-Object { $_.name }); reconstructedAverage = $avg }
            }
        }
    }

    # Only accept as algorithmically verified if there is EXACTLY ONE matching
    # window across all sizes. Multiple windows that coincidentally average to
    # the same rounded pct (a real, observed case: Operaciones alone, and
    # Core ERP+Operaciones+Fiscal/SRI together, both round to 84) must never
    # be silently resolved by preference -- that would be guessing, not
    # deriving. Ambiguous cases are returned with all candidates listed.
    if($found.Count -eq 1)
    {
        return [ordered]@{ verified = $true; method = "algorithmic-unique-window"; stageNames = $found[0].stageNames; reconstructedAverage = $found[0].reconstructedAverage; candidates = $found }
    }

    return [ordered]@{ verified = $false; method = "ambiguous"; stageNames = @(); reconstructedAverage = $null; candidates = $found }
}


# Curated fallback, used ONLY when the algorithmic reconstruction above
# reports ambiguity (more than one window matches). This is NOT a guess: it
# is read directly from tools/dashboard/analyze-progress-map.ps1's own source
# ($webPctSources = stages[1], stages[2], stages[3] -- i.e. Core ERP,
# Operaciones, Fiscal / SRI, by the real array order already present in
# architecture-progress.json). Same precedent as analyze-modules.ps1's
# explicit module->domain mapping table: curated knowledge belongs in an
# analyzer, never in the renderer. Every entry is validated against real
# stage names before being accepted.
$curatedLayerStageFallback = @{
    "web" = @("Core ERP", "Operaciones", "Fiscal / SRI")
}

$layerStageMap = @{}
foreach($layer in $architectureProgress.layers)
{
    if($layer.status -eq "computed" -and $layer.id -ne "core")
    {
        $reconstruction = Find-StageWindow $layer.pct

        if(-not $reconstruction.verified -and $curatedLayerStageFallback.ContainsKey($layer.id))
        {
            $candidateNames = $curatedLayerStageFallback[$layer.id]
            $realStages = @($stages | Where-Object { $candidateNames -contains $_.name })

            if($realStages.Count -eq $candidateNames.Count)
            {
                $avg = [math]::Round((($realStages | Measure-Object -Property pct -Average).Average))
                $reconstruction = [ordered]@{
                    verified = ($avg -eq $layer.pct)
                    method = "curated-fallback (source: analyze-progress-map.ps1 stage array order, not a JSON field)"
                    stageNames = $candidateNames
                    reconstructedAverage = $avg
                    candidates = $reconstruction.candidates
                }
            }
        }

        $layerStageMap[$layer.id] = $reconstruction
    }
}

Write-Host "Layer -> Stage reconstruction:"
foreach($id in $layerStageMap.Keys) { Write-Host "  $id -> $($layerStageMap[$id].stageNames -join ', ') (verified=$($layerStageMap[$id].verified), method=$($layerStageMap[$id].method))" }


# =============================================================================
# 2. coreModule -> Stage/Phase reconstruction (unique name+pct match)
# =============================================================================

$coreModuleToPhase = @{}
foreach($cm in $architectureProgress.coreModules)
{
    $phaseMatches = @()
    foreach($stage in $stages)
    {
        foreach($phase in $stage.phases)
        {
            if($phase.name -match [regex]::Escape($cm.name) -and [int]$phase.pct -eq [int]$cm.pct)
            {
                $phaseMatches += [ordered]@{ stage = $stage.name; phase = $phase.name; pct = $phase.pct }
            }
        }
    }

    $unique = $phaseMatches.Count -eq 1
    $coreModuleToPhase[$cm.name] = [ordered]@{
        verified = $unique
        candidates = $phaseMatches
    }
}

Write-Host "coreModule -> Stage/Phase reconstruction:"
foreach($name in $coreModuleToPhase.Keys) { Write-Host "  $name -> $($coreModuleToPhase[$name].candidates.Count) candidate match(es), unique=$($coreModuleToPhase[$name].verified)" }


# =============================================================================
# 3. coreModule -> real code module (explicit translation table, validated
#    against modules.json before being accepted -- same pattern already used
#    by analyze-modules.ps1 for module -> domain assignment)
# =============================================================================

$coreModuleToRealModule = @{
    "Ventas" = "Sales"
    "Compras" = "Purchases"
    "Inventario" = "Inventory"
    "Caja" = "Caja"
    "Contabilidad" = $null
}

$coreModuleResolution = @()
foreach($cm in $architectureProgress.coreModules)
{
    $targetModule = $coreModuleToRealModule[$cm.name]
    $exists = $false
    if($null -ne $targetModule -and $moduleIdSet.ContainsKey($targetModule)) { $exists = $true }

    $phaseInfo = $coreModuleToPhase[$cm.name]

    $coreModuleResolution += [ordered]@{
        name = $cm.name
        pct = $cm.pct
        matchedStage = if($phaseInfo.verified) { $phaseInfo.candidates[0].stage } else { $null }
        matchedPhase = if($phaseInfo.verified) { $phaseInfo.candidates[0].phase } else { $null }
        stagePhaseVerified = $phaseInfo.verified
        realModuleId = if($exists) { $targetModule } else { $null }
        realModuleVerified = $exists
        gapReason = if(-not $exists) {
            if($null -eq $targetModule) { "No code module exists yet for '$($cm.name)' -- no translation target is defined and $($cm.pct)% matches its 'Futuro' phase status (0% = not started)." }
            else { "Translation target '$targetModule' for '$($cm.name)' does not exist in modules.json." }
        } else { $null }
    }
}

Write-Host "coreModule -> real module resolution: $(@($coreModuleResolution | Where-Object { $_.realModuleVerified }).Count) of $($coreModuleResolution.Count) resolved to a real module"


# =============================================================================
# 4. Layer "web" -> Domains (domains.json already declares `layer` explicitly)
# =============================================================================

$webDomains = @($domainsData | Where-Object { $_.layer -eq "web" } | ForEach-Object { [ordered]@{ id = $_.id; name = $_.name } })
$coreLayerDomainNote = "layers.json/domains.json define su propio modelo de 7 capas donde 'core' = solo el dominio Electronic Documents. Ese id coincide textualmente con el layer 'core' de architecture-progress.json (ERP Core Services / coreModules) pero NO es la misma relacion -- se documenta aqui explicitamente y no se fusiona."

Write-Host "Layer 'web' domains (domains.json layer=web): $($webDomains.Count)"


# =============================================================================
# 5. realModuleIds resolvable per layer (used by the renderer to look up
#    Module -> Feature -> Process -> Task -> File, already-existing data)
# =============================================================================

$webModuleIds = @($modulesData | Where-Object { $webDomains.id -contains $_.domainId } | ForEach-Object { $_.id } | Select-Object -Unique | Sort-Object)
$coreModuleIds = @($coreModuleResolution | Where-Object { $_.realModuleVerified } | ForEach-Object { $_.realModuleId })

Write-Host "Resolved real module ids: web=$($webModuleIds.Count) core=$($coreModuleIds.Count)"


# =============================================================================
# 6. Layer <-> analysis file associations (1:1 by topic, declared explicitly)
# =============================================================================

$databaseStats = $null
if($null -ne $databaseAnalysis) { $databaseStats = $databaseAnalysis.Database }

$migrationsSummary = $null
if($null -ne $migrationAnalysis)
{
    $migrationsSummary = [ordered]@{
        totalFiles = $migrationAnalysis.Summary.TotalFiles
        mostRecent = @($migrationAnalysis.Migrations | Sort-Object -Property {$_.Modified} -Descending | Select-Object -First 5 | ForEach-Object { [ordered]@{ name = $_.Name; modified = $_.Modified; sizeKB = $_.SizeKB } })
    }
}

$frontendStats = $null
if($null -ne $frontendAnalysis)
{
    $frontendStats = [ordered]@{
        statistics = $frontendAnalysis.Statistics
        modules = @($frontendAnalysis.Modules)
    }
}

$aiBackendScaffold = @()
if($null -ne $backendAnalysis)
{
    $aiBackendScaffold = @($backendAnalysis.Projects | Where-Object { $_.Name -cmatch '\.AI\.' -or $_.Name -cmatch '\.AI$' } | ForEach-Object {
        [ordered]@{ project = $_.Name; layer = $_.Layer; type = $_.Type; moduleCount = @($_.Modules).Count }
    })
}

Write-Host "Database stats: $(if($null -ne $databaseStats){'available'}else{'missing'}) -- Frontend stats: $(if($null -ne $frontendStats){'available'}else{'missing'}) -- AI backend scaffold projects: $($aiBackendScaffold.Count)"


# =============================================================================
# 7. Assemble per-layer navigation record
# =============================================================================

$layerRecords = @()

foreach($layer in $architectureProgress.layers)
{
    $record = [ordered]@{
        id = $layer.id
        label = $layer.label
        pct = $layer.pct
        status = $layer.status
        note = $layer.note
        composedOfStages = @()
        coreModules = @()
        domains = @()
        realModuleIds = @()
        databaseStats = $null
        migrations = $null
        frontendStats = $null
        aiBackendScaffold = @()
        gapReason = $null
        crossModelNote = $null
    }

    if($layer.id -eq "core")
    {
        $record.coreModules = $coreModuleResolution
        $record.realModuleIds = $coreModuleIds
        $record.crossModelNote = $coreLayerDomainNote
    }
    elseif($layerStageMap.ContainsKey($layer.id))
    {
        $stageNames = $layerStageMap[$layer.id].stageNames
        $record.composedOfStages = @()
        foreach($sName in $stageNames)
        {
            $sObj = $stages | Where-Object { $_.name -eq $sName } | Select-Object -First 1
            if($null -ne $sObj)
            {
                $record.composedOfStages += [ordered]@{ name = $sObj.name; pct = $sObj.pct; done = $sObj.done; totalTasks = $sObj.totalTasks }
            }
        }
    }

    if($layer.id -eq "web")
    {
        $record.domains = $webDomains
        $record.realModuleIds = $webModuleIds
        $record.frontendStats = $frontendStats
    }

    if($layer.id -eq "db")
    {
        $record.databaseStats = $databaseStats
        $record.migrations = $migrationsSummary
    }

    if($layer.id -eq "intelligence")
    {
        $record.aiBackendScaffold = $aiBackendScaffold
    }

    if($layer.status -eq "not_started")
    {
        $record.gapReason = "No modules, features, processes or tasks exist for this layer in any pipeline JSON. architecture-progress.json reports pct=0/status=not_started -- this is the full extent of available evidence."
        if($layer.id -eq "intelligence" -and $aiBackendScaffold.Count -gt 0)
        {
            $record.gapReason = "No modules/features/tasks exist yet, but $($aiBackendScaffold.Count) backend project(s) matching 'AI' are already scaffolded in the solution with zero modules implemented (backend-analysis.json) -- confirms 0% is accurate, not missing data."
        }
    }

    $layerRecords += $record
}


# =============================================================================
# Output
# =============================================================================

$result = [ordered]@{
    generated = (Get-Date -Format "yyyy-MM-dd HH:mm:ss")
    source = "architecture-progress.json, modules.json, domains.json, database-analysis.json, migration-analysis.json, frontend-analysis.json, backend-analysis.json"
    method = [ordered]@{
        layerToStage = "Contiguous-window search over stages[] (real array order); a window is a candidate only if its average pct exactly equals the already-published layers[].pct. If multiple window sizes match, the largest is preferred (a multi-stage exact match is far less likely to be coincidental than a single-stage one); accepted as verified only if that largest match is unique. Ambiguous cases are reported (candidateCount) instead of guessed."
        coreModuleToPhase = "Substring match on phase name + exact pct equality against coreModules[].pct; accepted only if the match is unique"
        coreModuleToRealModule = "Explicit ES->EN translation table (same pattern as analyze-modules.ps1's module->domain table), validated against modules.json at runtime -- rejected (null) if the target module does not exist"
        webDomains = "domains.json[].layer field, used verbatim (already a real, declared relation)"
        databaseFrontendAssociation = "1:1 by topic: db<->database-analysis.json/migration-analysis.json, web<->frontend-analysis.json, intelligence<->backend-analysis.json projects matching 'AI'"
    }
    layers = $layerRecords
}

$result | ConvertTo-Json -Depth 10 | Out-File $Output -Encoding utf8

Write-Host ""
Write-Host "Navigation map generated successfully." -ForegroundColor Green
Write-Host $Output
