# =============================================================================
# ZH Technologies
# validate-dashboard-model.ps1
#
# Valida la integridad referencial y completitud del modelo de conocimiento
# (docs/ProgressDashboard/data/*.json) generado por los analizadores. Es un
# validador puro: no modifica ningun JSON existente, no toca codigo del ERP,
# no toca PROGRESS.html ni render-dashboard.ps1. Solo lee y reporta.
#
# Genera: docs/ProgressDashboard/data/model-health.json
#
# Chequeos:
#   1) Referencias entre JSON:
#      - modules[].domainId       -> domains[].id (o "unmapped", documentado)
#      - features[].module        -> modules[].id
#      - processes[].steps[].module -> modules[].id
#      - tasks[].evidence          -> propiedad debe existir (estructural)
#   2) Datos incompletos:
#      - missing evidence: feature "implemented" o step "verified" sin evidencia
#      - unmapped relationships: domainId/status "unmapped", features pendientes
#   3) integrityScore: % de referencias validas sobre el total de referencias
#      chequeadas (brokenReferences no cuenta contra missingEvidence/unmappedItems,
#      que son completitud, no integridad referencial).
# =============================================================================

$ErrorActionPreference = "Stop"


$ProjectRoot =
(Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path


$DataRoot =
Join-Path $ProjectRoot "docs\ProgressDashboard\data"


$OutputFile =
Join-Path $DataRoot "model-health.json"



Write-Host ""
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host " validate-dashboard-model.ps1"
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



$domains = @(LoadJson "domains.json")
$modulesData = @(LoadJson "modules.json")
$featuresData = @(LoadJson "features.json")
$processesData = @(LoadJson "processes.json")
$tasksData = @(LoadJson "tasks.json")

$domainIds = @($domains | ForEach-Object { $_.id })
$moduleIds = @($modulesData | ForEach-Object { $_.id })


Write-Host "Loaded: $($domains.Count) domains, $($modulesData.Count) modules, $($featuresData.Count) feature entries, $($processesData.Count) processes, $($tasksData.Count) tasks"


$totalReferenceChecks = 0
$brokenReferences = 0
$missingEvidence = 0
$unmappedItems = 0

$brokenDetails = @()
$missingEvidenceDetails = @()
$unmappedDetails = @()


# -----------------------------------------------------------------------
# 1a. modules[].domainId -> domains[].id
# -----------------------------------------------------------------------

foreach($module in $modulesData)
{
    $totalReferenceChecks++

    if($module.domainId -eq "unmapped")
    {
        $unmappedItems++
        $unmappedDetails += "module '$($module.id)' has no domain assigned (domainId: unmapped)"
        continue
    }

    if($domainIds -notcontains $module.domainId)
    {
        $brokenReferences++
        $brokenDetails += "module '$($module.id)' references unknown domainId '$($module.domainId)'"
    }
}


# -----------------------------------------------------------------------
# 1b. features[].module -> modules[].id
# -----------------------------------------------------------------------

foreach($entry in $featuresData)
{
    $totalReferenceChecks++

    if($moduleIds -notcontains $entry.module)
    {
        $brokenReferences++
        $brokenDetails += "features.json entry references unknown module '$($entry.module)'"
        continue
    }

    $features = @($entry.features)

    if($features.Count -eq 0)
    {
        $unmappedItems++
        $unmappedDetails += "module '$($entry.module)' has zero features mapped$(if($entry.reason) { " ($($entry.reason))" })"
        continue
    }

    foreach($feature in $features)
    {
        if($feature.status -eq "implemented" -and @($feature.evidence).Count -eq 0)
        {
            $missingEvidence++
            $missingEvidenceDetails += "feature '$($feature.name)' (module '$($entry.module)') is 'implemented' but has no evidence"
        }
    }
}


# -----------------------------------------------------------------------
# 1c. processes[].steps[].module -> modules[].id (y, indirectamente, a
#     features[] del mismo modulo -- un proceso verificado sobre un modulo
#     sin ninguna feature registrada es una señal de modelo incompleto)
# -----------------------------------------------------------------------

foreach($process in $processesData)
{
    $steps = @($process.steps)

    if($steps.Count -eq 0)
    {
        $unmappedItems++
        $unmappedDetails += "process '$($process.process)' has zero steps defined"
        continue
    }

    foreach($step in $steps)
    {
        $totalReferenceChecks++

        if($moduleIds -notcontains $step.module)
        {
            $brokenReferences++
            $brokenDetails += "process '$($process.process)' step '$($step.name)' references unknown module '$($step.module)'"
            continue
        }

        if($step.status -eq "unmapped")
        {
            $unmappedItems++
            $unmappedDetails += "process '$($process.process)' step '$($step.name)' is unmapped$(if($step.reason) { " ($($step.reason))" })"
            continue
        }

        if($step.status -eq "verified" -and @($step.evidence).Count -eq 0)
        {
            $missingEvidence++
            $missingEvidenceDetails += "process '$($process.process)' step '$($step.name)' is 'verified' but has no evidence"
        }

        $moduleFeatureEntry = $featuresData | Where-Object { $_.module -eq $step.module } | Select-Object -First 1

        if($null -eq $moduleFeatureEntry -or @($moduleFeatureEntry.features).Count -eq 0)
        {
            $unmappedItems++
            $unmappedDetails += "process '$($process.process)' step '$($step.name)' touches module '$($step.module)' which has zero features registered"
        }
    }
}


# -----------------------------------------------------------------------
# 1d. tasks[].evidence -> la propiedad debe existir (estructural). Un
#     array vacio es valido cuando el "source" es un conteo agregado sin
#     desglose por archivo (ver AI-RULES / DASHBOARD-CONTRACT.md) -- solo
#     la AUSENCIA de la propiedad es una referencia rota.
# -----------------------------------------------------------------------

foreach($task in $tasksData)
{
    $totalReferenceChecks++

    $hasEvidenceProperty = ($task.PSObject.Properties.Name -contains "evidence")

    if(-not $hasEvidenceProperty)
    {
        $brokenReferences++
        $brokenDetails += "task '$($task.task)' is missing the 'evidence' property"
    }

    if([string]::IsNullOrWhiteSpace($task.source))
    {
        $brokenReferences++
        $brokenDetails += "task '$($task.task)' is missing a 'source' reference"
    }
}


# -----------------------------------------------------------------------
# Integrity score: % de referencias validas sobre el total chequeado.
# missingEvidence / unmappedItems se reportan aparte (son señales de
# completitud, no de integridad referencial).
# -----------------------------------------------------------------------

$integrityScore = 100

if($totalReferenceChecks -gt 0)
{
    $integrityScore =
    [math]::Round(
        ((($totalReferenceChecks - $brokenReferences) / $totalReferenceChecks) * 100),
        2
    )
}


Write-Host ""
Write-Host "Reference checks : $totalReferenceChecks"
Write-Host "Broken references: $brokenReferences" -ForegroundColor $(if($brokenReferences -gt 0) { "Red" } else { "Green" })
Write-Host "Missing evidence : $missingEvidence" -ForegroundColor $(if($missingEvidence -gt 0) { "Yellow" } else { "Green" })
Write-Host "Unmapped items   : $unmappedItems" -ForegroundColor $(if($unmappedItems -gt 0) { "Yellow" } else { "Green" })
Write-Host "Integrity score  : $integrityScore%" -ForegroundColor $(if($integrityScore -ge 90) { "Green" } elseif($integrityScore -ge 70) { "Yellow" } else { "Red" })


$output =
[PSCustomObject]@{
    generated        = (Get-Date -Format "yyyy-MM-dd HH:mm:ss")
    integrityScore    = $integrityScore
    brokenReferences  = $brokenReferences
    missingEvidence   = $missingEvidence
    unmappedItems     = $unmappedItems
    totalReferenceChecks = $totalReferenceChecks
    details =
    [PSCustomObject]@{
        brokenReferences = $brokenDetails
        missingEvidence  = $missingEvidenceDetails
        unmappedItems    = $unmappedDetails
    }
}


$output |
    ConvertTo-Json -Depth 6 |
    Out-File $OutputFile -Encoding utf8


Write-Host ""
Write-Host "model-health.json generated successfully." -ForegroundColor Green
Write-Host $OutputFile
