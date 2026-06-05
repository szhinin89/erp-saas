#Requires -Version 5.1
<#
.SYNOPSIS
  ERP SaaS Development Tool — herramienta robusta de desarrollo.

.DESCRIPTION
  Modo interactivo (sin parametros) o directo por flags CLI.

  Modos:
    1) -Normal          Desarrollo normal (Docker + Migraciones + API + Frontend)
    2) -ResetDatabase   Drop DB + recrear + migraciones
    3) -ResetAll        docker compose down -v + up + migraciones
    4) -Doctor          Diagnostico completo sin cambios
    5) -DockerUp        Solo levantar PostgreSQL + Redis

  Flags adicionales (combinables con -Normal):
    -NoMigrate            Omitir migraciones
    -NoStart              Omitir arranque de apps
    -StrictMigrate        Cortar si falla database update
    -NoAutoFixMigration   No autocorregir PendingModelChangesWarning
    -SkipDocker           No ejecutar docker compose

.EXAMPLE
  .\scripts\dev\dev-restart.ps1                  # Menu interactivo
  .\scripts\dev\dev-restart.ps1 -Normal
  .\scripts\dev\dev-restart.ps1 -ResetDatabase
  .\scripts\dev\dev-restart.ps1 -ResetAll
  .\scripts\dev\dev-restart.ps1 -Doctor
  .\scripts\dev\dev-restart.ps1 -DockerUp
  .\scripts\dev\dev-restart.ps1 -Normal -NoStart -SkipDocker
#>
param(
    # Modos exclusivos
    [switch] $Normal,
    [switch] $ResetDatabase,
    [switch] $ResetAll,
    [switch] $Doctor,
    [switch] $DockerUp,

    # Modificadores (se combinan con -Normal o el menu)
    [switch] $NoMigrate,
    [switch] $NoStart,
    [switch] $StrictMigrate,
    [switch] $NoAutoFixMigration,
    [switch] $SkipDocker
)

Set-StrictMode -Off
$ErrorActionPreference = 'Stop'

# Nombres de contenedores Docker (sincronizar con docker-compose.yml)
$script:PgContainerName    = 'postgreszh'
$script:RedisContainerName = 'erp-saas-redis'
$script:PgDatabase         = 'dberpsaas'
$script:PgUser             = 'postgres'

# ═══════════════════════════════════════════════════════════════════════════════
# LOGGING
# ═══════════════════════════════════════════════════════════════════════════════

function Write-Step([string]$msg) {
    Write-Host ''
    Write-Host "==> $msg" -ForegroundColor Cyan
}

function Write-Ok([string]$msg) {
    Write-Host "    [OK]  $msg" -ForegroundColor Green
}

function Write-Warn([string]$msg) {
    Write-Host "    [!!]  $msg" -ForegroundColor Yellow
}

function Write-Info([string]$msg) {
    Write-Host "    [..]  $msg" -ForegroundColor DarkCyan
}

function Write-Err([string]$msg) {
    Write-Host "    [ERR] $msg" -ForegroundColor Red
}

# ═══════════════════════════════════════════════════════════════════════════════
# DOCTOR — CONTADORES
# ═══════════════════════════════════════════════════════════════════════════════

$script:DocOK    = 0
$script:DocWarn  = 0
$script:DocError = 0

function Reset-DocCounters {
    $script:DocOK    = 0
    $script:DocWarn  = 0
    $script:DocError = 0
}

function Register-DocOk([string]$msg)    { $script:DocOK++;    Write-Ok   $msg }
function Register-DocWarn([string]$msg)  { $script:DocWarn++;  Write-Warn $msg }
function Register-DocError([string]$msg) { $script:DocError++; Write-Err  $msg }

# ═══════════════════════════════════════════════════════════════════════════════
# UTILIDADES GENERALES
# ═══════════════════════════════════════════════════════════════════════════════

function Get-ErpSaasRepoRoot {
    $dir = $PSScriptRoot
    while ($dir) {
        if ((Test-Path (Join-Path $dir 'backend')) -and (Test-Path (Join-Path $dir 'docker-compose.yml'))) {
            return $dir
        }
        $parent = Split-Path -Parent $dir
        if ([string]::IsNullOrEmpty($parent) -or $parent -eq $dir) { break }
        $dir = $parent
    }
    throw 'No se encontro la raiz del repo ERP SaaS (busca backend/ + docker-compose.yml).'
}

# Ejecuta un comando dotnet capturando stdout+stderr sin romper en $ErrorActionPreference='Stop'.
function Invoke-EfCommand {
    param([string[]] $CmdArgs)
    $prevEap = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $out = & dotnet @CmdArgs 2>&1
    }
    finally {
        $ErrorActionPreference = $prevEap
    }
    $text = ($out | ForEach-Object { $_.ToString() }) -join [Environment]::NewLine
    return [PSCustomObject]@{ ExitCode = $LASTEXITCODE; Text = $text }
}

function New-AutoMigrationName {
    return "AutoDev_$(Get-Date -Format 'yyyyMMddHHmmss')"
}

function Stop-ListenersOnPort([int[]]$Ports) {
    foreach ($port in $Ports) {
        try {
            $listeners = Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue |
                Select-Object -ExpandProperty OwningProcess -Unique
            foreach ($procId in $listeners) {
                if (-not $procId -or $procId -lt 1) { continue }
                $p = Get-Process -Id $procId -ErrorAction SilentlyContinue
                if ($p) {
                    Write-Host "    Deteniendo puerto $port : PID $procId ($($p.ProcessName))" -ForegroundColor DarkGray
                    Stop-Process -Id $procId -Force -ErrorAction SilentlyContinue
                }
            }
        }
        catch {
            Write-Warn "No se pudo consultar el puerto $port. Continua si no hay nada escuchando."
        }
    }
    Start-Sleep -Milliseconds 800
}

function Stop-DotnetApiProcesses([string]$ApiHint) {
    try {
        $dotnetProcs = Get-CimInstance Win32_Process -Filter "Name = 'dotnet.exe'" -ErrorAction SilentlyContinue
        foreach ($proc in $dotnetProcs) {
            $cmd = $proc.CommandLine
            if ([string]::IsNullOrWhiteSpace($cmd)) { continue }
            if ($cmd -notmatch [Regex]::Escape($ApiHint)) { continue }
            Write-Host "    Deteniendo proceso dotnet bloqueando build: PID $($proc.ProcessId)" -ForegroundColor DarkGray
            Stop-Process -Id $proc.ProcessId -Force -ErrorAction SilentlyContinue
        }
    }
    catch {
        Write-Warn "No se pudo inspeccionar procesos dotnet. Se continua."
    }
}

function Get-ListeningProcessSummary([int[]]$Ports) {
    $rows = @()
    foreach ($port in $Ports) {
        try {
            $listeners = Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue
            if (-not $listeners) {
                $rows += [PSCustomObject]@{ Port = $port; Status = 'FREE'; Process = '-' }
                continue
            }
            $pids = $listeners | Select-Object -ExpandProperty OwningProcess -Unique
            foreach ($pid in $pids) {
                $proc  = Get-Process -Id $pid -ErrorAction SilentlyContinue
                $pname = if ($proc) { $proc.ProcessName } else { "PID $pid" }
                $rows += [PSCustomObject]@{ Port = $port; Status = 'LISTEN'; Process = $pname }
            }
        }
        catch {
            $rows += [PSCustomObject]@{ Port = $port; Status = 'UNKNOWN'; Process = 'No access' }
        }
    }
    return $rows
}

function Remove-OrphanDockerContainers {
    param(
        [string]   $ComposeProjectName    = 'erp-saas',
        [string[]] $ManagedContainerNames = @('postgreszh', 'erp-saas-redis')
    )
    foreach ($name in $ManagedContainerNames) {
        $exists = docker ps -a --filter "name=^/${name}$" --format '{{.ID}}' 2>$null
        if (-not $exists) { continue }

        $managed = docker ps -a `
            --filter "name=^/${name}$" `
            --filter "label=com.docker.compose.project=$ComposeProjectName" `
            --format '{{.ID}}' 2>$null
        if ($managed) { continue }

        Write-Warn "Contenedor '$name' huerfano (fuera de compose '$ComposeProjectName'). Eliminando..."
        docker rm -f $name | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "No se pudo eliminar el contenedor huerfano '$name'" }
        Write-Ok "Contenedor huerfano '$name' eliminado."
    }
}

# Muestra advertencia destructiva y devuelve $true si el usuario confirma con S.
function Invoke-Confirm([string]$Question) {
    Write-Host ''
    Write-Host '  !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!' -ForegroundColor Red
    Write-Host '  ADVERTENCIA — OPERACION DESTRUCTIVA' -ForegroundColor Red
    Write-Host '  !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!' -ForegroundColor Red
    Write-Host ''
    Write-Host "  $Question" -ForegroundColor Yellow
    Write-Host '  Esta operacion eliminara datos locales.' -ForegroundColor Yellow
    Write-Host ''
    $resp = Read-Host '  Continuar? [S] Si / [N] No'
    return ($resp -match '^[sS]$')
}

# ═══════════════════════════════════════════════════════════════════════════════
# EF MIGRATIONS
# ═══════════════════════════════════════════════════════════════════════════════

function Write-EfFailureGuidance {
    param(
        [string] $EfText,
        [string] $InfraCsprojPath,
        [string] $ApiCsprojPath,
        [bool]   $AutofixUsed
    )
    if ($EfText -match 'already exists|ya existe') {
        Write-Warn 'La migracion intento crear un objeto que ya existe en la base.'
        if ($AutofixUsed) {
            Write-Info 'Si la ultima migracion AutoDev no fue aplicada, eliminala:'
            Write-Info ('  dotnet ef migrations remove --project "' + $InfraCsprojPath + '" --startup-project "' + $ApiCsprojPath + '"')
        }
        Write-Info 'Opcion recomendada: .\scripts\dev\dev-restart.ps1 -ResetDatabase'
        return
    }
    if ($EfText -match 'MSB3021|MSB3027|being used by another process|se ha bloqueado por') {
        Write-Warn 'Bloqueo de DLL detectado. Cierra procesos dotnet/IDE y vuelve a ejecutar.'
        return
    }
    if ($EfText -match 'PendingModelChangesWarning') {
        Write-Warn 'Cambios de modelo sin migracion pendiente de crear.'
        Write-Info ('  dotnet ef migrations add <Nombre> --project "' + $InfraCsprojPath + '" --startup-project "' + $ApiCsprojPath + '"')
        return
    }
    Write-Warn 'Error EF no identificado automaticamente. Revisa la salida completa arriba.'
    Write-Info 'Opcion nuclear: .\scripts\dev\dev-restart.ps1 -ResetDatabase'
}

# Bucle de migraciones con autofix de PendingModelChanges y reintento en DLL lock.
function Invoke-ApplyMigrations {
    param(
        [string] $WorkDir,
        [string] $InfraCsproj,
        [string] $ApiCsproj,
        [bool]   $NoAutoFix,
        [bool]   $Strict
    )

    Write-Step 'Aplicando migraciones EF (dotnet ef database update)'
    Push-Location $WorkDir

    $dbUpdated   = $false
    $autofixUsed = $false
    $maxAttempts = 3
    $lastEfText  = ''

    try {
        for ($attempt = 1; $attempt -le $maxAttempts; $attempt++) {
            $run = Invoke-EfCommand -CmdArgs @(
                'ef', 'database', 'update',
                '--project', $InfraCsproj,
                '--startup-project', $ApiCsproj
            )
            $lastEfText = $run.Text

            if ($run.ExitCode -eq 0) {
                $dbUpdated = $true
                break
            }

            $hasPendingModel = $run.Text -match 'PendingModelChangesWarning'
            $isBuildLocked   = $run.Text -match 'MSB3021|MSB3027|being used by another process|se ha bloqueado por'

            if ($hasPendingModel -and (-not $NoAutoFix) -and (-not $autofixUsed)) {
                $autofixUsed   = $true
                $migrationName = New-AutoMigrationName
                Write-Warn "Modelo con cambios pendientes. Autocorrigiendo con migracion: $migrationName"

                $addMig = Invoke-EfCommand -CmdArgs @(
                    'ef', 'migrations', 'add', $migrationName,
                    '--project', $InfraCsproj,
                    '--startup-project', $ApiCsproj
                )
                if ($addMig.ExitCode -eq 0) {
                    Write-Ok 'Migracion automatica creada. Reintentando database update...'
                    continue
                }
                Write-Warn "No se pudo crear la migracion automatica (exit $($addMig.ExitCode))."
                Write-Info ('  dotnet ef migrations add <Nombre> --project "' + $InfraCsproj + '" --startup-project "' + $ApiCsproj + '"')
            }
            elseif ($hasPendingModel) {
                Write-Warn 'Cambios de modelo sin migracion (-NoAutoFixMigration activo).'
                Write-Info ('  dotnet ef migrations add <Nombre> --project "' + $InfraCsproj + '" --startup-project "' + $ApiCsproj + '"')
            }

            if ($isBuildLocked -and $attempt -lt $maxAttempts) {
                Write-Warn 'Bloqueo de DLL detectado. Cerrando procesos API y reintentando...'
                Stop-DotnetApiProcesses 'ERP.API'
                Start-Sleep -Milliseconds 900
                continue
            }

            Write-Warn "dotnet ef database update fallo (exit $($run.ExitCode))."
            if ($Strict) { throw "Migraciones fallaron (exit $($run.ExitCode))" }
            break
        }
    }
    finally {
        Pop-Location
    }

    if ($dbUpdated) {
        if ($autofixUsed) {
            Write-Ok 'Base de datos actualizada tras autocorreccion de migracion.'
        }
        else {
            Write-Ok 'Base de datos al dia con las migraciones del repo.'
        }
    }
    else {
        Write-Warn 'No se completo database update.'
        Write-EfFailureGuidance `
            -EfText           $lastEfText `
            -InfraCsprojPath  $InfraCsproj `
            -ApiCsprojPath    $ApiCsproj `
            -AutofixUsed      $autofixUsed
        if (-not $Strict) {
            Write-Warn 'Continua el arranque (usa -StrictMigrate para cortar en error).'
        }
    }

    return $dbUpdated
}

# ═══════════════════════════════════════════════════════════════════════════════
# MODO 5 — SOLO DOCKER
# ═══════════════════════════════════════════════════════════════════════════════

function Invoke-DockerUp {
    param([string] $SaasRoot)

    Write-Step 'Docker compose up -d (Postgres + Redis)'
    Push-Location $SaasRoot
    try {
        Remove-OrphanDockerContainers
        docker compose up -d
        if ($LASTEXITCODE -ne 0) { throw "docker compose up -d fallo (exit $LASTEXITCODE)" }
        docker compose ps
        Write-Ok 'Contenedores levantados.'
        Write-Info "Redis ping: docker exec $($script:RedisContainerName) redis-cli ping  (esperado: PONG)"
    }
    finally {
        Pop-Location
    }
}

# ═══════════════════════════════════════════════════════════════════════════════
# MODO 1 — DESARROLLO NORMAL
# ═══════════════════════════════════════════════════════════════════════════════

function Invoke-NormalMode {
    param(
        [string] $SaasRoot,
        [string] $BackendRoot,
        [string] $FrontendRoot,
        [string] $InfraCsproj,
        [string] $ApiCsproj,
        [bool]   $SkipDockerUp,
        [bool]   $NoMigrate,
        [bool]   $NoStart,
        [bool]   $NoAutoFix,
        [bool]   $Strict
    )

    if (-not $SkipDockerUp) {
        Invoke-DockerUp -SaasRoot $SaasRoot
    }
    else {
        Write-Warn 'Docker omitido (-SkipDocker).'
    }

    Write-Step 'Liberando puertos (API 5003/7253, Vite 5173)'
    Stop-ListenersOnPort @(5003, 7253, 5173)
    Stop-DotnetApiProcesses 'ERP.API'
    Write-Ok 'Puertos liberados.'

    if (-not $NoMigrate) {
        Invoke-ApplyMigrations `
            -WorkDir     $BackendRoot `
            -InfraCsproj $InfraCsproj `
            -ApiCsproj   $ApiCsproj `
            -NoAutoFix   $NoAutoFix `
            -Strict      $Strict
    }
    else {
        Write-Warn 'Migraciones omitidas (-NoMigrate).'
    }

    if ($NoStart) {
        Write-Step 'Arranque omitido (-NoStart)'
        Write-Info ('Backend:  cd "' + $BackendRoot  + '"; dotnet run --project "' + $ApiCsproj + '"')
        Write-Info ('Frontend: cd "' + $FrontendRoot + '"; npm run dev')
        return
    }

    Write-Step 'Iniciando API y Vite en ventanas nuevas'
    $apiCmd = "Set-Location -LiteralPath `"$BackendRoot`"; Write-Host 'API (ERP.API) http://localhost:5003' -ForegroundColor Cyan; dotnet run --project `"$ApiCsproj`""
    $feCmd  = "Set-Location -LiteralPath `"$FrontendRoot`"; Write-Host 'Frontend (Vite) http://localhost:5173' -ForegroundColor Cyan; npm run dev"

    Start-Process powershell -WorkingDirectory $BackendRoot  -ArgumentList @('-NoExit', '-NoLogo', '-Command', $apiCmd)
    Start-Sleep -Milliseconds 400
    Start-Process powershell -WorkingDirectory $FrontendRoot -ArgumentList @('-NoExit', '-NoLogo', '-Command', $feCmd)

    Write-Ok 'API y Frontend iniciados en ventanas separadas.'
    Write-Host ''
    Write-Host '  API:      http://localhost:5003'        -ForegroundColor Gray
    Write-Host '  Swagger:  http://localhost:5003/swagger' -ForegroundColor Gray
    Write-Host '  Frontend: http://localhost:5173'         -ForegroundColor Gray
}

# ═══════════════════════════════════════════════════════════════════════════════
# MODO 2 — RESET DATABASE
# ═══════════════════════════════════════════════════════════════════════════════

function Invoke-ResetDatabase {
    param(
        [string] $SaasRoot,
        [string] $BackendRoot,
        [string] $InfraCsproj,
        [string] $ApiCsproj,
        [bool]   $NoAutoFix,
        [bool]   $Strict,
        [bool]   $SkipDockerUp
    )

    if (-not (Invoke-Confirm 'Eliminar la base de datos PostgreSQL y recrearla desde cero?')) {
        Write-Warn 'Operacion cancelada.'
        return
    }

    if (-not $SkipDockerUp) {
        Invoke-DockerUp -SaasRoot $SaasRoot
    }

    Write-Step 'Liberando puertos antes del drop'
    Stop-ListenersOnPort @(5003, 7253, 5173)
    Stop-DotnetApiProcesses 'ERP.API'

    Write-Step 'Eliminando base de datos (dotnet ef database drop --force)'
    Push-Location $BackendRoot
    try {
        $drop = Invoke-EfCommand -CmdArgs @(
            'ef', 'database', 'drop', '--force',
            '--project', $InfraCsproj,
            '--startup-project', $ApiCsproj
        )
        if ($drop.ExitCode -ne 0) {
            Write-Warn "database drop fallo (exit $($drop.ExitCode))."
            Write-Info $drop.Text
            if ($Strict) { throw 'database drop fallo' }
        }
        else {
            Write-Ok 'Base de datos eliminada.'
        }
    }
    finally {
        Pop-Location
    }

    Invoke-ApplyMigrations `
        -WorkDir     $BackendRoot `
        -InfraCsproj $InfraCsproj `
        -ApiCsproj   $ApiCsproj `
        -NoAutoFix   $NoAutoFix `
        -Strict      $Strict
}

# ═══════════════════════════════════════════════════════════════════════════════
# MODO 3 — RESET COMPLETO DOCKER
# ═══════════════════════════════════════════════════════════════════════════════

function Invoke-ResetAll {
    param(
        [string] $SaasRoot,
        [string] $BackendRoot,
        [string] $InfraCsproj,
        [string] $ApiCsproj,
        [bool]   $NoAutoFix,
        [bool]   $Strict
    )

    if (-not (Invoke-Confirm 'Ejecutar docker compose down -v? Se eliminaran contenedores, volumenes, Postgres y Redis.')) {
        Write-Warn 'Operacion cancelada.'
        return
    }

    Write-Step 'Liberando puertos antes del reset'
    Stop-ListenersOnPort @(5003, 7253, 5173)
    Stop-DotnetApiProcesses 'ERP.API'

    Write-Step 'docker compose down -v (elimina contenedores y volumenes)'
    Push-Location $SaasRoot
    try {
        docker compose down -v
        if ($LASTEXITCODE -ne 0) { throw "docker compose down -v fallo (exit $LASTEXITCODE)" }
        Write-Ok 'Contenedores y volumenes eliminados.'
    }
    finally {
        Pop-Location
    }

    Invoke-DockerUp -SaasRoot $SaasRoot

    Write-Step 'Esperando que PostgreSQL este listo...'
    Start-Sleep -Seconds 3

    Invoke-ApplyMigrations `
        -WorkDir     $BackendRoot `
        -InfraCsproj $InfraCsproj `
        -ApiCsproj   $ApiCsproj `
        -NoAutoFix   $NoAutoFix `
        -Strict      $Strict
}

# ═══════════════════════════════════════════════════════════════════════════════
# MODO 4 — DOCTOR
# ═══════════════════════════════════════════════════════════════════════════════

function Invoke-DoctorMode {
    param(
        [string] $SaasRoot,
        [string] $BackendRoot,
        [string] $FrontendRoot,
        [string] $InfraCsproj,
        [string] $ApiCsproj
    )

    Reset-DocCounters

    Write-Step 'DOCTOR — diagnostico completo de entorno (sin cambios)'
    Write-Info "Raiz del repo: $SaasRoot"

    # ── 1. Toolchain ──────────────────────────────────────────────────────────
    Write-Step 'Doctor 1/6  Toolchain'

    $dotnetVer = & dotnet --version 2>$null
    if ($LASTEXITCODE -eq 0 -and $dotnetVer) { Register-DocOk   "dotnet SDK: $dotnetVer" }
    else                                       { Register-DocError 'dotnet SDK no disponible.' }

    $efVer = & dotnet ef --version 2>$null
    if ($LASTEXITCODE -eq 0 -and $efVer) { Register-DocOk   "dotnet ef tools: $($efVer -replace '\s+', ' ')" }
    else                                  { Register-DocWarn  'dotnet ef tools no instalado. Ejecuta: dotnet tool install --global dotnet-ef' }

    $nodeVer = & node --version 2>$null
    if ($LASTEXITCODE -eq 0 -and $nodeVer) { Register-DocOk   "Node.js: $nodeVer" }
    else                                    { Register-DocError 'Node.js no disponible.' }

    $npmVer = & npm --version 2>$null
    if ($LASTEXITCODE -eq 0 -and $npmVer) { Register-DocOk   "npm: $npmVer" }
    else                                   { Register-DocError 'npm no disponible.' }

    $dockerVer = & docker --version 2>$null
    if ($LASTEXITCODE -eq 0 -and $dockerVer) { Register-DocOk   "Docker: $dockerVer" }
    else                                      { Register-DocError 'Docker no disponible o daemon detenido.' }

    $prevEap = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    $dcVer = & docker compose version 2>$null
    $dcExit = $LASTEXITCODE
    $ErrorActionPreference = $prevEap
    if ($dcExit -eq 0 -and $dcVer) { Register-DocOk   "docker compose: $dcVer" }
    else                            { Register-DocError 'docker compose no disponible.' }

    # ── 2. Estructura del repo ────────────────────────────────────────────────
    Write-Step 'Doctor 2/6  Estructura del repo'

    if (Test-Path $InfraCsproj) { Register-DocOk   'ERP.Infrastructure.csproj encontrado.' }
    else                        { Register-DocError "Falta: $InfraCsproj" }

    if (Test-Path $ApiCsproj)   { Register-DocOk   'ERP.API.csproj encontrado.' }
    else                        { Register-DocError "Falta: $ApiCsproj" }

    $pkgJson = Join-Path $FrontendRoot 'package.json'
    if (Test-Path $pkgJson)     { Register-DocOk   'frontend/package.json encontrado.' }
    else                        { Register-DocError "Falta: $pkgJson" }

    $nodeModules = Join-Path $FrontendRoot 'node_modules'
    if (Test-Path $nodeModules) { Register-DocOk   'frontend/node_modules presente.' }
    else                        { Register-DocWarn  "frontend/node_modules no existe. Ejecuta: npm install (en frontend/)." }

    # ── 3. Infraestructura Docker ─────────────────────────────────────────────
    Write-Step 'Doctor 3/6  Infraestructura Docker'

    $pgStatus = docker ps --filter "name=$($script:PgContainerName)" --format '{{.Status}}' 2>$null
    if ($pgStatus -match 'Up')       { Register-DocOk   "PostgreSQL container: $pgStatus" }
    elseif ($pgStatus)               { Register-DocWarn  "PostgreSQL container existe pero no esta Up: $pgStatus" }
    else                             { Register-DocError "PostgreSQL container '$($script:PgContainerName)' no encontrado. Ejecuta: -DockerUp" }

    $redisStatus = docker ps --filter "name=$($script:RedisContainerName)" --format '{{.Status}}' 2>$null
    if ($redisStatus -match 'Up')    { Register-DocOk   "Redis container: $redisStatus" }
    elseif ($redisStatus)            { Register-DocWarn  "Redis container existe pero no esta Up: $redisStatus" }
    else                             { Register-DocError "Redis container '$($script:RedisContainerName)' no encontrado. Ejecuta: -DockerUp" }

    if ($redisStatus -match 'Up') {
        $prevEap2 = $ErrorActionPreference
        $ErrorActionPreference = 'Continue'
        $redisPing = & docker exec $script:RedisContainerName redis-cli ping 2>$null
        $ErrorActionPreference = $prevEap2
        if ($redisPing -match 'PONG') { Register-DocOk  'Redis PONG: OK.' }
        else                          { Register-DocWarn "Redis no responde PONG (respuesta: '$redisPing')." }
    }

    # ── 4. Puertos ────────────────────────────────────────────────────────────
    Write-Step 'Doctor 4/6  Puertos de desarrollo'

    $portRows = Get-ListeningProcessSummary @(5003, 7253, 5173)
    foreach ($r in $portRows) {
        switch ($r.Status) {
            'FREE'   { Register-DocOk  "Puerto $($r.Port): libre" }
            'LISTEN' { Register-DocWarn "Puerto $($r.Port): en uso por $($r.Process)" }
            default  { Register-DocWarn "Puerto $($r.Port): estado desconocido" }
        }
    }

    # ── 5. Estado EF Migrations ───────────────────────────────────────────────
    Write-Step 'Doctor 5/6  Estado EF Migrations'

    Push-Location $SaasRoot
    try {
        $migList = Invoke-EfCommand -CmdArgs @(
            'ef', 'migrations', 'list',
            '--project', 'backend/src/ERP.Infrastructure',
            '--startup-project', 'backend/src/ERP.API'
        )

        if ($migList.ExitCode -eq 0) {
            $lines   = ($migList.Text -split "`r?`n") | Where-Object { $_.Trim() -ne '' -and $_ -match '^\d{14}' }
            $pending = $lines | Where-Object { $_ -match '\(Pending\)' }
            $applied = $lines | Where-Object { $_ -notmatch '\(Pending\)' }

            Write-Info "Migraciones aplicadas : $($applied.Count)"
            Write-Info "Migraciones pendientes: $($pending.Count)"

            if ($pending.Count -gt 0) {
                Register-DocWarn "Hay $($pending.Count) migracion(es) pendiente(s):"
                foreach ($line in $pending) { Write-Info "  $($line.Trim())" }
                Write-Info 'Sugerencia: .\scripts\dev\dev-restart.ps1 -Normal  o  -ResetDatabase'
            }
            else {
                Register-DocOk 'Sin migraciones pendientes.'
            }
        }
        else {
            Register-DocWarn 'No se pudo obtener lista de migraciones (dotnet ef migrations list).'
        }

        # Verificar __EFMigrationsHistory directamente en la DB
        if ($pgStatus -match 'Up') {
            $prevEap3 = $ErrorActionPreference
            $ErrorActionPreference = 'Continue'
            $histOut  = & docker exec $script:PgContainerName psql `
                -U $script:PgUser `
                -d $script:PgDatabase `
                -c 'SELECT COUNT(*) FROM "__EFMigrationsHistory";' `
                -t 2>$null
            $histExit = $LASTEXITCODE
            $ErrorActionPreference = $prevEap3

            if ($histExit -eq 0) {
                $count = ($histOut | Where-Object { $_.Trim() -match '^\d+$' }) | Select-Object -First 1
                if ($null -ne $count) {
                    $countVal = [int]($count.Trim())
                    if ($countVal -eq 0) {
                        Register-DocError '__EFMigrationsHistory esta VACIA — el schema existe pero EF no registro ninguna migracion.'
                        Write-Info 'Causa tipica: DROP TABLE __EFMigrationsHistory o restore parcial de backup.'
                        Write-Info 'Solucion recomendada: .\scripts\dev\dev-restart.ps1 -ResetDatabase'
                    }
                    else {
                        Register-DocOk "__EFMigrationsHistory contiene $countVal registro(s) aplicados."
                    }
                }
                else {
                    Register-DocWarn '__EFMigrationsHistory: no se pudo parsear la respuesta de psql.'
                }
            }
            else {
                Register-DocWarn "__EFMigrationsHistory: no se pudo consultar (la DB '$($script:PgDatabase)' puede no existir aun)."
            }
        }
        else {
            Register-DocWarn 'PostgreSQL no esta corriendo — no se puede verificar __EFMigrationsHistory.'
        }
    }
    finally {
        Pop-Location
    }

    # ── 6. Build ──────────────────────────────────────────────────────────────
    Write-Step 'Doctor 6/6  Build (sin modificar nada)'

    Push-Location $BackendRoot
    try {
        Write-Info 'dotnet build --verbosity quiet ...'
        $build = Invoke-EfCommand -CmdArgs @('build', '--verbosity', 'quiet', '--nologo')
        if ($build.ExitCode -eq 0) {
            Register-DocOk 'dotnet build: OK'
        }
        else {
            Register-DocError 'dotnet build fallo. Revisa errores de compilacion.'
            $buildErrors = ($build.Text -split "`r?`n") | Where-Object { $_ -match 'error TS|error CS|Error' } | Select-Object -First 10
            foreach ($line in $buildErrors) { Write-Info "  $($line.Trim())" }
        }
    }
    finally {
        Pop-Location
    }

    if (Test-Path (Join-Path $FrontendRoot 'package.json')) {
        Push-Location $FrontendRoot
        try {
            Write-Info 'npm install --dry-run ...'
            $prevEap4 = $ErrorActionPreference
            $ErrorActionPreference = 'Continue'
            $npmOut  = & npm install --dry-run 2>&1
            $npmExit = $LASTEXITCODE
            $ErrorActionPreference = $prevEap4

            if ($npmExit -eq 0) {
                Register-DocOk 'npm install --dry-run: OK (node_modules sincronizado con package-lock.json)'
            }
            else {
                Register-DocWarn 'npm install --dry-run reporto cambios o errores. Ejecuta: npm install (en frontend/).'
            }
        }
        finally {
            Pop-Location
        }
    }

    # ── Resumen ───────────────────────────────────────────────────────────────
    Write-Host ''
    Write-Host '=======================================' -ForegroundColor White
    Write-Host '  DOCTOR SUMMARY' -ForegroundColor White
    Write-Host '=======================================' -ForegroundColor White
    Write-Host ("  OK      : {0}" -f $script:DocOK)    -ForegroundColor Green
    Write-Host ("  WARNING : {0}" -f $script:DocWarn)  -ForegroundColor Yellow
    Write-Host ("  ERROR   : {0}" -f $script:DocError) -ForegroundColor Red
    Write-Host ''

    if ($script:DocError -gt 0) {
        Write-Host '  Estado: CRITICO — resuelve los ERRORs antes de continuar.' -ForegroundColor Red
    }
    elseif ($script:DocWarn -gt 0) {
        Write-Host '  Estado: ADVERTENCIAS — el entorno puede funcionar con limitaciones.' -ForegroundColor Yellow
    }
    else {
        Write-Host '  Estado: ENTORNO SALUDABLE' -ForegroundColor Green
    }
    Write-Host ''
}

# ═══════════════════════════════════════════════════════════════════════════════
# MENU INTERACTIVO
# ═══════════════════════════════════════════════════════════════════════════════

function Show-Menu {
    Write-Host ''
    Write-Host '=========================================' -ForegroundColor Cyan
    Write-Host '  ERP SaaS DEVELOPMENT TOOL'             -ForegroundColor Cyan
    Write-Host '=========================================' -ForegroundColor Cyan
    Write-Host ''
    Write-Host '  Seleccione una opcion:' -ForegroundColor White
    Write-Host ''
    Write-Host '  1) Desarrollo normal      (Docker + Migraciones + API + Frontend)' -ForegroundColor Gray
    Write-Host '  2) Reset base de datos    (drop DB + recrear + migraciones)'        -ForegroundColor Yellow
    Write-Host '  3) Reset completo Docker  (down -v + up + migraciones)'             -ForegroundColor Red
    Write-Host '  4) Doctor                 (diagnostico completo sin cambios)'        -ForegroundColor DarkCyan
    Write-Host '  5) Solo Docker            (docker compose up -d)'                    -ForegroundColor Gray
    Write-Host '  6) Salir'                                                            -ForegroundColor DarkGray
    Write-Host ''
    $choice = Read-Host '  Opcion'
    return $choice.Trim()
}

# ═══════════════════════════════════════════════════════════════════════════════
# MAIN
# ═══════════════════════════════════════════════════════════════════════════════

$saasRoot     = Get-ErpSaasRepoRoot
$backendRoot  = Join-Path $saasRoot 'backend'
$frontendRoot = Join-Path $saasRoot 'frontend'
$infraCsproj  = Join-Path $backendRoot 'src\ERP.Infrastructure\ERP.Infrastructure.csproj'
$apiCsproj    = Join-Path $backendRoot 'src\ERP.API\ERP.API.csproj'

if (-not (Test-Path $infraCsproj)) {
    Write-Err "No se encontro el backend en: $infraCsproj"
    exit 1
}

Write-Host ''
Write-Host '========================================' -ForegroundColor White
Write-Host '  ERP SaaS — Development Tool'          -ForegroundColor White
Write-Host '========================================' -ForegroundColor White
Write-Host "  Raiz: $saasRoot" -ForegroundColor DarkGray
Write-Host ''

# Determinar modo: flag CLI tiene prioridad sobre el menu interactivo
$mode = ''
if     ($Normal)        { $mode = 'normal' }
elseif ($ResetDatabase) { $mode = 'reset-db' }
elseif ($ResetAll)      { $mode = 'reset-all' }
elseif ($Doctor)        { $mode = 'doctor' }
elseif ($DockerUp)      { $mode = 'docker-up' }
else {
    $choice = Show-Menu
    switch ($choice) {
        '1'     { $mode = 'normal' }
        '2'     { $mode = 'reset-db' }
        '3'     { $mode = 'reset-all' }
        '4'     { $mode = 'doctor' }
        '5'     { $mode = 'docker-up' }
        '6'     { Write-Info 'Saliendo.'; exit 0 }
        default { Write-Warn "Opcion no valida: '$choice'"; exit 1 }
    }
}

switch ($mode) {

    'normal' {
        Invoke-NormalMode `
            -SaasRoot     $saasRoot `
            -BackendRoot  $backendRoot `
            -FrontendRoot $frontendRoot `
            -InfraCsproj  $infraCsproj `
            -ApiCsproj    $apiCsproj `
            -SkipDockerUp $SkipDocker.IsPresent `
            -NoMigrate    $NoMigrate.IsPresent `
            -NoStart      $NoStart.IsPresent `
            -NoAutoFix    $NoAutoFixMigration.IsPresent `
            -Strict       $StrictMigrate.IsPresent
    }

    'reset-db' {
        Invoke-ResetDatabase `
            -SaasRoot     $saasRoot `
            -BackendRoot  $backendRoot `
            -InfraCsproj  $infraCsproj `
            -ApiCsproj    $apiCsproj `
            -NoAutoFix    $NoAutoFixMigration.IsPresent `
            -Strict       $StrictMigrate.IsPresent `
            -SkipDockerUp $SkipDocker.IsPresent
    }

    'reset-all' {
        Invoke-ResetAll `
            -SaasRoot    $saasRoot `
            -BackendRoot $backendRoot `
            -InfraCsproj $infraCsproj `
            -ApiCsproj   $apiCsproj `
            -NoAutoFix   $NoAutoFixMigration.IsPresent `
            -Strict      $StrictMigrate.IsPresent
    }

    'doctor' {
        Invoke-DoctorMode `
            -SaasRoot     $saasRoot `
            -BackendRoot  $backendRoot `
            -FrontendRoot $frontendRoot `
            -InfraCsproj  $infraCsproj `
            -ApiCsproj    $apiCsproj
    }

    'docker-up' {
        Invoke-DockerUp -SaasRoot $saasRoot
    }
}

Write-Host ''
Write-Host 'Listo.' -ForegroundColor Green
