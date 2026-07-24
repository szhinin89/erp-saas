# =============================================================================
# ZH Technologies
# analyze-impact.ps1
#
# Genera docs/ProgressDashboard/data/impact.json: conecta
# Domain -> Module -> Feature -> Process -> Risk usando exclusivamente datos
# ya reales (modules.json/features.json/processes.json/dashboard-model-v12.json)
# mas evidencia de codigo fuente calculada en el momento (conteo real de
# TODO/FIXME/HACK/NotImplementedException por modulo, archivos grandes reales,
# archivos de seguridad reales atribuidos por ruta).
#
# Ningun riesgo se inventa: cada entrada de "risks" nace de una señal real
# (Security.SecretFiles/AnonymousFiles, TechnicalDebt.LargeFiles, conteo real
# de marcadores en el codigo, dominio "unmapped" en modules.json, o un paso de
# proceso "unmapped" en processes.json).
# =============================================================================

$ErrorActionPreference = "Stop"


$ProjectRoot =
(Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path


$DataRoot =
Join-Path $ProjectRoot "docs\ProgressDashboard\data"


$AppModulesRoot =
Join-Path $ProjectRoot "backend\src\ERP.Application\Modules"


$OutputFile =
Join-Path $DataRoot "impact.json"



Write-Host ""
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host " analyze-impact.ps1"
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


function Get-RiskLevel($value, $isFindingsCount)
{
    if($isFindingsCount)
    {
        if($value -gt 40) { return "CRITICAL" }
        elseif($value -gt 20) { return "HIGH" }
        elseif($value -ge 10) { return "MEDIUM" }
        else { return "LOW" }
    }
    else
    {
        if($value -lt 40) { return "CRITICAL" }
        elseif($value -lt 70) { return "HIGH" }
        elseif($value -lt 90) { return "MEDIUM" }
        else { return "LOW" }
    }
}


function Get-RiskRank($level)
{
    switch($level)
    {
        "CRITICAL" { return 4 }
        "HIGH"     { return 3 }
        "MEDIUM"   { return 2 }
        default    { return 1 }
    }
}


function Escalate($currentLevel, $minimumLevel)
{
    if((Get-RiskRank $minimumLevel) -gt (Get-RiskRank $currentLevel))
    {
        return $minimumLevel
    }

    return $currentLevel
}



$model = LoadJson "dashboard-model-v12.json"
$domains = @(LoadJson "domains.json")
$modulesData = @(LoadJson "modules.json")
$featuresData = @(LoadJson "features.json")
$processesData = @(LoadJson "processes.json")

$security = $model.Security
$technicalDebt = $model.TechnicalDebt


Write-Host "Modules to analyze: $($modulesData.Count)"


# -----------------------------------------------------------------------
# Security evidence real: atribuir archivos de secretos/anonimos a un modulo
# por coincidencia de ruta real "\Modules\{ModuleId}\"
# -----------------------------------------------------------------------

function Count-SecurityHits($fileList, $moduleId)
{
    return @($fileList | Where-Object { $_ -match [regex]::Escape("\Modules\$moduleId\") }).Count
}


# -----------------------------------------------------------------------
# Deuda tecnica real por modulo: conteo directo sobre el codigo fuente
# (no se usa el agregado global de dashboard-model-v12.json porque ese
# numero no esta desglosado por modulo)
# -----------------------------------------------------------------------

function Get-ModuleDebtCounts($moduleId)
{
    $modulePath = Join-Path $AppModulesRoot $moduleId

    $result = [PSCustomObject]@{
        todo            = 0
        fixme           = 0
        hack            = 0
        notImplemented  = 0
        largeFiles      = 0
    }

    if(!(Test-Path $modulePath))
    {
        return $result
    }

    $files = Get-ChildItem -Path $modulePath -Recurse -File -Filter "*.cs" -ErrorAction SilentlyContinue

    foreach($file in $files)
    {
        $content = Get-Content $file.FullName -Raw -ErrorAction SilentlyContinue

        if($null -eq $content) { continue }

        $result.todo += ([regex]::Matches($content, "TODO")).Count
        $result.fixme += ([regex]::Matches($content, "FIXME")).Count
        $result.hack += ([regex]::Matches($content, "HACK")).Count
        $result.notImplemented += ([regex]::Matches($content, "NotImplementedException")).Count

        $lineCount = ($content -split "`n").Count

        if($lineCount -ge 500)
        {
            $result.largeFiles++
        }
    }

    return $result
}



$impactDomains = @()

$totalFeaturePoints = 0
$mappedFeaturePoints = 0


foreach($domain in $domains)
{
    $domainModuleIds = @($modulesData | Where-Object { $_.domainId -eq $domain.id } | ForEach-Object { $_.id })

    $moduleEntries = @()

    foreach($moduleId in $domainModuleIds)
    {
        $moduleInfo = $modulesData | Where-Object { $_.id -eq $moduleId } | Select-Object -First 1
        $featureEntry = $featuresData | Where-Object { $_.module -eq $moduleId } | Select-Object -First 1
        $featureCount = 0

        if($null -ne $featureEntry)
        {
            $featureCount = @($featureEntry.features).Count
        }

        $totalFeaturePoints += $featureCount


        # ---- Procesos que tocan este modulo ----

        $moduleProcesses = @()
        $hasVerifiedProcess = $false

        foreach($process in $processesData)
        {
            $moduleSteps = @($process.steps | Where-Object { $_.module -eq $moduleId })

            if($moduleSteps.Count -eq 0) { continue }

            $allVerified = @($moduleSteps | Where-Object { $_.status -ne "verified" }).Count -eq 0

            if($allVerified)
            {
                $processStatus = "verified"
                $processRisk = "LOW"
                $hasVerifiedProcess = $true
            }
            else
            {
                $processStatus = "partial"
                $processRisk = "HIGH"
            }

            $moduleProcesses +=
            [PSCustomObject]@{
                name   = $process.process
                status = $processStatus
                risk   = $processRisk
            }
        }

        if($hasVerifiedProcess)
        {
            $mappedFeaturePoints += $featureCount
        }


        # ---- Deuda tecnica real (grep sobre el codigo) ----

        $debtCounts = Get-ModuleDebtCounts $moduleId


        # ---- Seguridad real (atribucion por ruta) ----

        $secretHits = Count-SecurityHits @($security.SecretFiles) $moduleId
        $anonymousHits = Count-SecurityHits @($security.AnonymousFiles) $moduleId


        # ---- Construir lista de riesgos con evidencia real ----

        $risks = @()

        if($null -ne $moduleInfo -and [double]$moduleInfo.score -lt 70)
        {
            $risks += "Low module quality score ($($moduleInfo.score)%)"
        }

        if($null -ne $moduleInfo -and [double]$moduleInfo.tests -lt 60)
        {
            $risks += "Low test coverage ($($moduleInfo.tests)%)"
        }

        if($debtCounts.largeFiles -gt 0)
        {
            $risks += "$($debtCounts.largeFiles) large file(s) (>=500 lines)"
        }

        if($debtCounts.todo -ge 10)
        {
            $risks += "$($debtCounts.todo) TODO marker(s) in source"
        }

        if($debtCounts.fixme -gt 0)
        {
            $risks += "$($debtCounts.fixme) FIXME marker(s) in source"
        }

        if($debtCounts.hack -gt 0)
        {
            $risks += "$($debtCounts.hack) HACK marker(s) in source"
        }

        if($debtCounts.notImplemented -gt 0)
        {
            $risks += "$($debtCounts.notImplemented) NotImplementedException in source"
        }

        if($secretHits -gt 0)
        {
            $risks += "$secretHits possible secret(s) detected in module source"
        }

        if($anonymousHits -gt 0)
        {
            $risks += "$anonymousHits anonymous-access file(s) detected in module source"
        }

        foreach($process in $processesData)
        {
            foreach($step in @($process.steps | Where-Object { $_.module -eq $moduleId -and $_.status -ne "verified" }))
            {
                $risks += "Unverified process step: $($step.name) ($($process.process))"
            }
        }


        # ---- Nivel de riesgo del modulo ----

        $baseline = "LOW"

        if($null -ne $moduleInfo)
        {
            $baseline = Get-RiskLevel ([double]$moduleInfo.score) $false
        }

        $moduleRisk = $baseline

        if($secretHits -gt 0 -or $anonymousHits -gt 0 -or $debtCounts.notImplemented -gt 0 -or $debtCounts.fixme -gt 3)
        {
            $moduleRisk = Escalate $moduleRisk "HIGH"
        }

        if(@($moduleProcesses | Where-Object { $_.status -ne "verified" }).Count -gt 0)
        {
            $moduleRisk = Escalate $moduleRisk "MEDIUM"
        }


        $moduleEntries +=
        [PSCustomObject]@{
            name      = $moduleId
            score     = $moduleInfo.score
            features  = $featureCount
            processes = $moduleProcesses
            risks     = $risks
            risk      = $moduleRisk
        }
    }

    if($moduleEntries.Count -eq 0) { continue }

    $impactDomains +=
    [PSCustomObject]@{
        domain  = $domain.name
        modules = $moduleEntries
    }
}


# -----------------------------------------------------------------------
# Dominio "Unmapped" -- modulos reales sin dominio de negocio asignado en
# domains.json (ver analyze-modules.ps1). Se listan aparte, no se fuerzan
# a un dominio incorrecto.
# -----------------------------------------------------------------------

$unmappedModuleIds = @($modulesData | Where-Object { $_.domainId -eq "unmapped" } | ForEach-Object { $_.id })

if($unmappedModuleIds.Count -gt 0)
{
    $unmappedEntries = @()

    foreach($moduleId in $unmappedModuleIds)
    {
        $moduleInfo = $modulesData | Where-Object { $_.id -eq $moduleId } | Select-Object -First 1
        $featureEntry = $featuresData | Where-Object { $_.module -eq $moduleId } | Select-Object -First 1
        $featureCount = 0

        if($null -ne $featureEntry)
        {
            $featureCount = @($featureEntry.features).Count
        }

        $totalFeaturePoints += $featureCount

        $baseline = "LOW"

        if($null -ne $moduleInfo)
        {
            $baseline = Get-RiskLevel ([double]$moduleInfo.score) $false
        }

        $moduleRisk = Escalate $baseline "MEDIUM"

        $unmappedEntries +=
        [PSCustomObject]@{
            name      = $moduleId
            score     = $moduleInfo.score
            features  = $featureCount
            processes = @()
            risks     = @("Unmapped business domain - no domains.json relationship")
            risk      = $moduleRisk
        }
    }

    $impactDomains +=
    [PSCustomObject]@{
        domain  = "Unmapped"
        modules = $unmappedEntries
    }
}


# -----------------------------------------------------------------------
# Engineering Risk Coverage
# Mapped Business Capability Points / Total Known Capability Points
# Un "capability point" = una feature real (features.json). Se considera
# "mapeada" cuando su modulo participa en al menos un proceso verificado
# (processes.json). Es la unica granularidad disponible hoy: los procesos
# se verifican a nivel modulo, no a nivel feature individual.
# -----------------------------------------------------------------------

$coveragePct = 0

if($totalFeaturePoints -gt 0)
{
    $coveragePct =
    [math]::Round(
        (($mappedFeaturePoints / $totalFeaturePoints) * 100),
        2
    )
}


Write-Host "Engineering Risk Coverage: $mappedFeaturePoints / $totalFeaturePoints = $coveragePct%"


$output =
[PSCustomObject]@{
    generated = (Get-Date -Format "yyyy-MM-dd HH:mm:ss")
    coverage  =
    [PSCustomObject]@{
        mappedFeaturePoints = $mappedFeaturePoints
        totalFeaturePoints  = $totalFeaturePoints
        percentage          = $coveragePct
    }
    domains   = $impactDomains
}


$output |
    ConvertTo-Json -Depth 8 |
    Out-File $OutputFile -Encoding utf8


Write-Host ""
Write-Host "impact.json generated successfully." -ForegroundColor Green
Write-Host $OutputFile
