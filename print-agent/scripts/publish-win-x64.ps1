<#
.SYNOPSIS
Publishes ZH.PrintAgent.App as a self-contained win-x64 build ready to be packaged by the Inno Setup installer.

.DESCRIPTION
Self-contained is used on purpose: cash-register PCs are not expected to have the matching .NET runtime
preinstalled, and the goal is a double-click installer with zero manual prerequisites. Pass
-SelfContained:$false to produce a framework-dependent build instead (requires the .NET 8 ASP.NET Core
Runtime to already be installed on the target machine - only do this if you know every till already has it).
#>
param(
    [string]$Configuration = "Release",
    [bool]$SelfContained = $true,
    [string]$OutputDir = "$PSScriptRoot\..\publish\win-x64"
)

$ErrorActionPreference = "Stop"

$projectPath = Resolve-Path -LiteralPath "$PSScriptRoot\..\src\ZH.PrintAgent.App\ZH.PrintAgent.App.csproj"

if (Test-Path -LiteralPath $OutputDir) {
    Write-Host "Cleaning previous publish output: $OutputDir"
    Remove-Item -LiteralPath $OutputDir -Recurse -Force
}

$selfContainedArg = if ($SelfContained) { "true" } else { "false" }

Write-Host "Publishing $projectPath ($Configuration, win-x64, self-contained=$selfContainedArg)..."

dotnet publish "$projectPath" `
    --configuration $Configuration `
    --runtime win-x64 `
    --self-contained $selfContainedArg `
    --output "$OutputDir" `
    /p:PublishReadyToRun=false

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

# appsettings.Development.json ships with a shared dev API key and simulated test printers - never
# meant to reach a customer install. appsettings.Production.json (real, per-till config) is written by
# the installer, not by this publish step, so no production secrets ever pass through here either.
$devSettingsPath = Join-Path $OutputDir "appsettings.Development.json"
if (Test-Path -LiteralPath $devSettingsPath) {
    Remove-Item -LiteralPath $devSettingsPath -Force
    Write-Host "Removed appsettings.Development.json from publish output."
}

foreach ($excluded in @("data", "logs", "TestResults")) {
    $excludedPath = Join-Path $OutputDir $excluded
    if (Test-Path -LiteralPath $excludedPath) {
        Remove-Item -LiteralPath $excludedPath -Recurse -Force
        Write-Host "Removed unexpected '$excluded' folder from publish output."
    }
}

Write-Host "`nPublish output ready: $OutputDir"
