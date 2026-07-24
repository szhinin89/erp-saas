# =============================================================================
# ZH Technologies
# analyze-modules.ps1
#
# Deriva docs/ProgressDashboard/data/modules.json (hoy vacio: "{}") a partir de
# datos que YA existen en el repo:
#   - dashboard-model-v12.json -> Health.value (29 modulos reales, con Score)
#   - domains.json             -> 11 dominios reales del ERP
#
# No inventa modulos ni scores: solo asigna cada modulo real de Health.value a
# su dominio real de domains.json mediante un mapeo explicito. Un modulo cuyo
# dominio de negocio no esta modelado todavia en domains.json (ej. Dashboard,
# Menu, Common) se marca como "unmapped" en vez de forzarlo a un dominio
# incorrecto.
# =============================================================================

$ErrorActionPreference = "Stop"


$ProjectRoot =
(Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path


$DataRoot =
Join-Path $ProjectRoot "docs\ProgressDashboard\data"


$OutputFile =
Join-Path $DataRoot "modules.json"



Write-Host ""
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host " analyze-modules.ps1"
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host ""



function LoadJson($file)
{
    $path = Join-Path $DataRoot $file

    if(!(Test-Path $path))
    {
        throw "Missing file: $path"
    }

    Get-Content $path -Raw | ConvertFrom-Json
}



$model = LoadJson "dashboard-model-v12.json"
$domains = LoadJson "domains.json"

$domainIds = @($domains | ForEach-Object { $_.id })


Write-Host "Modules source : dashboard-model-v12.json (Health.value)"
Write-Host "Domains source : domains.json ($($domainIds.Count) domains)"


# -----------------------------------------------------------------------
# Modulo real (Health.value.Module) -> dominio real (domains.json.id)
#
# Mapeo explicito, revisado a mano. Los modulos sin dominio de negocio
# modelado todavia quedan en "unmapped" en vez de forzarse a un dominio
# incorrecto (ver AI-RULES: no inventar relaciones).
# -----------------------------------------------------------------------

$domainMap = @{
    "Company"              = "configuration"
    "Dashboard"            = "unmapped"
    "Purchases"            = "purchases"
    "Companies"            = "configuration"
    "Menu"                 = "unmapped"
    "Auth"                 = "security"
    "Integration"          = "unmapped"
    "Access"               = "security"
    "Sales"                = "sales"
    "Items"                = "inventory"
    "Tenants"              = "configuration"
    "Session"              = "security"
    "Common"               = "unmapped"
    "Inventory"            = "inventory"
    "Finance"              = "accounting"
    "Branches"             = "configuration"
    "Auxiliary"            = "unmapped"
    "Pricing"              = "sales"
    "Caja"                 = "cash"
    "ElectronicInvoicing"  = "electronic-documents"
    "Configuration"        = "configuration"
    "Audit"                = "security"
    "Ride"                 = "electronic-documents"
    "Media"                = "unmapped"
    "ElectronicDocuments"  = "electronic-documents"
    "Security"             = "security"
    "SriCatalogs"          = "electronic-documents"
    "OrgConfig"            = "configuration"
    "Navigation"           = "unmapped"
}


$modulesOutput = @()
$unmappedCount = 0


foreach($module in $model.Health.value)
{
    $domainId = "unmapped"

    if($domainMap.ContainsKey($module.Module))
    {
        $domainId = $domainMap[$module.Module]
    }

    if($domainId -eq "unmapped" -or $domainIds -notcontains $domainId)
    {
        $domainId = "unmapped"
        $unmappedCount++
    }

    $modulesOutput +=
    [PSCustomObject]@{
        id            = $module.Module
        domainId      = $domainId
        score         = $module.Score
        architecture  = $module.Architecture
        tests         = $module.Tests
        documentation = $module.Documentation
        backend       = $module.Backend
        frontend      = $module.Frontend
    }
}


Write-Host "Modules mapped : $($modulesOutput.Count - $unmappedCount)"
Write-Host "Unmapped       : $unmappedCount"


$modulesOutput |
    ConvertTo-Json -Depth 5 |
    Out-File $OutputFile -Encoding utf8


Write-Host ""
Write-Host "modules.json generated successfully." -ForegroundColor Green
Write-Host $OutputFile
