<#
.SYNOPSIS
Compiles ZH-Print-Agent-Setup.exe from the win-x64 publish output using Inno Setup.

.DESCRIPTION
Requires Inno Setup 6 (ISCC.exe) to be installed on this machine - https://jrsoftware.org/isdl.php.
Runs publish-win-x64.ps1 automatically if the win-x64 publish output is missing; pass -Force to
always republish first.
#>
param(
    [string]$Configuration = "Release",
    [switch]$Force
)

$ErrorActionPreference = "Stop"

function Find-Iscc {
    $candidates = @(
        (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe"),
        (Join-Path $env:ProgramFiles "Inno Setup 6\ISCC.exe"),
        (Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe")
    )

    foreach ($candidate in $candidates) {
        if ($candidate -and (Test-Path -LiteralPath $candidate)) {
            return $candidate
        }
    }

    $onPath = Get-Command "ISCC.exe" -ErrorAction SilentlyContinue
    if ($onPath) {
        return $onPath.Source
    }

    return $null
}

$isccPath = Find-Iscc
if (-not $isccPath) {
    throw "Inno Setup (ISCC.exe) was not found. Install Inno Setup 6 from https://jrsoftware.org/isdl.php and re-run this script."
}

Write-Host "Using Inno Setup compiler: $isccPath"

$publishOutputExe = Join-Path $PSScriptRoot "..\publish\win-x64\ZH.PrintAgent.App.exe"
if ($Force -or -not (Test-Path -LiteralPath $publishOutputExe)) {
    Write-Host "win-x64 publish output missing or -Force specified; running publish-win-x64.ps1..."
    & "$PSScriptRoot\publish-win-x64.ps1" -Configuration $Configuration
}

$issPath = Resolve-Path -LiteralPath "$PSScriptRoot\..\installers\windows\inno\ZH.PrintAgent.iss"

Write-Host "Compiling installer from $issPath..."
& "$isccPath" "$issPath"

if ($LASTEXITCODE -ne 0) {
    throw "ISCC.exe failed with exit code $LASTEXITCODE."
}

$installerExe = Resolve-Path -LiteralPath "$PSScriptRoot\..\publish\installer\ZH-Print-Agent-Setup.exe" -ErrorAction SilentlyContinue
if (-not $installerExe) {
    throw "Installer compilation reported success but ZH-Print-Agent-Setup.exe was not found at the expected output path."
}

Write-Host "`nInstaller ready: $installerExe"
