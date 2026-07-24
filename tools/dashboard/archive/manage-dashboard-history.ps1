# =============================================================================
# ZH Technologies
# Progress Dashboard v8
# History Retention Manager v1.0
# =============================================================================

$ErrorActionPreference = "Stop"


$ProjectRoot =
(Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path


$HistoryRoot =
Join-Path $ProjectRoot "docs\ProgressDashboard\history"


$DataRoot =
Join-Path $ProjectRoot "docs\ProgressDashboard\data"



$KeepSnapshots = 90



Write-Host ""
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host " History Retention Manager v1.0"
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host ""



if(!(Test-Path $HistoryRoot))
{
    throw "History folder not found"
}



$snapshots =
Get-ChildItem `
$HistoryRoot `
-Filter "*.json" |
Sort-Object Name -Descending



$total =
$snapshots.Count



Write-Host ""
Write-Host "Snapshots found :" $total



$removed = 0



if($total -gt $KeepSnapshots)
{

    $removeList =
    $snapshots |
    Select-Object -Skip $KeepSnapshots



    foreach($file in $removeList)
    {

        Remove-Item $file.FullName

        $removed++

    }

}



$result = [ordered]@{


Generated =
Get-Date -Format "yyyy-MM-dd HH:mm:ss"



TotalBefore =
$total



RetentionLimit =
$KeepSnapshots



Removed =
$removed



Remaining =
(
Get-ChildItem `
$HistoryRoot `
-Filter "*.json"
).Count


}



$output =
Join-Path `
$DataRoot `
"history-retention.json"



$result |
ConvertTo-Json -Depth 10 |
Set-Content `
$output `
-Encoding UTF8



Write-Host ""

Write-Host "Retention completed successfully." -ForegroundColor Green

Write-Host ""

Write-Host "Removed :" $removed

Write-Host "Remaining :" $result.Remaining