# =============================================================================
# ZH Technologies
# Progress Dashboard v11
# Dashboard Renderer v11
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
Write-Host " Dashboard Renderer v11"
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host ""



function LoadJson($file)
{

    $path =
    Join-Path $DataRoot $file


    if(Test-Path $path)
    {
        return Get-Content $path -Raw |
        ConvertFrom-Json
    }

    return $null

}



$engineering =
LoadJson "engineering-score.json"


$trend =
LoadJson "engineering-trend.json"


$health =
LoadJson "health-score.json"



if(!$engineering)
{
    throw "engineering-score.json not found."
}



$trendRows = ""


if($trend.History)
{

    foreach($item in $trend.History)
    {

        $trendRows += @"

<tr>

<td>
$($item.Date)
</td>


<td>
$($item.Score) %
</td>


</tr>

"@

    }

}



$moduleRows = ""


foreach($module in $health)
{

$moduleRows += @"

<tr>

<td>
$($module.Module)
</td>

<td>
$($module.Score) %
</td>

</tr>

"@

}



$html = @"

<!DOCTYPE html>

<html>

<head>

<title>
ZH Technologies ERP Dashboard v11
</title>


<style>

body
{
font-family:Segoe UI,Arial;
background:#f5f6fa;
margin:40px;
}


.card
{
background:white;
padding:25px;
margin-bottom:25px;
border-radius:12px;
box-shadow:0 2px 8px #ccc;
}


.score
{
font-size:70px;
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
ZH Technologies ERP Dashboard v11
</h1>



<div class="card">


<h2>
ERP Engineering Score
</h2>


<div class="score">

$($engineering.Overall) %

</div>


<p>
Architecture:
$($engineering.Architecture) %
</p>


<p>
Health:
$($engineering.ModuleHealth) %
</p>


<p>
Quality:
$($engineering.Quality) %
</p>


<p>
Security:
$($engineering.Security) %
</p>


<p>
Dependencies:
$($engineering.Dependencies) %
</p>


</div>




<div class="card">

<h2>
Engineering Trend
</h2>


<table>

<tr>
<th>
Date
</th>

<th>
Score
</th>

</tr>


$trendRows


</table>

</div>




<div class="card">

<h2>
Module Health Ranking
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


$moduleRows


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

Write-Host "Dashboard v11 generated successfully." -ForegroundColor Green

Write-Host ""

Write-Host $Output