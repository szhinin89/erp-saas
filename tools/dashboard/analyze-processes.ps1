# =============================================================================
# ZH Technologies
# analyze-processes.ps1
#
# Genera docs/ProgressDashboard/data/processes.json (hoy vacio: "{}").
#
# Un proceso de negocio (Venta, Compra) se declara como una secuencia de pasos
# ordenada. Cada paso NO se asume verdadero solo por describirlo: el script
# busca evidencia real (grep) del termino asociado dentro del codigo del
# modulo origen. Si no encuentra coincidencias, el paso queda "unmapped" con
# la razon documentada -- nunca se marca "verified" sin evidencia real.
# =============================================================================

$ErrorActionPreference = "Stop"


$ProjectRoot =
(Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path


$DataRoot =
Join-Path $ProjectRoot "docs\ProgressDashboard\data"


$AppModulesRoot =
Join-Path $ProjectRoot "backend\src\ERP.Application\Modules"


$OutputFile =
Join-Path $DataRoot "processes.json"



Write-Host ""
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host " analyze-processes.ps1"
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host ""



function Find-Evidence($module, $pattern)
{
    $modulePath = Join-Path $AppModulesRoot $module

    if(!(Test-Path $modulePath))
    {
        return @()
    }

    $matches =
    Get-ChildItem -Path $modulePath -Recurse -File -Filter "*.cs" -ErrorAction SilentlyContinue |
    Select-String -Pattern $pattern -ErrorAction SilentlyContinue

    if($null -eq $matches)
    {
        return @()
    }

    $files =
    $matches |
    Select-Object -ExpandProperty Path -Unique |
    Select-Object -First 3

    return @($files | ForEach-Object { $_.Substring($ProjectRoot.Length + 1) -replace "\\", "/" })
}


function Build-Step($name, $module, $pattern)
{
    $evidence = Find-Evidence $module $pattern

    if($evidence.Count -gt 0)
    {
        return [PSCustomObject]@{
            name     = $name
            module   = $module
            status   = "verified"
            evidence = $evidence
        }
    }

    return [PSCustomObject]@{
        name     = $name
        module   = $module
        status   = "unmapped"
        reason   = "No code evidence found for pattern '$pattern' in module '$module'"
        evidence = @()
    }
}



$processesOutput = @()


# -----------------------------------------------------------------------
# Proceso: Venta
# Customer -> Quote/Draft -> Invoice -> ElectronicDocument -> SRI Authorization -> Accounting
# -----------------------------------------------------------------------

$ventaSteps = @(
    Build-Step "Customer"             "Sales" "Customer"
    Build-Step "Quote / Draft"        "Sales" "Draft"
    Build-Step "Invoice"              "Sales" "Invoice"
    Build-Step "Electronic Document"  "Sales" "ElectronicDocument"
    Build-Step "SRI Authorization"    "Sales" "Authoriz"
    Build-Step "Accounting"           "Sales" "Receivable|Accounting|JournalEntry"
)

$processesOutput +=
[PSCustomObject]@{
    process = "Venta"
    steps   = $ventaSteps
}


# -----------------------------------------------------------------------
# Proceso: Compra
# Supplier -> Purchase Order -> Reception -> Inventory -> Accounting
# -----------------------------------------------------------------------

$compraSteps = @(
    Build-Step "Supplier"        "Purchases" "Supplier"
    Build-Step "Purchase Order"  "Purchases" "Confirm|PurchaseOrder"
    Build-Step "Reception"       "Purchases" "Recep|Receiv"
    Build-Step "Inventory"       "Purchases" "Stock|Warehouse|Inventory"
    Build-Step "Accounting"      "Purchases" "Withholding|PaymentSchedule|Accounting"
)

$processesOutput +=
[PSCustomObject]@{
    process = "Compra"
    steps   = $compraSteps
}


$verifiedCount = 0
$unmappedCount = 0

foreach($process in $processesOutput)
{
    foreach($step in $process.steps)
    {
        if($step.status -eq "verified") { $verifiedCount++ }
        else { $unmappedCount++ }
    }
}


Write-Host "Processes defined : $($processesOutput.Count)"
Write-Host "Steps verified    : $verifiedCount"
Write-Host "Steps unmapped    : $unmappedCount"


$processesOutput |
    ConvertTo-Json -Depth 6 |
    Out-File $OutputFile -Encoding utf8


Write-Host ""
Write-Host "processes.json generated successfully." -ForegroundColor Green
Write-Host $OutputFile
