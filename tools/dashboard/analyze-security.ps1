# =============================================================================
# ZH Technologies
# Progress Dashboard v8
# Security Analyzer v1.0
# =============================================================================

$ErrorActionPreference = "Stop"


$ProjectRoot =
(Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path


$BackendRoot =
Join-Path $ProjectRoot "backend"


$FrontendRoot =
Join-Path $ProjectRoot "frontend"


$DataRoot =
Join-Path $ProjectRoot "docs\ProgressDashboard\data"



Write-Host ""
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host " Security Analyzer v1.0"
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host ""



$files = @()



if(Test-Path $BackendRoot)
{
    $files += Get-ChildItem `
    $BackendRoot `
    -Recurse `
    -Include *.cs,*.json,*.config `
    -File
}


if(Test-Path $FrontendRoot)
{
    $files += Get-ChildItem `
    $FrontendRoot `
    -Recurse `
    -Include *.ts,*.tsx,*.js,*.jsx `
    -File
}



$secrets = @()

$anonymous = @()

$connectionStrings = @()



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
        continue
    }


    if([string]::IsNullOrWhiteSpace($content))
    {
        continue
    }



    if(
        $content -match
        "(password|secret|apikey|api_key|private_key)\s*[:=]"
    )
    {

        $secrets += $file.FullName

    }



    if(
        $content -match
        "AllowAnonymous"
    )
    {

        $anonymous += $file.FullName

    }



    if(
        $content -match
        "ConnectionStrings"
    )
    {

        $connectionStrings += $file.FullName

    }

}



$result = [ordered]@{


Generated =
Get-Date -Format "yyyy-MM-dd HH:mm:ss"



FilesAnalyzed =
$files.Count



SecretsFound =
$secrets.Count



SecretFiles =
$secrets



AnonymousDetected =
$anonymous.Count



AnonymousFiles =
$anonymous



ConnectionStringsFound =
$connectionStrings.Count



ConnectionStringFiles =
$connectionStrings



Warnings =
(
    $secrets.Count +
    $anonymous.Count +
    $connectionStrings.Count
)


}



$output =
Join-Path `
$DataRoot `
"security-analysis.json"



$result |
ConvertTo-Json -Depth 20 |
Set-Content `
$output `
-Encoding UTF8



Write-Host ""

Write-Host "Security analysis generated." -ForegroundColor Green

Write-Host ""

Write-Host "Files analyzed       :" $files.Count

Write-Host "Secrets detected     :" $secrets.Count

Write-Host "Anonymous detected   :" $anonymous.Count

Write-Host "Connection strings   :" $connectionStrings.Count