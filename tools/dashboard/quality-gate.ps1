# =============================================================================
# ZH Technologies
# Progress Dashboard v12
# Quality Gate Engine v1.0
# =============================================================================

$ErrorActionPreference = "Stop"


$ProjectRoot =
(Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path


$DataRoot =
Join-Path $ProjectRoot "docs\ProgressDashboard\data"



Write-Host ""
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host " Quality Gate Engine v1.0"
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host ""



function LoadJson($file)
{
    $path = Join-Path $DataRoot $file

    if(Test-Path $path)
    {
        return Get-Content $path -Raw |
        ConvertFrom-Json
    }

    return $null
}



$engineering =
LoadJson "engineering-score.json"


$debt =
LoadJson "technical-debt.json"


$security =
LoadJson "security-analysis.json"


$dependencies =
LoadJson "dependency-analysis.json"


$health =
LoadJson "health-score.json"



$status = "GREEN"

$warnings = @()

$strengths = @()



# -------------------------------
# Engineering Score
# -------------------------------

if($engineering)
{

    if($engineering.Overall -lt 60)
    {
        $status="RED"
        $warnings += "Engineering score below 60%"
    }
    elseif($engineering.Overall -lt 80)
    {
        $status="YELLOW"
        $warnings += "Engineering score needs improvement"
    }
    else
    {
        $strengths += "Engineering score healthy"
    }

}



# -------------------------------
# Security
# -------------------------------

if($security)
{

    if($security.SecretsFound -gt 50)
    {
        $status="RED"
        $warnings += "High number of possible secrets detected"
    }


    elseif($security.SecretsFound -gt 10)
    {
        if($status -eq "GREEN")
        {
            $status="YELLOW"
        }

        $warnings += "Secrets review recommended"
    }


    if($security.AnonymousDetected -eq 0)
    {
        $strengths += "No anonymous endpoints detected"
    }

}



# -------------------------------
# Technical Debt
# -------------------------------

if($debt)
{

    if($debt.TODO -gt 500)
    {
        if($status -eq "GREEN")
        {
            $status="YELLOW"
        }

        $warnings += "High TODO count"
    }


    if($debt.NotImplemented -gt 0)
    {
        $warnings += "Not implemented code detected"
    }

}



# -------------------------------
# Dependencies
# -------------------------------

if($dependencies)
{

    if($dependencies.Violations -gt 0)
    {
        $status="RED"
        $warnings += "Architecture dependency violations detected"
    }
    else
    {
        $strengths += "Dependency rules respected"
    }

}



# -------------------------------
# Module Health
# -------------------------------

if($health)
{

    $avg =
    ($health.Score |
    Measure-Object -Average).Average


    if($avg -ge 75)
    {
        $strengths += "Module health above 75%"
    }

}



$result =
[ordered]@{


Generated =
Get-Date -Format "yyyy-MM-dd HH:mm:ss"


Status =
$status


EngineeringScore =
$engineering.Overall


Warnings =
$warnings


Strengths =
$strengths


}



$output =
Join-Path `
$DataRoot `
"quality-gate.json"



$result |
ConvertTo-Json -Depth 20 |
Set-Content `
$output `
-Encoding UTF8



Write-Host ""

Write-Host "Quality gate generated successfully." -ForegroundColor Green

Write-Host ""

Write-Host "Status:"
Write-Host $status

Write-Host ""

Write-Host "Warnings:"
$warnings | ForEach-Object {
    Write-Host " - $_"
}

Write-Host ""

Write-Host "Output:"
Write-Host $output