# =============================================================================
# ZH Technologies
# Progress Dashboard v5
# Dashboard Renderer v2
# =============================================================================

$ErrorActionPreference = "Stop"


$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path

$DataRoot = Join-Path $ProjectRoot "docs\ProgressDashboard\data"

$output = Join-Path $ProjectRoot "docs\ProgressDashboard\index.html"


Write-Host ""
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host " Dashboard Renderer v2"
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host ""



$model =
Get-Content (Join-Path $DataRoot "dashboard-model.json") -Raw |
ConvertFrom-Json



$healthRows = ""

foreach($module in $model.ModuleHealth.Modules)
{

    $healthRows += @"

<tr>
<td>$($module.Name)</td>
<td>$($module.Score)%</td>
<td>$($module.Domain)</td>
<td>$($module.Frontend)</td>
</tr>

"@

}



$riskRows = ""

foreach($module in $model.ModuleHealth.Modules)
{

    if($module.Score -lt 70)
    {

        $riskRows += @"

<tr>
<td>$($module.Name)</td>
<td>Below health threshold</td>
<td>$($module.Score)%</td>
</tr>

"@

    }

}



$html = @"

<!DOCTYPE html>

<html>

<head>

<meta charset="UTF-8">

<title>
ZH ERP Health Monitor
</title>


<style>

body {

font-family:Arial;
margin:40px;
background:#f4f6f8;

}


.card {

background:white;
padding:20px;
border-radius:10px;
display:inline-block;
margin:10px;
min-width:180px;

}


.number {

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
ZH Technologies ERP Health Monitor
</h1>


<p>
Generated: $($model.Generated)
</p>



<div>


<div class="card">

Projects

<div class="number">
$($model.Summary.Projects)
</div>

</div>



<div class="card">

Modules

<div class="number">
$($model.Summary.Modules)
</div>

</div>



<div class="card">

Health

<div class="number">
$($model.Summary.ModuleHealthAverage)%
</div>

</div>



<div class="card">

Architecture Issues

<div class="number">
$($model.Summary.DependencyViolations)
</div>

</div>



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


$healthRows


</table>

</div>





<div class="section">

<h2>
Risks
</h2>


<table>

<tr>

<th>
Module
</th>

<th>
Issue
</th>

<th>
Score
</th>

</tr>


$riskRows


</table>


</div>



</body>

</html>

"@



$html |
Set-Content $output -Encoding UTF8



Write-Host ""
Write-Host "Dashboard v2 generated." -ForegroundColor Green
Write-Host $output