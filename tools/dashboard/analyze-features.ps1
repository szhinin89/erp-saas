# =============================================================================
# ZH Technologies
# analyze-features.ps1
#
# Genera docs/ProgressDashboard/data/features.json (hoy vacio: "{}") escaneando
# el CODIGO REAL del backend, no inventando features.
#
# Para cada modulo real de modules.json:
#   1) Busca carpetas "UseCases" (recursivo, cubre submodulos como
#      Inventory/Stock/UseCases, Inventory/Warehouses/UseCases).
#   2) Si no hay carpeta "UseCases", busca archivos *Query.cs / *Command.cs
#      directamente en la raiz del modulo (patron usado por modulos chicos
#      como Dashboard: GetDashboardKpisQuery.cs sin carpeta UseCases).
#   3) Si el modulo no tiene carpeta en ERP.Application/Modules/*, revisa si
#      existe como modulo solo-de-dominio en ERP.Domain/Modules/* (catalogos
#      sin capa Application todavia, ej. SriCatalogs, Tenants).
#   4) Si no se encuentra evidencia real, el modulo queda con features: []
#      y una razon documentada -- nunca se inventa una feature.
# =============================================================================

$ErrorActionPreference = "Stop"


$ProjectRoot =
(Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path


$DataRoot =
Join-Path $ProjectRoot "docs\ProgressDashboard\data"


$AppModulesRoot =
Join-Path $ProjectRoot "backend\src\ERP.Application\Modules"


$DomainModulesRoot =
Join-Path $ProjectRoot "backend\src\ERP.Domain\Modules"


$OutputFile =
Join-Path $DataRoot "features.json"



Write-Host ""
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host " analyze-features.ps1"
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


function ToFeatureName($rawName)
{
    $name = $rawName

    foreach($suffix in @("UseCases", "CommandHandler", "QueryHandler", "Command", "Query", "Handler", "Validator"))
    {
        if($name.EndsWith($suffix))
        {
            $name = $name.Substring(0, $name.Length - $suffix.Length)
        }
    }

    # PascalCase -> "Pascal Case"
    $spaced = [regex]::Replace($name, '(?<!^)(?=[A-Z])', ' ')

    return $spaced.Trim()
}



$modulesData = @(LoadJson "modules.json")


Write-Host "Modules to analyze: $($modulesData.Count)"


$featuresOutput = @()
$modulesWithFeatures = 0
$modulesWithoutFeatures = 0


foreach($module in $modulesData)
{
    $moduleId = $module.id
    $moduleAppPath = Join-Path $AppModulesRoot $moduleId

    $features = @()
    $seenNames = @{}

    if(Test-Path $moduleAppPath)
    {
        # Tier 1: any "UseCases" folder anywhere under the module (covers submodules)
        $useCaseDirs = Get-ChildItem -Path $moduleAppPath -Recurse -Directory -Filter "UseCases" -ErrorAction SilentlyContinue

        foreach($dir in $useCaseDirs)
        {
            $items = Get-ChildItem -Path $dir.FullName -ErrorAction SilentlyContinue

            foreach($item in $items)
            {
                $baseName = [System.IO.Path]::GetFileNameWithoutExtension($item.Name)
                $featureName = ToFeatureName $baseName

                if(-not [string]::IsNullOrWhiteSpace($featureName) -and -not $seenNames.ContainsKey($featureName))
                {
                    $seenNames[$featureName] = $true

                    $evidencePath = $item.FullName.Substring($ProjectRoot.Length + 1) -replace "\\", "/"

                    $features +=
                    [PSCustomObject]@{
                        name     = $featureName
                        status   = "implemented"
                        evidence = @($evidencePath)
                    }
                }
            }
        }

        # Tier 2: fallback for modules without a "UseCases" folder convention
        # (ej. Dashboard: *Query.cs directamente en la raiz del modulo)
        if($features.Count -eq 0)
        {
            $rootFiles =
            Get-ChildItem -Path $moduleAppPath -Recurse -File -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -match "(Query|Command)\.cs$" -and $_.Name -notmatch "(Handler|Validator)\.cs$" }

            foreach($file in $rootFiles)
            {
                $baseName = [System.IO.Path]::GetFileNameWithoutExtension($file.Name)
                $featureName = ToFeatureName $baseName

                if(-not [string]::IsNullOrWhiteSpace($featureName) -and -not $seenNames.ContainsKey($featureName))
                {
                    $seenNames[$featureName] = $true

                    $evidencePath = $file.FullName.Substring($ProjectRoot.Length + 1) -replace "\\", "/"

                    $features +=
                    [PSCustomObject]@{
                        name     = $featureName
                        status   = "implemented"
                        evidence = @($evidencePath)
                    }
                }
            }
        }
    }


    if($features.Count -gt 0)
    {
        $modulesWithFeatures++

        $featuresOutput +=
        [PSCustomObject]@{
            module   = $moduleId
            features = $features
        }
    }
    else
    {
        $modulesWithoutFeatures++

        $reason = "No Application-layer UseCases/Query/Command evidence found"
        $evidence = @()

        if(!(Test-Path $moduleAppPath))
        {
            $domainOnlyPath = Join-Path $DomainModulesRoot $moduleId

            if(Test-Path $domainOnlyPath)
            {
                $reason = "Domain-only catalog module (no Application UseCases layer yet)"
                $evidence = @(($domainOnlyPath.Substring($ProjectRoot.Length + 1)) -replace "\\", "/")
            }
            else
            {
                $reason = "No matching folder found in ERP.Application/Modules or ERP.Domain/Modules"
            }
        }
        else
        {
            $reason = "Application module exists but exposes no discrete UseCases/Query/Command (likely shared/infrastructure module)"
            $evidence = @(($moduleAppPath.Substring($ProjectRoot.Length + 1)) -replace "\\", "/")
        }

        $featuresOutput +=
        [PSCustomObject]@{
            module   = $moduleId
            features = @()
            pending  = $true
            reason   = $reason
            evidence = $evidence
        }
    }
}


Write-Host "Modules with features    : $modulesWithFeatures"
Write-Host "Modules pending (no data): $modulesWithoutFeatures"


$featuresOutput |
    ConvertTo-Json -Depth 6 |
    Out-File $OutputFile -Encoding utf8


Write-Host ""
Write-Host "features.json generated successfully." -ForegroundColor Green
Write-Host $OutputFile
