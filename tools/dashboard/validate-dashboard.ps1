# =============================================================================
# ZH Technologies
# Dashboard Quality Gate -- Fase Dashboard 11.0
#
# NO genera datos de negocio -- valida la CALIDAD de los datasets ya
# existentes que consume tools/dashboard/render-dashboard.ps1. Escribe
# docs/ProgressDashboard/data/dashboard-validation.json y nada mas.
#
# Alcance de los 12 checks pedidos:
#   1/2/12 (existe / JSON valido / no vacio) se aplican a las 25 fuentes
#     completas que consume render-dashboard.ps1 (mismo universo que el
#     $requiredDataFiles de build-dashboard-data.ps1, Fase 9.0, mas
#     module-coverage-audit.json).
#   3/4/5/6/7/8/9/10/11 (claves obligatorias, duplicados, referencias rotas,
#     modulos inexistentes, rangos 0-100, estados desconocidos, fechas,
#     nulls no permitidos) se aplican especificamente a los 6 datasets con
#     semantica real de "modulo" (modules-status.json, architecture-
#     governance.json, roadmap.json, blockers.json, architecture-
#     dependencies.json, module-coverage-audit.json) mas explorer-index.json/
#     modules.json (registro tecnico). Los ~17 JSON puramente analiticos
#     restantes (technical-debt.json, security-analysis.json, etc.) NO tienen
#     un esquema de "modulo" verificado en este script -- exigirles claves/
#     rangos inventados seria fabricar una regla no respaldada por su
#     estructura real, asi que solo reciben los checks 1/2/12.
# =============================================================================

$ErrorActionPreference = "Stop"

$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$DataRoot = Join-Path $ProjectRoot "docs\ProgressDashboard\data"
$DomainModulesPath = Join-Path $ProjectRoot "backend\src\ERP.Domain\Modules"

$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()

Write-Host ""
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host " ZH Dashboard Quality Gate (Fase 11.0)" -ForegroundColor Cyan
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host ""

$allDataFiles = @(
    "dashboard-model-v12.json", "dashboard-summary.json", "erp.json", "layers.json", "domains.json",
    "modules.json", "features.json", "processes.json", "tasks.json", "impact.json",
    "model-health.json", "architecture-progress.json", "completion-intelligence.json", "navigation-map.json",
    "explorer-index.json", "modules-status.json", "dependencies.json", "critical-path.json",
    "release-simulation.json", "recommendations.json", "roadmap.json", "architecture-dependencies.json",
    "blockers.json", "architecture-governance.json", "module-coverage-audit.json"
)

$checks = New-Object System.Collections.Generic.List[object]
$errorsList = New-Object System.Collections.Generic.List[object]
$warningsList = New-Object System.Collections.Generic.List[object]

function Add-Check($name, $ok, $severity, $detail)
{
    $script:checks.Add([ordered]@{ check = $name; ok = $ok; severity = $severity; detail = $detail })
    if(-not $ok)
    {
        if($severity -eq "Critica" -or $severity -eq "Error") { $script:errorsList.Add([ordered]@{ check = $name; severity = $severity; detail = $detail }) }
        else { $script:warningsList.Add([ordered]@{ check = $name; severity = $severity; detail = $detail }) }
    }
}

$parsedCache = @{}

function Get-ParsedJson($file)
{
    if($script:parsedCache.ContainsKey($file)) { return $script:parsedCache[$file] }
    $path = Join-Path $DataRoot $file
    if(!(Test-Path $path)) { $script:parsedCache[$file] = $null; return $null }
    try
    {
        $raw = Get-Content $path -Raw
        if([string]::IsNullOrWhiteSpace($raw)) { $script:parsedCache[$file] = $null; return $null }
        $parsed = $raw | ConvertFrom-Json
        $script:parsedCache[$file] = $parsed
        return $parsed
    }
    catch
    {
        $script:parsedCache[$file] = $null
        return $null
    }
}

# -----------------------------------------------------------------------------
# Checks 1, 2, 12: existencia, JSON valido, no vacio -- las 25 fuentes.
# -----------------------------------------------------------------------------
foreach($file in $allDataFiles)
{
    $path = Join-Path $DataRoot $file

    $exists = Test-Path $path
    Add-Check "1. Existe: $file" $exists "Critica" $(if($exists){"OK"}else{"No se encuentra $path"})
    if(-not $exists) { continue }

    $info = Get-Item $path
    $notEmpty = $info.Length -gt 0
    Add-Check "12. No vacio: $file" $notEmpty "Critica" $(if($notEmpty){"OK ($($info.Length) bytes)"}else{"Archivo de 0 bytes"})
    if(-not $notEmpty) { continue }

    $raw = Get-Content $path -Raw
    $validJson = $true
    try { $parsed = $raw | ConvertFrom-Json } catch { $validJson = $false }
    Add-Check "2. JSON valido: $file" $validJson "Critica" $(if($validJson){"OK"}else{"Error de parseo JSON"})
    if(-not $validJson) { continue }

    $meaningfullyEmpty = $false
    if($parsed -is [System.Array] -and $parsed.Count -eq 0) { $meaningfullyEmpty = $true }
    if($parsed -is [System.Management.Automation.PSCustomObject] -and (@($parsed.PSObject.Properties)).Count -eq 0) { $meaningfullyEmpty = $true }
    Add-Check "12b. Contenido no trivial: $file" (-not $meaningfullyEmpty) "Alta" $(if($meaningfullyEmpty){"Objeto/array JSON vacio ({} o [])"}else{"OK"})
}

# -----------------------------------------------------------------------------
# Datos ya parseados (solo para los datasets con semantica de modulo).
# -----------------------------------------------------------------------------
$moduleStatus = Get-ParsedJson "modules-status.json"
$governance = Get-ParsedJson "architecture-governance.json"
$roadmap = Get-ParsedJson "roadmap.json"
$blockers = Get-ParsedJson "blockers.json"
$archDeps = Get-ParsedJson "architecture-dependencies.json"
$coverageAudit = Get-ParsedJson "module-coverage-audit.json"
$explorerIdx = Get-ParsedJson "explorer-index.json"

$realModuleIds = @{}
if(Test-Path $DomainModulesPath)
{
    Get-ChildItem $DomainModulesPath -Directory | ForEach-Object { $realModuleIds[$_.Name] = $true }
}
$explorerModuleIds = @{}
if($explorerIdx) { foreach($m in $explorerIdx.modules) { $explorerModuleIds[$m.id] = $true } }

# Excepcion conocida y documentada (Fase Dashboard 3.0/10.0): BusinessPartner
# es un modulo real FROZEN pero vive fuera de Modules/ por completo -- no es
# un bug, se trata como advertencia informativa, no como error critico.
$knownOutOfTreeModules = @("BusinessPartner")

# -----------------------------------------------------------------------------
# Check 3: claves obligatorias por dataset (solo para los 6 con esquema
# verificado por este mismo pipeline en fases anteriores).
# -----------------------------------------------------------------------------
function Test-RequiredKeys($items, $requiredKeys, $datasetName)
{
    $missingReports = @()
    foreach($item in $items)
    {
        $props = @($item.PSObject.Properties.Name)
        $missing = @($requiredKeys | Where-Object { $props -notcontains $_ })
        if($missing.Count -gt 0)
        {
            $idLabel = if($props -contains "id") { $item.id } else { "(sin id)" }
            $missingReports += "$datasetName/$idLabel : faltan claves $($missing -join ', ')"
        }
    }
    return $missingReports
}

$keyIssues = @()
if($moduleStatus) { $keyIssues += Test-RequiredKeys $moduleStatus.modules @("id","functionalStatus","freezeStatus","adr","observations") "modules-status.json" }
if($governance) { $keyIssues += Test-RequiredKeys $governance.modules @("id","adr","freezeStatus","architectureStatus","lastAudit") "architecture-governance.json" }
if($roadmap) { $keyIssues += Test-RequiredKeys $roadmap.stages @("id","nombre","estado","modulos") "roadmap.json" }
if($blockers) { $keyIssues += Test-RequiredKeys $blockers.blockers @("id","titulo","severidad","estado") "blockers.json" }
if($archDeps) { $keyIssues += Test-RequiredKeys $archDeps.edges @("sourceModule","targetModule","dependencyType","critical") "architecture-dependencies.json" }
if($coverageAudit) { $keyIssues += Test-RequiredKeys $coverageAudit.modules @("id","existsInCode","coverageGapReal") "module-coverage-audit.json" }

Add-Check "3. Claves obligatorias presentes" ($keyIssues.Count -eq 0) "Alta" $(if($keyIssues.Count -eq 0){"OK"}else{($keyIssues -join "; ")})

# -----------------------------------------------------------------------------
# Checks 4/5: modulos e IDs duplicados dentro de cada dataset.
# -----------------------------------------------------------------------------
function Find-Duplicates($values)
{
    return @($values | Group-Object | Where-Object { $_.Count -gt 1 } | ForEach-Object { $_.Name })
}

$dupIssues = @()
if($moduleStatus) { $d = Find-Duplicates (@($moduleStatus.modules | ForEach-Object { $_.id })); if($d.Count -gt 0) { $dupIssues += "modules-status.json ids duplicados: $($d -join ', ')" } }
if($governance) { $d = Find-Duplicates (@($governance.modules | ForEach-Object { $_.id })); if($d.Count -gt 0) { $dupIssues += "architecture-governance.json ids duplicados: $($d -join ', ')" } }
if($roadmap) { $d = Find-Duplicates (@($roadmap.stages | ForEach-Object { $_.id })); if($d.Count -gt 0) { $dupIssues += "roadmap.json etapa ids duplicados: $($d -join ', ')" } }
if($blockers) { $d = Find-Duplicates (@($blockers.blockers | ForEach-Object { $_.id })); if($d.Count -gt 0) { $dupIssues += "blockers.json ids duplicados: $($d -join ', ')" } }
if($archDeps) { $d = Find-Duplicates (@($archDeps.edges | ForEach-Object { "$($_.sourceModule)->$($_.targetModule)" })); if($d.Count -gt 0) { $dupIssues += "architecture-dependencies.json aristas duplicadas: $($d -join ', ')" } }
if($explorerIdx) { $d = Find-Duplicates (@($explorerIdx.modules | ForEach-Object { $_.id })); if($d.Count -gt 0) { $dupIssues += "explorer-index.json ids duplicados: $($d -join ', ')" } }

Add-Check "4/5. Sin modulos/IDs duplicados" ($dupIssues.Count -eq 0) "Critica" $(if($dupIssues.Count -eq 0){"OK"}else{($dupIssues -join "; ")})

# -----------------------------------------------------------------------------
# Check 6: referencias rotas entre datasets (cruces ya establecidos en fases
# anteriores del pipeline -- se re-verifican aqui como gate, no se recalculan
# con logica nueva).
# -----------------------------------------------------------------------------
$brokenRefs = @()

if($roadmap -and $explorerModuleIds.Count -gt 0)
{
    foreach($stage in $roadmap.stages)
    {
        foreach($modId in @($stage.modulos))
        {
            if(-not $explorerModuleIds.ContainsKey($modId) -and $knownOutOfTreeModules -notcontains $modId)
            {
                $brokenRefs += "roadmap.json ($($stage.id)) -> modulo inexistente '$modId'"
            }
        }
    }
}

if($blockers -and $explorerModuleIds.Count -gt 0)
{
    $roadmapStageIds = @{}
    if($roadmap) { foreach($st in $roadmap.stages) { $roadmapStageIds[$st.id] = $true } }
    foreach($b in $blockers.blockers)
    {
        foreach($modId in @($b.modulosAfectados))
        {
            if(-not $explorerModuleIds.ContainsKey($modId) -and $knownOutOfTreeModules -notcontains $modId)
            {
                $brokenRefs += "blockers.json ($($b.id)) -> modulo inexistente '$modId'"
            }
        }
        if($b.etapa -and $roadmap -and -not $roadmapStageIds.ContainsKey($b.etapa))
        {
            $brokenRefs += "blockers.json ($($b.id)) -> etapa inexistente '$($b.etapa)'"
        }
    }
}

if($archDeps -and $explorerModuleIds.Count -gt 0)
{
    foreach($e in $archDeps.edges)
    {
        if(-not $explorerModuleIds.ContainsKey($e.sourceModule)) { $brokenRefs += "architecture-dependencies.json -> sourceModule inexistente '$($e.sourceModule)'" }
        if(-not $explorerModuleIds.ContainsKey($e.targetModule)) { $brokenRefs += "architecture-dependencies.json -> targetModule inexistente '$($e.targetModule)'" }
    }
}

if($governance)
{
    $adrDir = Join-Path $ProjectRoot "docs\adr"
    $adrFiles = @{}
    if(Test-Path $adrDir) { Get-ChildItem $adrDir -Filter "*.md" | ForEach-Object { $adrFiles[$_.Name] = $true } }
    foreach($gm in $governance.modules)
    {
        $refs = [regex]::Matches($gm.adr, "ADR-\d{3}[\w\-\.]*\.md") | ForEach-Object { $_.Value }
        foreach($r in $refs) { if(-not $adrFiles.ContainsKey($r)) { $brokenRefs += "architecture-governance.json ($($gm.id)) -> ADR inexistente '$r'" } }
    }
}

Add-Check "6. Sin referencias rotas entre datasets" ($brokenRefs.Count -eq 0) "Alta" $(if($brokenRefs.Count -eq 0){"OK"}else{($brokenRefs -join "; ")})

# -----------------------------------------------------------------------------
# Check 7: modulos referenciados que no existen respecto al codigo fuente.
# Fuente de "codigo fuente" = explorer-index.json (registro tecnico real ya
# usado por el resto del pipeline, Fase 4.0/5.0/6.0) -- BusinessPartner es la
# unica excepcion conocida (real, FROZEN, fuera del arbol Modules/ por diseno).
# -----------------------------------------------------------------------------
$nonexistentModules = @()
$knownExceptionHits = @()
if($explorerModuleIds.Count -gt 0)
{
    $allReferencedIds = New-Object System.Collections.Generic.HashSet[string]
    if($roadmap) { foreach($st in $roadmap.stages) { foreach($m in @($st.modulos)) { [void]$allReferencedIds.Add($m) } } }
    if($blockers) { foreach($b in $blockers.blockers) { foreach($m in @($b.modulosAfectados)) { [void]$allReferencedIds.Add($m) } } }
    if($archDeps) { foreach($e in $archDeps.edges) { [void]$allReferencedIds.Add($e.sourceModule); [void]$allReferencedIds.Add($e.targetModule) } }

    foreach($modId in $allReferencedIds)
    {
        if(-not $explorerModuleIds.ContainsKey($modId))
        {
            if($knownOutOfTreeModules -contains $modId) { $knownExceptionHits += $modId }
            else { $nonexistentModules += $modId }
        }
    }
}

Add-Check "7. Sin modulos inexistentes respecto al codigo fuente" ($nonexistentModules.Count -eq 0) "Critica" $(if($nonexistentModules.Count -eq 0){"OK"}else{"Modulos referenciados sin correlato en explorer-index.json: $($nonexistentModules -join ', ')"})
if($knownExceptionHits.Count -gt 0)
{
    Add-Check "7b. Excepciones conocidas (fuera del arbol Modules/, ya documentadas)" $true "Informativa" "$($knownExceptionHits | Select-Object -Unique | Sort-Object) -- ver Fase Dashboard 3.0/10.0"
}

# -----------------------------------------------------------------------------
# Check 8: porcentajes fuera de rango 0-100.
# -----------------------------------------------------------------------------
$pctIssues = @()
if($explorerIdx)
{
    foreach($m in $explorerIdx.modules)
    {
        foreach($field in @("score","architecture","tests","documentation","backend","frontend"))
        {
            $val = $m.$field
            if($null -ne $val)
            {
                $numeric = 0.0
                if([double]::TryParse("$val", [System.Globalization.NumberStyles]::Float, [System.Globalization.CultureInfo]::InvariantCulture, [ref]$numeric))
                {
                    if($numeric -lt 0 -or $numeric -gt 100) { $pctIssues += "explorer-index.json ($($m.id)).$field = $val fuera de rango [0,100]" }
                }
            }
        }
    }
}
if($coverageAudit -and $null -ne $coverageAudit.coveragePct)
{
    $cp = [double]$coverageAudit.coveragePct
    if($cp -lt 0 -or $cp -gt 100) { $pctIssues += "module-coverage-audit.json.coveragePct = $cp fuera de rango [0,100]" }
}

Add-Check "8. Porcentajes dentro de rango 0-100" ($pctIssues.Count -eq 0) "Alta" $(if($pctIssues.Count -eq 0){"OK"}else{($pctIssues -join "; ")})

# -----------------------------------------------------------------------------
# Check 9: estados desconocidos -- functionalStatus/architectureStatus deben
# contener al menos una palabra clave de un vocabulario conocido (mismo
# vocabulario ya usado como reglas de clasificacion en Fase Dashboard 7.0 --
# no se inventa uno nuevo aqui).
# -----------------------------------------------------------------------------
$knownFunctionalStatusWords = @("Frozen", "Operativo", "Skeleton", "Parcial", "iniciad", "Pendiente de evaluacion")
$knownArchitectureStatusValues = @("Freeze", "Accepted", "Draft", "Deprecated", "Experimental", "En construccion", "Pendiente de auditoria")

$unknownStates = @()
if($moduleStatus)
{
    foreach($m in $moduleStatus.modules)
    {
        $matched = $false
        foreach($w in $knownFunctionalStatusWords) { if($m.functionalStatus -match [regex]::Escape($w)) { $matched = $true; break } }
        if(-not $matched) { $unknownStates += "modules-status.json ($($m.id)).functionalStatus = '$($m.functionalStatus)' no coincide con ningun estado conocido" }
    }
}
if($governance)
{
    foreach($m in $governance.modules)
    {
        if($knownArchitectureStatusValues -notcontains $m.architectureStatus) { $unknownStates += "architecture-governance.json ($($m.id)).architectureStatus = '$($m.architectureStatus)' no es un valor conocido" }
    }
}
if($blockers)
{
    $knownBlockerStates = @("Abierto", "En progreso", "Resuelto")
    foreach($b in $blockers.blockers)
    {
        if($knownBlockerStates -notcontains $b.estado) { $unknownStates += "blockers.json ($($b.id)).estado = '$($b.estado)' no es un valor conocido (Abierto/En progreso/Resuelto)" }
    }
}

Add-Check "9. Sin estados desconocidos" ($unknownStates.Count -eq 0) "Media" $(if($unknownStates.Count -eq 0){"OK"}else{($unknownStates -join "; ")})

# -----------------------------------------------------------------------------
# Check 10: fechas invalidas -- todo campo que no sea literalmente el
# placeholder de pendiente debe parsear como fecha real.
# -----------------------------------------------------------------------------
$dateIssues = @()
$pendingDateLiterals = @("Pendiente de auditoria", "Pendiente de evaluacion")

function Test-DateFieldValid($value, $label)
{
    if($null -eq $value) { return }
    if($pendingDateLiterals -contains $value) { return }
    if($value -match "^\d{4}-\d{2}-\d{2}") { return } # ISO real, valida directo
    $parsed = [DateTime]::MinValue
    if(-not [DateTime]::TryParse($value, [System.Globalization.CultureInfo]::InvariantCulture, [System.Globalization.DateTimeStyles]::None, [ref]$parsed))
    {
        $script:dateIssues += "$label = '$value' no es una fecha valida ni el literal de pendiente"
    }
}

if($governance) { foreach($m in $governance.modules) { Test-DateFieldValid $m.lastAudit "architecture-governance.json ($($m.id)).lastAudit"; Test-DateFieldValid $m.nextAudit "architecture-governance.json ($($m.id)).nextAudit" } }
foreach($f in @("modules-status.json","roadmap.json","blockers.json","architecture-governance.json","architecture-dependencies.json","module-coverage-audit.json"))
{
    $p = Get-ParsedJson $f
    if($p -and $p.generated) { Test-DateFieldValid $p.generated "$f.generated" }
}

Add-Check "10. Sin fechas invalidas" ($dateIssues.Count -eq 0) "Media" $(if($dateIssues.Count -eq 0){"OK"}else{($dateIssues -join "; ")})

# -----------------------------------------------------------------------------
# Check 11: valores null en campos que el esquema no permite (mismos 6
# datasets, mismas claves obligatorias del check 3, ahora exigiendo ademas
# que no sean null aunque la clave exista).
# -----------------------------------------------------------------------------
function Test-NoNulls($items, $requiredKeys, $datasetName)
{
    $nullReports = @()
    foreach($item in $items)
    {
        foreach($key in $requiredKeys)
        {
            $props = @($item.PSObject.Properties.Name)
            if($props -contains $key -and $null -eq $item.$key)
            {
                $idLabel = if($props -contains "id") { $item.id } else { "(sin id)" }
                $nullReports += "$datasetName/$idLabel.$key es null"
            }
        }
    }
    return $nullReports
}

$nullIssues = @()
if($moduleStatus) { $nullIssues += Test-NoNulls $moduleStatus.modules @("id","functionalStatus","freezeStatus","adr") "modules-status.json" }
if($governance) { $nullIssues += Test-NoNulls $governance.modules @("id","adr","freezeStatus","architectureStatus") "architecture-governance.json" }
if($roadmap) { $nullIssues += Test-NoNulls $roadmap.stages @("id","nombre","estado") "roadmap.json" }
if($blockers) { $nullIssues += Test-NoNulls $blockers.blockers @("id","titulo","severidad","estado") "blockers.json" }
if($archDeps) { $nullIssues += Test-NoNulls $archDeps.edges @("sourceModule","targetModule","dependencyType") "architecture-dependencies.json" }
if($coverageAudit) { $nullIssues += Test-NoNulls $coverageAudit.modules @("id","existsInCode") "module-coverage-audit.json" }

Add-Check "11. Sin nulls en campos obligatorios" ($nullIssues.Count -eq 0) "Alta" $(if($nullIssues.Count -eq 0){"OK"}else{($nullIssues -join "; ")})

# -----------------------------------------------------------------------------
# Resultado final
# -----------------------------------------------------------------------------
$stopwatch.Stop()

$totalChecks = $checks.Count
$passed = (@($checks | Where-Object { $_.ok })).Count
$failed = (@($checks | Where-Object { -not $_.ok })).Count
$criticalErrors = (@($errorsList | Where-Object { $_.severity -eq "Critica" })).Count
$score = if($totalChecks -gt 0) { [math]::Round(100.0 * $passed / $totalChecks, 1) } else { 0 }

$result = [ordered]@{
    timestamp = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
    totalChecks = $totalChecks
    passed = $passed
    failed = $failed
    criticalErrors = $criticalErrors
    warnings = $warningsList.Count
    score = $score
    tiempoMs = $stopwatch.ElapsedMilliseconds
    metodo = "Checks 1/2/12 (existencia/JSON valido/no vacio) aplicados a las 25 fuentes que consume render-dashboard.ps1. Checks 3-11 (claves obligatorias, duplicados, referencias rotas, modulos vs codigo fuente, rangos 0-100, estados conocidos, fechas, nulls) aplicados solo a los 6 datasets con esquema de modulo verificado (modules-status.json/architecture-governance.json/roadmap.json/blockers.json/architecture-dependencies.json/module-coverage-audit.json) mas explorer-index.json como referencia de codigo fuente real. Score = 100 * passed / totalChecks."
    errores = $errorsList.ToArray()
    advertencias = $warningsList.ToArray()
    checksCompletos = $checks.ToArray()
}

$outputPath = Join-Path $DataRoot "dashboard-validation.json"
$result | ConvertTo-Json -Depth 10 | Set-Content $outputPath -Encoding UTF8

Write-Host ""
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host " Resultado" -ForegroundColor Cyan
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Total checks: $totalChecks ($passed OK, $failed con problemas)"
Write-Host "Errores criticos: $criticalErrors" -ForegroundColor $(if($criticalErrors -gt 0){"Red"}else{"Green"})
Write-Host "Advertencias: $($warningsList.Count)" -ForegroundColor $(if($warningsList.Count -gt 0){"Yellow"}else{"Green"})
Write-Host "Score: $score%" -ForegroundColor $(if($score -ge 80){"Green"}elseif($score -ge 60){"Yellow"}else{"Red"})
Write-Host "Tiempo: $($stopwatch.ElapsedMilliseconds) ms"
Write-Host ""
Write-Host "Escrito: $outputPath" -ForegroundColor Green

if($criticalErrors -gt 0)
{
    Write-Host ""
    foreach($e in $errorsList) { if($e.severity -eq "Critica") { Write-Host "CRITICO: $($e.check) -> $($e.detail)" -ForegroundColor Red } }
    throw "validate-dashboard.ps1: $criticalErrors error(es) critico(s) de calidad detectado(s). Ver dashboard-validation.json para el detalle completo."
}
