# ============================================================
#  ZH Technologies – ERP SaaS
#  erp-dev.ps1  |  Script de automatización de desarrollo
# ============================================================
#  USO:
#    .\erp-dev.ps1 migrate "NombreDeLaMigracion"
#    .\erp-dev.ps1 build
#    .\erp-dev.ps1 reset
#    .\erp-dev.ps1 status
# ============================================================

param(
    [Parameter(Position = 0)]
    [ValidateSet("migrate", "build", "reset", "status", "rollback")]
    [string]$Command,

    [Parameter(Position = 1)]
    [string]$Arg1
)

# ── Configuración ─────────────────────────────────────────────
$ROOT    = "C:\ProyectCursor\erp-saas\backend\src"
$INFRA   = "ERP.Infrastructure"
$API     = "ERP.API"
$SEPARATOR = "─" * 60

function Write-Header([string]$text) {
    Write-Host ""
    Write-Host $SEPARATOR -ForegroundColor Cyan
    Write-Host "  $text" -ForegroundColor Cyan
    Write-Host $SEPARATOR -ForegroundColor Cyan
}

function Write-Ok([string]$text)   { Write-Host "  ✓ $text" -ForegroundColor Green }
function Write-Err([string]$text)  { Write-Host "  ✗ $text" -ForegroundColor Red }
function Write-Info([string]$text) { Write-Host "  → $text" -ForegroundColor Yellow }

# ── Navegar al directorio raíz ────────────────────────────────
Set-Location $ROOT

# ════════════════════════════════════════════════════════════
#  COMANDO: build
#  Compila todas las capas y muestra errores limpios
# ════════════════════════════════════════════════════════════
function Invoke-Build {
    Write-Header "Build – ZH Technologies ERP"
    
    $result = dotnet build --verbosity minimal 2>&1
    $errors = $result | Where-Object { $_ -match "error" }
    $warnings = $result | Where-Object { $_ -match "warning" }

    if ($LASTEXITCODE -eq 0) {
        Write-Ok "Compilación exitosa en todas las capas"
        if ($warnings.Count -gt 0) {
            Write-Info "$($warnings.Count) advertencia(s) encontrada(s)"
        }
    } else {
        Write-Err "Compilación fallida"
        $errors | ForEach-Object { Write-Host "    $_" -ForegroundColor Red }
        exit 1
    }
}

# ════════════════════════════════════════════════════════════
#  COMANDO: migrate <Nombre>
#  Crea y aplica una migración nueva
#  Uso: .\erp-dev.ps1 migrate "AddCustomerEntity"
# ════════════════════════════════════════════════════════════
function Invoke-Migrate([string]$name) {
    if ([string]::IsNullOrWhiteSpace($name)) {
        Write-Err "Debes especificar un nombre para la migración"
        Write-Info "Uso: .\erp-dev.ps1 migrate `"NombreDeLaMigracion`""
        exit 1
    }

    Write-Header "Migración: $name"

    # 1. Build primero
    Write-Info "Verificando compilación..."
    dotnet build --verbosity quiet
    if ($LASTEXITCODE -ne 0) {
        Write-Err "El proyecto no compila. Corrige los errores antes de migrar."
        exit 1
    }
    Write-Ok "Compilación OK"

    # 2. Crear migración
    Write-Info "Creando migración '$name'..."
    dotnet ef migrations add $name --project $INFRA --startup-project $API
    if ($LASTEXITCODE -ne 0) {
        Write-Err "Error al crear la migración"
        exit 1
    }
    Write-Ok "Migración creada"

    # 3. Aplicar migración
    Write-Info "Aplicando migración a la base de datos..."
    dotnet ef database update --project $INFRA --startup-project $API
    if ($LASTEXITCODE -ne 0) {
        Write-Err "Error al aplicar la migración"
        Write-Info "Puedes revertir con: .\erp-dev.ps1 rollback"
        exit 1
    }
    Write-Ok "Migración aplicada correctamente"
    Write-Host ""
    Write-Ok "Listo. Migración '$name' completada."
}

# ════════════════════════════════════════════════════════════
#  COMANDO: rollback
#  Revierte la última migración
# ════════════════════════════════════════════════════════════
function Invoke-Rollback {
    Write-Header "Rollback – última migración"
    
    Write-Info "Revirtiendo última migración en la base de datos..."
    dotnet ef database update --project $INFRA --startup-project $API -- PreviousMigration 2>&1
    
    # Obtener la migración anterior
    $migrations = dotnet ef migrations list --project $INFRA --startup-project $API 2>&1
    $migrationList = $migrations | Where-Object { $_ -notmatch "Build" -and $_ -notmatch "^$" }
    
    if ($migrationList.Count -gt 1) {
        $previous = $migrationList[-2] -replace "\s*\(.*\)", "" -replace "^\s+", ""
        Write-Info "Volviendo a migración: $previous"
        dotnet ef database update $previous --project $INFRA --startup-project $API
    } else {
        Write-Info "Volviendo al estado inicial (sin tablas)..."
        dotnet ef database update 0 --project $INFRA --startup-project $API
    }

    if ($LASTEXITCODE -eq 0) {
        Write-Info "Eliminando archivo de migración..."
        dotnet ef migrations remove --project $INFRA --startup-project $API
        Write-Ok "Rollback completado"
    } else {
        Write-Err "Error durante el rollback"
        exit 1
    }
}

# ════════════════════════════════════════════════════════════
#  COMANDO: status
#  Muestra el estado actual de las migraciones
# ════════════════════════════════════════════════════════════
function Invoke-Status {
    Write-Header "Estado de migraciones"
    dotnet ef migrations list --project $INFRA --startup-project $API
}

# ════════════════════════════════════════════════════════════
#  COMANDO: reset
#  PELIGROSO: Elimina todas las migraciones y recrea la BD
#  Solo usar en desarrollo
# ════════════════════════════════════════════════════════════
function Invoke-Reset {
    Write-Header "⚠ RESET COMPLETO – Solo para desarrollo"
    
    $confirm = Read-Host "  Esto borrará TODAS las migraciones y la base de datos. ¿Continuar? (s/N)"
    if ($confirm -ne "s") {
        Write-Info "Operación cancelada"
        return
    }

    Write-Info "Eliminando migraciones..."
    Remove-Item "$ROOT\$INFRA\Migrations\*" -Recurse -Force -ErrorAction SilentlyContinue
    Write-Ok "Migraciones eliminadas"

    Write-Info "Creando migración inicial..."
    dotnet ef migrations add InitialCreate --project $INFRA --startup-project $API
    if ($LASTEXITCODE -ne 0) {
        Write-Err "Error al crear migración inicial"
        exit 1
    }

    Write-Info "Aplicando a la base de datos..."
    dotnet ef database update --project $INFRA --startup-project $API
    if ($LASTEXITCODE -eq 0) {
        Write-Ok "Reset completado. Base de datos recreada limpiamente."
    } else {
        Write-Err "Error al aplicar migración inicial"
        exit 1
    }
}

# ── Dispatcher ────────────────────────────────────────────────
switch ($Command) {
    "build"    { Invoke-Build }
    "migrate"  { Invoke-Migrate $Arg1 }
    "rollback" { Invoke-Rollback }
    "status"   { Invoke-Status }
    "reset"    { Invoke-Reset }
    default {
        Write-Header "ZH Technologies – ERP Dev Tools"
        Write-Host ""
        Write-Host "  Comandos disponibles:" -ForegroundColor White
        Write-Host ""
        Write-Host "  .\erp-dev.ps1 build                        " -NoNewline; Write-Host "Compilar todas las capas" -ForegroundColor Gray
        Write-Host "  .\erp-dev.ps1 migrate `"NombreMigracion`"    " -NoNewline; Write-Host "Crear y aplicar migración" -ForegroundColor Gray
        Write-Host "  .\erp-dev.ps1 rollback                     " -NoNewline; Write-Host "Revertir última migración" -ForegroundColor Gray
        Write-Host "  .\erp-dev.ps1 status                       " -NoNewline; Write-Host "Ver estado de migraciones" -ForegroundColor Gray
        Write-Host "  .\erp-dev.ps1 reset                        " -NoNewline; Write-Host "⚠ Reset completo (solo dev)" -ForegroundColor Gray
        Write-Host ""
    }
}
