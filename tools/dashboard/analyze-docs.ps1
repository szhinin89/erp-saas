# =============================================================================
# ZH Technologies
# Progress Dashboard v4
# Documentation Analyzer v1.0
# =============================================================================

$ErrorActionPreference = "Stop"


$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path

$DocsRoot = Join-Path $ProjectRoot "docs"

$DashboardData = Join-Path $ProjectRoot "docs\ProgressDashboard\data"


Write-Host ""
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host " Documentation Analyzer v1.0"
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host ""



if (!(Test-Path $DashboardData)) {
    New-Item -ItemType Directory -Path $DashboardData | Out-Null
}



function Test-FileExists($file)
{
    return Test-Path (Join-Path $ProjectRoot $file)
}



function Count-Files($path,$filter)
{
    if(Test-Path $path)
    {
        return (
            Get-ChildItem `
            -Path $path `
            -Recurse `
            -File `
            -Filter $filter `
            -ErrorAction SilentlyContinue
        ).Count
    }

    return 0
}



$rootDocuments = @(
    "CLAUDE.md",
    "STATUS.md",
    "FEATURES.md"
)



$documents = @()


foreach($doc in $rootDocuments)
{
    $documents += [ordered]@{

        Name = $doc

        Exists = Test-FileExists $doc
    }
}



$adrPath = Join-Path $DocsRoot "adr"

$adrCount = Count-Files $adrPath "*.md"



$docFiles = Count-Files $DocsRoot "*.md"



$result = [ordered]@{


    Generated = Get-Date -Format "yyyy-MM-dd HH:mm:ss"


    RootDocuments = $documents


    Documentation = [ordered]@{

        TotalMarkdownFiles = $docFiles

        ADRCount = $adrCount
    }


}



$output = Join-Path $DashboardData "docs-analysis.json"



$result |
ConvertTo-Json -Depth 20 |
Set-Content $output -Encoding UTF8



Write-Host ""
Write-Host "Markdown Files : $docFiles" -ForegroundColor Green
Write-Host "ADR Count      : $adrCount"


Write-Host ""

foreach($d in $documents)
{
    $status = if($d.Exists) {"FOUND"} else {"MISSING"}

    Write-Host "$($d.Name) : $status"
}


Write-Host ""
Write-Host "docs-analysis.json generated successfully." -ForegroundColor Green