# =============================================================================
# ZH Technologies
# Explorer Index Analyzer
#
# Backend consolidado del "Engineering Control Center": Module Health,
# Impact Explorer, Risk-of-change, indice inverso Archivo->Feature/Process/
# Task->Module->Domain, Vista Ejecutiva (top 10s) y el indice de busqueda
# global. TODA la logica de cruce vive aqui -- render-dashboard.ps1 solo lee
# explorer-index.json y formatea HTML.
#
# Fuentes (todas ya validadas por sus propios analizadores; ninguna se
# recalcula, solo se cruzan por su clave real ya existente -- module id,
# domain id, file path):
#   modules.json, features.json, processes.json, tasks.json, domains.json
#   dependencies.json, critical-path.json, impact.json
#   dashboard-model-v12.json (TechnicalDebt.LargeFiles, Security.*Files)
#   completion-intelligence.json
#   git log (busFactor / lastEvidenceDate -- misma tecnica que
#   analyze-module-graph.ps1)
#
# Limitaciones declaradas explicitamente (ver 'method.knownGaps' en la
# salida): TODO/FIXME/HACK/NotImplemented y test coverage NO existen por
# modulo en ningun analizador -- solo como conteo global. No se inventa un
# desglose por modulo para estos cuatro campos.
# =============================================================================

$ErrorActionPreference = "Stop"

$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$DataRoot = Join-Path $ProjectRoot "docs\ProgressDashboard\data"
$Output = Join-Path $DataRoot "explorer-index.json"

Write-Host ""
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host " Explorer Index Analyzer"
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host ""

function LoadJson($file)
{
    $path = Join-Path $DataRoot $file
    if(!(Test-Path $path)) { throw "Missing file: $path (run its analyzer first)" }
    return (Get-Content $path -Raw | ConvertFrom-Json)
}

$modulesData = @(LoadJson "modules.json")
$featuresData = @(LoadJson "features.json")
$processesData = @(LoadJson "processes.json")
$tasksData = @(LoadJson "tasks.json")
$domainsData = @(LoadJson "domains.json")
$dependencyGraph = LoadJson "dependencies.json"
$criticalPathData = LoadJson "critical-path.json"
$impactData = LoadJson "impact.json"
$model = LoadJson "dashboard-model-v12.json"
$completionIntelligence = LoadJson "completion-intelligence.json"

Write-Host "Loaded: $($modulesData.Count) modules, $($featuresData.Count) feature entries, $($processesData.Count) processes, $($tasksData.Count) tasks, $($domainsData.Count) domains"


# =============================================================================
# 1. Indices (built once, reused everywhere below -- O(1) lookup instead of
#    repeated Where-Object scans; this is the "no recalcular, indexar una
#    sola vez" requirement applied inside the analyzer itself)
# =============================================================================

$domainById = @{}
foreach($d in $domainsData) { $domainById[$d.id] = $d }

$depNodeById = @{}
foreach($n in $dependencyGraph.nodes) { $depNodeById[$n.id] = $n }

$moduleImpactById = @{}
foreach($mi in $criticalPathData.moduleImpact) { $moduleImpactById[$mi.module] = $mi }

$impactByModuleName = @{}
foreach($domain in $impactData.domains)
{
    foreach($m in $domain.modules) { $impactByModuleName[$m.name] = [ordered]@{ domain = $domain.domain; features = $m.features; processes = $m.processes; risks = $m.risks; risk = $m.risk } }
}

$featuresByModule = @{}
foreach($fe in $featuresData) { $featuresByModule[$fe.module] = $fe }

$processStepsByModule = @{}
foreach($process in $processesData)
{
    foreach($step in $process.steps)
    {
        if(-not $processStepsByModule.ContainsKey($step.module)) { $processStepsByModule[$step.module] = New-Object System.Collections.Generic.List[object] }
        $processStepsByModule[$step.module].Add([ordered]@{ process = $process.process; step = $step.name; status = $step.status; evidence = $step.evidence })
    }
}

Write-Host "Indices built: domains, dependency nodes, module impact, features/processes by module"


# =============================================================================
# 2. Per-module Technical Debt / Security attribution (path substring match --
#    same evidence-matching pattern already used by analyze-critical-path.ps1
#    for tasks; large files and security findings are real absolute paths, a
#    module only "claims" a file if its path contains "\Modules\<id>\" or
#    "/<id>/")
# =============================================================================

function Get-ModuleMatchedFiles($fileList, $moduleId)
{
    if($null -eq $fileList) { return @() }
    $pattern1 = [regex]::Escape("\Modules\$moduleId\")
    $pattern2 = [regex]::Escape("/$moduleId/")
    return ,@($fileList | Where-Object { $_ -match $pattern1 -or $_ -match $pattern2 })
}

# dashboard-model-v12.json (TechnicalDebt.LargeFiles / Security.*Files) stores
# absolute Windows paths ("C:\...\Modules\Sales\..."), while features.json /
# processes.json / tasks.json (and therefore reverseFileIndex) store relative
# forward-slash paths ("backend/src/.../Modules/Sales/..."). This is a pure
# path-FORMAT normalization (strip project root, backslash->slash) -- not a
# new relation -- done so a file that happens to be both a large-file/secret
# AND real feature/process/task evidence resolves to the same reverse-index
# key. Files that are ONLY in the large-files/secrets list (never referenced
# as evidence anywhere) will still correctly report "no reverse references".
function Normalize-EvidencePath($absolutePath)
{
    $normalized = $absolutePath.Replace($ProjectRoot, "").TrimStart("\", "/")
    return $normalized.Replace("\", "/")
}

$largeFilesAll = @($model.TechnicalDebt.LargeFiles | ForEach-Object { Normalize-EvidencePath $_.File })
$largeFilesLookup = @{}
foreach($lf in $model.TechnicalDebt.LargeFiles) { $largeFilesLookup[(Normalize-EvidencePath $lf.File)] = $lf.Lines }

$secretFilesAll = @($model.Security.SecretFiles | ForEach-Object { Normalize-EvidencePath $_ })
$anonymousFilesAll = @($model.Security.AnonymousFiles | ForEach-Object { Normalize-EvidencePath $_ })

Write-Host "Global debt/security file lists loaded: $($largeFilesAll.Count) large files, $($secretFilesAll.Count) secret files, $($anonymousFilesAll.Count) anonymous-access files"


# =============================================================================
# 3. git log helpers (same technique already used by analyze-module-graph.ps1
#    for busFactor; here also used for lastEvidenceDate)
# =============================================================================

$moduleFoldersCache = @{}

function Get-ModuleFolders($moduleId)
{
    if($moduleFoldersCache.ContainsKey($moduleId)) { return $moduleFoldersCache[$moduleId] }

    $folders = @()
    foreach($project in @("backend\src\ERP.Application\Modules", "backend\src\ERP.Domain\Modules", "backend\src\ERP.Infrastructure"))
    {
        $candidate = Join-Path $ProjectRoot (Join-Path $project $moduleId)
        if(Test-Path $candidate) { $folders += $candidate }
    }

    $moduleFoldersCache[$moduleId] = $folders
    return $folders
}

function Get-LastEvidenceDate($moduleId)
{
    $mostRecent = $null
    foreach($folder in (Get-ModuleFolders $moduleId))
    {
        $relFolder = $folder.Replace($ProjectRoot, "").TrimStart("\", "/")
        $date = git -C $ProjectRoot log -1 --format="%cd" --date=short -- "$relFolder" 2>$null
        if($date -and ($null -eq $mostRecent -or $date -gt $mostRecent)) { $mostRecent = $date }
    }
    return $mostRecent
}


# =============================================================================
# 4. Reverse index: File -> Feature/Process/Task -> Module -> Domain
#    (Fase 5 -- navegacion bidireccional). Construido con un diccionario
#    (una sola pasada por fuente), nunca con busquedas anidadas repetidas.
# =============================================================================

$reverseFileIndex = @{}

function Add-ReverseEntry($file, $entryType, $moduleId, $label)
{
    if(-not $reverseFileIndex.ContainsKey($file)) { $reverseFileIndex[$file] = New-Object System.Collections.Generic.List[object] }
    $domainId = $null
    $domainName = $null
    $moduleRecord = $modulesData | Where-Object { $_.id -eq $moduleId } | Select-Object -First 1
    if($null -ne $moduleRecord) { $domainId = $moduleRecord.domainId }
    if($null -ne $domainId -and $domainById.ContainsKey($domainId)) { $domainName = $domainById[$domainId].name }

    $reverseFileIndex[$file].Add([ordered]@{ type = $entryType; module = $moduleId; label = $label; domainId = $domainId; domainName = $domainName })
}

foreach($fe in $featuresData)
{
    foreach($feature in $fe.features)
    {
        foreach($file in @($feature.evidence)) { Add-ReverseEntry $file "Feature" $fe.module $feature.name }
    }
}

foreach($process in $processesData)
{
    foreach($step in $process.steps)
    {
        if($null -ne $step.evidence)
        {
            foreach($file in @($step.evidence)) { Add-ReverseEntry $file "Process" $step.module "$($process.process) / $($step.name)" }
        }
    }
}

foreach($task in $tasksData)
{
    foreach($file in @($task.evidence))
    {
        # Tasks don't declare a module directly -- attribute by path substring
        # against every real module id (same evidence-matching rule used
        # elsewhere in this pipeline), first match wins, none forced.
        $matchedModule = $null
        foreach($m in $modulesData) { if($file -match [regex]::Escape("\$($m.id)\") -or $file -match [regex]::Escape("/$($m.id)/")) { $matchedModule = $m.id; break } }
        Add-ReverseEntry $file "Task" $matchedModule $task.task
    }
}

$reverseFileIndexOutput = @()
foreach($file in $reverseFileIndex.Keys)
{
    $refs = $reverseFileIndex[$file].ToArray()
    $reverseFileIndexOutput += [ordered]@{ file = $file; referencedBy = $refs }
}

Write-Host "Reverse file index built: $($reverseFileIndexOutput.Count) distinct files"


# =============================================================================
# 5. Change-risk formula (documented, deterministic, every input traceable)
# =============================================================================

function Get-RiskCategoryScore($riskLabel)
{
    switch($riskLabel) { "LOW" { return 10 }; "MEDIUM" { return 40 }; "HIGH" { return 70 }; "CRITICAL" { return 100 }; default { return 50 } }
}

function Get-ChangeRiskBand($riskScore)
{
    if($riskScore -lt 20) { return "Safe" }
    elseif($riskScore -lt 40) { return "Low Risk" }
    elseif($riskScore -lt 60) { return "Medium Risk" }
    elseif($riskScore -lt 80) { return "High Risk" }
    else { return "Critical" }
}

$changeRiskFormula = "riskScore = (100-score)*0.25 + min(coupling,20)/20*100*0.20 + min(largeFilesCount,10)/10*100*0.15 + RiskCategoryScore(impact.json risk: LOW=10/MEDIUM=40/HIGH=70/CRITICAL=100)*0.25 + min(transitiveDependents,15)/15*100*0.10 + (busFactor<=1 ? 100 : 0)*0.05 -- bands: <20 Safe, <40 Low Risk, <60 Medium Risk, <80 High Risk, >=80 Critical"


# =============================================================================
# 6. Assemble per-module profile
# =============================================================================

$moduleProfiles = @()

foreach($m in $modulesData)
{
    $depNode = $null
    if($depNodeById.ContainsKey($m.id)) { $depNode = $depNodeById[$m.id] }

    $impact = $null
    if($moduleImpactById.ContainsKey($m.id)) { $impact = $moduleImpactById[$m.id] }

    $legacyImpact = $null
    if($impactByModuleName.ContainsKey($m.id)) { $legacyImpact = $impactByModuleName[$m.id] }

    $featureEntry = $null
    if($featuresByModule.ContainsKey($m.id)) { $featureEntry = $featuresByModule[$m.id] }
    $featureNames = @()
    if($null -ne $featureEntry) { $featureNames = @($featureEntry.features | ForEach-Object { $_.name }) }

    $processEntries = @()
    if($processStepsByModule.ContainsKey($m.id)) { $processEntries = $processStepsByModule[$m.id].ToArray() }

    $moduleLargeFiles = Get-ModuleMatchedFiles $largeFilesAll $m.id
    $moduleLargeFilesDetailed = @($moduleLargeFiles | ForEach-Object { [ordered]@{ file = $_; lines = $largeFilesLookup[$_] } })
    $moduleSecrets = Get-ModuleMatchedFiles $secretFilesAll $m.id
    $moduleAnonymous = Get-ModuleMatchedFiles $anonymousFilesAll $m.id

    $busFactor = if($null -ne $depNode) { $depNode.busFactor } else { $null }
    $coupling = if($null -ne $depNode) { $depNode.coupling } else { 0 }
    $fanIn = if($null -ne $depNode) { $depNode.fanIn } else { 0 }
    $fanOut = if($null -ne $depNode) { $depNode.fanOut } else { 0 }
    $instability = if($null -ne $depNode) { $depNode.instability } else { 0 }
    $dependsOn = if($null -ne $depNode) { @($depNode.dependsOn) } else { @() }
    $dependedOnBy = if($null -ne $depNode) { @($depNode.dependedOnBy) } else { @() }

    $criticalDependencies = @($dependsOn | Where-Object { $dependencyGraph.criticalModules -contains $_ })
    $cyclesInvolved = @($dependencyGraph.cycles | Where-Object { $_ -match "(^|\s)$([regex]::Escape($m.id))(\s|$)" })

    $riskOfModifying = "UNKNOWN"
    $percentOfErp = 0
    $transitiveDependentCount = 0
    $dependentProcesses = @()
    $relatedTasks = @()
    if($null -ne $impact)
    {
        $riskOfModifying = $impact.riskOfModifying
        $percentOfErp = $impact.percentOfErp
        $transitiveDependentCount = $impact.transitiveDependentCount
        $dependentProcesses = @($impact.dependentProcesses)
        $relatedTasks = @($impact.relatedTasks)
    }

    # Dependent features: union of features belonging to every transitive
    # dependent module (already computed in critical-path.json), joined here
    # by module id -- no new relation, same key already used everywhere.
    $dependentFeatures = @()
    if($null -ne $impact)
    {
        foreach($depId in @($impact.transitiveDependents))
        {
            if($featuresByModule.ContainsKey($depId))
            {
                $dependentFeatures += @($featuresByModule[$depId].features | ForEach-Object { "$depId / $($_.name)" })
            }
        }
    }

    $scoreVal = [double]$m.score
    $largeFilesCount = $moduleLargeFilesDetailed.Count
    $impactRiskScore = Get-RiskCategoryScore $riskOfModifying

    $riskScore =
        ((100 - $scoreVal) * 0.25) +
        (([math]::Min($coupling, 20) / 20 * 100) * 0.20) +
        (([math]::Min($largeFilesCount, 10) / 10 * 100) * 0.15) +
        ($impactRiskScore * 0.25) +
        (([math]::Min($transitiveDependentCount, 15) / 15 * 100) * 0.10) +
        $(if($null -ne $busFactor -and $busFactor -le 1) { 5 } else { 0 })

    $riskScore = [math]::Round($riskScore, 2)
    $riskBand = Get-ChangeRiskBand $riskScore

    $moduleProfiles += [ordered]@{
        id = $m.id
        domainId = $m.domainId
        domainName = if($domainById.ContainsKey($m.domainId)) { $domainById[$m.domainId].name } else { $null }
        score = $m.score
        architecture = $m.architecture
        tests = $m.tests
        documentation = $m.documentation
        backend = $m.backend
        frontend = $m.frontend
        filesScanned = if($null -ne $depNode) { $depNode.filesScanned } else { $null }
        busFactor = $busFactor
        lastEvidenceDate = Get-LastEvidenceDate $m.id
        featuresCount = $featureNames.Count
        featureNames = $featureNames
        processesCount = $processEntries.Count
        processes = $processEntries
        debt = [ordered]@{
            largeFiles = $moduleLargeFilesDetailed
            largeFilesCount = $largeFilesCount
            globalTodo = $model.TechnicalDebt.TODO
            globalFixme = $model.TechnicalDebt.FIXME
            globalHack = $model.TechnicalDebt.HACK
            globalNotImplemented = $model.TechnicalDebt.NotImplemented
            note = "TODO/FIXME/HACK/NotImplemented only exist as GLOBAL counts in this pipeline (technical-debt.json) -- no per-file/per-module list is generated by any analyzer, so these 4 fields are repeated verbatim (not module-specific) and must not be read as this module's own count."
        }
        security = [ordered]@{
            secretFiles = $moduleSecrets
            anonymousFiles = $moduleAnonymous
        }
        dependencies = [ordered]@{
            dependsOn = $dependsOn
            dependedOnBy = $dependedOnBy
            coupling = $coupling
            fanIn = $fanIn
            fanOut = $fanOut
            instability = $instability
            criticalDependencies = $criticalDependencies
            cyclesInvolved = $cyclesInvolved
        }
        impact = [ordered]@{
            directDependents = $dependedOnBy
            transitiveDependents = if($null -ne $impact) { @($impact.transitiveDependents) } else { @() }
            transitiveDependentCount = $transitiveDependentCount
            dependentProcesses = $dependentProcesses
            dependentFeatures = $dependentFeatures
            relatedTasks = $relatedTasks
            percentOfErp = $percentOfErp
            riskOfModifying = $riskOfModifying
            legacyRisks = if($null -ne $legacyImpact) { @($legacyImpact.risks) } else { @() }
        }
        changeRisk = [ordered]@{
            score = $riskScore
            band = $riskBand
            formula = $changeRiskFormula
            inputs = [ordered]@{ score = $scoreVal; coupling = $coupling; largeFilesCount = $largeFilesCount; riskOfModifying = $riskOfModifying; transitiveDependentCount = $transitiveDependentCount; busFactor = $busFactor }
        }
    }
}

Write-Host "Module profiles assembled: $($moduleProfiles.Count)"


# =============================================================================
# 7. Executive view (top 10s + status, all copied/derived from already
#    published fields -- zero new computation beyond sorting/slicing)
# =============================================================================

$top10Risk = @($moduleProfiles | Sort-Object -Property {$_.changeRisk.score} -Descending | Select-Object -First 10 | ForEach-Object { [ordered]@{ module = $_.id; riskScore = $_.changeRisk.score; band = $_.changeRisk.band } })
$top10Debt = @($moduleProfiles | Sort-Object -Property {$_.debt.largeFilesCount} -Descending | Select-Object -First 10 | ForEach-Object { [ordered]@{ module = $_.id; largeFilesCount = $_.debt.largeFilesCount } })
$top10LowScore = @($moduleProfiles | Sort-Object -Property {$_.score} | Select-Object -First 10 | ForEach-Object { [ordered]@{ module = $_.id; score = $_.score } })

$executive = [ordered]@{
    engineeringScoreOverall = $model.EngineeringScore.Overall
    erpCompletion = $completionIntelligence.erpCompletion
    productionDecision = $completionIntelligence.productionReadiness
    overallStatus = $completionIntelligence.overallStatus
    nextMilestone = $completionIntelligence.nextMilestone
    blockers = @($completionIntelligence.criticalGaps)
    top10Risk = $top10Risk
    top10Debt = $top10Debt
    top10LowScore = $top10LowScore
}

Write-Host "Executive view assembled"


# =============================================================================
# 8. Consolidated search index (modules, features, processes, tasks, files,
#    domains -- single source of truth, replaces any ad-hoc index building)
# =============================================================================

$searchEntries = New-Object System.Collections.Generic.List[object]

foreach($m in $modulesData) { $searchEntries.Add([ordered]@{ type = "Module"; label = $m.id; moduleId = $m.id }) }
foreach($d in $domainsData) { $searchEntries.Add([ordered]@{ type = "Domain"; label = $d.name; moduleId = $null; domainId = $d.id }) }
foreach($fe in $featuresData) { foreach($feature in $fe.features) { $searchEntries.Add([ordered]@{ type = "Feature"; label = "$($fe.module) / $($feature.name)"; moduleId = $fe.module }) } }
foreach($process in $processesData) { foreach($step in $process.steps) { $searchEntries.Add([ordered]@{ type = "Process"; label = "$($process.process) / $($step.name)"; moduleId = $step.module }) } }
foreach($task in $tasksData) { $searchEntries.Add([ordered]@{ type = "Task"; label = $task.task; moduleId = $null }) }
foreach($file in $reverseFileIndex.Keys) { $searchEntries.Add([ordered]@{ type = "File"; label = $file; moduleId = $reverseFileIndex[$file][0].module }) }

Write-Host "Search index assembled: $($searchEntries.Count) entries"


# =============================================================================
# Output
# =============================================================================

$searchEntriesArray = $searchEntries.ToArray()

$result = [ordered]@{
    generated = (Get-Date -Format "yyyy-MM-dd HH:mm:ss")
    source = "modules.json, features.json, processes.json, tasks.json, domains.json, dependencies.json, critical-path.json, impact.json, dashboard-model-v12.json, completion-intelligence.json, git log"
    method = [ordered]@{
        changeRiskFormula = $changeRiskFormula
        criticalDependencies = "A dependency edge is flagged critical if its target module is in dependencies.json.centralModules (top-5 highest coupling) -- reuses an already-published classification, no new threshold invented"
        debtAttribution = "Large files attributed to a module only if their real absolute path matches \Modules\<id>\ (backend) or /<id>/ (frontend); TODO/FIXME/HACK/NotImplemented cannot be attributed per module (see knownGaps)"
        reverseFileIndex = "Built from features.json[].features[].evidence, processes.json[].steps[].evidence, tasks.json[].evidence -- one dictionary pass per source, O(1) lookup by file path"
        lastEvidenceDate = "git log -1 --format=%cd --date=short over the module's real folders (same technique as analyze-module-graph.ps1's busFactor)"
        knownGaps = @(
            "TODO/FIXME/HACK/NotImplemented counts exist only globally (technical-debt.json) -- no analyzer stores a per-file list for these 4 markers, so no per-module breakdown is possible without inventing one."
            "Test coverage (%) does not exist anywhere in the pipeline -- tests-analysis.json only has per-TEST-PROJECT file counts (not linked to business modules, not a coverage percentage). modules.json's 'tests' field is a composite quality score, not a coverage measurement -- explorer-index.json does not relabel it as coverage."
        )
    }
    modules = $moduleProfiles
    reverseFileIndex = $reverseFileIndexOutput
    executive = $executive
    searchEntries = $searchEntriesArray
}

$result | ConvertTo-Json -Depth 12 | Out-File $Output -Encoding utf8

Write-Host ""
Write-Host "Explorer index generated successfully." -ForegroundColor Green
Write-Host $Output
