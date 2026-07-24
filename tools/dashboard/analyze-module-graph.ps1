# =============================================================================
# ZH Technologies
# Module Dependency Graph Analyzer
#
# NOTA DE NOMBRE: ya existe tools/dashboard/analyze-dependencies.ps1 (detector
# de dependencias externas prohibidas -> dependency-analysis.json, consumido
# por calculate-engineering-score.ps1). Ese script NO se modifica. Este es un
# analizador nuevo y distinto: construye el GRAFO de dependencias entre
# MODULOS del ERP (Sales -> Inventory, etc.), algo que ningun analizador
# existente calcula. Su salida es dependencies.json (nombre pedido por el
# usuario), un archivo que no existia antes.
#
# Evidencia: escaneo real de "using ERP.(Application|Domain|Infrastructure)."
# en los archivos .cs de cada modulo (backend/src/ERP.Application/Modules/*,
# ERP.Domain/Modules/*, ERP.Infrastructure/*). Un modulo objetivo solo cuenta
# como dependencia si su nombre coincide EXACTAMENTE con un id real de
# modules.json -- ninguna relacion se infiere o se inventa.
#
# Bus Factor: conteo de autores distintos de git (git log --format=%an) sobre
# las carpetas reales de cada modulo -- evidencia de control de versiones, no
# una estimacion.
# =============================================================================

$ErrorActionPreference = "Stop"

$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$BackendRoot = Join-Path $ProjectRoot "backend\src"
$DataRoot = Join-Path $ProjectRoot "docs\ProgressDashboard\data"
$Output = Join-Path $DataRoot "dependencies.json"

Write-Host ""
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host " Module Dependency Graph Analyzer"
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host ""

function LoadJson($file)
{
    $path = Join-Path $DataRoot $file
    if(!(Test-Path $path)) { throw "Missing file: $path" }
    return (Get-Content $path -Raw | ConvertFrom-Json)
}

$modulesData = @(LoadJson "modules.json")
$moduleIds = @($modulesData | ForEach-Object { $_.id })
$moduleIdSet = @{}
foreach($id in $moduleIds) { $moduleIdSet[$id] = $true }

Write-Host "Modules known from modules.json: $($moduleIds.Count)"


# =============================================================================
# 1. Locate real folders per module (Application / Domain / Infrastructure)
# =============================================================================

$moduleFolders = @{}

foreach($id in $moduleIds)
{
    $folders = @()
    foreach($project in @("ERP.Application\Modules", "ERP.Domain\Modules", "ERP.Infrastructure"))
    {
        $candidate = Join-Path $BackendRoot (Join-Path $project $id)
        if(Test-Path $candidate) { $folders += $candidate }
    }
    $moduleFolders[$id] = $folders
}


# =============================================================================
# 2. Scan real .cs files per module, extract using ERP.* references
# =============================================================================

$usingPattern = 'using\s+ERP\.(Application|Domain|Infrastructure)\.(?:Modules\.)?(\w+)'

$edgesByPair = @{}
$fileCountByModule = @{}
$intraRefCount = @{}
$totalRefCount = @{}

foreach($id in $moduleIds)
{
    $files = @()
    foreach($folder in $moduleFolders[$id])
    {
        $files += @(Get-ChildItem -Path $folder -Recurse -Filter "*.cs" -File -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' })
    }

    $fileCountByModule[$id] = $files.Count
    $intraRefCount[$id] = 0
    $totalRefCount[$id] = 0

    foreach($file in $files)
    {
        $lines = Get-Content $file.FullName -ErrorAction SilentlyContinue
        foreach($line in $lines)
        {
            $m = [regex]::Match($line, $usingPattern)
            if(!$m.Success) { continue }

            $targetModule = $m.Groups[2].Value
            if(-not $moduleIdSet.ContainsKey($targetModule)) { continue }

            $totalRefCount[$id]++

            if($targetModule -eq $id)
            {
                $intraRefCount[$id]++
                continue
            }

            $relativePath = $file.FullName.Replace($ProjectRoot, "").TrimStart("\", "/").Replace("\", "/")
            $pairKey = "$id||$targetModule"

            if(-not $edgesByPair.ContainsKey($pairKey))
            {
                $edgesByPair[$pairKey] = [ordered]@{
                    from = $id
                    to = $targetModule
                    referenceCount = 0
                    evidence = New-Object System.Collections.Generic.List[string]
                }
            }

            $edgesByPair[$pairKey].referenceCount++
            if($edgesByPair[$pairKey].evidence.Count -lt 5)
            {
                $edgesByPair[$pairKey].evidence.Add($relativePath)
            }
        }
    }
}

$edges = @($edgesByPair.Values | ForEach-Object {
    [ordered]@{
        from = $_.from
        to = $_.to
        referenceCount = $_.referenceCount
        evidence = @($_.evidence)
    }
})

Write-Host "Edges discovered: $($edges.Count)"


# =============================================================================
# 3. Graph metrics: Fan-In / Fan-Out / Coupling / Instability / Cohesion
# =============================================================================

$outgoing = @{}
$incoming = @{}
foreach($id in $moduleIds) { $outgoing[$id] = New-Object System.Collections.Generic.List[string]; $incoming[$id] = New-Object System.Collections.Generic.List[string] }

foreach($edge in $edges)
{
    $outgoing[$edge.from].Add($edge.to)
    $incoming[$edge.to].Add($edge.from)
}

function Get-BusFactor($id)
{
    $authors = New-Object System.Collections.Generic.HashSet[string]
    foreach($folder in $moduleFolders[$id])
    {
        $relFolder = $folder.Replace($ProjectRoot, "").TrimStart("\", "/")
        $gitAuthors = git -C $ProjectRoot log --format="%an" -- "$relFolder" 2>$null
        foreach($a in @($gitAuthors)) { if($a) { [void]$authors.Add($a) } }
    }
    return $authors.Count
}

$nodes = @()

foreach($id in $moduleIds)
{
    $fanOut = @($outgoing[$id] | Select-Object -Unique).Count
    $fanIn = @($incoming[$id] | Select-Object -Unique).Count
    $coupling = $fanIn + $fanOut

    $instability = 0
    if($coupling -gt 0) { $instability = [math]::Round($fanOut / $coupling, 3) }

    $cohesionApprox = $null
    if($totalRefCount[$id] -gt 0)
    {
        $cohesionApprox = [math]::Round($intraRefCount[$id] / $totalRefCount[$id], 3)
    }

    $busFactor = Get-BusFactor $id

    $nodes += [ordered]@{
        id = $id
        filesScanned = $fileCountByModule[$id]
        fanIn = $fanIn
        fanOut = $fanOut
        coupling = $coupling
        instability = $instability
        cohesionApprox = $cohesionApprox
        busFactor = $busFactor
        isolated = ($fanIn -eq 0 -and $fanOut -eq 0)
        dependsOn = @($outgoing[$id] | Select-Object -Unique | Sort-Object)
        dependedOnBy = @($incoming[$id] | Select-Object -Unique | Sort-Object)
    }
}

Write-Host "Node metrics computed for $($nodes.Count) modules"


# =============================================================================
# 4. Circular dependencies (DFS with recursion stack)
# =============================================================================

$cycles = New-Object System.Collections.Generic.List[object]
$visited = @{}
$inStack = @{}
$stackPath = New-Object System.Collections.Generic.List[string]

function Find-Cycles($node)
{
    $visited[$node] = $true
    $inStack[$node] = $true
    $stackPath.Add($node)

    foreach($next in @($outgoing[$node] | Select-Object -Unique))
    {
        if(-not $visited.ContainsKey($next))
        {
            Find-Cycles $next
        }
        elseif($inStack.ContainsKey($next) -and $inStack[$next])
        {
            $startIdx = $stackPath.IndexOf($next)
            if($startIdx -ge 0)
            {
                $cyclePath = @($stackPath.GetRange($startIdx, $stackPath.Count - $startIdx)) + @($next)
                $cycles.Add(($cyclePath -join " -> "))
            }
        }
    }

    $stackPath.RemoveAt($stackPath.Count - 1)
    $inStack[$node] = $false
}

foreach($id in $moduleIds)
{
    if(-not $visited.ContainsKey($id)) { Find-Cycles $id }
}

$uniqueCycles = @($cycles | Select-Object -Unique)

Write-Host "Circular dependencies found: $($uniqueCycles.Count)"


# =============================================================================
# 5. Dependency depth (longest outgoing chain per module, cycle-safe)
# =============================================================================

$depthMemo = @{}
$depthVisiting = @{}

function Get-Depth($id)
{
    if($depthMemo.ContainsKey($id)) { return $depthMemo[$id] }
    if($depthVisiting.ContainsKey($id)) { return 0 }

    $depthVisiting[$id] = $true
    $maxChild = 0
    foreach($dep in @($outgoing[$id] | Select-Object -Unique))
    {
        $childDepth = Get-Depth $dep
        if($childDepth -gt $maxChild) { $maxChild = $childDepth }
    }
    $depthVisiting.Remove($id)

    $result = 0
    if(@($outgoing[$id]).Count -gt 0) { $result = 1 + $maxChild }
    $depthMemo[$id] = $result
    return $result
}

$depthByModule = [ordered]@{}
foreach($id in $moduleIds) { $depthByModule[$id] = Get-Depth $id }

$maxDepth = 0
if($depthByModule.Values.Count -gt 0) { $maxDepth = ($depthByModule.Values | Measure-Object -Maximum).Maximum }

Write-Host "Max dependency depth: $maxDepth"


# =============================================================================
# 6. Central / Critical / Isolated module classification
# =============================================================================

$centralModules = @($nodes | Sort-Object -Property {$_.coupling} -Descending | Select-Object -First 5 | ForEach-Object { $_.id })
$criticalModules = @($nodes | Sort-Object -Property {$_.fanIn} -Descending | Where-Object { $_.fanIn -gt 0 } | Select-Object -First 5 | ForEach-Object { $_.id })
$isolatedModules = @($nodes | Where-Object { $_.isolated } | ForEach-Object { $_.id })

Write-Host "Central: $($centralModules -join ', ')"
Write-Host "Critical (highest fan-in): $($criticalModules -join ', ')"
Write-Host "Isolated: $($isolatedModules -join ', ')"


# =============================================================================
# Output
# =============================================================================

$result = [ordered]@{
    generated = (Get-Date -Format "yyyy-MM-dd HH:mm:ss")
    source = "Static scan of backend/src/ERP.Application/Modules, ERP.Domain/Modules, ERP.Infrastructure (using ERP.* directives) + git log authorship per module folder"
    method = [ordered]@{
        edgeDetection = "Regex match on 'using ERP.(Application|Domain|Infrastructure).(Modules.)?<Name>' per .cs line; edge kept only if <Name> matches an existing modules.json id"
        fanIn = "Count of distinct modules with an edge pointing TO this module (Afferent Coupling / Ca)"
        fanOut = "Count of distinct modules this module has an edge pointing TO (Efferent Coupling / Ce)"
        coupling = "fanIn + fanOut"
        instability = "fanOut / (fanIn + fanOut) (Robert C. Martin instability metric, 0 = fully stable, 1 = fully unstable)"
        cohesionApprox = "intra-module using references / total using ERP.* references found in the module's own files (approximation only -- not method-level cohesion)"
        busFactor = "Distinct count of git commit authors (git log --format=%an) across the module's real folders"
        dependencyDepth = "Longest outgoing dependency chain reachable from this module (cycle-safe DFS, memoized)"
    }
    nodes = $nodes
    edges = $edges
    cycles = $uniqueCycles
    depthByModule = $depthByModule
    maxDependencyDepth = $maxDepth
    centralModules = $centralModules
    criticalModules = $criticalModules
    isolatedModules = $isolatedModules
}

$result | ConvertTo-Json -Depth 10 | Out-File $Output -Encoding utf8

Write-Host ""
Write-Host "Module dependency graph generated successfully." -ForegroundColor Green
Write-Host $Output
