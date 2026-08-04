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
    pwsh -File scripts/ci/run-e2e.ps1
.EXAMPLE
    pwsh -File scripts/ci/run-e2e.ps1 -SkipDocker -PlaywrightArgs "e2e/enterprise-auth.spec.ts"
#>
[CmdletBinding()]
param(
    [switch] $SkipDocker,
    [switch] $SkipMigrations,
    [string] $PlaywrightArgs = "",
    [string] $ApiUrl = "http://localhost:5003",
    [int]    $HealthTimeoutSec = 120,
    [int]    $MigrationTimeoutSec = 180,
    [int]    $BuildTimeoutSec = 180,
    [int]    $FrontendTimeoutSec = 300,
    [int]    $PlaywrightTimeoutSec = 300
)

$ErrorActionPreference = "Stop"
function Get-ErpSaasRepoRoot {
    $dir = $PSScriptRoot
    while ($dir) {
        if ((Test-Path (Join-Path $dir 'backend')) -and (Test-Path (Join-Path $dir 'frontend'))) {
            return $dir
        }
        $parent = Split-Path -Parent $dir
        if ([string]::IsNullOrEmpty($parent) -or $parent -eq $dir) { break }
        $dir = $parent
    }
    throw 'No se encontró la raíz del repo ERP SaaS.'
}
$repoRoot = Get-ErpSaasRepoRoot
$backendRoot = Join-Path $repoRoot "backend\src"
$apiProject = Join-Path $backendRoot "ERP.API\ERP.API.csproj"
$infraProject = Join-Path $backendRoot "ERP.Infrastructure\ERP.Infrastructure.csproj"
$frontendRoot = Join-Path $repoRoot "frontend"
$runnerLogDir = Join-Path $repoRoot "backend\artifacts\e2e-runner"
New-Item -ItemType Directory -Force -Path $runnerLogDir | Out-Null

function Write-E2eLog {
    param([string] $Message, [ConsoleColor] $Color = [ConsoleColor]::Cyan)
    $line = "[{0:yyyy-MM-dd HH:mm:ss}] {1}" -f (Get-Date), $Message
    Write-Host $line -ForegroundColor $Color
    [Console]::Out.Flush()
}

function Get-CommandLogTail {
    param([string] $Path)
    if (-not (Test-Path $Path)) { return "(sin salida)" }
    return ((Get-Content -Path $Path -Tail 40 -ErrorAction SilentlyContinue) -join [Environment]::NewLine)
}

function Invoke-E2eProcess {
    param(
        [string] $Stage,
        [string] $FilePath,
        [string[]] $ArgumentList,
        [string] $WorkingDirectory,
        [int] $TimeoutSec,
        [switch] $AllowFailure
    )

    $safeStage = $Stage -replace "[^A-Za-z0-9._-]", "_"
    $stdout = Join-Path $runnerLogDir "$safeStage.stdout.log"
    $stderr = Join-Path $runnerLogDir "$safeStage.stderr.log"
    Remove-Item -LiteralPath $stdout, $stderr -Force -ErrorAction SilentlyContinue
    Write-E2eLog "[$Stage] comando: $FilePath $($ArgumentList -join ' ') | timeout=${TimeoutSec}s"
    $started = Get-Date
    $process = Start-Process -FilePath $FilePath -ArgumentList $ArgumentList `
        -WorkingDirectory $WorkingDirectory -RedirectStandardOutput $stdout `
        -RedirectStandardError $stderr -PassThru -WindowStyle Hidden

    $completed = $true
    try {
        Wait-Process -Id $process.Id -Timeout $TimeoutSec -ErrorAction Stop
    } catch {
        $completed = $false
    }
    if (-not $completed) {
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        throw "[$Stage] timeout tras $TimeoutSec s (PID $($process.Id)). stdout: $stdout; stderr: $stderr. Último stderr: $(Get-CommandLogTail $stderr)"
    }

    $exitCode = $process.ExitCode
    $duration = [math]::Round(((Get-Date) - $started).TotalSeconds, 1)
    $resultColor = if ($exitCode -eq 0) { [ConsoleColor]::Green } else { [ConsoleColor]::Red }
    Write-E2eLog "[$Stage] exit=$exitCode duration=${duration}s stdout=$stdout stderr=$stderr" -Color $resultColor
    if ($exitCode -ne 0 -and -not $AllowFailure) {
        throw "[$Stage] exit=$exitCode. Último stdout: $(Get-CommandLogTail $stdout) Último stderr: $(Get-CommandLogTail $stderr)"
    }
    return $exitCode
}

function Wait-HttpOk {
    param([string] $Url, [int] $TimeoutSec, [System.Diagnostics.Process] $Process, [string] $Stdout, [string] $Stderr)
    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    $attempt = 0
    $lastError = "sin intento"
    while ((Get-Date) -lt $deadline) {
        $attempt++
        try {
            $r = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 5
            if ($r.StatusCode -ge 200 -and $r.StatusCode -lt 300) { return $true }
            $lastError = "HTTP $($r.StatusCode)"
        } catch { $lastError = $_.Exception.Message }
        if ($Process.HasExited) {
            throw "[api-health] API finalizó con exit=$($Process.ExitCode). stdout: $Stdout; stderr: $Stderr. Último stderr: $(Get-CommandLogTail $Stderr)"
        }
        if ($attempt -eq 1 -or $attempt % 5 -eq 0) {
            Write-E2eLog "[api-health] intento=$attempt url=$Url último-error=$lastError" -Color ([ConsoleColor]::Yellow)
        }
        Start-Sleep -Seconds 2
    }
    Write-E2eLog "[api-health] timeout=$TimeoutSec s url=$Url último-error=$lastError stdout=$Stdout stderr=$Stderr" -Color ([ConsoleColor]::Red)
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
    Write-E2eLog "E2E runner iniciado. repo=$repoRoot api=$ApiUrl logs=$runnerLogDir"

    if ([string]::IsNullOrWhiteSpace($env:E2E_PASSWORD)) {
        throw "E2E_PASSWORD es obligatorio para ejecutar el seed E2E. Defínelo en el entorno local o CI; no se almacena en el repositorio."
    }
    if ([string]::IsNullOrWhiteSpace($env:E2E_USERNAME)) {
        $env:E2E_USERNAME = "e2e.admin"
    }

    if (-not $SkipDocker) {
        $null = Invoke-E2eProcess -Stage "docker-compose" -FilePath "docker" -ArgumentList @("compose", "up", "-d") -WorkingDirectory $repoRoot -TimeoutSec 90
        $deadline = (Get-Date).AddSeconds(60)
        while ((Get-Date) -lt $deadline) {
            $status = docker inspect -f "{{.State.Health.Status}}" postgreszh 2>$null
            if ($status -eq "healthy") { break }
            Start-Sleep -Seconds 2
        }
        if ($status -ne "healthy") {
            Write-E2eLog "[docker-health] postgres no reportó healthy en 60s; se continuará y la migración dará el error exacto." -Color ([ConsoleColor]::Yellow)
        } else {
            Write-E2eLog "[docker-health] postgreszh=healthy" -Color ([ConsoleColor]::Green)
        }
    }

    if (-not $SkipMigrations) {
        $migrationExit = Invoke-E2eProcess -Stage "migrations-no-build" -FilePath "dotnet" `
            -ArgumentList @("ef", "database", "update", "--project", $infraProject, "--startup-project", $infraProject, "--no-build") `
            -WorkingDirectory $backendRoot -TimeoutSec $MigrationTimeoutSec -AllowFailure
        if ($migrationExit -ne 0) {
            $null = Invoke-E2eProcess -Stage "infrastructure-build-for-migrations" -FilePath "dotnet" `
                -ArgumentList @("build", $infraProject, "-c", "Release") -WorkingDirectory $backendRoot -TimeoutSec $BuildTimeoutSec
            $null = Invoke-E2eProcess -Stage "migrations-after-build" -FilePath "dotnet" `
                -ArgumentList @("ef", "database", "update", "--project", $infraProject, "--startup-project", $infraProject) `
                -WorkingDirectory $backendRoot -TimeoutSec $MigrationTimeoutSec
        }
    }

    $null = Invoke-E2eProcess -Stage "api-release-build" -FilePath "dotnet" `
        -ArgumentList @("build", $apiProject, "-c", "Release", "-v", "q") -WorkingDirectory $backendRoot -TimeoutSec $BuildTimeoutSec

    $env:ASPNETCORE_ENVIRONMENT = "Development"
    $env:ASPNETCORE_URLS = $ApiUrl
    $env:E2E__SeedEnabled = "true"
    $env:E2E__Username = $env:E2E_USERNAME
    $env:E2E__Password = $env:E2E_PASSWORD
    $apiStdout = Join-Path $runnerLogDir "api.stdout.log"
    $apiStderr = Join-Path $runnerLogDir "api.stderr.log"
    Remove-Item -LiteralPath $apiStdout, $apiStderr -Force -ErrorAction SilentlyContinue
    Write-E2eLog "[api-start] comando: dotnet run --project $apiProject --no-build -c Release --urls $ApiUrl"
    $script:ApiProcess = Start-Process -FilePath "dotnet" `
        -ArgumentList @("run", "--project", $apiProject, "--no-build", "-c", "Release", "--urls", $ApiUrl) `
        -WorkingDirectory $backendRoot `
        -RedirectStandardOutput $apiStdout `
        -RedirectStandardError $apiStderr `
        -PassThru `
        -WindowStyle Hidden

    $healthUrl = "$($ApiUrl.TrimEnd('/'))/health/live"
    Write-E2eLog "[api-health] esperando url=$healthUrl puerto=$(([uri]$ApiUrl).Port) timeout=${HealthTimeoutSec}s"
    if (-not (Wait-HttpOk -Url $healthUrl -TimeoutSec $HealthTimeoutSec -Process $script:ApiProcess -Stdout $apiStdout -Stderr $apiStderr)) {
        throw "[api-health] API no respondió. stdout: $apiStdout; stderr: $apiStderr. Último stderr: $(Get-CommandLogTail $apiStderr)"
    }
    Write-E2eLog "[api-health] API lista. ready-url=$($ApiUrl.TrimEnd('/'))/health/ready" -Color ([ConsoleColor]::Green)

    if (-not (Test-Path (Join-Path $frontendRoot "node_modules"))) {
        $null = Invoke-E2eProcess -Stage "frontend-npm-ci" -FilePath "npm.cmd" -ArgumentList @("ci") -WorkingDirectory $frontendRoot -TimeoutSec $FrontendTimeoutSec
    }
    $null = Invoke-E2eProcess -Stage "frontend-build" -FilePath "npm.cmd" -ArgumentList @("run", "build") -WorkingDirectory $frontendRoot -TimeoutSec $FrontendTimeoutSec

    $env:E2E_API_URL = $ApiUrl
    $env:E2E_BASE_URL = "http://127.0.0.1:4173"
    $env:CI = "true"

    $setupExit = Invoke-E2eProcess -Stage "operational-e2e-setup" -FilePath "npx.cmd" -ArgumentList @("playwright", "test", "e2e/operational-data.setup.spec.ts") -WorkingDirectory $frontendRoot -TimeoutSec $PlaywrightTimeoutSec -AllowFailure
    if ($setupExit -ne 0) { exit $setupExit }

    if ([string]::IsNullOrWhiteSpace($PlaywrightArgs)) {
        $playExit = Invoke-E2eProcess -Stage "playwright" -FilePath "npx.cmd" -ArgumentList @("playwright", "test") -WorkingDirectory $frontendRoot -TimeoutSec $PlaywrightTimeoutSec -AllowFailure
    } else {
        Write-E2eLog "[playwright] argumentos personalizados: $PlaywrightArgs"
        Push-Location $frontendRoot
        Invoke-Expression "npx playwright test $PlaywrightArgs"
        $playExit = $LASTEXITCODE
        Pop-Location
    }

    if ($playExit -ne 0) { exit $playExit }
    Write-E2eLog "E2E completado OK." -Color ([ConsoleColor]::Green)
}
finally {
    Stop-ApiIfStarted
}
