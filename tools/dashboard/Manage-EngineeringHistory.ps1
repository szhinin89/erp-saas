# =============================================================================
# ZH Technologies
# Manage Engineering History
#
# Fusion de snapshot-dashboard-v2.ps1 + analyze-engineering-trend.ps1 -- ambos
# scripts comparten el mismo directorio (docs/ProgressDashboard/history/):
# uno escribe un snapshot por corrida, el otro relee TODOS los snapshots para
# calcular la tendencia. Nunca se ejecutaban por separado en la practica (el
# segundo no tiene sentido sin el primero) y no representan capacidades de
# negocio independientes -- son dos mitades de una sola operacion: "registrar
# y resumir el historial de Engineering Score". Logica identica a los dos
# scripts originales, sin cambios de comportamiento; ambos originales se
# conservan sin archivar hasta validar esta fusion en una corrida completa.
#
# Paso 1 (antes snapshot-dashboard-v2.ps1): guarda un snapshot con timestamp
#         de EngineeringScore/Health/Security/TechnicalDebt.
# Paso 2 (antes analyze-engineering-trend.ps1): relee todos los snapshots del
#         historial y calcula engineering-trend.json.
# =============================================================================

$ErrorActionPreference = "Stop"

$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$DataRoot = Join-Path $ProjectRoot "docs\ProgressDashboard\data"
$HistoryRoot = Join-Path $ProjectRoot "docs\ProgressDashboard\history"

Write-Host ""
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host " Manage Engineering History"
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host ""

function LoadJsonOrNull($file)
{
    $path = Join-Path $DataRoot $file
    if(Test-Path $path) { return (Get-Content $path -Raw | ConvertFrom-Json) }
    return $null
}

if(!(Test-Path $HistoryRoot))
{
    New-Item -ItemType Directory $HistoryRoot | Out-Null
}


# =============================================================================
# Paso 1: Snapshot (logica identica a snapshot-dashboard-v2.ps1)
# =============================================================================

$engineering = LoadJsonOrNull "engineering-score.json"
$health = LoadJsonOrNull "health-score.json"
$security = LoadJsonOrNull "security-analysis.json"
$technicalDebt = LoadJsonOrNull "technical-debt.json"

$timestamp = Get-Date -Format "yyyy-MM-dd-HHmm"
$snapshotFileName = "dashboard-$timestamp-v2.json"
$snapshotOutput = Join-Path $HistoryRoot $snapshotFileName

$snapshot = [ordered]@{
    Generated = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    EngineeringScore = $engineering
    Health = $health
    Security = $security
    TechnicalDebt = $technicalDebt
}

$snapshot | ConvertTo-Json -Depth 100 | Set-Content $snapshotOutput -Encoding UTF8

$snapshotCount = (Get-ChildItem $HistoryRoot -Filter "*-v2.json").Count

Write-Host "Snapshot created: $snapshotFileName"
Write-Host "Total v2 snapshots: $snapshotCount"


# =============================================================================
# Paso 2: Trend (logica identica a analyze-engineering-trend.ps1)
# =============================================================================

$snapshots = Get-ChildItem $HistoryRoot -Filter "*.json" | Sort-Object Name

$history = @()

foreach($file in $snapshots)
{
    try
    {
        $snapshotData = Get-Content $file.FullName -Raw | ConvertFrom-Json

        $score = $null

        if($snapshotData.EngineeringScore) { $score = $snapshotData.EngineeringScore.Overall }
        elseif($snapshotData.Summary.EngineeringScore) { $score = $snapshotData.Summary.EngineeringScore }

        if($score -ne $null)
        {
            $history += [ordered]@{
                Date = $snapshotData.Generated
                File = $file.Name
                Score = [double]$score
            }
        }
    }
    catch
    {
        Write-Host "Skipped:" $file.Name -ForegroundColor Yellow
    }
}

$trendResult = [ordered]@{
    Generated = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    Snapshots = $history.Count
    History = $history
}

$trendOutput = Join-Path $DataRoot "engineering-trend.json"
$trendResult | ConvertTo-Json -Depth 20 | Set-Content $trendOutput -Encoding UTF8

Write-Host ""
Write-Host "Engineering trend generated successfully." -ForegroundColor Green
Write-Host "Snapshots analyzed :" $history.Count
Write-Host ""
Write-Host "Outputs:"
Write-Host $snapshotOutput
Write-Host $trendOutput
