# =============================================================================
# ZH Technologies
# Progress Dashboard v9
# HTML Renderer
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
Write-Host " Dashboard Renderer v9"
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host ""



$model =
Get-Content `
(Join-Path $DataRoot "dashboard-model-v9.json") `
-Raw |
ConvertFrom-Json



$quality =
$model.Quality



$html = @"

<!DOCTYPE html>

<html>

<head>

<title>
ZH Technologies ERP Dashboard v9
</title>


<meta charset="UTF-8">


<style>

body {

font-family: Arial, sans-serif;
margin:40px;

}


.card {

border:1px solid #ddd;
border-radius:8px;
padding:20px;
margin-bottom:20px;

}


.title {

font-size:24px;
font-weight:bold;

}


.metric {

font-size:18px;
margin:8px;

}


</style>


</head>


<body>


<div class="title">
ZH Technologies ERP Dashboard v9
</div>


<div class="card">

<h2>
System Health
</h2>


<div class="metric">
Modules:
$($model.HealthScore.Count)
</div>


</div>



<div class="card">

<h2>
Technical Debt
</h2>


<div class="metric">
TODO:
$($quality.TechnicalDebt.TODO)
</div>


<div class="metric">
FIXME:
$($quality.TechnicalDebt.FIXME)
</div>


<div class="metric">
Not Implemented:
$($quality.TechnicalDebt.NotImplemented)
</div>


<div class="metric">
Large Files:
$($quality.TechnicalDebt.LargeFiles.Count)
</div>


</div>



<div class="card">

<h2>
Security
</h2>


<div class="metric">
Warnings:
$($quality.Security.Warnings)
</div>


<div class="metric">
Secrets:
$($quality.Security.SecretsFound)
</div>


<div class="metric">
Anonymous:
$($quality.Security.AnonymousDetected)
</div>


<div class="metric">
Connection Strings:
$($quality.Security.ConnectionStringsFound)
</div>


</div>



</body>

</html>

"@



$html |
Set-Content `
$Output `
-Encoding UTF8



Write-Host ""

Write-Host "Dashboard v9 generated successfully." -ForegroundColor Green

Write-Host ""

Write-Host $Output