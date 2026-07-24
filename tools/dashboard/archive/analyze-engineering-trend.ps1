# =============================================================================
# ZH Technologies
# Progress Dashboard v11
# Engineering Trend Analyzer v1.0
# =============================================================================

$ErrorActionPreference = "Stop"


$ProjectRoot =
(Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path


$HistoryRoot =
Join-Path $ProjectRoot "docs\ProgressDashboard\history"


$DataRoot =
Join-Path $ProjectRoot "docs\ProgressDashboard\data"



Write-Host ""
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host " Engineering Trend Analyzer v1.0"
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host ""



if(!(Test-Path $HistoryRoot))
{
    throw "History folder not found."
}



$snapshots =
Get-ChildItem `
$HistoryRoot `
-Filter "*.json" |
Sort-Object Name



$history = @()



foreach($file in $snapshots)
{

    try
    {

        $snapshot =
        Get-Content $file.FullName -Raw |
        ConvertFrom-Json



        $score = $null



        if($snapshot.EngineeringScore)
        {
            $score =
            $snapshot.EngineeringScore.Overall
        }


        elseif($snapshot.Summary.EngineeringScore)
        {
            $score =
            $snapshot.Summary.EngineeringScore
        }



        if($score -ne $null)
        {

            $history +=
            [ordered]@{

                Date =
                $snapshot.Generated


                File =
                $file.Name


                Score =
                [double]$score

            }

        }

    }

    catch
    {

        Write-Host `
        "Skipped:" `
        $file.Name `
        -ForegroundColor Yellow

    }

}



$result =
[ordered]@{

Generated =
Get-Date -Format "yyyy-MM-dd HH:mm:ss"


Snapshots =
$history.Count


History =
$history

}



$output =
Join-Path `
$DataRoot `
"engineering-trend.json"



$result |
ConvertTo-Json -Depth 20 |
Set-Content `
$output `
-Encoding UTF8



Write-Host ""

Write-Host "Engineering trend generated successfully." -ForegroundColor Green

Write-Host ""

Write-Host "Snapshots analyzed :" $history.Count

Write-Host ""

Write-Host "Output:"
Write-Host $output