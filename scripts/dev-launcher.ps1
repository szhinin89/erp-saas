#Requires -Version 7.0

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# =============================================================================
# ZH TECHNOLOGIES ERP
# DEV LAUNCHER v2.0
#
# ERP Core Baseline
# - Backend ASP.NET Core
# - Frontend React + Vite
# - PostgreSQL
# - EF Core
#
# Filosofía: un único comando para administrar todo el entorno local del ERP.
#
# Uso:
# Ejecutar este script desde cualquier ubicación.
# Detectará automáticamente la raíz del repositorio.
# =============================================================================


# =============================================================================
# CONFIGURACIÓN
# =============================================================================

$script:Config = @{
    ApiPort           = 5003
    FrontendPort      = 5173
    ApiTimeoutSeconds = 45
    FrontendTimeoutSeconds = 45
}
$script:ApiLaunch = $null
$script:FrontendLaunch = $null


# =============================================================================
# URLS
# =============================================================================

$script:Urls = @{
    Frontend = "http://localhost:$($Config.FrontendPort)"
    Api      = "http://localhost:$($Config.ApiPort)"
    Swagger  = "http://localhost:$($Config.ApiPort)/swagger"
}


# =============================================================================
# LOGGING
# =============================================================================

function Write-Title {
    param([string]$Message)

    Write-Host ""
    Write-Host "=================================================" -ForegroundColor Cyan
    Write-Host $Message -ForegroundColor Cyan
    Write-Host "=================================================" -ForegroundColor Cyan
}

function Write-Step {
    param([string]$Message)
    Write-Host ""
    Write-Host "[INFO] $Message" -ForegroundColor Cyan
}

function Write-Ok {
    param([string]$Message)
    Write-Host "[OK]   $Message" -ForegroundColor Green
}

function Write-Warn {
    param([string]$Message)
    Write-Host "[WARN] $Message" -ForegroundColor Yellow
}

function Write-Err {
    param([string]$Message)
    Write-Host "[ERROR] $Message" -ForegroundColor Red
}


# =============================================================================
# VALIDACIÓN DE DEPENDENCIAS
# =============================================================================

function Test-Command {
    param(
        [Parameter(Mandatory)]
        [string]$Command
    )

    return $null -ne (Get-Command $Command -ErrorAction SilentlyContinue)
}


function Assert-Prerequisites {

    Write-Step "Verificando herramientas requeridas"

    $required = @(
        "dotnet",
        "npm",
        "pwsh"
    )

    $missing = @()

    foreach ($cmd in $required) {

        if (Test-Command $cmd) {
            Write-Ok "$cmd encontrado"
        }
        else {
            $missing += $cmd
        }
    }

    if ($missing.Count -gt 0) {

        Write-Err "Faltan herramientas requeridas:"

        foreach ($item in $missing) {
            Write-Host " - $item" -ForegroundColor Red
        }

        throw "No se puede continuar"
    }
}


# =============================================================================
# RESOLUCIÓN DEL REPOSITORIO
# =============================================================================

function Get-RepositoryRoot {

    $directory = $PSScriptRoot

    while ($directory) {

        $hasBackend = Test-Path (
            Join-Path $directory "backend"
        )

        $hasFrontend = Test-Path (
            Join-Path $directory "frontend"
        )

        if ($hasBackend -and $hasFrontend) {
            return $directory
        }

        $parent = Split-Path $directory -Parent

        if ($parent -eq $directory) {
            break
        }

        $directory = $parent
    }

    throw "No se encontró la raíz del repositorio ERP"
}


function Initialize-Paths {

    Write-Step "Resolviendo estructura del repositorio"

    $script:Root = Get-RepositoryRoot

    $script:BackendPath  = Join-Path $Root "backend"
    $script:FrontendPath = Join-Path $Root "frontend"

    $script:ApiProject = Join-Path `
        $BackendPath `
        "src/ERP.API/ERP.API.csproj"

    $script:InfrastructureProject = Join-Path `
        $BackendPath `
        "src/ERP.Infrastructure/ERP.Infrastructure.csproj"


    Write-Ok "Root: $Root"
    Write-Ok "Backend: $BackendPath"
    Write-Ok "Frontend: $FrontendPath"


    if (-not (Test-Path $ApiProject)) {
        throw "No existe ERP.API.csproj"
    }

    if (-not (Test-Path $InfrastructureProject)) {
        throw "No existe ERP.Infrastructure.csproj"
    }
}


# =============================================================================
# INFORMACIÓN DEL ENTORNO
# =============================================================================

function Show-Environment {

    Write-Title "ZH Technologies ERP - Dev Environment"

    Write-Host ""
    Write-Host "Root       : $Root"
    Write-Host "Backend    : $BackendPath"
    Write-Host "Frontend   : $FrontendPath"

    Write-Host ""
    Write-Host "URLs"

    Write-Host " API       : $($Urls.Api)"
    Write-Host " Swagger   : $($Urls.Swagger)"
    Write-Host " Frontend  : $($Urls.Frontend)"

    Write-Host ""
}


# =============================================================================
# CONTROL DE PROCESOS
# =============================================================================

function Stop-ApiProcess {

    Write-Step "Deteniendo instancia actual del ERP API"

    $connections = Get-NetTCPConnection `
        -LocalPort $Config.ApiPort `
        -State Listen `
        -ErrorAction SilentlyContinue

    if ($connections) {

        # Validate all listeners before stopping anything. Never kill an unrelated port owner.
        foreach ($ownerId in @($connections.OwningProcess | Select-Object -Unique)) {
            $info = Get-CimInstance Win32_Process -Filter "ProcessId = $ownerId" -ErrorAction Stop
            if (-not (Test-ApiExecutable $info)) {
                throw "Puerto $($Config.ApiPort) ocupado por un proceso ajeno (PID=$ownerId). No se detuvo."
            }
        }

        $connections |
            Select-Object -ExpandProperty OwningProcess -Unique |
            ForEach-Object {

                try {
                    Stop-Process `
                        -Id $_ `
                        -Force `
                        -ErrorAction Stop

                    Write-Ok "Proceso detenido PID=$_"
                }
                catch {
                    throw "No se pudo detener el proceso API. Inicio cancelado: $($_.Exception.Message)"
                }
            }
    }
    else {
        Write-Ok "No existe API ejecutándose"
    }
}


function Stop-FrontendProcess {
    Write-Step "Verificando puerto Frontend"
    $listeners = @(Get-NetTCPConnection -State Listen -ErrorAction Stop |
        Where-Object LocalPort -eq $Config.FrontendPort)
    if ($listeners.Count -eq 0) {
        Write-Ok "No existe Frontend ejecutándose"
        return
    }
    if ($null -eq $script:FrontendLaunch) {
        throw "Puerto $($Config.FrontendPort) sin propiedad del launcher. No se detuvo."
    }

    $owned = @()
    try {
        # Retain native handles before inspecting identity; never terminate by a reusable PID.
        foreach ($ownerId in @($listeners.OwningProcess | Select-Object -Unique)) {
            $process = Get-Process -Id $ownerId -ErrorAction Stop
            $owned += $process
            $null = $process.Handle
            if (-not (Test-FrontendListenerOwnership $script:FrontendLaunch $process)) {
                throw "Propiedad frontend no verificable (PID=$ownerId). No se detuvo."
            }
        }
        $current = @(Get-NetTCPConnection -State Listen -ErrorAction Stop |
            Where-Object LocalPort -eq $Config.FrontendPort)
        if ($current.Count -eq 0 -or
            @(Compare-Object @($listeners.OwningProcess | Sort-Object -Unique) @($current.OwningProcess | Sort-Object -Unique)).Count -gt 0) {
            throw "El listener frontend cambio. No se detuvo."
        }
        foreach ($process in $owned) {
            if (-not (Test-FrontendListenerOwnership $script:FrontendLaunch $process)) {
                throw "La identidad frontend cambio. No se detuvo."
            }
        }
        foreach ($process in $owned) {
            $process.Kill()
            Write-Ok "Frontend detenido PID=$($process.Id)"
        }
    }
    finally {
        foreach ($process in $owned) { $process.Dispose() }
    }
}

function Test-FrontendListenerOwnership {
    param($Launch, $Process)

    $Launch.Process.Refresh()
    $Process.Refresh()
    if ($Launch.Process.HasExited -or $Process.HasExited) { return $false }
    $current = Get-CimInstance Win32_Process -Filter "ProcessId = $($Process.Id)" -ErrorAction Stop
    if ($null -eq $current -or $current.Name -ine 'node.exe' -or
        [string]::IsNullOrWhiteSpace($current.ExecutablePath) -or
        [IO.Path]::GetFullPath($current.ExecutablePath) -ine $Launch.NodePath -or
        [Math]::Abs(($current.CreationDate.ToUniversalTime() - $Process.StartTime.ToUniversalTime()).TotalMilliseconds) -gt 1) {
        return $false
    }
    for ($depth = 0; $depth -lt 32 -and $null -ne $current; $depth++) {
        if ($current.ProcessId -eq $Launch.Process.Id) {
            return [Math]::Abs(($current.CreationDate.ToUniversalTime() - $Launch.StartTimeUtc).TotalMilliseconds) -le 1
        }
        $parent = Get-CimInstance Win32_Process -Filter "ProcessId = $($current.ParentProcessId)" -ErrorAction Stop
        if ($null -eq $parent -or $parent.CreationDate -gt $current.CreationDate) { return $false }
        $current = $parent
    }
    return $false
}


function Stop-DevelopmentServices {

    Write-Step "Cerrando servicios de desarrollo"

    Stop-ApiProcess
    Stop-FrontendProcess
}


# =============================================================================
# LIMPIEZA (responsabilidad interna del flujo de inicio, no es una opción del menú)
# =============================================================================

function Invoke-Clean {

    Write-Step "Eliminando archivos temporales"

    $folders = @(
        "bin",
        "obj"
    )


    foreach ($folder in $folders) {

        Get-ChildItem `
            -Path $BackendPath `
            -Directory `
            -Recurse `
            -Filter $folder `
            -ErrorAction SilentlyContinue |
            ForEach-Object {

                # $_ dentro de un bloque catch se rebinda al ErrorRecord capturado (sin
                # propiedad FullName) — se captura la carpeta actual en $dirPath ANTES del
                # try/catch para que el mensaje de error pueda seguir refiriéndose a ella.
                $dirPath = $_.FullName

                try {
                    Remove-Item `
                        $dirPath `
                        -Force `
                        -Recurse `
                        -ErrorAction Stop

                    Write-Ok "Eliminado $dirPath"
                }
                catch {
                    Write-Warn "No se pudo eliminar $dirPath"
                }
            }
    }


    $dist = Join-Path $FrontendPath "dist"


    if (Test-Path $dist) {

        Remove-Item `
            $dist `
            -Force `
            -Recurse

        Write-Ok "Frontend dist eliminado"
    }
    else {
        Write-Ok "No existe carpeta dist"
    }
}


# =============================================================================
# DEPENDENCIAS
# =============================================================================

function Invoke-DotnetRestore {

    Write-Step "Restaurando dependencias .NET"

    dotnet restore "$BackendPath\src\ERP.slnx"

    if ($LASTEXITCODE -ne 0) {
        throw "Falló dotnet restore"
    }

    Write-Ok "Dependencias .NET restauradas"
}


function Ensure-FrontendDependencies {

    $nodeModules = Join-Path $FrontendPath "node_modules"

    if (Test-Path $nodeModules) {
        Write-Ok "node_modules ya existe — se omite npm install"
        return
    }

    Write-Step "Instalando dependencias del Frontend (npm install)"

    Push-Location $FrontendPath

    try {

        npm install

        if ($LASTEXITCODE -ne 0) {
            throw "Falló npm install"
        }

        Write-Ok "Dependencias del Frontend instaladas"
    }
    finally {
        Pop-Location
    }
}


# =============================================================================
# MIGRACIONES EF CORE
# =============================================================================

# El reset NO borra ni regenera migraciones: la línea base versionada contiene raw SQL
# (índices, función y triggers, pg_trgm) que el model snapshot no conoce, y regenerarla desde
# el modelo los pierde en silencio. Ver RawSqlDatabaseObjectsSurviveMigrationSquashTests.

function Invoke-DatabaseUpdate {

    Write-Step "Aplicando migraciones EF Core (Development)"

    $previousAspNetEnvironment = $env:ASPNETCORE_ENVIRONMENT
    $previousDotnetEnvironment = $env:DOTNET_ENVIRONMENT

    # La factory de EF (ERP.API) usa las mismas fuentes que el runtime: en Development
    # lee la conexión de los user-secrets de ERP.API (o de ConnectionStrings__DefaultConnection).
    Push-Location (Split-Path $ApiProject -Parent)

    try {
        $env:ASPNETCORE_ENVIRONMENT = "Development"
        $env:DOTNET_ENVIRONMENT = "Development"

        # dotnet-ef versionado en .config/dotnet-tools.json (idempotente).
        dotnet tool restore | Out-Null
        if ($LASTEXITCODE -ne 0) {
            throw "Falló dotnet tool restore (dotnet-ef)"
        }

        dotnet ef database update `
            --project $InfrastructureProject `
            --startup-project $ApiProject `
            --context ErpDbContext

        if ($LASTEXITCODE -ne 0) {
            throw "Falló EF Database Update"
        }

        Write-Ok "Base de datos actualizada"
    }
    finally {
        $env:ASPNETCORE_ENVIRONMENT = $previousAspNetEnvironment
        $env:DOTNET_ENVIRONMENT = $previousDotnetEnvironment
        Pop-Location
    }
}


function Invoke-DatabaseDrop {

    Write-Warn "Eliminando base de datos actual"

    $previousAspNetEnvironment = $env:ASPNETCORE_ENVIRONMENT
    $previousDotnetEnvironment = $env:DOTNET_ENVIRONMENT
    Push-Location (Split-Path $ApiProject -Parent)

    try {
        $env:ASPNETCORE_ENVIRONMENT = "Development"
        $env:DOTNET_ENVIRONMENT = "Development"

        dotnet tool restore | Out-Null
        if ($LASTEXITCODE -ne 0) {
            throw "Falló dotnet tool restore (dotnet-ef)"
        }

        dotnet ef database drop `
            --force `
            --project $InfrastructureProject `
            --startup-project $ApiProject `
            -- --launcher-reset=true

        if ($LASTEXITCODE -ne 0) {
            throw "Falló EF Database Drop"
        }

        Write-Ok "Base de datos eliminada"
    }
    finally {
        $env:ASPNETCORE_ENVIRONMENT = $previousAspNetEnvironment
        $env:DOTNET_ENVIRONMENT = $previousDotnetEnvironment
        Pop-Location
    }
}


# =============================================================================
# FIRST RUN — consulta de estado (ver docs/architecture/first-run.md)
# =============================================================================

function Get-SetupStatus {

    try {

        $response = Invoke-RestMethod `
            -Uri "$($Urls.Api)/api/v1/setup/status" `
            -Method Get `
            -TimeoutSec 5 `
            -ErrorAction Stop

        return $response.data
    }
    catch {
        return $null
    }
}


function Confirm-SystemInitialized {
    $status = Get-SetupStatus
    if ($null -eq $status) {
        Write-Warn "No se pudo consultar el estado de instalacion."
        return $false
    }
    if ($status.isInitialized -eq $true) {
        Write-Ok "Sistema inicializado."
        return $true
    }
    Write-Warn "Instalacion pendiente: solo el propietario debe crear manualmente el primer administrador en el flujo first-run existente."
    return $false
}


# =============================================================================
# BUILD BACKEND
# =============================================================================

function Invoke-BackendBuild {

    Write-Step "Compilando Backend"

    Push-Location $BackendPath

    try {

        dotnet build `
            $ApiProject `
            --configuration Debug

        if ($LASTEXITCODE -ne 0) {
            throw "El build del Backend falló"
        }

        Write-Ok "Backend compilado correctamente"
    }
    finally {
        Pop-Location
    }
}


# =============================================================================
# HEALTH CHECK API
# =============================================================================

function Test-ApiExecutable {
    param($ProcessInfo)

    if ($null -eq $ProcessInfo) { return $false }
    $apiName = [IO.Path]::GetFileNameWithoutExtension($ApiProject)
    $binPath = [IO.Path]::GetFullPath((Join-Path (Split-Path $ApiProject -Parent) 'bin')) + [IO.Path]::DirectorySeparatorChar
    if ($ProcessInfo.Name -ine "$apiName.exe" -or [string]::IsNullOrWhiteSpace($ProcessInfo.ExecutablePath)) {
        return $false
    }
    return [IO.Path]::GetFullPath($ProcessInfo.ExecutablePath).StartsWith($binPath, [StringComparison]::OrdinalIgnoreCase)
}

function Test-ApiListenerOwnership {
    param($Launch, [int]$OwnerId)

    $current = Get-CimInstance Win32_Process -Filter "ProcessId = $OwnerId" -ErrorAction Stop
    if (-not (Test-ApiExecutable $current)) { return $false }
    for ($depth = 0; $depth -lt 32 -and $null -ne $current; $depth++) {
        if ($current.ProcessId -eq $Launch.Process.Id) {
            # The retained process handle plus creation time prevents accepting a reused PID.
            return [Math]::Abs(($current.CreationDate.ToUniversalTime() - $Launch.StartTimeUtc).TotalMilliseconds) -le 1
        }
        $parent = Get-CimInstance Win32_Process -Filter "ProcessId = $($current.ParentProcessId)" -ErrorAction Stop
        if ($null -eq $parent -or $parent.CreationDate -gt $current.CreationDate) { return $false }
        $current = $parent
    }
    return $false
}

function Wait-ApiReady {
    param([Parameter(Mandatory)]$Launch)

    Write-Step "Esperando disponibilidad del API"

    $healthUrl = "$($Urls.Api)/api/v1/setup/status"

    for ($i = 1; $i -le $Config.ApiTimeoutSeconds; $i++) {

        try {

            $Launch.Process.Refresh()
            if ($Launch.Process.HasExited) {
                Write-Warn "El proceso API lanzado termino antes de estar disponible (codigo=$($Launch.Process.ExitCode))."
                return $false
            }
            $listeners = @(Get-NetTCPConnection -LocalPort $Config.ApiPort -State Listen -ErrorAction SilentlyContinue)
            foreach ($listener in $listeners) {
                if (-not (Test-ApiListenerOwnership $Launch $listener.OwningProcess)) {
                    Write-Warn "Puerto $($Config.ApiPort) ocupado por otro proceso. Inicio rechazado."
                    return $false
                }
            }
            if ($listeners.Count -eq 0) { Start-Sleep -Seconds 1; continue }

        }
        catch {
            Write-Warn "No se pudo verificar la identidad del proceso API: $($_.Exception.Message)"
            return $false
        }
        $httpResponded = $false
        try {
            Invoke-RestMethod `
                -Uri $healthUrl `
                -TimeoutSec 2 `
                -ErrorAction Stop |
                Out-Null
            $httpResponded = $true

            # Re-check after HTTP: a successful response alone cannot prove process identity.
            $Launch.Process.Refresh()
            if ($Launch.Process.HasExited) {
                Write-Warn "El proceso API termino durante la comprobacion HTTP."
                return $false
            }
            $listeners = @(Get-NetTCPConnection -LocalPort $Config.ApiPort -State Listen -ErrorAction SilentlyContinue)
            if ($listeners.Count -eq 0) { continue }
            foreach ($listener in $listeners) {
                if (-not (Test-ApiListenerOwnership $Launch $listener.OwningProcess)) {
                    Write-Warn "La identidad del listener cambio durante la comprobacion HTTP."
                    return $false
                }
            }
            Write-Ok "API disponible después de $i segundos"

            return $true
        }
        catch {

            if ($httpResponded) {
                Write-Warn "No se pudo confirmar la identidad del proceso despues del HTTP: $($_.Exception.Message)"
                return $false
            }
            Start-Sleep -Seconds 1
        }
    }

    Write-Warn "El API no respondió dentro del tiempo esperado"

    return $false
}


# =============================================================================
# START BACKEND
# =============================================================================

function Start-Backend {

    Write-Step "Iniciando ERP API"


    # powershell.exe (Windows PowerShell clásico) en vez de pwsh: en este equipo pwsh solo
    # está instalado vía Microsoft Store, que corre en un AppContainer con PATH aislado del
    # resto del sistema — las ventanas abiertas con Start-Process pwsh no ven dotnet/npm
    # aunque la sesión que las lanza sí los vea. powershell.exe no tiene ese problema.
    $script:ApiLaunch = $null
    if (@(Get-NetTCPConnection -LocalPort $Config.ApiPort -State Listen -ErrorAction SilentlyContinue).Count -gt 0) {
        throw "Puerto $($Config.ApiPort) ocupado. No se inicio otro proceso API."
    }
    $backendLiteral = $BackendPath.Replace("'", "''")
    $projectLiteral = $ApiProject.Replace("'", "''")
    try {
        # Exit with dotnet: keeping an idle shell alive would conceal premature API termination.
        $process = Start-Process powershell -PassThru -ErrorAction Stop -ArgumentList @(
            "-NoProfile", "-Command",
            "`$ErrorActionPreference = 'Stop'; try { Set-Location -LiteralPath '$backendLiteral'; `$env:ASPNETCORE_ENVIRONMENT = 'Development'; `$env:DOTNET_ENVIRONMENT = 'Development'; & dotnet run --project '$projectLiteral'; exit `$LASTEXITCODE } catch { Write-Error `$_ -ErrorAction Continue; exit 1 }"
        )
        $null = $process.Handle
        $script:ApiLaunch = [pscustomobject]@{ Process = $process; StartTimeUtc = $process.StartTime.ToUniversalTime() }
    }
    catch {
        throw "No se pudo iniciar el proceso API: $($_.Exception.Message)"
    }
    Write-Ok "Proceso de API lanzado (PID=$($process.Id)); esperando disponibilidad."
}


# =============================================================================
# START FRONTEND
# =============================================================================

function Start-Frontend {

    Write-Step "Iniciando Frontend React"

    if (@(Get-NetTCPConnection -State Listen -ErrorAction Stop |
        Where-Object LocalPort -eq $Config.FrontendPort).Count -gt 0) {
        throw "Puerto $($Config.FrontendPort) ocupado. No se inicio otro frontend."
    }
    if ($null -ne $script:FrontendLaunch) {
        $script:FrontendLaunch.Process.Refresh()
        if (-not $script:FrontendLaunch.Process.HasExited) {
            throw "El frontend anterior aun esta activo. No se inicio otro."
        }
        $script:FrontendLaunch.Process.Dispose()
        $script:FrontendLaunch = $null
    }
    $nodePath = [IO.Path]::GetFullPath((Get-Command node.exe -ErrorAction Stop).Source)
    $frontendLiteral = $FrontendPath.Replace("'", "''")
    # Ver comentario en Start-Backend: powershell.exe evita el sandbox de pwsh (Store).
    $process = Start-Process powershell -PassThru -ErrorAction Stop -ArgumentList @(
        '-NoProfile', '-Command',
        "`$ErrorActionPreference = 'Stop'; try { Set-Location -LiteralPath '$frontendLiteral'; & npm.cmd run dev -- --strictPort; exit `$LASTEXITCODE } catch { Write-Error `$_ -ErrorAction Continue; exit 1 }"
    )
    $null = $process.Handle
    $script:FrontendLaunch = [pscustomobject]@{
        Process = $process; StartTimeUtc = $process.StartTime.ToUniversalTime(); NodePath = $nodePath
    }

    Write-Ok "Proceso Frontend iniciado"
}

function Assert-FrontendListeners {
    param([Parameter(Mandatory)]$Launch)
    $Launch.Process.Refresh()
    if ($Launch.Process.HasExited) { throw 'FAIL frontend: proceso lanzado terminado.' }
    $listeners = @(Get-NetTCPConnection -State Listen -ErrorAction Stop |
        Where-Object LocalPort -eq $Config.FrontendPort)
    foreach ($listener in $listeners) {
        $process = Get-Process -Id $listener.OwningProcess -ErrorAction Stop
        try {
            $null = $process.Handle
            if (-not (Test-FrontendListenerOwnership $Launch $process)) {
                throw 'FAIL frontend: listener sin propiedad verificable.'
            }
        }
        finally { $process.Dispose() }
    }
    return ($listeners.Count -gt 0)
}

function Wait-FrontendReady {
    param([Parameter(Mandatory)]$Launch)
    Write-Step 'Esperando disponibilidad del Frontend'
    for ($i = 0; $i -lt $Config.FrontendTimeoutSeconds; $i++) {
        if (Assert-FrontendListeners $Launch) {
            $response = $null
            try {
                $response = Invoke-WebRequest -Uri "$($Urls.Frontend)/@vite/client" -UseBasicParsing -TimeoutSec 2 -ErrorAction Stop
            }
            catch { } # HTTP can be unavailable while Vite starts; ownership errors are never retried.
            $owned = Assert-FrontendListeners $Launch
            if ($owned -and $null -ne $response -and $response.StatusCode -eq 200 -and
                $response.Content -match 'vite') {
                Write-Ok 'Frontend disponible: proceso, propiedad y HTTP verificados.'
                return
            }
        }
        Start-Sleep -Seconds 1
    }
    throw 'FAIL frontend: timeout esperando Vite HTTP en el puerto configurado.'
}


# =============================================================================
# CHROME LAUNCHER
# =============================================================================

function Get-ChromePath {

    $paths = @(
        "$env:ProgramFiles\Google\Chrome\Application\chrome.exe",
        "${env:ProgramFiles(x86)}\Google\Chrome\Application\chrome.exe"
    )

    foreach ($path in $paths) {

        if (Test-Path $path) {
            return $path
        }
    }

    return $null
}


function Open-Chrome {

    param(
        [Parameter(Mandatory)]
        [string]$Url
    )

    $chrome = Get-ChromePath

    if (-not $chrome) {

        Write-Warn "Google Chrome no encontrado"
        return
    }

    Start-Process `
        -FilePath $chrome `
        -ArgumentList $Url

    Write-Ok "Abierto en Chrome: $Url"
}


# =============================================================================
# TESTS
# =============================================================================

function Invoke-TestSuite {

    Write-Title "ERP - SUITE DE TESTS COMPLETA"

    $projects = [ordered]@{
        "Domain"         = "src/ERP.Domain.Tests/ERP.Domain.Tests.csproj"
        "Application"    = "src/ERP.Application.Tests/ERP.Application.Tests.csproj"
        "Infrastructure" = "src/ERP.Infrastructure.Tests/ERP.Infrastructure.Tests.csproj"
        "API"            = "src/ERP.API.Tests/ERP.API.Tests.csproj"
        "Architecture"   = "src/ERP.Architecture.Tests/ERP.Architecture.Tests.csproj"
    }

    $results = [ordered]@{}

    Push-Location $BackendPath

    try {

        foreach ($name in $projects.Keys) {

            $csproj = $projects[$name]

            if (-not (Test-Path $csproj)) {
                Write-Warn "No existe $csproj — se omite $name"
                $results[$name] = "SKIP"
                continue
            }

            Write-Step "Ejecutando tests: $name"

            dotnet test $csproj --nologo

            $results[$name] = if ($LASTEXITCODE -eq 0) { "PASS" } else { "FAIL" }
        }
    }
    finally {
        Pop-Location
    }

    Write-Title "RESUMEN DE TESTS"

    foreach ($name in $results.Keys) {

        $status = $results[$name]
        $line   = "{0,-16} {1}" -f $name, $status

        switch ($status) {
            "PASS" { Write-Ok   $line }
            "FAIL" { Write-Err  $line }
            default { Write-Warn $line }
        }
    }
}


# =============================================================================
# FLUJOS PRINCIPALES
# =============================================================================

function Start-ErpDev {

    Write-Title "ERP - INICIAR DESARROLLO"

    Stop-DevelopmentServices
    Invoke-Clean
    Invoke-DotnetRestore
    Ensure-FrontendDependencies
    Invoke-DatabaseUpdate
    Invoke-BackendBuild
    Start-Backend

    if (Wait-ApiReady -Launch $script:ApiLaunch) {

        $initialized = Confirm-SystemInitialized

        Start-Frontend
        Wait-FrontendReady -Launch $script:FrontendLaunch

        if ($initialized) { Open-Chrome $Urls.Frontend }
        else { Open-Chrome "$($Urls.Frontend)/setup" }
        Open-Chrome $Urls.Swagger
    }
    else { Write-Warn "ERP no iniciado: API no disponible o proceso rechazado."; return }

    Write-Ok "ERP iniciado correctamente."
}


function Invoke-FullReset {

    Write-Title "RESET COMPLETO DEL SISTEMA"

    Write-Warn "Esta operación ELIMINA la base de datos actual y todos sus datos."
    Write-Warn "Se recreará desde las migraciones versionadas y con alta manual del administrador por el propietario."

    $confirm = Read-Host "Escriba RESETEAR para confirmar"

    if ($confirm -ne "RESETEAR") {
        Write-Warn "Operación cancelada."
        return
    }

    # EF resuelve, muestra y confirma el destino efectivo antes del drop.
    Invoke-DatabaseDrop
    Stop-DevelopmentServices
    Invoke-Clean
    Invoke-DotnetRestore
    Invoke-DatabaseUpdate
    Invoke-BackendBuild
    Start-Backend
    if (Wait-ApiReady -Launch $script:ApiLaunch) {

        $initialized = Confirm-SystemInitialized

        Ensure-FrontendDependencies
        Start-Frontend
        Wait-FrontendReady -Launch $script:FrontendLaunch

        if ($initialized) { Open-Chrome $Urls.Frontend }
        else { Open-Chrome "$($Urls.Frontend)/setup" }
        Open-Chrome $Urls.Swagger
    }
    else { Write-Warn "Reset sin arranque confirmado de API."; return }

    Write-Ok "Reset completo finalizado."
}


function Start-BackendOnly {

    Write-Title "ERP - SOLO BACKEND API"

    Stop-ApiProcess
    Invoke-BackendBuild
    Start-Backend

    if (Wait-ApiReady -Launch $script:ApiLaunch) {
        Open-Chrome $Urls.Swagger
    }
    else { Write-Warn "Backend API no iniciado: disponibilidad no confirmada."; return }

    Write-Ok "Backend API iniciado correctamente."
}


# =============================================================================
# MENU
# =============================================================================

function Write-MenuRow {

    param(
        [Parameter(Mandatory)]
        [string]$Text,

        [string]$Color = "White"
    )

    $innerWidth = 49

    Write-Host ("║ " + $Text.PadRight($innerWidth) + " ║") -ForegroundColor $Color
}


function Show-Menu {

    $innerWidth = 49
    $title      = "ZH TECHNOLOGIES ERP DEV OS"
    $pad        = $innerWidth + 2 - $title.Length
    $padLeft    = [math]::Floor($pad / 2)
    $padRight   = $pad - $padLeft

    Write-Host ""
    Write-Host ("╔" + ("═" * ($innerWidth + 2)) + "╗") -ForegroundColor Cyan
    Write-Host ("║" + (" " * $padLeft) + $title + (" " * $padRight) + "║") -ForegroundColor Cyan
    Write-Host ("╠" + ("═" * ($innerWidth + 2)) + "╣") -ForegroundColor Cyan

    Write-MenuRow "[1] Iniciar ERP (uso diario)"                  "Green"
    Write-MenuRow "[2] Reset completo del sistema  (DESTRUCTIVO)" "Red"
    Write-MenuRow "[3] Iniciar solamente Backend API"             "Cyan"
    Write-MenuRow "[4] Ejecutar Tests completos"                  "Magenta"

    Write-Host ("╟" + ("─" * ($innerWidth + 2)) + "╢") -ForegroundColor DarkGray

    Write-MenuRow "[5] Salir" "DarkGray"

    Write-Host ("╚" + ("═" * ($innerWidth + 2)) + "╝") -ForegroundColor Cyan
    Write-Host ""

    return Read-Host "Seleccione una opción"
}


# =============================================================================
# MAIN LOOP
# =============================================================================

function Start-DevLauncher {

    Assert-Prerequisites

    Initialize-Paths

    Show-Environment


    $running = $true


    while ($running) {

        $option = Show-Menu


        switch ($option) {

            "1" {

                try {
                    Start-ErpDev
                }
                catch {
                    Write-Err $_.Exception.Message
                }
            }

            "2" {

                try {
                    Invoke-FullReset
                }
                catch {
                    Write-Err $_.Exception.Message
                }
            }

            "3" {

                try {
                    Start-BackendOnly
                }
                catch {
                    Write-Err $_.Exception.Message
                }
            }

            "4" {

                try {
                    Invoke-TestSuite
                }
                catch {
                    Write-Err $_.Exception.Message
                }
            }

            "5" {

                Write-Ok "Cerrando ERP DEV OS"

                $running = $false
            }

            default {

                Write-Warn "Opción no válida"
            }
        }
    }


    Write-Ok "Hasta luego"
}


# =============================================================================
# ENTRY POINT
# =============================================================================

Start-DevLauncher
