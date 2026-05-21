<#
.SYNOPSIS
    Entorno reproducible para Playwright E2E: Docker Postgres, migraciones, API :5003, preview :4173.
.DESCRIPTION
    1. Levanta docker compose (Postgres + Redis) si no está corriendo.
    2. Aplica migraciones EF contra localhost:5435.
    3. Inicia ERP.API en http://localhost:5003 (Development + seed demo si está en appsettings).
    4. Espera /health/live.
    5. npm ci + build + playwright test (vite preview lo levanta playwright.config.ts).
.PARAMETER SkipDocker
    No ejecuta docker compose (útil si ya tienes Postgres en 5435).
.PARAMETER SkipMigrations
    Omite dotnet ef database update.
.PARAMETER PlaywrightArgs
    Argumentos extra para playwright (ej. "e2e/smoke.spec.ts").
.EXAMPLE
    pwsh -File scripts/run-e2e.ps1
.EXAMPLE
    pwsh -File scripts/run-e2e.ps1 -SkipDocker -PlaywrightArgs "e2e/enterprise-auth.spec.ts"
#>
[CmdletBinding()]
param(
    [switch] $SkipDocker,
    [switch] $SkipMigrations,
    [string] $PlaywrightArgs = "",
    [string] $ApiUrl = "http://localhost:5003",
    [int]    $HealthTimeoutSec = 120
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path $PSScriptRoot -Parent
$backendRoot = Join-Path $repoRoot "backend\src"
$apiProject = Join-Path $backendRoot "ERP.API\ERP.API.csproj"
$infraProject = Join-Path $backendRoot "ERP.Infrastructure\ERP.Infrastructure.csproj"
$frontendRoot = Join-Path $repoRoot "frontend"

function Wait-HttpOk {
    param([string] $Url, [int] $TimeoutSec)
    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    while ((Get-Date) -lt $deadline) {
        try {
            $r = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 5
            if ($r.StatusCode -ge 200 -and $r.StatusCode -lt 300) { return $true }
        } catch { }
        Start-Sleep -Seconds 2
    }
    return $false
}

function Stop-ApiIfStarted {
    if ($script:ApiProcess -and -not $script:ApiProcess.HasExited) {
        Write-Host "==> Deteniendo API (PID $($script:ApiProcess.Id))" -ForegroundColor DarkGray
        Stop-Process -Id $script:ApiProcess.Id -Force -ErrorAction SilentlyContinue
    }
}

$script:ApiProcess = $null
Register-EngineEvent -SourceIdentifier PowerShell.Exiting -Action { Stop-ApiIfStarted } | Out-Null

try {
    Set-Location $repoRoot

    if (-not $SkipDocker) {
        Write-Host "==> Docker compose up -d" -ForegroundColor Cyan
        docker compose up -d
        $deadline = (Get-Date).AddSeconds(60)
        while ((Get-Date) -lt $deadline) {
            $status = docker inspect -f "{{.State.Health.Status}}" postgreszh 2>$null
            if ($status -eq "healthy") { break }
            Start-Sleep -Seconds 2
        }
        if ($status -ne "healthy") {
            Write-Warning "Postgres no reportó healthy a tiempo; continuando igualmente..."
        }
    }

    if (-not $SkipMigrations) {
        Write-Host "==> dotnet ef database update" -ForegroundColor Cyan
        Push-Location $backendRoot
        dotnet ef database update `
            --project $infraProject `
            --startup-project $apiProject `
            --no-build
        if ($LASTEXITCODE -ne 0) {
            dotnet build $apiProject -c Release
            dotnet ef database update `
                --project $infraProject `
                --startup-project $apiProject
        }
        Pop-Location
    }

    Write-Host "==> Iniciando API en $ApiUrl" -ForegroundColor Cyan
    Push-Location $backendRoot
    dotnet build $apiProject -c Release -v q
    Pop-Location

    $env:ASPNETCORE_ENVIRONMENT = "Development"
    $env:ASPNETCORE_URLS = $ApiUrl
    $script:ApiProcess = Start-Process -FilePath "dotnet" `
        -ArgumentList @("run", "--project", $apiProject, "--no-build", "-c", "Release", "--urls", $ApiUrl) `
        -WorkingDirectory $backendRoot `
        -PassThru `
        -WindowStyle Hidden

    $healthUrl = "$($ApiUrl.TrimEnd('/'))/health/live"
    Write-Host "==> Esperando $healthUrl (max ${HealthTimeoutSec}s)" -ForegroundColor Cyan
    if (-not (Wait-HttpOk -Url $healthUrl -TimeoutSec $HealthTimeoutSec)) {
        throw "API no respondió en $healthUrl dentro del timeout."
    }
    Write-Host "    API lista." -ForegroundColor Green

    Write-Host "==> Frontend: npm ci + build + Playwright" -ForegroundColor Cyan
    Push-Location $frontendRoot
    if (-not (Test-Path "node_modules")) {
        npm ci
    }
    npm run build

    $env:E2E_API_URL = $ApiUrl
    $env:E2E_BASE_URL = "http://127.0.0.1:4173"
    $env:CI = "true"

    if ([string]::IsNullOrWhiteSpace($PlaywrightArgs)) {
        npx playwright test
    } else {
        Invoke-Expression "npx playwright test $PlaywrightArgs"
    }
    $playExit = $LASTEXITCODE
    Pop-Location

    if ($playExit -ne 0) { exit $playExit }
    Write-Host "`nE2E completado OK." -ForegroundColor Green
}
finally {
    Stop-ApiIfStarted
}
