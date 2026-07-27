# =============================================================================
# ZH Technologies
# analyze-progress-map.ps1
#
# Lee docs/ProgressDashboard/data/architecture-progress-source.json (la fuente
# oficial y estructurada del progreso manual, extraida 1:1 desde el antiguo
# array embebido `const D = [...]` de PROGRESS.html) EN MODO SOLO LECTURA y
# calcula hacia docs/ProgressDashboard/data/architecture-progress.json.
#
# PROGRESS.html ya NO es la fuente de datos de este pipeline (FASE DASHBOARD
# 13.0): es unicamente una vista que carga el mismo
# architecture-progress-source.json en el navegador. Este script consume
# esa misma fuente y REPLICA exactamente los mismos calculos que la vista
# hace en el navegador (mismos pesos d/f=1, p=0.5, n=0; mismas formulas de
# porcentaje; mismo lookup findPhase() para los 5 modulos core y los 3
# stages que alimentan el "Web ERP %"). Ningun numero se inventa: todo sale
# de tareas reales con su status real (d/f/p/n) tal como estan escritas en
# la fuente.
#
# NO modifica PROGRESS.html. NO modifica el ERP. NO modifica ningun otro
# JSON existente en docs/ProgressDashboard/data/ salvo el output propio de
# este script (architecture-progress.json).
# =============================================================================

$ErrorActionPreference = "Stop"


$ProjectRoot =
(Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path


$SourceJsonPath =
Join-Path $ProjectRoot "docs\ProgressDashboard\data\architecture-progress-source.json"


$SourceJsMirrorPath =
Join-Path $ProjectRoot "docs\ProgressDashboard\data\architecture-progress-source.js"


$OutputFile =
Join-Path $ProjectRoot "docs\ProgressDashboard\data\architecture-progress.json"



Write-Host ""
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host " analyze-progress-map.ps1"
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host ""


if(!(Test-Path $SourceJsonPath))
{
    throw "architecture-progress-source.json not found at: $SourceJsonPath"
}


function Decode-Entities($text)
{
    if($null -eq $text) { return "" }

    return $text -replace "&amp;","&" -replace "&lt;","<" -replace "&gt;",">"
}


# -----------------------------------------------------------------------
# Cargar la fuente estructurada -- unica fuente real de datos
# -----------------------------------------------------------------------

$sourceD = Get-Content $SourceJsonPath -Raw -Encoding UTF8 | ConvertFrom-Json

if($null -eq $sourceD -or $sourceD.Count -eq 0)
{
    throw "architecture-progress-source.json parsed to zero stages -- aborting rather than emitting an empty/fabricated result."
}

Write-Host "Source loaded: $($sourceD.Count) top-level stages" -ForegroundColor Green


# -----------------------------------------------------------------------
# Espejo .js para PROGRESS.html -- PROGRESS.html ya no embebe datos a mano
# (FASE DASHBOARD 13.0): carga este archivo generado via <script src>, que
# simplemente expone el mismo JSON como variable global. Un <script src>
# local funciona igual bajo file:// y bajo http://; fetch()/XHR de un
# archivo .json local son bloqueados por CORS en Chrome bajo file://, por
# eso no se usa fetch() aqui. Este .js es 100% generado -- nunca se edita
# a mano, se regenera en cada corrida desde architecture-progress-source.json.
# -----------------------------------------------------------------------

$sourceRawJson = Get-Content $SourceJsonPath -Raw -Encoding UTF8
$sourceRawJson = $sourceRawJson -replace "^\xEF\xBB\xBF", ""

$jsMirrorContent =
"// AUTO-GENERADO por tools/dashboard/analyze-progress-map.ps1`n" +
"// desde docs/ProgressDashboard/data/architecture-progress-source.json.`n" +
"// NO editar a mano -- editar el .json fuente y volver a correr el script.`n" +
"window.__ARCHITECTURE_PROGRESS_SOURCE__ = $sourceRawJson;`n"

$jsMirrorContent | Out-File $SourceJsMirrorPath -Encoding utf8 -NoNewline

Write-Host "Browser mirror written: $SourceJsMirrorPath" -ForegroundColor Green


# -----------------------------------------------------------------------
# Reconstruir stages -> phases -> tasks (tareas aplanadas desde los
# "groups" de cada fase, igual que hacia la extraccion por regex anterior)
# -----------------------------------------------------------------------

$stages = @()
$allTasksFlat = @()

for($si = 0; $si -lt $sourceD.Count; $si++)
{
    $srcStage = $sourceD[$si]

    $stageObj = [PSCustomObject]@{
        index  = $si
        name   = Decode-Entities $srcStage.stage
        phases = @()
    }

    for($pi = 0; $pi -lt $srcStage.phases.Count; $pi++)
    {
        $srcPhase = $srcStage.phases[$pi]

        $phaseObj = [PSCustomObject]@{
            index      = $pi
            stageIndex = $si
            name       = Decode-Entities $srcPhase.name
            desc       = Decode-Entities $srcPhase.desc
            tasks      = @()
        }

        foreach($group in $srcPhase.groups)
        {
            foreach($task in $group.tasks)
            {
                $taskObj = [PSCustomObject]@{
                    status      = $task.s
                    name        = Decode-Entities $task.n
                    description = Decode-Entities $task.d
                    stageName   = $stageObj.name
                    phaseName   = $phaseObj.name
                }

                $phaseObj.tasks += $taskObj
                $allTasksFlat += $taskObj
            }
        }

        $stageObj.phases += $phaseObj
    }

    $stages += $stageObj
}

$phaseCount = ($stages | ForEach-Object { $_.phases.Count } | Measure-Object -Sum).Sum

Write-Host "Parsed: $($stages.Count) stages, $phaseCount phases, $($allTasksFlat.Count) tasks"

if($stages.Count -eq 0 -or $allTasksFlat.Count -eq 0)
{
    throw "Parsed zero stages/tasks from architecture-progress-source.json -- data model shape may have changed. Aborting rather than emitting an empty/fabricated result."
}


# -----------------------------------------------------------------------
# Replica exacta de calcPhase / calcStage / calcGlobal / countByStatus
# (mismas formulas que PROGRESS.html usa en su propio JS)
# -----------------------------------------------------------------------

$taskWeight = @{ d = 1.0; f = 1.0; p = 0.5; n = 0.0 }

function Calc-Phase($phase)
{
    $total = $phase.tasks.Count
    $done = ($phase.tasks | ForEach-Object { $taskWeight[$_.status] } | Measure-Object -Sum).Sum

    if($null -eq $done) { $done = 0 }

    $pct = 0
    if($total -gt 0) { $pct = [math]::Round(($done / $total) * 100) }

    return [PSCustomObject]@{ total = $total; done = $done; pct = $pct }
}


function Calc-Stage($stage)
{
    $total = 0
    $done = 0.0

    foreach($phase in $stage.phases)
    {
        $c = Calc-Phase $phase
        $total += $c.total
        $done += $c.done
    }

    $pct = 0
    if($total -gt 0) { $pct = [math]::Round(($done / $total) * 100) }

    return [PSCustomObject]@{ total = $total; done = $done; pct = $pct }
}


$globalTotal = $allTasksFlat.Count
$globalDone = ($allTasksFlat | ForEach-Object { $taskWeight[$_.status] } | Measure-Object -Sum).Sum
$globalPct = 0
if($globalTotal -gt 0) { $globalPct = [math]::Round(($globalDone / $globalTotal) * 100) }

$statusCounts = [PSCustomObject]@{
    d = @($allTasksFlat | Where-Object { $_.status -eq "d" }).Count
    f = @($allTasksFlat | Where-Object { $_.status -eq "f" }).Count
    p = @($allTasksFlat | Where-Object { $_.status -eq "p" }).Count
    n = @($allTasksFlat | Where-Object { $_.status -eq "n" }).Count
}


Write-Host "Global progress: $globalDone / $globalTotal = $globalPct%"
Write-Host "Status counts: d=$($statusCounts.d) f=$($statusCounts.f) p=$($statusCounts.p) n=$($statusCounts.n)"


# -----------------------------------------------------------------------
# findPhase() replica: busca por substring de stage y de phase, igual que
# la funcion homonima de PROGRESS.html. Usada por el diagrama para los 5
# "core modules" (Ventas/Compras/Inventario/Caja/Contabilidad).
# -----------------------------------------------------------------------

function Find-PhaseLike($stageNameIncludes, $phaseNameIncludes)
{
    foreach($stage in $stages)
    {
        if($stage.name -notmatch [regex]::Escape($stageNameIncludes)) { continue }

        foreach($phase in $stage.phases)
        {
            if($phase.name -match [regex]::Escape($phaseNameIncludes))
            {
                return Calc-Phase $phase
            }
        }
    }

    return [PSCustomObject]@{ total = 0; done = 0; pct = 0 }
}


$ventas = Find-PhaseLike "Operaciones" "Ventas"
$compras = Find-PhaseLike "Operaciones" "Compras"
$inventario = Find-PhaseLike "Operaciones" "Inventario"
$caja = Find-PhaseLike "Futuro" "Caja"
$contabilidad = Find-PhaseLike "Futuro" "Contabilidad"

$coreModulesList = @($ventas, $compras, $inventario, $caja, $contabilidad)
$corePct = [math]::Round((($coreModulesList | Measure-Object -Property pct -Average).Average))

$dbPct = 0
if($stages.Count -gt 0) { $dbPct = (Calc-Stage $stages[0]).pct }

$webPctSources = @()
foreach($idx in 1, 2, 3)
{
    if($idx -lt $stages.Count)
    {
        $webPctSources += (Calc-Stage $stages[$idx]).pct
    }
}

$webPct = 0
if($webPctSources.Count -gt 0)
{
    $webPct = [math]::Round((($webPctSources | Measure-Object -Average).Average))
}


Write-Host "Architecture diagram (replicated from PROGRESS.html's own formulas): Web=$webPct% Core=$corePct% DB=$dbPct%"


# -----------------------------------------------------------------------
# Layers -- extraidas literalmente de las cajas del diagrama de
# arquitectura en el HTML estatico (<div class="abox-title">...). Las que
# tienen un "0%" LITERAL en el HTML se marcan not_started (dato real). Las
# que dependen del calculo (id="arch-*") se resuelven arriba con las
# mismas formulas reales; ninguna se inventa.
# -----------------------------------------------------------------------

$layers = @(
    [PSCustomObject]@{ id = "web"; label = "Web ERP"; pct = $webPct; status = "computed"; note = "React SPA -- promedio de las etapas Core ERP + Operaciones + Fiscal/SRI" }
    [PSCustomObject]@{ id = "mobile"; label = "Mobile App"; pct = 0; status = "not_started"; note = "0% literal en PROGRESS.html -- 'Futuro'" }
    [PSCustomObject]@{ id = "chat"; label = "Chat Interface"; pct = 0; status = "not_started"; note = "0% literal en PROGRESS.html -- 'Futuro'" }
    [PSCustomObject]@{ id = "intelligence"; label = "ERP Intelligence Layer"; pct = 0; status = "not_started"; note = "0% literal en PROGRESS.html -- 'Futuro'" }
    [PSCustomObject]@{ id = "ai-assistant"; label = "User Assistant AI"; pct = 0; status = "not_started"; note = "0% literal en PROGRESS.html -- 'Futuro'" }
    [PSCustomObject]@{ id = "ai-analyst"; label = "Business Analyst AI"; pct = 0; status = "not_started"; note = "0% literal en PROGRESS.html -- 'Futuro'" }
    [PSCustomObject]@{ id = "ai-automation"; label = "Process Automation AI"; pct = 0; status = "not_started"; note = "0% literal en PROGRESS.html -- 'Futuro'" }
    [PSCustomObject]@{ id = "core"; label = "ERP Core Services"; pct = $corePct; status = "computed"; note = "Promedio de Ventas/Compras/Inventario/Caja/Contabilidad" }
    [PSCustomObject]@{ id = "db"; label = "Base de Datos"; pct = $dbPct; status = "computed"; note = "PostgreSQL -- progreso de la etapa Infraestructura" }
    [PSCustomObject]@{ id = "data-warehouse"; label = "Data Warehouse"; pct = 0; status = "not_started"; note = "0% literal en PROGRESS.html -- 'Futuro'" }
    [PSCustomObject]@{ id = "ai-advanced"; label = "IA Modelos Avanzados"; pct = 0; status = "not_started"; note = "0% literal en PROGRESS.html -- 'Futuro'" }
)


$coreModules = @(
    [PSCustomObject]@{ name = "Ventas"; pct = $ventas.pct }
    [PSCustomObject]@{ name = "Compras"; pct = $compras.pct }
    [PSCustomObject]@{ name = "Inventario"; pct = $inventario.pct }
    [PSCustomObject]@{ name = "Caja"; pct = $caja.pct }
    [PSCustomObject]@{ name = "Contabilidad"; pct = $contabilidad.pct }
)


# -----------------------------------------------------------------------
# Components -- cada fase real es un "componente" arquitectonico con su
# progreso real calculado
# -----------------------------------------------------------------------

$components = @()

foreach($stage in $stages)
{
    foreach($phase in $stage.phases)
    {
        $c = Calc-Phase $phase

        $components +=
        [PSCustomObject]@{
            stage       = $stage.name
            name        = $phase.name
            description = $phase.desc
            totalTasks  = $c.total
            done        = $c.done
            pct         = $c.pct
        }
    }
}


# -----------------------------------------------------------------------
# completed / pending / nextSteps -- derivados de status real (d/f/n).
# "nextSteps" = tareas "n" cuya fase ya tiene progreso real (pct > 0) --
# es decir, lo inmediato siguiente dentro de trabajo ya iniciado.
# "pending" = tareas "n" cuya fase todavia esta en 0% (no iniciada).
# Distincion basada 100% en los mismos numeros reales de arriba.
# -----------------------------------------------------------------------

$completed = @()
$pending = @()
$nextSteps = @()

foreach($stage in $stages)
{
    foreach($phase in $stage.phases)
    {
        $phasePct = (Calc-Phase $phase).pct

        foreach($task in $phase.tasks)
        {
            $entry =
            [PSCustomObject]@{
                stage = $stage.name
                phase = $phase.name
                name  = $task.name
                status = $task.status
            }

            if($task.status -eq "d" -or $task.status -eq "f")
            {
                $completed += $entry
            }
            elseif($task.status -eq "n")
            {
                if($phasePct -gt 0)
                {
                    $nextSteps += $entry
                }
                else
                {
                    $pending += $entry
                }
            }
        }
    }
}


Write-Host "Completed: $($completed.Count) | Pending (not started phases): $($pending.Count) | Next steps (in-progress phases): $($nextSteps.Count)"


# -----------------------------------------------------------------------
# possibleBlockers -- HEURISTICO, no un campo real del modelo (el modelo
# no tiene un status "blocked" entre d/f/p/n). Busca en las DESCRIPCIONES
# reales de las tareas formas de "bloqueado/bloqueante/bloqueo(s)" que NO
# esten negadas ("no bloqueante", "sin bloqueos"). Se documenta como
# heuristico en el propio campo -- nunca se presenta como dato autoritativo.
# -----------------------------------------------------------------------

$blockerRegex = [regex]'(?i)bloque(ad[oa]s?|antes?|os?)\b'
$negationRegex = [regex]'(?i)(no|sin)\s+bloque'

$possibleBlockers = @()

foreach($task in $allTasksFlat)
{
    if($blockerRegex.IsMatch($task.description) -and -not $negationRegex.IsMatch($task.description))
    {
        $possibleBlockers +=
        [PSCustomObject]@{
            stage       = $task.stageName
            phase       = $task.phaseName
            name        = $task.name
            status      = $task.status
            excerpt     = $task.description
        }
    }
}


Write-Host "Possible blockers (heuristic, real text matches): $($possibleBlockers.Count)"


# -----------------------------------------------------------------------
# stages summary (con phases anidadas, cada una con su status letra
# calculada igual que phaseStatus() en PROGRESS.html)
# -----------------------------------------------------------------------

function Get-PhaseStatusLetter($phase)
{
    $hasN = $false
    $hasP = $false
    $allDone = $true

    foreach($task in $phase.tasks)
    {
        if($task.status -eq "n") { $hasN = $true }
        if($task.status -eq "p") { $hasP = $true }
        if($task.status -ne "d" -and $task.status -ne "f") { $allDone = $false }
    }

    $hasFrozen = @($phase.tasks | Where-Object { $_.status -eq "f" }).Count -gt 0

    if($allDone -and $hasFrozen) { return "f" }
    if($allDone) { return "d" }

    return "p"
}


$stagesOutput = @()

foreach($stage in $stages)
{
    $stageCalc = Calc-Stage $stage

    $phasesOutput = @()

    foreach($phase in $stage.phases)
    {
        $phaseCalc = Calc-Phase $phase

        $phasesOutput +=
        [PSCustomObject]@{
            name       = $phase.name
            description = $phase.desc
            totalTasks = $phaseCalc.total
            done       = $phaseCalc.done
            pct        = $phaseCalc.pct
            statusLetter = Get-PhaseStatusLetter $phase
        }
    }

    $stagesOutput +=
    [PSCustomObject]@{
        name       = $stage.name
        totalTasks = $stageCalc.total
        done       = $stageCalc.done
        pct        = $stageCalc.pct
        phases     = $phasesOutput
    }
}


# -----------------------------------------------------------------------
# Output
# -----------------------------------------------------------------------

$output =
[PSCustomObject]@{
    source            = "docs/ProgressDashboard/data/architecture-progress-source.json"
    generated         = (Get-Date -Format "yyyy-MM-dd HH:mm:ss")
    extractionMethod  = "Direct load of architecture-progress-source.json (structured data, no HTML parsing), replicating PROGRESS.html's own calcPhase/calcStage/calcGlobal/countByStatus/findPhase formulas. No JavaScript executed; no data fabricated."
    global            = [PSCustomObject]@{ totalTasks = $globalTotal; done = $globalDone; pct = $globalPct }
    statusCounts      = $statusCounts
    layers            = $layers
    coreModules       = $coreModules
    stages            = $stagesOutput
    components        = $components
    completed         = $completed
    pending           = $pending
    nextSteps         = $nextSteps
    possibleBlockers  = $possibleBlockers
    blockedFieldNote  = "The data model has no explicit 'blocked' status (only d/f/p/n). 'possibleBlockers' above is a heuristic keyword match over real task descriptions, not an authoritative field."
}


$output |
    ConvertTo-Json -Depth 8 |
    Out-File $OutputFile -Encoding utf8


Write-Host ""
Write-Host "architecture-progress.json generated successfully." -ForegroundColor Green
Write-Host $OutputFile
