# =============================================================================
# ZH Technologies
# Progress Dashboard v8
# Report Exporter v1.0
# =============================================================================

$ErrorActionPreference = "Stop"


$ProjectRoot =
(Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path


$DataRoot =
Join-Path $ProjectRoot "docs\ProgressDashboard\data"


$ReportRoot =
Join-Path $ProjectRoot "docs\ProgressDashboard\reports"



Write-Host ""
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host " Dashboard Report Exporter v1.0"
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host ""



if(!(Test-Path $ReportRoot))
{
    New-Item `
    -ItemType Directory `
    -Path $ReportRoot |
    Out-Null
}



$modelFile =
Join-Path $DataRoot "dashboard-model-v7.json"



if(!(Test-Path $modelFile))
{
    throw "dashboard-model-v7.json not found"
}



$model =
Get-Content $modelFile -Raw |
ConvertFrom-Json



$date =
Get-Date -Format "yyyy-MM-dd"



$jsonReport =
Join-Path `
$ReportRoot `
"ERP-Health-Report-$date.json"



$htmlReport =
Join-Path `
$ReportRoot `
"ERP-Health-Report-$date.html"



$model |
ConvertTo-Json -Depth 80 |
Set-Content `
$jsonReport `
-Encoding UTF8



$rows = ""

foreach($item in
($model.HealthScore | Sort-Object Score -Descending))
{

$rows += @"

<tr>

<td>$($item.Module)</td>

<td>$($item.Score)%</td>

<td>$($item.Architecture)%</td>

<td>$($item.Tests)%</td>

</tr>

"@

}



$html = @"

<!DOCTYPE html>

<html>

<head>

<meta charset="UTF-8">

<title>
ZH ERP Health Report
</title>


<style>

body{

font-family:Arial;
margin:40px;
background:#f4f6f8;

}


.card{

background:white;
padding:20px;
border-radius:10px;
margin-bottom:20px;

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

</style>


</head>


<body>


<h1>
ZH Technologies ERP Health Report
</h1>


<div class="card">

<h2>
Summary
</h2>


<p>
Projects:
$model.Summary.Projects
</p>


<p>
Modules:
$model.Summary.Modules
</p>


<p>
Health:
$model.Summary.HealthAverage %
</p>


<p>
API Endpoints:
$model.API.API.Endpoints
</p>


<p>
Migrations:
$model.Migrations.Summary.TotalFiles
</p>


</div>



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
Architecture
</th>

<th>
Tests
</th>

</tr>


$rows


</table>



</body>

</html>

"@



$html |
Set-Content `
$htmlReport `
-Encoding UTF8



Write-Host ""

Write-Host "Report exported successfully." -ForegroundColor Green

Write-Host ""

Write-Host "JSON:"
Write-Host $jsonReport

Write-Host ""

Write-Host "HTML:"
Write-Host $htmlReport