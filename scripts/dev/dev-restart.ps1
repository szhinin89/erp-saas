<#
.SYNOPSIS
  Reinicia backend + frontend en desarrollo y aplica migraciones EF (orden seguro).

.DESCRIPTION
  1) Libera puertos del API (5003, 7253) y de Vite (5173) para poder compilar.
  2) Ejecuta `dotnet ef database update` contra el proyecto Infrastructure.
  3) Opcionalmente abre dos ventanas nuevas con `dotnet run` y `npm run dev`.

.PARAMETER NoMigrate
  No ejecuta migraciones (solo reinicia procesos en puertos y arranca apps).

.PARAMETER NoStart
  Solo para procesos + migraciones; no abre ventanas nuevas con el API ni el front.

.PARAMETER StrictMigrate
  Si está presente, falla el script cuando `dotnet ef database update` falle.
  Sin este flag, ante errores de migración el script avisa y continúa con el arranque.

.PARAMETER NoAutoFixMigration
  Desactiva autocorrección de migraciones pendientes de modelo.
  Por defecto, el script intenta crear una migración automática y volver a aplicar `database update`.

.PARAMETER Doctor
  Ejecuta diagnóstico del entorno de desarrollo (sin matar procesos, sin migrar, sin arrancar apps).
  Útil para detectar bloqueos, toolchain faltante y estado de migraciones.

.PARAMETER DockerUp
  Solo levanta PostgreSQL + Redis con docker compose y termina (equivale a `-DockerUp`).

.PARAMETER SkipDocker
  No ejecuta docker compose antes de migrar (útil si Postgres/Redis ya están corriendo).

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\scripts\dev\dev-restart.ps1

.EXAMPLE
  .\scripts\dev-restart.ps1 -DockerUp

.EXAMPLE
  cd C:\ProyectCursor\erp-saas
  .\scripts\dev-restart.ps1 -NoStart
#>
param(
    [switch] $NoMigrate,
    [switch] $NoStart,
    [switch] $StrictMigrate,
    [switch] $NoAutoFixMigration,
    [switch] $Doctor,
    [switch] $DockerUp,
    [switch] $SkipDocker
)

$ErrorActionPreference = 'Stop'

function Write-Step([string]$msg) {
    Write-Host ""
    Write-Host "==> $msg" -ForegroundColor Cyan
}

function Write-Ok([string]$msg) {
    Write-Host "    OK  $msg" -ForegroundColor Green
}

function Write-Warn([string]$msg) {
    Write-Host "    !!  $msg" -ForegroundColor Yellow
}

function Write-Info([string]$msg) {
    Write-Host "    ..  $msg" -ForegroundColor DarkCyan
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
            Write-Warn "No se pudo consultar el puerto $port (Get-NetTCPConnection). Continúa si no hay nada escuchando."
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
        Write-Warn "No se pudo inspeccionar procesos dotnet por CommandLine. Se continúa."
    }
}

function Invoke-EfCommand {
    param(
        [Parameter(Mandatory = $true)][string[]] $Args
    )
    # En PowerShell 5, con $ErrorActionPreference='Stop', cualquier salida stderr
    # de comandos nativos (incluyendo warnings) puede convertirse en excepción.
    # Bajamos temporalmente la severidad para capturar stdout+stderr sin abortar.
    $prevEap = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $out = & dotnet @Args 2>&1
    }
    finally {
        $ErrorActionPreference = $prevEap
    }
    $text = ($out | ForEach-Object { $_.ToString() }) -join [Environment]::NewLine
    return [PSCustomObject]@{
        ExitCode = $LASTEXITCODE
        Text = $text
    }
}

function New-AutoMigrationName {
    $stamp = Get-Date -Format 'yyyyMMddHHmmss'
    return "AutoDev_$stamp"
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
                $proc = Get-Process -Id $pid -ErrorAction SilentlyContinue
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
        [string] $ComposeProjectName = 'erp-saas',
        [string[]] $ManagedContainerNames = @('postgreszh', 'erp-saas-redis')
    )

    foreach ($name in $ManagedContainerNames) {
        $exists = docker ps -a --filter "name=^/${name}$" --format "{{.ID}}" 2>$null
        if (-not $exists) { continue }

        $managed = docker ps -a `
            --filter "name=^/${name}$" `
            --filter "label=com.docker.compose.project=$ComposeProjectName" `
            --format "{{.ID}}" 2>$null
        if ($managed) { continue }

        Write-Warn "Contenedor '$name' existe fuera de docker compose ($ComposeProjectName). Eliminando huérfano..."
        docker rm -f $name | Out-Null
        if ($LASTEXITCODE -ne 0) {
            throw "No se pudo eliminar el contenedor huérfano '$name'"
        }
        Write-Ok "Contenedor huérfano '$name' eliminado."
    }
}

function Invoke-DockerUp {
    param([string] $SaasRoot)

    Write-Step "Docker compose up -d (Postgres + Redis)"
    Push-Location $SaasRoot
    try {
        Remove-OrphanDockerContainers
        docker compose up -d
        if ($LASTEXITCODE -ne 0) {
            throw "docker compose up -d falló (exit $LASTEXITCODE)"
        }
        docker compose ps
        Write-Ok "Contenedores levantados."
        Write-Info "Redis: docker exec erp-saas-redis redis-cli ping  (esperado: PONG)"
        Write-Info "Siguiente: appsettings.Development.json + .\scripts\dev-restart.ps1 -NoStart"
    }
    finally {
        Pop-Location
    }
}

function Invoke-DoctorMode {
    param(
        [string] $SaasRoot,
        [string] $BackendRoot,
        [string] $FrontendRoot,
        [string] $InfraCsprojPath,
        [string] $ApiCsprojPath
    )

    Write-Step "Doctor: diagnóstico de entorno (sin cambios)"
    Write-Info "Raíz: $SaasRoot"

    Write-Step "Doctor 1/5 Puertos de desarrollo"
    $portRows = Get-ListeningProcessSummary @(5003, 7253, 5173)
    foreach ($r in $portRows) {
        if ($r.Status -eq 'FREE') {
            Write-Host ("    OK  Puerto {0}: libre" -f $r.Port) -ForegroundColor Green
        }
        elseif ($r.Status -eq 'LISTEN') {
            Write-Host ("    !!  Puerto {0}: en uso por {1}" -f $r.Port, $r.Process) -ForegroundColor Yellow
        }
        else {
            Write-Warn ("Puerto {0}: estado desconocido" -f $r.Port)
        }
    }

    Write-Step "Doctor 2/5 Toolchain"
    $dotnetVersion = (& dotnet --version 2>$null)
    if ($LASTEXITCODE -eq 0 -and $dotnetVersion) { Write-Ok "dotnet SDK: $dotnetVersion" } else { Write-Warn "dotnet SDK no disponible." }
    $nodeVersion = (& node --version 2>$null)
    if ($LASTEXITCODE -eq 0 -and $nodeVersion) { Write-Ok "Node.js: $nodeVersion" } else { Write-Warn "Node.js no disponible." }
    $npmVersion = (& npm --version 2>$null)
    if ($LASTEXITCODE -eq 0 -and $npmVersion) { Write-Ok "npm: $npmVersion" } else { Write-Warn "npm no disponible." }

    Write-Step "Doctor 3/5 Estructura del repo"
    if (Test-Path $InfraCsprojPath) { Write-Ok "Infrastructure csproj detectado." } else { Write-Warn "Falta $InfraCsprojPath" }
    if (Test-Path $ApiCsprojPath) { Write-Ok "API csproj detectado." } else { Write-Warn "Falta $ApiCsprojPath" }
    if (Test-Path (Join-Path $FrontendRoot 'package.json')) { Write-Ok "frontend/package.json detectado." } else { Write-Warn "Falta frontend/package.json" }
    if (Test-Path (Join-Path $FrontendRoot 'node_modules')) { Write-Ok "frontend/node_modules presente." } else { Write-Warn "frontend/node_modules no existe. Ejecuta: npm install (en frontend)." }

    Write-Step "Doctor 4/5 Estado EF migrations"
    Push-Location $SaasRoot
    try {
        $mig = Invoke-EfCommand -Args @(
            'ef', 'migrations', 'list',
            '--project', 'backend/src/ERP.Infrastructure',
            '--startup-project', 'backend/src/ERP.API'
        )
        if ($mig.ExitCode -eq 0) {
            $pending = ($mig.Text -split "`r?`n") | Where-Object { $_ -match '\(Pending\)' }
            if ($pending.Count -gt 0) {
                Write-Warn ("Migraciones pendientes: {0}" -f $pending.Count)
                foreach ($line in $pending) { Write-Info $line.Trim() }
                Write-Info "Sugerencia: dotnet ef database update --project backend/src/ERP.Infrastructure --startup-project backend/src/ERP.API"
            }
            else {
                Write-Ok "Sin migraciones pendientes."
            }
        }
        else {
            Write-Warn "No se pudo obtener lista de migraciones (dotnet ef migrations list)."
        }
    }
    finally {
        Pop-Location
    }

    Write-Step "Doctor 5/5 Recomendaciones"
    if ($portRows | Where-Object { $_.Status -eq 'LISTEN' }) {
        Write-Info "Si quieres reinicio limpio: .\scripts\dev-restart.ps1"
    }
    if (-not (Test-Path (Join-Path $FrontendRoot 'node_modules'))) {
        Write-Info 'Instala dependencias front: cd ".\frontend" ; npm install'
    }
    Write-Ok "Doctor finalizado."
}

function Write-EfFailureGuidance {
    param(
        [string] $EfText,
        [string] $InfraCsprojPath,
        [string] $ApiCsprojPath,
        [bool] $AutofixUsed
    )

    if ($EfText -match 'already exists|ya existe') {
        Write-Warn "La migración intentó crear un objeto que ya existe en la base (columna/tabla/etc.)."
        Write-Host "    Qué hacer:" -ForegroundColor DarkYellow
        $step = 1
        if ($AutofixUsed) {
            Write-Host ("    $step) Revisa si la última migración AutoDev es redundante y, si NO fue aplicada, elimínala con:") -ForegroundColor DarkYellow
            Write-Host ('       dotnet ef migrations remove --project "' + $InfraCsprojPath + '" --startup-project "' + $ApiCsprojPath + '"') -ForegroundColor DarkYellow
            $step++
        }
        Write-Host ("    $step) Revisa tus migraciones recientes para evitar operaciones duplicadas.") -ForegroundColor DarkYellow
        $step++
        Write-Host ("    $step) Vuelve a correr dev-restart.ps1.") -ForegroundColor DarkYellow
        return
    }

    if ($EfText -match 'MSB3021|MSB3027|being used by another process|se ha bloqueado por') {
        Write-Warn "Se detectó bloqueo de archivos DLL por procesos en ejecución."
        Write-Host "    Qué hacer: cierra procesos dotnet/IDE que estén usando ERP.API y vuelve a ejecutar el script." -ForegroundColor DarkYellow
        return
    }

    if ($EfText -match 'PendingModelChangesWarning') {
        Write-Warn "Hay cambios de modelo sin migración."
        Write-Host ('    Qué hacer: dotnet ef migrations add <NombreMigracion> --project "' + $InfraCsprojPath + '" --startup-project "' + $ApiCsprojPath + '"') -ForegroundColor DarkYellow
        Write-Host "    Luego ejecuta de nuevo dev-restart.ps1." -ForegroundColor DarkYellow
        return
    }

    Write-Warn "No se pudo determinar una autocorrección segura para el error EF."
    Write-Host "    Revisa el mensaje completo de dotnet ef y aplica la corrección sugerida por EF Core." -ForegroundColor DarkYellow
}

# Rutas: resolver raíz del monorepo (scripts/dev → repo root)
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
    throw 'No se encontró la raíz del repo ERP SaaS.'
}
$saasRoot = Get-ErpSaasRepoRoot
$backendRoot = Join-Path $saasRoot 'backend'
$frontendRoot = Join-Path $saasRoot 'frontend'
$infraCsproj = Join-Path $backendRoot 'src\ERP.Infrastructure\ERP.Infrastructure.csproj'
$apiCsproj = Join-Path $backendRoot 'src\ERP.API\ERP.API.csproj'

if (-not (Test-Path $infraCsproj)) {
    Write-Host "No encuentro el backend en: $infraCsproj" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "========================================"  -ForegroundColor White
Write-Host "  ERP SaaS - reinicio dev + migraciones" -ForegroundColor White
Write-Host "========================================"  -ForegroundColor White
Write-Host "  Raíz SaaS: $saasRoot"

if ($DockerUp) {
    Invoke-DockerUp -SaasRoot $saasRoot
    exit 0
}

if ($Doctor) {
    Invoke-DoctorMode -SaasRoot $saasRoot -BackendRoot $backendRoot -FrontendRoot $frontendRoot -InfraCsprojPath $infraCsproj -ApiCsprojPath $apiCsproj
    exit 0
}

if (-not $SkipDocker -and -not $NoMigrate) {
    Invoke-DockerUp -SaasRoot $saasRoot
}

Write-Step "1/3 Deteniendo procesos en puertos (API 5003/7253, Vite 5173)"
Stop-ListenersOnPort @(5003, 7253, 5173)
Stop-DotnetApiProcesses "ERP.API"
Write-Ok "Puertos liberados (si había algo escuchando, se cerró)."

if (-not $NoMigrate) {
    Write-Step "2/3 Aplicando migraciones EF (dotnet ef database update)"
    Push-Location $backendRoot
    try {
        $dbUpdated = $false
        $autofixUsed = $false
        $maxAttempts = 3
        $lastEfText = ''
        for ($attempt = 1; $attempt -le $maxAttempts; $attempt++) {
            $run = Invoke-EfCommand -Args @(
                'ef', 'database', 'update',
                '--project', $infraCsproj,
                '--startup-project', $apiCsproj
            )
            $lastEfText = $run.Text
            if ($run.ExitCode -eq 0) {
                $dbUpdated = $true
                break
            }

            $hasPendingModel = $run.Text -match 'PendingModelChangesWarning'
            $isBuildLocked = ($run.Text -match 'MSB3021|MSB3027|being used by another process|se ha bloqueado por')

            if ($hasPendingModel -and -not $NoAutoFixMigration -and -not $autofixUsed) {
                $autofixUsed = $true
                $migrationName = New-AutoMigrationName
                Write-Warn "Modelo con cambios pendientes detectado. Intentando autocorregir con migración: $migrationName"

                $addMig = Invoke-EfCommand -Args @(
                    'ef', 'migrations', 'add', $migrationName,
                    '--project', $infraCsproj,
                    '--startup-project', $apiCsproj
                )
                if ($addMig.ExitCode -eq 0) {
                    Write-Ok "Migración automática creada. Reintentando database update..."
                    continue
                }

                Write-Warn "No se pudo crear migración automática (exit $($addMig.ExitCode))."
                Write-Host ('    Sugerencia: dotnet ef migrations add <NombreMigracion> --project "' + $infraCsproj + '" --startup-project "' + $apiCsproj + '"') -ForegroundColor DarkYellow
            }
            elseif ($hasPendingModel) {
                Write-Warn "Se detectaron cambios de modelo sin migración."
                Write-Host ('    Sugerencia: dotnet ef migrations add <NombreMigracion> --project "' + $infraCsproj + '" --startup-project "' + $apiCsproj + '"') -ForegroundColor DarkYellow
            }

            if ($isBuildLocked -and $attempt -lt $maxAttempts) {
                Write-Warn "Se detectó bloqueo de archivos en build. Cerrando procesos API y reintentando..."
                Stop-DotnetApiProcesses "ERP.API"
                Start-Sleep -Milliseconds 900
                continue
            }

            Write-Warn "dotnet ef database update falló (exit $($run.ExitCode))."
            if ($StrictMigrate) {
                throw "dotnet ef terminó con código $($run.ExitCode)"
            }
            break
        }

        if ($dbUpdated) {
            if ($autofixUsed) {
                Write-Ok "Base de datos actualizada tras autocorrección de migración."
            } else {
                Write-Ok "Base de datos al día con las migraciones del repo."
            }
        } else {
            Write-Warn "No se completó database update."
            Write-EfFailureGuidance -EfText $lastEfText -InfraCsprojPath $infraCsproj -ApiCsprojPath $apiCsproj -AutofixUsed $autofixUsed
            if (-not $StrictMigrate) {
                Write-Warn "Se continúa con el arranque para no bloquear desarrollo (use -StrictMigrate para cortar en error)."
            }
        }
    }
    finally {
        Pop-Location
    }
}
else {
    Write-Warn "Migraciones omitidas (-NoMigrate)."
}

if ($NoStart) {
    Write-Step "3/3 Arranque de apps omitido (-NoStart)"
    Write-Host ""
    Write-Host "Para levantar a mano:" -ForegroundColor White
    Write-Host ('  Backend:  cd "' + $backendRoot + '" ; dotnet run --project "' + $apiCsproj + '"') -ForegroundColor Gray
    Write-Host ('  Frontend: cd "' + $frontendRoot + '" ; npm run dev') -ForegroundColor Gray
    Write-Host ""
    exit 0
}

Write-Step "3/3 Iniciando API y Vite en ventanas nuevas"
# -Command en una sola cadena con comillas dobles escapadas (evita errores de parser con '' anidadas)
$apiCmd = "Set-Location -LiteralPath `"$backendRoot`"; Write-Host 'API (ERP.API) http://localhost:5003' -ForegroundColor Cyan; dotnet run --project `"$apiCsproj`""
$feCmd = "Set-Location -LiteralPath `"$frontendRoot`"; Write-Host 'Frontend (Vite) http://localhost:5173' -ForegroundColor Cyan; npm run dev"

Start-Process powershell -WorkingDirectory $backendRoot -ArgumentList @(
    '-NoExit', '-NoLogo', '-Command', $apiCmd
)
Start-Sleep -Milliseconds 400
Start-Process powershell -WorkingDirectory $frontendRoot -ArgumentList @(
    '-NoExit', '-NoLogo', '-Command', $feCmd
)

Write-Ok "Se abrieron dos ventanas de PowerShell (API y frontend)."
Write-Host ""
Write-Host "URLs tipicas:" -ForegroundColor White
Write-Host "  API:      http://localhost:5003" -ForegroundColor Gray
Write-Host "  Swagger:  http://localhost:5003/swagger (si esta habilitado)" -ForegroundColor Gray
Write-Host "  Frontend: http://localhost:5173" -ForegroundColor Gray
Write-Host ""
Write-Host "Listo." -ForegroundColor Green
