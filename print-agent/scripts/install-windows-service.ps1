param(
    [string]$ServiceName = "ZHPrintAgent",
    [string]$DisplayName = "ZH Print Agent",
    [string]$PublishDirectory = "$PSScriptRoot\..\publish",
    [string]$DataDirectory = "C:\ProgramData\ZH Technologies\PrintAgent",
    [string]$AdminUrl = "http://127.0.0.1:9817/admin"
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
        throw "Production settings not found: $settingsPath. Copy appsettings.Production.sample.json first."
    }

    $settings = Get-Content -LiteralPath $settingsPath -Raw | ConvertFrom-Json
    $apiKey = $settings.PrintAgent.ApiKey
    $allowLan = [bool]$settings.PrintAgent.AllowLan
    $isSentinelKey = [string]::IsNullOrWhiteSpace($apiKey) -or
        $apiKey -eq "local-dev-key-change-me" -or
        $apiKey -eq "replace-with-cash-register-local-secret"

    if ($isSentinelKey -and $allowLan) {
        throw "PrintAgent:ApiKey must be changed before installing with AllowLan enabled."
    }

    if ($isSentinelKey) {
        Write-Host "Note: PrintAgent:ApiKey is still the sample value. The service will boot into local setup mode - complete the wizard at $AdminUrl before this till can be used."
    }
}

function New-DataDirectories {
    param([string]$Root)

    foreach ($subfolder in @("config", "data", "logs", "queue", "printed")) {
        New-Item -ItemType Directory -Force -Path (Join-Path $Root $subfolder) | Out-Null
    }
}

Assert-Administrator

$resolvedPublishDirectory = Resolve-Path -LiteralPath $PublishDirectory
$exePath = Join-Path $resolvedPublishDirectory "ZH.PrintAgent.App.exe"

if (-not (Test-Path -LiteralPath $exePath)) {
    throw "Published executable not found: $exePath. Run dotnet publish first."
}

Assert-ProductionSettings -Directory $resolvedPublishDirectory
New-DataDirectories -Root $DataDirectory

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

Write-Host "`nOpen $AdminUrl on this machine to finish configuring the till (printer, API key, test print)."
