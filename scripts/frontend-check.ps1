# C:\ProyectCursor\erp-saas\scripts\frontend-check.ps1
#
# Uso:
#   .\scripts\frontend-check.ps1
#
# Opcionales:
#   .\scripts\frontend-check.ps1 -FixLint
#   .\scripts\frontend-check.ps1 -RunE2E
#   .\scripts\frontend-check.ps1 -FixLint -RunE2E

param(
    [switch]$FixLint,
    [switch]$RunE2E
)

$ErrorActionPreference = "Stop"

$Root = "C:\ProyectCursor\erp-saas"
$Frontend = Join-Path $Root "frontend"

Set-Location $Frontend

function Invoke-Step {
    param(
        [string]$Name,
        [scriptblock]$Action
    )

    Write-Host ""
    Write-Host "==> $Name" -ForegroundColor Cyan

    & $Action

    if ($LASTEXITCODE -ne 0) {
        Write-Host "FALLO: $Name (exit $LASTEXITCODE)" -ForegroundColor Red
        exit $LASTEXITCODE
    }

    Write-Host "OK: $Name" -ForegroundColor Green
}

function Test-Port {
    param(
        [string]$HostName,
        [int]$Port
    )

    try {
        $client = [System.Net.Sockets.TcpClient]::new()
        $task = $client.ConnectAsync($HostName, $Port)

        if (-not $task.Wait(2000)) {
            $client.Dispose()
            return $false
        }

        $connected = $client.Connected
        $client.Dispose()
        return $connected
    }
    catch {
        return $false
    }
}

Write-Host "==========================================" -ForegroundColor DarkCyan
Write-Host " ZH ERP - FRONTEND CHECK" -ForegroundColor DarkCyan
Write-Host "==========================================" -ForegroundColor DarkCyan

# 1. Instalación reproducible
Invoke-Step "1. Restaurar dependencias desde package-lock.json" {
    npm ci
}

# 2. Seguridad runtime: sí importa para producción
Invoke-Step "2. Auditoría de dependencias runtime" {
    npm audit --omit=dev
}

# 3. Auditoría total: informativa
Write-Host ""
Write-Host "==> 3. Auditoría completa npm (informativa)" -ForegroundColor Cyan
npm audit

if ($LASTEXITCODE -ne 0) {
    Write-Host "WARN: existen vulnerabilidades en tooling/dev dependencies." -ForegroundColor Yellow
    Write-Host "      Runtime se validó por separado en el paso anterior." -ForegroundColor Yellow
}
else {
    Write-Host "OK: auditoría completa sin vulnerabilidades." -ForegroundColor Green
}

# 4. Lint
Invoke-Step "4. ESLint" {
    npm run lint
}

# 5. Fix opcional
if ($FixLint) {
    Invoke-Step "5. ESLint autofix solicitado" {
        npx eslint src --fix
    }

    Invoke-Step "5b. ESLint después del autofix" {
        npm run lint
    }
}
else {
    Write-Host ""
    Write-Host "SKIP: ESLint --fix no solicitado." -ForegroundColor DarkYellow
}

# 6. Build productivo
Invoke-Step "6. Build frontend + Platform Guard" {
    npm run build
}

# 7. Tests rápidos/focalizados del frontend
Invoke-Step "7. Tests de Sales" {
    npx vitest run src/modules/sales
}

Invoke-Step "8. Tests de rutas" {
    npx vitest run src/routes
}

# 9. Playwright
Write-Host ""
Write-Host "==> 9. Playwright E2E" -ForegroundColor Cyan

if (-not $RunE2E) {
    Write-Host "SKIP: E2E no solicitado. Use -RunE2E para ejecutarlo." -ForegroundColor DarkYellow
}
elseif (-not (Test-Port -HostName "localhost" -Port 5003)) {
    Write-Host "SKIP: API no disponible en localhost:5003." -ForegroundColor Yellow
    Write-Host "      Levante ERP.API antes de ejecutar Playwright." -ForegroundColor Yellow
}
else {
    Invoke-Step "9a. Listar Playwright" {
        npx playwright test --list
    }

    Invoke-Step "9b. Ejecutar Playwright" {
        npx playwright test
    }
}

# 10. Git
Set-Location $Root

Write-Host ""
Write-Host "==> 10. Estado Git" -ForegroundColor Cyan
git status --short

Write-Host ""
Write-Host "==> Diff stat" -ForegroundColor Cyan
git diff --stat

Write-Host ""
Write-Host "==========================================" -ForegroundColor DarkCyan
Write-Host " FRONTEND CHECK TERMINADO" -ForegroundColor Green
Write-Host "==========================================" -ForegroundColor DarkCyan