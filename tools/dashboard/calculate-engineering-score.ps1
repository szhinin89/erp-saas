# =============================================================================
# ZH Technologies
# Progress Dashboard v9
# Engineering Score Engine v1.1
# =============================================================================

$ErrorActionPreference = "Stop"


$ProjectRoot =
(Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path


$DataRoot =
Join-Path $ProjectRoot "docs\ProgressDashboard\data"



Write-Host ""
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host " Engineering Score Engine v1.1"
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host ""



function LoadJson($file)
{
    $path = Join-Path $DataRoot $file

    if(Test-Path $path)
    {
        try
        {
            return Get-Content $path -Raw |
            ConvertFrom-Json
        }
        catch
        {
            Write-Host "Invalid JSON: $file" -ForegroundColor Yellow
        }
    }

    return $null
}



function ClampScore($value)
{
    if($value -lt 0)
    {
        return 0
    }

    if($value -gt 100)
    {
        return 100
    }

    return [math]::Round($value,2)
}



# Load data

$health =
LoadJson "health-score.json"

$architecture =
LoadJson "architecture-analysis.json"

$quality =
LoadJson "technical-debt.json"

$security =
LoadJson "security-analysis.json"

$dependencies =
LoadJson "dependency-analysis.json"



# ==================================================
# MODULE HEALTH SCORE
# ==================================================

$healthScore = 0


if($health)
{

    $scores = @()


    # Caso actual:
    # health-score.json es un array directo

    if($health -is [array])
    {
        $scores =
        $health |
        ForEach-Object {

            if($_.Score)
            {
                [double]$_.Score
            }

        }
    }


    # Compatibilidad futura:
    # { Modules:[...] }

    elseif($health.Modules)
    {
        $scores =
        $health.Modules |
        ForEach-Object {

            if($_.Score)
            {
                [double]$_.Score
            }

        }
    }


    # Compatibilidad futura:
    # { Scores:[...] }

    elseif($health.Scores)
    {
        $scores =
        $health.Scores |
        ForEach-Object {

            if($_.Score)
            {
                [double]$_.Score
            }

        }
    }



    if($scores.Count -gt 0)
    {
        $healthScore =
        ($scores |
        Measure-Object -Average).Average
    }

}


$healthScore =
ClampScore $healthScore



# ==================================================
# ARCHITECTURE SCORE
# ==================================================

$architectureScore = 100


if($architecture)
{

    $violations = 0


    if($architecture.Violations)
    {

        if($architecture.Violations -is [array])
        {
            $violations =
            $architecture.Violations.Count
        }
        else
        {
            $violations =
            [int]$architecture.Violations
        }

    }


    $architectureScore -=
    ($violations * 5)

}



$architectureScore =
ClampScore $architectureScore



# ==================================================
# QUALITY SCORE
# ==================================================

$qualityScore = 100


if($quality)
{

    $fixme =
    [int]$quality.FIXME


    $notImplemented =
    [int]$quality.NotImplemented


    $largeFiles = 0


    if($quality.LargeFiles)
    {
        $largeFiles =
        @($quality.LargeFiles).Count
    }


    $qualityScore -=
    ($fixme * 0.5)


    $qualityScore -=
    ($notImplemented * 5)


    $qualityScore -=
    ($largeFiles * 0.05)

}



$qualityScore =
ClampScore $qualityScore



# ==================================================
# SECURITY SCORE
# ==================================================

$securityScore = 100


if($security)
{

    $secrets =
    [int]$security.SecretsFound


    $anonymous =
    [int]$security.AnonymousDetected


    $connections =
    [int]$security.ConnectionStringsFound



    $securityScore -=
    ($secrets * 0.2)


    $securityScore -=
    ($anonymous * 2)


    $securityScore -=
    ($connections * 2)

}



$securityScore =
ClampScore $securityScore



# ==================================================
# DEPENDENCY SCORE
# ==================================================

$dependencyScore = 100


if($dependencies)
{

    $violations = 0


    if($dependencies.Violations)
    {

        if($dependencies.Violations -is [array])
        {
            $violations =
            $dependencies.Violations.Count
        }
        else
        {
            $violations =
            [int]$dependencies.Violations
        }

    }


    $dependencyScore -=
    ($violations * 5)

}



$dependencyScore =
ClampScore $dependencyScore



# ==================================================
# FINAL SCORE
# ==================================================

$overall =
(
    ($healthScore * 0.30) +
    ($architectureScore * 0.20) +
    ($qualityScore * 0.20) +
    ($securityScore * 0.20) +
    ($dependencyScore * 0.10)
)


$overall =
ClampScore $overall



$result =
[ordered]@{

Generated =
Get-Date -Format "yyyy-MM-dd HH:mm:ss"


Architecture =
$architectureScore


ModuleHealth =
$healthScore


Quality =
$qualityScore


Security =
$securityScore


Dependencies =
$dependencyScore


Overall =
$overall

}



$output =
Join-Path `
$DataRoot `
"engineering-score.json"



$result |
ConvertTo-Json -Depth 10 |
Set-Content `
$output `
-Encoding UTF8



Write-Host ""

Write-Host "Engineering score generated successfully." -ForegroundColor Green

Write-Host ""

Write-Host "Architecture :" $result.Architecture "%"

Write-Host "Health       :" $result.ModuleHealth "%"

Write-Host "Quality      :" $result.Quality "%"

Write-Host "Security     :" $result.Security "%"

Write-Host "Dependencies :" $result.Dependencies "%"

Write-Host ""

Write-Host "OVERALL ERP ENGINEERING SCORE :" $result.Overall "%"