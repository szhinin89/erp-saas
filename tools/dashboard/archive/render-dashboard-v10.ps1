# =============================================================================
# ZH Technologies
# Progress Dashboard v10
# Dashboard Renderer v10
# =============================================================================

$ErrorActionPreference = "Stop"


$ProjectRoot =
(Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path


$DataRoot =
Join-Path $ProjectRoot "docs\ProgressDashboard\data"


$OutputRoot =
Join-Path $ProjectRoot "docs\ProgressDashboard"


$modelFile =
Join-Path $DataRoot "dashboard-model-v10.json"


$output =
Join-Path $OutputRoot "index.html"



Write-Host ""
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host " Dashboard Renderer v10"
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host ""



if(!(Test-Path $modelFile))
{
    throw "Dashboard model not found."
}



$model =
Get-Content $modelFile -Raw |
ConvertFrom-Json



$score =
$model.EngineeringScore



$healthRows = ""


if($model.Health)
{

    foreach($module in $model.Health)
    {

        $healthRows += @"

<tr>
<td>$($module.Module)</td>
<td>$($module.Score)%</td>
</tr>

"@

    }

}



$html = @"
<!DOCTYPE html>

<html>

<head>

<title>
ZH Technologies ERP Dashboard v10
</title>


<style>

body
{
font-family:Segoe UI,Arial;
margin:40px;
background:#f5f6fa;
}


.card
{
background:white;
padding:25px;
margin-bottom:20px;
border-radius:12px;
box-shadow:0 2px 8px #ccc;
}


.score
{
font-size:60px;
font-weight:bold;
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
ZH Technologies ERP Dashboard v10
</h1>



<div class="card">

<h2>
ERP Engineering Score
</h2>


<div class="score">
$($score.Overall) %
</div>


<p>
Architecture:
$($score.Architecture) %
</p>


<p>
Module Health:
$($score.ModuleHealth) %
</p>


<p>
Quality:
$($score.Quality) %
</p>


<p>
Security:
$($score.Security) %
</p>


<p>
Dependencies:
$($score.Dependencies) %
</p>


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


$healthRows


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

Write-Host "Dashboard v10 generated successfully." -ForegroundColor Green

Write-Host ""

Write-Host $output