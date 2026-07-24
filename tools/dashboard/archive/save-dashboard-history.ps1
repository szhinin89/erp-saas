$ProjectRoot =
(Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path


$Source =
Join-Path $ProjectRoot "docs\ProgressDashboard\data\dashboard-model-v12.json"


$History =
Join-Path $ProjectRoot "docs\ProgressDashboard\history"


if(!(Test-Path $History))
{
    New-Item -ItemType Directory -Path $History | Out-Null
}


$date =
Get-Date -Format "yyyy-MM-dd_HHmmss"


Copy-Item `
$Source `
(Join-Path $History "dashboard-$date.json")


Write-Host "Snapshot saved"