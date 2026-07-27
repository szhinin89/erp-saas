# =============================================================================
# ZH Technologies
# Dashboard Data Pipeline -- Fase Dashboard 9.0
#
# Pipeline UNICO para generar/validar TODAS las fuentes de datos que consume
# tools/dashboard/render-dashboard.ps1. Reemplaza la nocion de "correr los
# analizadores a mano" por un solo comando con validacion real.
#
# Investigacion previa (ver seccion "Hallazgos" en la entrega de esta fase):
#   - 29 de 32 archivos JSON consumidos por render-dashboard.ps1 SI tienen un
#     generador automatizado real (los analyze-*.ps1 / build-dashboard-v12.ps1
#     / health-score.ps1 / calculate-engineering-score.ps1 / quality-gate.ps1 /
#     Manage-EngineeringHistory.ps1 / validate-dashboard-model.ps1 ya
#     existentes -- MISMO orden que tools/dashboard/run-dashboard-final.ps1,
#     sin duplicados detectados entre ellos).
#   - 3 archivos (erp.json, layers.json, domains.json) son DATOS SEMILLA
#     estaticos sin generador POR DISENO (ERP metadata / 7 capas / 11 dominios
#     de negocio) -- ningun analyze-*.ps1 los produce, todos los consumen como
#     insumo. Se validan, nunca se regeneran aqui.
#   - 5 archivos (modules-status.json, roadmap.json, blockers.json,
#     architecture-governance.json, architecture-dependencies.json) son
#     MANTENIDOS MANUALMENTE por diseno (Fases Dashboard 3.0/4.0/5.0/6.0) --
#     su propio campo "method" interno ya declara "Revision manual, no
#     analizador automatizado". Escribir un generador automatico para estos
#     violaria el proposito explicito de esas fases (evitar que una heuristica
#     reinvente investigacion ya citada contra CLAUDE.md/STATUS.md/ROADMAP.md/
#     ADRs). Se validan, nunca se regeneran aqui -- si falta alguno, el
#     pipeline lo reporta como error y exige creacion manual.
#   - 2 scripts obsoletos detectados: analyze-api.ps1 y analyze-docs.ps1 (ya
#     retirados del pipeline segun memoria de sesiones previas -- confirmado:
#     no aparecen en run-dashboard-final.ps1 y sus salidas, api-analysis.json/
#     docs-analysis.json, no son consumidas por render-dashboard.ps1). No se
#     tocan en esta fase (la tarea es solo automatizar generacion, no limpiar
#     scripts) -- se dejan documentados aqui para una futura decision.
#   - 0 scripts duplicados detectados entre los generadores activos (cada uno
#     escribe un archivo de salida distinto; dependency-analysis.json y
#     dependencies.json son archivos DISTINTOS de analizadores DISTINTOS,
#     confirmado, no es una duplicacion).
# =============================================================================

$ErrorActionPreference = "Stop"

$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$DataRoot = Join-Path $ProjectRoot "docs\ProgressDashboard\data"

Write-Host ""
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host " ZH Dashboard Data Pipeline (Fase 9.0)" -ForegroundColor Cyan
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host ""

$pipelineStopwatch = [System.Diagnostics.Stopwatch]::StartNew()

# -----------------------------------------------------------------------------
# 0) Fuente manual requerida por analyze-progress-map.ps1 (FASE DASHBOARD 13.0)
#    -- docs/ProgressDashboard/data/architecture-progress-source.json es el
#    unico lugar donde se mantiene a mano el progreso manual (reemplaza al
#    antiguo array embebido en PROGRESS.html). analyze-progress-map.ps1 ya no
#    parsea PROGRESS.html: lee este JSON. Se valida ANTES de correr los
#    generadores porque analyze-progress-map.ps1 depende de que exista.
# -----------------------------------------------------------------------------
$progressSourceFile = "architecture-progress-source.json"

Write-Host "--- Fuente manual de progreso (prerequisito de analyze-progress-map.ps1) ---" -ForegroundColor Cyan
Write-Host ""

function Test-JsonFileValidPrereq($path)
{
    if(-not (Test-Path $path)) { return @{ Ok = $false; Reason = "No existe" } }
    $info = Get-Item $path
    if($info.Length -eq 0) { return @{ Ok = $false; Reason = "Archivo vacio (0 bytes)" } }
    $raw = Get-Content $path -Raw
    if([string]::IsNullOrWhiteSpace($raw)) { return @{ Ok = $false; Reason = "Contenido en blanco" } }
    try
    {
        $parsed = $raw | ConvertFrom-Json
    }
    catch
    {
        return @{ Ok = $false; Reason = "JSON invalido: $($_.Exception.Message)" }
    }
    if($parsed -is [System.Array] -and $parsed.Count -eq 0) { return @{ Ok = $false; Reason = "Array JSON vacio ([])" } }
    return @{ Ok = $true; Reason = "OK" }
}

$results = New-Object System.Collections.Generic.List[object]
$pipelineFailed = $false

$progressSourcePath = Join-Path $DataRoot $progressSourceFile
$progressSourceValidation = Test-JsonFileValidPrereq $progressSourcePath

$results.Add([ordered]@{
    Script = "(fuente manual, prerequisito -- sin generador por diseno)"
    Output = $progressSourceFile
    Tipo = "Fuente manual (FASE 13.0)"
    TiempoMs = 0
    Estado = if($progressSourceValidation.Ok) { "OK" } else { "FAIL" }
    Detalle = $progressSourceValidation.Reason
})

if($progressSourceValidation.Ok)
{
    Write-Host "  OK -> $progressSourceFile" -ForegroundColor Green
}
else
{
    Write-Host "  FALLO: $progressSourceFile -> $($progressSourceValidation.Reason) (fuente manual requerida por analyze-progress-map.ps1 -- requiere creacion manual, no se puede regenerar aqui)" -ForegroundColor Red
    $pipelineFailed = $true
}

if($pipelineFailed)
{
    Write-Host ""
    Write-Host "==============================================" -ForegroundColor Red
    Write-Host " Pipeline DETENIDO -- falta la fuente manual de progreso" -ForegroundColor Red
    Write-Host "==============================================" -ForegroundColor Red
    Write-Host ""
    throw "build-dashboard-data.ps1: pipeline detenido -- $progressSourceFile no existe o es invalido. Ver detalle arriba."
}

# -----------------------------------------------------------------------------
# 1) Generadores automatizados reales -- mismo orden que run-dashboard-final.ps1
#    (sin render-dashboard.ps1, que no genera datos, solo los consume).
# -----------------------------------------------------------------------------
$generators = @(
    @{ Script = "analyze-backend.ps1"; Output = "backend-analysis.json" },
    @{ Script = "analyze-frontend.ps1"; Output = "frontend-analysis.json" },
    @{ Script = "analyze-tests.ps1"; Output = "tests-analysis.json" },
    @{ Script = "analyze-architecture.ps1"; Output = "architecture-analysis.json" },
    @{ Script = "analyze-dependencies.ps1"; Output = "dependency-analysis.json" },
    @{ Script = "analyze-database.ps1"; Output = "database-analysis.json" },
    @{ Script = "analyze-migrations.ps1"; Output = "migration-analysis.json" },
    @{ Script = "analyze-technical-debt.ps1"; Output = "technical-debt.json" },
    @{ Script = "analyze-security.ps1"; Output = "security-analysis.json" },
    @{ Script = "analyze-module-health.ps1"; Output = "module-health.json" },
    @{ Script = "health-score.ps1"; Output = "health-score.json" },
    @{ Script = "calculate-engineering-score.ps1"; Output = "engineering-score.json" },
    @{ Script = "Manage-EngineeringHistory.ps1"; Output = "engineering-trend.json" },
    @{ Script = "quality-gate.ps1"; Output = "quality-gate.json" },
    @{ Script = "build-dashboard-v12.ps1"; Output = "dashboard-model-v12.json" },
    @{ Script = "analyze-progress-map.ps1"; Output = "architecture-progress.json" },
    @{ Script = "analyze-modules.ps1"; Output = "modules.json" },
    @{ Script = "analyze-features.ps1"; Output = "features.json" },
    @{ Script = "analyze-processes.ps1"; Output = "processes.json" },
    @{ Script = "analyze-tasks.ps1"; Output = "tasks.json" },
    @{ Script = "validate-dashboard-model.ps1"; Output = "model-health.json" },
    @{ Script = "analyze-impact.ps1"; Output = "impact.json" },
    @{ Script = "analyze-completion.ps1"; Output = "completion-intelligence.json" },
    @{ Script = "analyze-module-graph.ps1"; Output = "dependencies.json" },
    @{ Script = "analyze-critical-path.ps1"; Output = "critical-path.json" },
    @{ Script = "analyze-release-simulation.ps1"; Output = "release-simulation.json" },
    @{ Script = "analyze-recommendations.ps1"; Output = "recommendations.json" },
    @{ Script = "analyze-navigation-map.ps1"; Output = "navigation-map.json" },
    @{ Script = "analyze-dashboard-summary.ps1"; Output = "dashboard-summary.json" },
    @{ Script = "analyze-explorer-index.ps1"; Output = "explorer-index.json" }
)

# -----------------------------------------------------------------------------
# 2) Datos semilla estaticos -- sin generador por diseno, solo se validan.
# -----------------------------------------------------------------------------
$seedFiles = @("erp.json", "layers.json", "domains.json")

# -----------------------------------------------------------------------------
# 3) Archivos mantenidos manualmente -- sin generador por diseno (Fases 3.0/
#    4.0/5.0/6.0/14.0), solo se validan. Si falta alguno, es un error real:
#    nadie puede regenerar contenido investigado/citado con una heuristica.
#    erp-closure.json (Fase Dashboard 14.0) es una reestructuracion a JSON de
#    la Auditoria Tecnica del ERP (FASE ERP 4.0) y su Plan Maestro (FASE ERP
#    4.1) -- mismo patron que blockers.json, no un analizador de codigo.
# -----------------------------------------------------------------------------
$manualFiles = @("modules-status.json", "roadmap.json", "blockers.json", "architecture-governance.json", "architecture-dependencies.json", "erp-closure.json")

function Test-JsonFileValid($path)
{
    if(-not (Test-Path $path)) { return @{ Ok = $false; Reason = "No existe" } }
    $info = Get-Item $path
    if($info.Length -eq 0) { return @{ Ok = $false; Reason = "Archivo vacio (0 bytes)" } }
    $raw = Get-Content $path -Raw
    if([string]::IsNullOrWhiteSpace($raw)) { return @{ Ok = $false; Reason = "Contenido en blanco" } }
    try
    {
        $parsed = $raw | ConvertFrom-Json
    }
    catch
    {
        return @{ Ok = $false; Reason = "JSON invalido: $($_.Exception.Message)" }
    }
    # "Vacio" tambien cubre un objeto/array real pero sin ningun dato util
    # ({} o [] literales) -- distinto de 0 bytes, pero igual de inutil para el render.
    if($parsed -is [System.Array] -and $parsed.Count -eq 0) { return @{ Ok = $false; Reason = "Array JSON vacio ([])" } }
    if($parsed -is [System.Management.Automation.PSCustomObject])
    {
        $propCount = (@($parsed.PSObject.Properties)).Count
        if($propCount -eq 0) { return @{ Ok = $false; Reason = "Objeto JSON vacio ({})" } }
    }
    return @{ Ok = $true; Reason = "OK" }
}

Write-Host "--- Generadores automatizados ($($generators.Count)) ---" -ForegroundColor Cyan
Write-Host ""

foreach($gen in $generators)
{
    $scriptPath = Join-Path $PSScriptRoot $gen.Script
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $status = "OK"
    $errorMessage = $null

    Write-Host "Ejecutando $($gen.Script)..." -ForegroundColor Yellow

    try
    {
        powershell -ExecutionPolicy Bypass -File $scriptPath
        if($LASTEXITCODE -ne 0) { throw "Exit code $LASTEXITCODE" }
    }
    catch
    {
        $status = "FAIL"
        $errorMessage = $_.Exception.Message
    }

    $sw.Stop()

    $outputPath = Join-Path $DataRoot $gen.Output
    $validation = if($status -eq "OK") { Test-JsonFileValid $outputPath } else { @{ Ok = $false; Reason = "Generador fallo antes de escribir salida" } }

    $results.Add([ordered]@{
        Script = $gen.Script
        Output = $gen.Output
        Tipo = "Generador automatizado"
        TiempoMs = $sw.ElapsedMilliseconds
        Estado = if($status -eq "OK" -and $validation.Ok) { "OK" } else { "FAIL" }
        Detalle = if($status -eq "FAIL") { "Script fallo: $errorMessage" } else { $validation.Reason }
    })

    if($status -eq "FAIL" -or -not $validation.Ok)
    {
        Write-Host "  FALLO: $($gen.Script) -> $(if($status -eq 'FAIL'){$errorMessage}else{$validation.Reason})" -ForegroundColor Red
        $pipelineFailed = $true
        break
    }
    else
    {
        Write-Host "  OK ($($sw.ElapsedMilliseconds) ms) -> $($gen.Output)" -ForegroundColor Green
    }
}

if($pipelineFailed)
{
    Write-Host ""
    Write-Host "==============================================" -ForegroundColor Red
    Write-Host " Pipeline DETENIDO -- un generador fallo" -ForegroundColor Red
    Write-Host "==============================================" -ForegroundColor Red
    Write-Host ""
    $results | ForEach-Object { Write-Host "$($_.Estado.PadRight(6)) $($_.Script)" -ForegroundColor $(if($_.Estado -eq 'OK'){'Green'}else{'Red'}) }
    throw "build-dashboard-data.ps1: pipeline detenido por fallo en un generador. Ver detalle arriba."
}

Write-Host ""
Write-Host "--- Datos semilla estaticos ($($seedFiles.Count)) -- sin generador por diseno, solo validacion ---" -ForegroundColor Cyan
Write-Host ""

foreach($seed in $seedFiles)
{
    $path = Join-Path $DataRoot $seed
    $validation = Test-JsonFileValid $path
    $results.Add([ordered]@{
        Script = "(dato semilla, sin generador)"
        Output = $seed
        Tipo = "Semilla estatica"
        TiempoMs = 0
        Estado = if($validation.Ok) { "OK" } else { "FAIL" }
        Detalle = $validation.Reason
    })
    if($validation.Ok) { Write-Host "  OK -> $seed" -ForegroundColor Green }
    else
    {
        Write-Host "  FALLO: $seed -> $($validation.Reason) (dato semilla sin generador -- requiere creacion manual, no se puede regenerar aqui)" -ForegroundColor Red
        $pipelineFailed = $true
    }
}

Write-Host ""
Write-Host "--- Archivos mantenidos manualmente ($($manualFiles.Count)) -- sin generador por diseno, solo validacion ---" -ForegroundColor Cyan
Write-Host ""

foreach($manual in $manualFiles)
{
    $path = Join-Path $DataRoot $manual
    $validation = Test-JsonFileValid $path
    $results.Add([ordered]@{
        Script = "(mantenido manualmente, sin generador)"
        Output = $manual
        Tipo = "Manual (Fases 3.0/4.0/5.0/6.0)"
        TiempoMs = 0
        Estado = if($validation.Ok) { "OK" } else { "FAIL" }
        Detalle = $validation.Reason
    })
    if($validation.Ok) { Write-Host "  OK -> $manual" -ForegroundColor Green }
    else
    {
        Write-Host "  FALLO: $manual -> $($validation.Reason) (archivo de investigacion manual -- requiere que un agente/persona lo cree citando evidencia real, este pipeline no lo inventa)" -ForegroundColor Red
        $pipelineFailed = $true
    }
}

# -----------------------------------------------------------------------------
# Fase Dashboard 11.0 -- Quality Gate, se ejecuta AL FINAL del pipeline (una
# vez que todos los generadores/semillas/archivos manuales ya fueron
# validados arriba). Si detecta al menos un error critico de calidad,
# detiene el pipeline completo -- no genera datos, solo valida.
# -----------------------------------------------------------------------------
Write-Host ""
Write-Host "--- Quality Gate (validate-dashboard.ps1) ---" -ForegroundColor Cyan
Write-Host ""

$qgSw = [System.Diagnostics.Stopwatch]::StartNew()
$qgStatus = "OK"
$qgError = $null
try
{
    powershell -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot "validate-dashboard.ps1")
    if($LASTEXITCODE -ne 0) { throw "Exit code $LASTEXITCODE" }
}
catch
{
    $qgStatus = "FAIL"
    $qgError = $_.Exception.Message
}
$qgSw.Stop()

$results.Add([ordered]@{
    Script = "validate-dashboard.ps1"
    Output = "dashboard-validation.json"
    Tipo = "Quality Gate"
    TiempoMs = $qgSw.ElapsedMilliseconds
    Estado = $qgStatus
    Detalle = if($qgStatus -eq "FAIL") { "Quality Gate detecto error(es) critico(s): $qgError" } else { "OK" }
})

if($qgStatus -eq "FAIL") { $pipelineFailed = $true }

$pipelineStopwatch.Stop()

Write-Host ""
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host " Resumen final" -ForegroundColor Cyan
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host ""

$okCount = (@($results | Where-Object { $_.Estado -eq "OK" })).Count
$failCount = (@($results | Where-Object { $_.Estado -eq "FAIL" })).Count

foreach($r in $results)
{
    $color = if($r.Estado -eq "OK") { "Green" } else { "Red" }
    Write-Host "$($r.Estado.PadRight(6)) $($r.Output.PadRight(32)) $($r.Tipo.PadRight(28)) $($r.TiempoMs) ms" -ForegroundColor $color
}

Write-Host ""
Write-Host "Total archivos validados: $($results.Count) ($okCount OK, $failCount con problemas)" -ForegroundColor $(if($failCount -eq 0){"Green"}else{"Red"})
Write-Host "Tiempo total del pipeline: $($pipelineStopwatch.ElapsedMilliseconds) ms ($([math]::Round($pipelineStopwatch.Elapsed.TotalSeconds, 1)) s)"
Write-Host ""

if($failCount -gt 0)
{
    throw "build-dashboard-data.ps1: $failCount archivo(s)/paso(s) invalido(s) o con error(es) critico(s) tras ejecutar el pipeline. Ver detalle arriba."
}

Write-Host "Pipeline completado exitosamente -- todas las fuentes de datos existen, no estan vacias, son JSON valido y pasaron el Quality Gate." -ForegroundColor Green
