param(
    [string]$ServiceName = "ZHPrintAgent",
    [string]$DisplayName = "ZH Print Agent",
    [string]$PublishDirectory = "$PSScriptRoot\..\publish"
)

$ErrorActionPreference = "Stop"

function Assert-Administrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw "Run this script from an elevated PowerShell session."
    }
}

function Assert-ProductionSettings {
    param([string]$Directory)

    $settingsPath = Join-Path $Directory "appsettings.Production.json"
    if (-not (Test-Path -LiteralPath $settingsPath)) {
        throw "Production settings not found: $settingsPath. Copy appsettings.Production.sample.json and configure the local printer/API key first."
    }

    $settings = Get-Content -LiteralPath $settingsPath -Raw | ConvertFrom-Json
    $apiKey = $settings.PrintAgent.ApiKey
    if ([string]::IsNullOrWhiteSpace($apiKey) -or
        $apiKey -eq "local-dev-key-change-me" -or
        $apiKey -eq "replace-with-cash-register-local-secret") {
        throw "PrintAgent:ApiKey must be changed before installing the Windows Service."
    }
}

Assert-Administrator

$resolvedPublishDirectory = Resolve-Path -LiteralPath $PublishDirectory
$exePath = Join-Path $resolvedPublishDirectory "ZH.PrintAgent.App.exe"

if (-not (Test-Path -LiteralPath $exePath)) {
    throw "Published executable not found: $exePath. Run dotnet publish first."
}

Assert-ProductionSettings -Directory $resolvedPublishDirectory

$existing = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($existing) {
    throw "Service '$ServiceName' already exists. Stop/remove it or choose a different ServiceName."
}

New-Service `
    -Name $ServiceName `
    -DisplayName $DisplayName `
    -BinaryPathName "`"$exePath`"" `
    -StartupType Automatic `
    -Description "Local ZH Technologies POS receipt print agent."

sc.exe failure $ServiceName reset= 60 actions= restart/5000/restart/10000/restart/30000 | Out-Null
sc.exe failureflag $ServiceName 1 | Out-Null

Start-Service -Name $ServiceName
Get-Service -Name $ServiceName
