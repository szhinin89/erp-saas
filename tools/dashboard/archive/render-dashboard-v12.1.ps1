# =============================================================================
# ZH Technologies
# Progress Dashboard v12
# Dashboard Renderer v12
# =============================================================================

$ErrorActionPreference = "Stop"


$ProjectRoot =
(Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path


$DataRoot =
Join-Path $ProjectRoot "docs\ProgressDashboard\data"


$Output =
Join-Path $ProjectRoot "docs\ProgressDashboard\index.html"



Write-Host ""
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host " Dashboard Renderer v12"
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



$model =
LoadJson "dashboard-model-v12.json"

$architecture = $model.Architecture

$dependencies = $model.Dependencies

$criticalFindings = $model.CriticalFindings


if(!$model)
{
    throw "dashboard-model-v12.json not found"
}



$score =
$model.EngineeringScore


$gate =
$model.QualityGate



$warnings = ""

foreach($item in $gate.Warnings)
{
    $warnings += "<li>$item</li>"
}



$strengths = ""

foreach($item in $gate.Strengths)
{
    $strengths += "<li>$item</li>"
}



$modules = ""

foreach($m in $model.Health.value)
{
    $modules += @"

<tr>

<td>$($m.Module)</td>

<td>$($m.Score)%</td>

</tr>

"@
}



$html = @"

<!DOCTYPE html>

<html>

<head>

<title>
ZH Technologies ERP Dashboard v12
</title>


<style>

body
{
font-family:Segoe UI,Arial;
background:#f4f6f8;
margin:40px;
}


.card
{
background:white;
padding:25px;
margin-bottom:20px;
border-radius:12px;
box-shadow:0 3px 10px #ccc;
}


.score
{
font-size:72px;
font-weight:bold;
}


.red
{
color:#b00020;
}


.yellow
{
color:#856404;
}


.green
{
color:#087f23;
}


table
{
width:100%;
border-collapse:collapse;
}


td,th
{
padding:10px;
border-bottom:1px solid #ddd;
}


</style>


</head>


<body>


<h1>
ZH Technologies ERP Dashboard v12
</h1>



<div class="card">

<h2>
Engineering Score
</h2>

<div class="score">

$($score.Overall)%

</div>


<p>
Architecture:
$($score.Architecture)%
</p>


<p>
Health:
$($score.ModuleHealth)%
</p>


<p>
Quality:
$($score.Quality)%
</p>


<p>
Security:
$($score.Security)%
</p>


<p>
Dependencies:
$($score.Dependencies)%
</p>


</div>




<div class="card">

<h2>
Quality Gate
</h2>


<h1>

$($gate.Status)

</h1>


<h3>
Warnings
</h3>

<ul>

$warnings

</ul>


<h3>
Strengths
</h3>

<ul>

$strengths

</ul>


</div>




<div class="card">

<h2>
Module Health
</h2>


<table>

<tr>

<th>
Module
</th>

<th>
Score
</th>

</tr>


$modules


</table>


</div>



</body>

</html>

"@



$html |
Set-Content `
$output `
-Encoding UTF8



Write-Host ""

Write-Host "Dashboard v12 generated successfully." -ForegroundColor Green

Write-Host ""

Write-Host $Output