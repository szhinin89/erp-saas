# =============================================================================
# ZH Technologies
# analyze-progress-map.ps1
#
# Lee PROGRESS.html (mapa maestro de arquitectura, en la raiz del repo) EN
# MODO SOLO LECTURA y extrae su estructura real hacia
# docs/ProgressDashboard/data/architecture-progress.json.
#
# PROGRESS.html no es HTML estatico: su verdadera fuente de datos es el
# array JS `const D = [...]` embebido (linea ~360), que el propio archivo
# usa en tiempo de ejecucion (funciones taskWeight/calcPhase/calcStage/
# calcGlobal/countByStatus/findPhase) para calcular los porcentajes que
# se ven en el diagrama de arquitectura y las tarjetas de etapa.
#
# Este script NO ejecuta JavaScript: parsea el array D con expresiones
# regulares (formato consistente, generado a mano por el propio equipo) y
# REPLICA exactamente los mismos calculos que el HTML ya hace en el
# navegador (mismos pesos d/f=1, p=0.5, n=0; mismas formulas de porcentaje;
# mismo lookup findPhase() para los 5 modulos core y los 3 stages que
# alimentan el "Web ERP %"). Ningun numero se inventa: todo sale de tareas
# reales con su status real (d/f/p/n) tal como estan escritas en el archivo.
#
# Si un dato no existe en PROGRESS.html (ej. no hay un estado "blocked"
# explicito en el modelo real: solo d/f/p/n), este script NO LO INVENTA.
# Ver seccion "possibleBlockers" mas abajo: es un hallazgo heuristico sobre
# texto real, marcado explicitamente como tal, nunca como campo autoritativo.
#
# NO modifica PROGRESS.html. NO modifica el ERP. NO modifica ningun otro
# JSON existente en docs/ProgressDashboard/data/.
# =============================================================================

$ErrorActionPreference = "Stop"


$ProjectRoot =
(Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path


$ProgressHtmlPath =
Join-Path $ProjectRoot "PROGRESS.html"


$OutputFile =
Join-Path $ProjectRoot "docs\ProgressDashboard\data\architecture-progress.json"



Write-Host ""
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host " analyze-progress-map.ps1"
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host ""


if(!(Test-Path $ProgressHtmlPath))
{
    throw "PROGRESS.html not found at: $ProgressHtmlPath"
}


$html = Get-Content $ProgressHtmlPath -Raw -Encoding UTF8


function Decode-Entities($text)
{
    if($null -eq $text) { return "" }

    return $text -replace "&amp;","&" -replace "&lt;","<" -replace "&gt;",">"
}


# -----------------------------------------------------------------------
# Extraer el bloque "const D = [ ... ];" -- unica fuente real de datos
# -----------------------------------------------------------------------

$dBlockMatch = [regex]::Match($html, "(?s)const D = \[(.*?)\r?\n\];")

if(-not $dBlockMatch.Success)
{
    throw "Could not locate 'const D = [ ... ];' data block in PROGRESS.html -- extraction aborted (no fabricated fallback)."
}

$dBlock = $dBlockMatch.Groups[1].Value
$dBlockOffset = $dBlockMatch.Groups[1].Index


Write-Host "Data block located ($($dBlock.Length) chars)" -ForegroundColor Green


# -----------------------------------------------------------------------
# Regex de extraccion -- formato real y consistente del array D
# -----------------------------------------------------------------------

$stageRegex = [regex]'\{stage:"((?:[^"\\]|\\.)*)"'
$phaseRegex = [regex]'\{name:"((?:[^"\\]|\\.)*)",icon:"(?:[^"\\]|\\.)*",desc:"((?:[^"\\]|\\.)*)",groups:\['
$taskRegex  = [regex]'\{s:"([dfpn])",n:"((?:[^"\\]|\\.)*)",d:"((?:[^"\\]|\\.)*)"'

$stageMatches = $stageRegex.Matches($dBlock)
$phaseMatches = $phaseRegex.Matches($dBlock)
$taskMatches  = $taskRegex.Matches($dBlock)


Write-Host "Parsed: $($stageMatches.Count) stages, $($phaseMatches.Count) phases, $($taskMatches.Count) tasks"


if($stageMatches.Count -eq 0 -or $taskMatches.Count -eq 0)
{
    throw "Parsed zero stages/tasks from PROGRESS.html -- data model shape may have changed. Aborting rather than emitting an empty/fabricated result."
}


# -----------------------------------------------------------------------
# Asignar cada fase a su etapa contenedora, y cada tarea a su fase
# contenedora, por posicion en el texto (el array esta estrictamente
# anidado en orden de aparicion: stage -> phases -> groups -> tasks).
# -----------------------------------------------------------------------

function Find-ContainerIndex($position, $containerMatches)
{
    $result = -1

    for($i = 0; $i -lt $containerMatches.Count; $i++)
    {
        if($containerMatches[$i].Index -le $position)
        {
            $result = $i
        }
        else
        {
            break
        }
    }

    return $result
}


$stages = @()

for($i = 0; $i -lt $stageMatches.Count; $i++)
{
    $stages += [PSCustomObject]@{
        index  = $i
        name   = Decode-Entities $stageMatches[$i].Groups[1].Value
        phases = @()
    }
}


$phaseToStage = @{}

for($i = 0; $i -lt $phaseMatches.Count; $i++)
{
    $stageIdx = Find-ContainerIndex $phaseMatches[$i].Index $stageMatches
    $phaseToStage[$i] = $stageIdx

    $phaseObj = [PSCustomObject]@{
        index      = $i
        stageIndex = $stageIdx
        name       = Decode-Entities $phaseMatches[$i].Groups[1].Value
        desc       = Decode-Entities $phaseMatches[$i].Groups[2].Value
        tasks      = @()
    }

    if($stageIdx -ge 0)
    {
        $stages[$stageIdx].phases += $phaseObj
    }
}


$taskWeight = @{ d = 1.0; f = 1.0; p = 0.5; n = 0.0 }
$allTasksFlat = @()

foreach($taskMatch in $taskMatches)
{
    $phaseIdx = Find-ContainerIndex $taskMatch.Index $phaseMatches

    if($phaseIdx -lt 0) { continue }

    $status = $taskMatch.Groups[1].Value
    $name = Decode-Entities $taskMatch.Groups[2].Value
    $desc = Decode-Entities $taskMatch.Groups[3].Value

    $stageIdx = $phaseToStage[$phaseIdx]

    $taskObj = [PSCustomObject]@{
        status      = $status
        name        = $name
        description = $desc
        stageName   = $stages[$stageIdx].name
        phaseName   = $stages[$stageIdx].phases | Where-Object { $_.index -eq $phaseIdx } | Select-Object -First 1 -ExpandProperty name
    }

    $allTasksFlat += $taskObj

    ($stages[$stageIdx].phases | Where-Object { $_.index -eq $phaseIdx } | Select-Object -First 1).tasks += $taskObj
}


# -----------------------------------------------------------------------
# Replica exacta de calcPhase / calcStage / calcGlobal / countByStatus
# (mismas formulas que PROGRESS.html usa en su propio JS)
# -----------------------------------------------------------------------

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
# que dependen del calculo JS (id="arch-*") se resuelven arriba con las
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
# possibleBlockers -- HEURISTICO, no un campo real del modelo (PROGRESS.html
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
    source            = "PROGRESS.html"
    generated         = (Get-Date -Format "yyyy-MM-dd HH:mm:ss")
    extractionMethod  = "Regex parsing of the embedded 'const D = [...]' JS data model, replicating PROGRESS.html's own calcPhase/calcStage/calcGlobal/countByStatus/findPhase formulas. No JavaScript executed; no data fabricated."
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
    blockedFieldNote  = "PROGRESS.html's real data model has no explicit 'blocked' status (only d/f/p/n). 'possibleBlockers' above is a heuristic keyword match over real task descriptions, not an authoritative field."
}


$output |
    ConvertTo-Json -Depth 8 |
    Out-File $OutputFile -Encoding utf8


Write-Host ""
Write-Host "architecture-progress.json generated successfully." -ForegroundColor Green
Write-Host $OutputFile
