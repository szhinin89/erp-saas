# =====================================================
# ZH TECHNOLOGIES ERP - DOCKER LOCAL MANAGER
# =====================================================

param(
    [ValidateSet("Menu", "Start", "Update", "Rebuild", "Stop", "Restart", "Status", "Logs", "Health", "Clean")]
    [string]$Action = "Menu"
)

$ErrorActionPreference = "Stop"

$RepoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $RepoRoot

$ComposeFile = "docker-compose.localprod.yml"
$EnvFile = ".env.docker.local"
$ApiContainer = "erp-api-localprod"
$FrontendContainer = "erp-frontend-localprod"

function Show-Title {
    Clear-Host
    Write-Host ""
    Write-Host "============================================" -ForegroundColor Cyan
    Write-Host "       ZH TECHNOLOGIES ERP DOCKER" -ForegroundColor Cyan
    Write-Host "============================================" -ForegroundColor Cyan
    Write-Host ""
}

function Pause-System {
    Write-Host ""
    Read-Host "Presione ENTER para continuar" | Out-Null
}

function Validate-Environment {
    if (!(Test-Path -LiteralPath $EnvFile)) {
        throw "No existe $EnvFile"
    }

    $content = Get-Content -LiteralPath $EnvFile -Raw
    if ($content -notmatch "(?m)^POSTGRES_PASSWORD=.+$") {
        throw "POSTGRES_PASSWORD no configurado"
    }
    if ($content -notmatch "(?m)^JWT_SECRET_KEY=.+$") {
        throw "JWT_SECRET_KEY no configurado"
    }

    Write-Host "[OK] Variables Docker validadas" -ForegroundColor Green
}

function Docker-Compose {
    param(
        [Parameter(Mandatory, Position = 0)]
        [ValidateSet("up", "down", "build", "restart", "ps", "logs")]
        [string]$Command,

        [Alias("d")]
        [switch]$Detached,

        [Alias("f")]
        [switch]$Follow,

        [Parameter(ValueFromRemainingArguments = $true, Position = 1)]
        [string[]]$ComposeArguments
    )

    # PowerShell binds -d/-f as function parameters, so add those Compose flags explicitly.
    # Command and arguments remain separate: never use a single "up -d" string.
    $dockerArguments = @($Command)
    if ($Detached) { $dockerArguments += "-d" }
    if ($Follow) { $dockerArguments += "-f" }
    $dockerArguments += $ComposeArguments

    $display = $dockerArguments -join " "
    Write-Host ""
    Write-Host "docker compose $display" -ForegroundColor DarkGray

    & docker compose --env-file $EnvFile -f $ComposeFile @dockerArguments
    if ($LASTEXITCODE -ne 0) {
        throw "Docker Compose '$display' fallo (codigo $LASTEXITCODE)."
    }
}

function Wait-System {
    Write-Host ""
    Write-Host "[WAIT] Esperando servicios (20 segundos)..." -ForegroundColor Yellow
    Start-Sleep -Seconds 20
}

function Test-ContainerRunning {
    param([Parameter(Mandatory)][string]$Name, [Parameter(Mandatory)][string]$Label)

    $running = & docker container inspect --format '{{.State.Running}}' $Name 2>$null
    if ($LASTEXITCODE -eq 0 -and $running.Trim() -eq "true") {
        Write-Host "[OK] Container $Label" -ForegroundColor Green
        return $true
    }

    Write-Host "[ERROR] Container $Label no disponible" -ForegroundColor Red
    return $false
}

function Test-HttpEndpoint {
    param([Parameter(Mandatory)][string]$Uri, [Parameter(Mandatory)][string]$Label)

    try {
        $response = Invoke-WebRequest -Uri $Uri -UseBasicParsing -TimeoutSec 5
        if ($response.StatusCode -ge 200 -and $response.StatusCode -lt 400) {
            Write-Host "[OK] $Label HTTP" -ForegroundColor Green
            return $true
        }
    }
    catch {
        # The error is reported below with the endpoint label, without exposing response content.
    }

    Write-Host "[ERROR] $Label HTTP no responde" -ForegroundColor Red
    return $false
}

function Health-System {
    Validate-Environment

    Write-Host ""
    Write-Host "============================================"
    Write-Host "          ZH ERP HEALTH CHECK"
    Write-Host "============================================"
    Write-Host ""

    $apiContainerOk = Test-ContainerRunning -Name $ApiContainer -Label "API"
    $frontendContainerOk = Test-ContainerRunning -Name $FrontendContainer -Label "Frontend"
    $apiHttpOk = Test-HttpEndpoint -Uri "http://localhost:5003/health/ready" -Label "API"
    $frontendHttpOk = Test-HttpEndpoint -Uri "http://localhost:8080" -Label "Frontend"

    return ($apiContainerOk -and $frontendContainerOk -and $apiHttpOk -and $frontendHttpOk)
}

function Start-System {
    Validate-Environment
    Write-Host ""
    Write-Host "[START] Levantando ERP..." -ForegroundColor Yellow
    Docker-Compose up -d
    Wait-System
    Docker-Compose ps

    if (!(Health-System)) {
        throw "Operacion fallida: el ERP no supero el health check."
    }

    Write-Host "[OK] ERP iniciado correctamente" -ForegroundColor Green
}

function Update-System {
    Validate-Environment
    Write-Host ""
    Write-Host "[UPDATE] Actualizando ERP..." -ForegroundColor Yellow
    Write-Host "[1/3] Deteniendo servicios..."
    Docker-Compose down
    Write-Host "[2/3] Construyendo imagenes..."
    Docker-Compose build
    Write-Host "[3/3] Levantando nueva version..."
    Docker-Compose up -d
    Wait-System
    Docker-Compose ps

    if (!(Health-System)) {
        throw "Operacion fallida: el ERP no supero el health check."
    }

    Write-Host "[OK] Actualizacion terminada" -ForegroundColor Green
}

function Rebuild-System {
    Validate-Environment
    Write-Host ""
    Write-Host "[REBUILD] Reconstruccion sin cache..." -ForegroundColor Yellow
    Write-Host "[1/3] Bajando contenedores..."
    Docker-Compose down
    Write-Host "[2/3] Construyendo imagenes sin cache..."
    Docker-Compose build --no-cache
    Write-Host "[3/3] Levantando servicios..."
    Docker-Compose up -d
    Wait-System
    Docker-Compose ps

    if (!(Health-System)) {
        throw "Operacion fallida: el ERP no supero el health check."
    }

    Write-Host "[OK] Reconstruccion completa" -ForegroundColor Green
}

function Stop-System {
    Write-Host ""
    Write-Host "[STOP] Deteniendo ERP..." -ForegroundColor Yellow
    Docker-Compose down
    Write-Host "[OK] ERP detenido" -ForegroundColor Green
}

function Restart-System {
    Write-Host ""
    Write-Host "[RESTART] Reiniciando ERP..." -ForegroundColor Yellow
    Docker-Compose restart
    Write-Host "[OK] ERP reiniciado" -ForegroundColor Green
}

function Status-System {
    Write-Host ""
    Write-Host "[STATUS]" -ForegroundColor Yellow
    Docker-Compose ps
}

function Logs-System {
    Write-Host ""
    Write-Host "[LOGS] CTRL+C para salir" -ForegroundColor Yellow
    # This is intentionally the only foreground Compose operation.
    Docker-Compose logs -f
}

function Clean-System {
    Write-Host ""
    Write-Host "ADVERTENCIA: elimina contenedores localprod" -ForegroundColor Red
    if ((Read-Host "Continuar? (S/N)") -eq "S") {
        Docker-Compose down
        Write-Host "[OK] Limpieza realizada" -ForegroundColor Green
    }
}

function Invoke-MenuAction {
    param([Parameter(Mandatory)][string]$SelectedAction)

    switch ($SelectedAction) {
        "Start" { Start-System }
        "Update" { Update-System }
        "Rebuild" { Rebuild-System }
        "Stop" { Stop-System }
        "Restart" { Restart-System }
        "Status" { Status-System }
        "Logs" { Logs-System }
        "Health" {
            if (!(Health-System)) {
                throw "Operacion fallida: el ERP no supero el health check."
            }
        }
        "Clean" { Clean-System }
        default { throw "Accion no soportada: $SelectedAction" }
    }
}

if ($Action -ne "Menu") {
    try {
        Invoke-MenuAction -SelectedAction $Action
        exit 0
    }
    catch {
        Write-Host ""
        Write-Host "[ERROR] $($_.Exception.Message)" -ForegroundColor Red
        exit 1
    }
}

while ($true) {
    Show-Title
    Write-Host "1.  Iniciar ERP"
    Write-Host "2.  Actualizar ERP"
    Write-Host "3.  Reconstruir sin cache"
    Write-Host "4.  Detener ERP"
    Write-Host "5.  Reiniciar ERP"
    Write-Host "6.  Ver estado"
    Write-Host "7.  Ver logs"
    Write-Host "8.  Health Check"
    Write-Host "9.  Limpiar contenedores"
    Write-Host "0.  Salir"
    Write-Host ""

    $option = Read-Host "Seleccione opcion"
    try {
        switch ($option) {
            "1" { Start-System; Pause-System }
            "2" { Update-System; Pause-System }
            "3" { Rebuild-System; Pause-System }
            "4" { Stop-System; Pause-System }
            "5" { Restart-System; Pause-System }
            "6" { Status-System; Pause-System }
            "7" { Logs-System }
            "8" { Health-System | Out-Null; Pause-System }
            "9" { Clean-System; Pause-System }
            "0" { Write-Host "Cerrando..."; exit 0 }
            default { Write-Host "[ERROR] Opcion invalida" -ForegroundColor Red; Pause-System }
        }
    }
    catch {
        Write-Host ""
        Write-Host "[ERROR] $($_.Exception.Message)" -ForegroundColor Red
        Pause-System
    }
}
