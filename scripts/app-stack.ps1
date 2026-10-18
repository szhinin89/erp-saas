# =====================================================
# ZH TECHNOLOGIES ERP - STACK DE APLICACION (API + Worker + Frontend)
# =====================================================
# Unica entrada operativa de docker-compose.app.yml (ensayo local y produccion).
# Valida el archivo de entorno antes de cualquier accion:
#   - POSTGRES_DB explicita y distinta de erp_zh_dev (exclusiva de dotnet run/dev-launcher).
#   - Hangfire__StorageConnectionString apunta a la misma base.
#   - Secretos sin placeholders CHANGE_ME.
#   - Up: la base debe estar sin conexiones ajenas (p. ej. un E2E sobre erp_zh_test), salvo que el
#     stack ya este corriendo (actualizacion) o se pase -Force.
# Nunca ejecuta `down -v`: los volumenes guardan certificados, XML, RIDE y keyring.
#
# Uso (desde cualquier ubicacion):
#   .\scripts\app-stack.ps1 -Action Config
#   .\scripts\app-stack.ps1 -Action Build
#   .\scripts\app-stack.ps1 -Action Up
#   .\scripts\app-stack.ps1 -Action Status | Logs | Health | Down
#
# Contrato completo: docs/deployment/README.md

param(
    [Parameter(Mandatory)]
    [ValidateSet("Config", "Build", "Up", "Down", "Status", "Logs", "Health")]
    [string]$Action,
    [string]$EnvFile = ".env.app",
    [switch]$Force
)

$ErrorActionPreference = "Stop"

$RepoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $RepoRoot

$ComposeFile = "docker-compose.app.yml"
$DevelopmentDatabase = "erp_zh_dev"

function Read-EnvFile {
    param([Parameter(Mandatory)][string]$Path)

    if (!(Test-Path -LiteralPath $Path)) {
        throw "No existe $Path. Copia .env.app.example a $Path y completa sus valores."
    }
    $values = @{}
    foreach ($line in Get-Content -LiteralPath $Path) {
        if ($line -match '^\s*([A-Za-z_][A-Za-z0-9_]*)=(.*)$') {
            $values[$Matches[1]] = $Matches[2].Trim()
        }
    }
    return $values
}

function Get-ConnectionDatabase {
    param([string]$ConnectionString)

    $match = [regex]::Match($ConnectionString, '(?i)(?:^|;)\s*Database\s*=\s*([^;]+)')
    if ($match.Success) { return $match.Groups[1].Value.Trim() }
    return $null
}

function Assert-Environment {
    $values = Read-EnvFile -Path $EnvFile

    foreach ($key in @("POSTGRES_DB", "POSTGRES_PASSWORD", "JWT_SECRET_KEY", "Hangfire__StorageConnectionString", "Redis__InstanceName")) {
        if ([string]::IsNullOrWhiteSpace($values[$key])) {
            throw "$key no configurado en $EnvFile."
        }
    }
    foreach ($key in @("POSTGRES_PASSWORD", "JWT_SECRET_KEY", "Hangfire__StorageConnectionString")) {
        if ($values[$key] -match 'CHANGE_ME') {
            throw "$key conserva el placeholder CHANGE_ME en $EnvFile."
        }
    }

    $database = $values["POSTGRES_DB"]
    if ($database -ieq $DevelopmentDatabase) {
        throw "POSTGRES_DB=$DevelopmentDatabase no esta permitido para el stack de aplicacion (base de desarrollo). Usa erp_zh_test (ensayo local) o erp_zh_prod (produccion)."
    }
    $hangfireDatabase = Get-ConnectionDatabase -ConnectionString $values["Hangfire__StorageConnectionString"]
    if ($hangfireDatabase -ine $database) {
        throw "Hangfire__StorageConnectionString apunta a '$hangfireDatabase' y POSTGRES_DB a '$database': deben coincidir."
    }

    Write-Host "[OK] $EnvFile validado (base: $database)" -ForegroundColor Green
    return $values
}

function Invoke-Compose {
    param([Parameter(ValueFromRemainingArguments = $true)][string[]]$Arguments)

    Write-Host "docker compose $($Arguments -join ' ')" -ForegroundColor DarkGray
    & docker compose --env-file $EnvFile -f $ComposeFile @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "docker compose $($Arguments -join ' ') fallo (codigo $LASTEXITCODE)."
    }
}

function Test-StackRunning {
    $ids = & docker compose --env-file $EnvFile -f $ComposeFile ps -q --status running erp-api 2>$null
    return -not [string]::IsNullOrWhiteSpace(($ids | Out-String))
}

function Assert-DatabaseIdle {
    param([Parameter(Mandatory)][hashtable]$Values)

    $database = $Values["POSTGRES_DB"]
    $container = $Values["POSTGRES_HOST"]
    & docker container inspect $container *> $null
    if ($LASTEXITCODE -ne 0) {
        Write-Host "[WARN] '$container' no es un contenedor local: se omite la verificacion de conexiones ajenas." -ForegroundColor Yellow
        return
    }
    $sql = "SELECT count(*) FROM pg_stat_activity WHERE datname = '$database' AND pid <> pg_backend_pid();"
    $count = (& docker exec $container psql -U $Values["POSTGRES_USER"] -d postgres -At -c $sql).Trim()
    if ($LASTEXITCODE -ne 0) {
        throw "No se pudo consultar pg_stat_activity en '$container'."
    }
    if ([int]$count -gt 0) {
        throw "La base '$database' tiene $count conexion(es) activa(s) (p. ej. un E2E). Esperar a que termine o usar -Force."
    }
    Write-Host "[OK] Base '$database' sin conexiones ajenas" -ForegroundColor Green
}

function Test-Health {
    param([Parameter(Mandatory)][hashtable]$Values)

    $port = if ($Values["API_PORT"]) { $Values["API_PORT"] } else { "5003" }
    try {
        $response = Invoke-WebRequest -Uri "http://localhost:$port/health/ready" -UseBasicParsing -TimeoutSec 5
        if ($response.StatusCode -eq 200) {
            Write-Host "[OK] API /health/ready" -ForegroundColor Green
            return
        }
    }
    catch {
        # Se reporta abajo sin exponer el contenido de la respuesta.
    }
    throw "API /health/ready no responde en el puerto $port."
}

try {
    $values = Assert-Environment
    switch ($Action) {
        # Sin secretos en consola: --quiet solo valida la interpolacion.
        "Config" { Invoke-Compose config --quiet; Write-Host "[OK] Compose valido" -ForegroundColor Green }
        "Build" { Invoke-Compose build erp-api erp-frontend }
        "Up" {
            if ((Test-StackRunning) -or $Force) {
                Write-Host "[INFO] Stack en ejecucion o -Force: se omite la verificacion de base ociosa." -ForegroundColor DarkGray
            }
            else {
                Assert-DatabaseIdle -Values $values
            }
            Invoke-Compose up -d --no-build
        }
        "Down" { Invoke-Compose down }
        "Status" { Invoke-Compose ps }
        "Logs" { Invoke-Compose logs --tail 200 -f }
        "Health" { Invoke-Compose ps; Test-Health -Values $values }
    }
    exit 0
}
catch {
    Write-Host "[ERROR] $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
