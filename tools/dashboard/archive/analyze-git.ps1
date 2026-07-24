# =============================================================================
# ZH Technologies
# Progress Dashboard v6
# Git Analyzer v1.0
# =============================================================================

$ErrorActionPreference = "Stop"


$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path

$DataRoot = Join-Path $ProjectRoot "docs\ProgressDashboard\data"


Write-Host ""
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host " Git Analyzer v1.0"
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host ""


if(!(Test-Path (Join-Path $ProjectRoot ".git")))
{
    throw "Git repository not found"
}


function Run-Git($command)
{
    return git -C $ProjectRoot $command
}



$branch = Run-Git "branch --show-current"


$commitCount = Run-Git "rev-list --count HEAD"


$lastCommitHash = Run-Git "rev-parse HEAD"


$lastCommitDate = Run-Git "log -1 --format=%ci"


$lastCommitMessage = Run-Git "log -1 --format=%s"



$recentCommits = @()


$logs = git -C $ProjectRoot log `
    -10 `
    --format="%h|%ad|%s" `
    --date=short



foreach($line in $logs)
{

    $parts = $line -split "\|"


    $recentCommits += [ordered]@{

        Hash = $parts[0]

        Date = $parts[1]

        Message = $parts[2]

    }

}



$changedFiles = git -C $ProjectRoot log `
    --name-only `
    --pretty=format: |
    Where-Object {
        $_ -and $_.EndsWith(".cs")
    }



$moduleActivity = @{}



foreach($file in $changedFiles)
{

    if($file -match "Modules\\([^\\]+)")
    {

        $module = $Matches[1]


        if(!$moduleActivity.ContainsKey($module))
        {
            $moduleActivity[$module] = 0
        }


        $moduleActivity[$module]++

    }

}



$modules = @()


foreach($key in $moduleActivity.Keys)
{

    $modules += [ordered]@{

        Name = $key

        Changes = $moduleActivity[$key]

    }

}



$modules =
$modules |
Sort-Object Changes -Descending



$result = [ordered]@{

Generated = Get-Date -Format "yyyy-MM-dd HH:mm:ss"


Branch = $branch


TotalCommits = [int]$commitCount


LastCommit = [ordered]@{

Hash = $lastCommitHash

Date = $lastCommitDate

Message = $lastCommitMessage

}



RecentCommits = $recentCommits


ModuleActivity = $modules


}



$output = Join-Path $DataRoot "git-analysis.json"



$result |
ConvertTo-Json -Depth 30 |
Set-Content $output -Encoding UTF8



Write-Host ""
Write-Host "Git analysis generated successfully." -ForegroundColor Green

Write-Host ""

Write-Host "Branch       : $branch"

Write-Host "Commits      : $commitCount"

Write-Host "Last Commit  : $lastCommitDate"

Write-Host "Modules Seen : $($modules.Count)"