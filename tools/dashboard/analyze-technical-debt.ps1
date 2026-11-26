# =============================================================================
# ZH Technologies
# Progress Dashboard v8
# Technical Debt Analyzer v1.0
# =============================================================================

$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "source-population.ps1")


$ProjectRoot =
(Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path


$SourceRoot =
Join-Path $ProjectRoot "backend"


$FrontendRoot =
Join-Path $ProjectRoot "frontend"


$DataRoot =
Join-Path $ProjectRoot "docs\ProgressDashboard\data"



Write-Host ""
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host " Technical Debt Analyzer v1.0"
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host ""



$backendInclude = @("*.cs")
$frontendInclude = @("*.ts", "*.tsx", "*.js", "*.jsx")

$backendFiles = @(Get-SourceFiles $ProjectRoot @("backend") $backendInclude)
$frontendFiles = @(Get-SourceFiles $ProjectRoot @("frontend") $frontendInclude)
$files = $backendFiles + $frontendFiles

Register-SourcePopulation $ProjectRoot "analyze-technical-debt:backend" @("backend") $backendInclude $backendFiles
Register-SourcePopulation $ProjectRoot "analyze-technical-debt:frontend" @("frontend") $frontendInclude $frontendFiles



$todo = 0
$fixme = 0
$hack = 0
$notImplemented = 0

$largeFiles = @()



foreach($file in $files)
{

   try
{
    $content =
    Get-Content `
    $file.FullName `
    -Raw `
    -ErrorAction Stop
}
catch
{
    Write-Host "Skipped unreadable file: $($file.FullName)" -ForegroundColor DarkYellow
    continue
}


if([string]::IsNullOrWhiteSpace($content))
{
    continue
}



    $todo +=
    ([regex]::Matches(
        $content,
        "TODO"
    )).Count



    $fixme +=
    ([regex]::Matches(
        $content,
        "FIXME"
    )).Count



    $hack +=
    ([regex]::Matches(
        $content,
        "HACK"
    )).Count



    $notImplemented +=
    ([regex]::Matches(
        $content,
        "NotImplementedException"
    )).Count



    $lines =
@($content -split "`n").Count



    if($lines -gt 500)
    {

        $largeFiles += [ordered]@{

            File =
            Get-SourceRelativePath $ProjectRoot $file.FullName

            Lines =
            $lines

        }

    }

}



$result = [ordered]@{


Generated =
Get-Date -Format "yyyy-MM-dd HH:mm:ss"



FilesAnalyzed =
$files.Count



TODO =
$todo



FIXME =
$fixme



HACK =
$hack



NotImplemented =
$notImplemented



LargeFiles =
$largeFiles



CriticalFindings =
(
    $notImplemented +
    $fixme
)


}



$output =
Join-Path `
$DataRoot `
"technical-debt.json"



$result |
ConvertTo-Json -Depth 20 |
Set-Content `
$output `
-Encoding UTF8



Write-Host ""

Write-Host "Technical debt analysis generated." -ForegroundColor Green

Write-Host ""

Write-Host "Files analyzed :" $files.Count

Write-Host "TODO           :" $todo

Write-Host "FIXME          :" $fixme

Write-Host "NotImplemented :" $notImplemented

Write-Host "Large Files    :" $largeFiles.Count