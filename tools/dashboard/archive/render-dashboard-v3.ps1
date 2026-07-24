# =============================================================================
# ZH Technologies
# Progress Dashboard v6
# Dashboard Renderer v3
# =============================================================================

$ErrorActionPreference = "Stop"


$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path

$DataRoot = Join-Path $ProjectRoot "docs\ProgressDashboard\data"

$output = Join-Path $ProjectRoot "docs\ProgressDashboard\index.html"



Write-Host ""
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host " Dashboard Renderer v3"
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host ""



$model =
Get-Content `
(Join-Path $DataRoot "dashboard-model.json") `
-Raw |
ConvertFrom-Json



$rows = ""

foreach($m in $model.ModuleHealth.Modules)
{

$rows += @"

<tr>

<td>$($m.Name)</td>

<td>$($m.Score)%</td>

<td>$($m.Domain)</td>

<td>$($m.Frontend)</td>

</tr>

"@

}



$html = @"

<!DOCTYPE html>

<html>

<head>

<meta charset="UTF-8">

<title>
ZH Technologies ERP Dashboard
</title>


<style>

body {

font-family: Arial;
background:#f5f6f8;
margin:40px;

}


.card {

background:white;
padding:20px;
border-radius:12px;
display:inline-block;
margin:10px;

}


.value {

font-size:32px;
font-weight:bold;

}


table {

width:100%;
background:white;
border-collapse:collapse;
margin-top:20px;

}


td,th {

padding:10px;
border-bottom:1px solid #ddd;

}


.section {

margin-top:40px;

}


</style>


</head>


<body>


<h1>
ZH Technologies ERP Engineering Dashboard
</h1>


<p>
Generated:
$model.Generated
</p>



<h2>
Executive Summary
</h2>


<div class="card">

Projects

<div class="value">
$($model.Summary.Projects)
</div>

</div>


<div class="card">

Modules

<div class="value">
$($model.Summary.Modules)
</div>

</div>


<div class="card">

Health

<div class="value">
$($model.Summary.HealthAverage)%
</div>

</div>


<div class="card">

API

<div class="value">
$($model.Summary.APIEndpoints)
</div>

</div>


<div class="card">

Migrations

<div class="value">
$($model.Summary.Migrations)
</div>

</div>




<div class="section">

<h2>
Architecture
</h2>


<p>

Domain Files:
$model.Architecture.Layers.Domain.Files

</p>


<p>

Dependency Violations:
$model.Dependencies.Summary.Total

</p>


</div>




<div class="section">

<h2>
API
</h2>


<p>
Controllers:
$model.API.API.Controllers
</p>

<p>
Endpoints:
$model.API.API.Endpoints
</p>

<p>
Authorize:
$model.API.API.AuthorizeAttributes
</p>


</div>




<div class="section">

<h2>
Database
</h2>


<p>
DbContext:
$model.Database.Database.DbContext
</p>

<p>
DbSets:
$model.Database.Database.DbSets
</p>

<p>
Repositories:
$model.Database.Database.Repositories
</p>


</div>




<div class="section">

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

<th>
Domain
</th>

<th>
Frontend
</th>

</tr>


$rows


</table>


</div>


</body>

</html>

"@



$html |
Set-Content $output -Encoding UTF8



Write-Host ""

Write-Host "Dashboard Renderer v3 completed." -ForegroundColor Green

Write-Host ""

Write-Host $output