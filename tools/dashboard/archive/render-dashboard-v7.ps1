# =============================================================================
# ZH Technologies
# Progress Dashboard v7
# Dashboard Renderer v7
# =============================================================================

$ErrorActionPreference = "Stop"


$ProjectRoot =
(Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path


$DataRoot =
Join-Path $ProjectRoot "docs\ProgressDashboard\data"


$output =
Join-Path $ProjectRoot "docs\ProgressDashboard\index.html"



Write-Host ""
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host " Dashboard Renderer v7"
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host ""



$model =
Get-Content `
(Join-Path $DataRoot "dashboard-model-v7.json") `
-Raw |
ConvertFrom-Json



$healthRows = ""


foreach($item in
($model.HealthScore |
Sort-Object Score -Descending))
{

$healthRows += @"

<tr>

<td>$($item.Module)</td>

<td>$($item.Score)%</td>

<td>$($item.Architecture)%</td>

<td>$($item.Tests)%</td>

<td>$($item.Backend)%</td>

</tr>

"@

}



$diffSection = ""


if($model.SnapshotDiff)
{

$diffSection = @"

<h2>
Last Evolution
</h2>

<table>

<tr>
<th>Metric</th>
<th>Change</th>
</tr>

<tr>
<td>Projects</td>
<td>$($model.SnapshotDiff.Changes.Projects)</td>
</tr>


<tr>
<td>Modules</td>
<td>$($model.SnapshotDiff.Changes.Modules)</td>
</tr>


<tr>
<td>API Endpoints</td>
<td>$($model.SnapshotDiff.Changes.APIEndpoints)</td>
</tr>


<tr>
<td>Migrations</td>
<td>$($model.SnapshotDiff.Changes.Migrations)</td>
</tr>


<tr>
<td>Health</td>
<td>$($model.SnapshotDiff.Changes.Health)</td>
</tr>


</table>

"@

}



$html = @"

<!DOCTYPE html>

<html>

<head>

<meta charset="UTF-8">

<title>
ZH Technologies ERP Dashboard v7
</title>


<style>

body{

font-family:Arial;
background:#f4f6f8;
margin:40px;

}


.card{

background:white;
padding:20px;
margin:10px;
display:inline-block;
border-radius:12px;

}


.value{

font-size:30px;
font-weight:bold;

}


table{

width:100%;
background:white;
border-collapse:collapse;

}


td,th{

padding:10px;
border-bottom:1px solid #ddd;

}


.section{

margin-top:40px;

}


</style>


</head>


<body>


<h1>
ZH Technologies ERP Engineering Dashboard v7
</h1>


<p>
Generated:
$model.Generated
</p>



<div class="card">

Projects

<div class="value">
$model.Summary.Projects
</div>

</div>



<div class="card">

Modules

<div class="value">
$model.Summary.Modules
</div>

</div>



<div class="card">

Health

<div class="value">
$model.Summary.HealthAverage %
</div>

</div>



<div class="card">

API

<div class="value">
$model.API.API.Endpoints
</div>

</div>



<div class="section">

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

<th>
Architecture
</th>

<th>
Tests
</th>

<th>
Backend
</th>

</tr>


$healthRows


</table>


</div>



<div class="section">

<h2>
Engineering Metrics
</h2>


<p>
Controllers:
$model.API.API.Controllers
</p>


<p>
Database DbSets:
$model.Database.Database.DbSets
</p>


<p>
Migration Files:
$model.Migrations.Summary.TotalFiles
</p>



</div>



<div class="section">

$diffSection

</div>



</body>

</html>

"@



$html |
Set-Content $output -Encoding UTF8



Write-Host ""

Write-Host "Dashboard v7 generated successfully." -ForegroundColor Green

Write-Host ""

Write-Host $output