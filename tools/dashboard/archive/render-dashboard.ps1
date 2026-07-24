# =============================================================================
# ZH Technologies
# Progress Dashboard v4
# Dashboard Renderer v1.0
# =============================================================================

$ErrorActionPreference = "Stop"


$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path

$DataRoot = Join-Path $ProjectRoot "docs\ProgressDashboard\data"

$OutputFile = Join-Path $ProjectRoot "docs\ProgressDashboard\index.html"



Write-Host ""
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host " Dashboard Renderer v1.0"
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host ""



$modelFile = Join-Path $DataRoot "dashboard-model.json"


if(!(Test-Path $modelFile))
{
    throw "dashboard-model.json not found. Run build-dashboard.ps1 first."
}



$model = Get-Content $modelFile -Raw | ConvertFrom-Json



$summary = $model.Summary



$backendRows = ""

foreach($project in $model.Backend.Projects)
{
    $backendRows += @"

<tr>
<td>$($project.Name)</td>
<td>$($project.Layer)</td>
<td>$($project.Type)</td>
<td>$($project.Modules.Count)</td>
</tr>

"@
}



$moduleRows = ""

foreach($module in $model.Backend.Modules)
{
    $moduleRows += @"

<tr>
<td>$($module.Name)</td>
<td>$([string]::Join(", ",$module.Layers))</td>
<td>$([string]::Join(", ",$module.Projects))</td>
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

font-family: Arial, sans-serif;
margin:40px;
background:#f5f6f8;

}


h1 {

color:#1f2937;

}


.cards {

display:flex;
gap:20px;
flex-wrap:wrap;

}


.card {

background:white;
padding:20px;
border-radius:10px;
min-width:180px;
box-shadow:0 2px 8px rgba(0,0,0,.1);

}


.number {

font-size:32px;
font-weight:bold;

}


table {

width:100%;
border-collapse:collapse;
background:white;
margin-top:20px;

}


th,td {

padding:10px;
border-bottom:1px solid #ddd;
text-align:left;

}


th {

background:#eee;

}


.section {

margin-top:40px;

}

</style>


</head>


<body>


<h1>
ZH Technologies ERP Dashboard
</h1>


<p>
Generated:
$model.Generated
</p>



<div class="cards">


<div class="card">
Projects
<div class="number">
$($summary.Projects)
</div>
</div>


<div class="card">
Modules
<div class="number">
$($summary.Modules)
</div>
</div>


<div class="card">
Frontend Files
<div class="number">
$($summary.FrontendFiles)
</div>
</div>


<div class="card">
Tests
<div class="number">
$($summary.Tests)
</div>
</div>


<div class="card">
ADR
<div class="number">
$($summary.ADRs)
</div>
</div>


</div>




<div class="section">

<h2>
Backend Projects
</h2>


<table>

<tr>
<th>
Project
</th>

<th>
Layer
</th>

<th>
Type
</th>

<th>
Modules
</th>

</tr>


$backendRows


</table>

</div>





<div class="section">

<h2>
Backend Modules
</h2>


<table>

<tr>

<th>
Module
</th>

<th>
Layers
</th>

<th>
Projects
</th>

</tr>


$moduleRows


</table>


</div>




</body>

</html>
"@



$html |
Set-Content $OutputFile -Encoding UTF8



Write-Host ""
Write-Host "Dashboard generated successfully." -ForegroundColor Green

Write-Host ""
Write-Host "Output:"
Write-Host $OutputFile