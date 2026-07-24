# =============================================================================
# ZH Technologies
# analyze-tasks.ps1
#
# Genera docs/ProgressDashboard/data/tasks.json (hoy vacio: "{}") a partir de
# señales YA calculadas por los analizadores existentes en
# dashboard-model-v12.json:
#   - QualityGate.Warnings   (mensajes reales generados por quality-gate.ps1)
#   - TechnicalDebt.LargeFiles (archivos reales por encima del umbral)
#   - TechnicalDebt.TODO / FIXME / HACK / NotImplemented (conteos reales)
#   - Security.SecretsFound / AnonymousDetected (con archivos de evidencia
#     reales, filtrando node_modules para no generar ruido irrelevante)
#
# No se inventa ningun pendiente: cada tarea referencia el dato real que la
# origino via "source" y, cuando aplica, "evidence" con archivos reales.
# =============================================================================

$ErrorActionPreference = "Stop"


$ProjectRoot =
(Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path


$DataRoot =
Join-Path $ProjectRoot "docs\ProgressDashboard\data"


$OutputFile =
Join-Path $DataRoot "tasks.json"



Write-Host ""
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host " analyze-tasks.ps1"
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



$model = LoadJson "dashboard-model-v12.json"

$gate = $model.QualityGate
$technicalDebt = $model.TechnicalDebt
$security = $model.Security


$tasksOutput = @()


# -----------------------------------------------------------------------
# Source: QualityGate.Warnings (mensajes reales ya generados)
# -----------------------------------------------------------------------

foreach($warning in $gate.Warnings)
{
    $tasksOutput +=
    [PSCustomObject]@{
        task     = $warning
        category = "Quality Gate"
        priority = "HIGH"
        source   = "QualityGate.Warnings"
        evidence = @()
    }
}


# -----------------------------------------------------------------------
# Source: TechnicalDebt.LargeFiles (archivos reales, evidencia = ruta real)
# Se prioriza por tamaño; no se incluye el listado completo de migraciones
# generadas por EF (ruido esperado en un modular monolith), solo codigo
# fuente real por encima de 500 lineas.
# -----------------------------------------------------------------------

$largeSourceFiles =
@($technicalDebt.LargeFiles) |
Where-Object {
    $_.File -notmatch "\\Migrations\\" -and
    $_.File -notmatch "node_modules" -and
    $_.Lines -ge 500
} |
Sort-Object -Property Lines -Descending


foreach($file in $largeSourceFiles)
{
    $relativePath = $file.File -replace [regex]::Escape("$ProjectRoot\"), "" -replace "\\", "/"

    $priority = "MEDIUM"

    if($file.Lines -ge 800)
    {
        $priority = "HIGH"
    }

    $tasksOutput +=
    [PSCustomObject]@{
        task     = "Refactor large file ($($file.Lines) lines): $relativePath"
        category = "Technical Debt"
        priority = $priority
        source   = "TechnicalDebt.LargeFiles"
        evidence = @($relativePath)
    }
}


# -----------------------------------------------------------------------
# Source: TechnicalDebt aggregate counts (TODO/FIXME/HACK/NotImplemented)
# El modelo actual no persiste ubicacion linea-a-linea de cada TODO, solo
# el conteo agregado -- se refleja como una sola tarea de seguimiento por
# categoria en vez de inventar 686 tareas individuales sin evidencia real.
# -----------------------------------------------------------------------

if($technicalDebt.TODO -gt 0)
{
    $tasksOutput +=
    [PSCustomObject]@{
        task     = "Triage and reduce outstanding TODO markers ($($technicalDebt.TODO) found)"
        category = "Technical Debt"
        priority = "MEDIUM"
        source   = "TechnicalDebt.TODO"
        evidence = @()
    }
}


if($technicalDebt.FIXME -gt 0)
{
    $tasksOutput +=
    [PSCustomObject]@{
        task     = "Resolve outstanding FIXME markers ($($technicalDebt.FIXME) found)"
        category = "Technical Debt"
        priority = "HIGH"
        source   = "TechnicalDebt.FIXME"
        evidence = @()
    }
}


if($technicalDebt.HACK -gt 0)
{
    $tasksOutput +=
    [PSCustomObject]@{
        task     = "Review HACK markers ($($technicalDebt.HACK) found)"
        category = "Technical Debt"
        priority = "MEDIUM"
        source   = "TechnicalDebt.HACK"
        evidence = @()
    }
}


if($technicalDebt.NotImplemented -gt 0)
{
    $tasksOutput +=
    [PSCustomObject]@{
        task     = "Complete NotImplementedException code paths ($($technicalDebt.NotImplemented) found)"
        category = "Technical Debt"
        priority = "HIGH"
        source   = "TechnicalDebt.NotImplemented"
        evidence = @()
    }
}


# -----------------------------------------------------------------------
# Source: Security findings (con evidencia real, excluyendo node_modules)
# -----------------------------------------------------------------------

$realSecretFiles =
@($security.SecretFiles) |
Where-Object { $_ -notmatch "node_modules" }

if($realSecretFiles.Count -gt 0)
{
    $sample = @($realSecretFiles | Select-Object -First 5 | ForEach-Object { $_ -replace [regex]::Escape("$ProjectRoot\"), "" -replace "\\", "/" })

    $tasksOutput +=
    [PSCustomObject]@{
        task     = "Review possible secrets in source files ($($realSecretFiles.Count) real matches, excluding node_modules)"
        category = "Security"
        priority = "HIGH"
        source   = "Security.SecretFiles"
        evidence = $sample
    }
}


if($security.AnonymousDetected -gt 0)
{
    $sample = @(@($security.AnonymousFiles) | Where-Object { $_ -notmatch "node_modules" } | Select-Object -First 5 | ForEach-Object { $_ -replace [regex]::Escape("$ProjectRoot\"), "" -replace "\\", "/" })

    $tasksOutput +=
    [PSCustomObject]@{
        task     = "Review anonymous-access endpoints ($($security.AnonymousDetected) found)"
        category = "Security"
        priority = "HIGH"
        source   = "Security.AnonymousFiles"
        evidence = $sample
    }
}


Write-Host "Tasks generated: $($tasksOutput.Count)"


$tasksOutput |
    ConvertTo-Json -Depth 6 |
    Out-File $OutputFile -Encoding utf8


Write-Host ""
Write-Host "tasks.json generated successfully." -ForegroundColor Green
Write-Host $OutputFile
