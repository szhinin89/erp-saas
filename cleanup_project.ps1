# ZH Technologies - Limpieza del proyecto
# Ejecutar desde: C:\ProyectCursor\erp-saas

Write-Host "=== Limpieza del proyecto ===" -ForegroundColor Cyan

# PASO 1: Crear carpeta de scripts
Write-Host "[1/3] Moviendo scripts..." -ForegroundColor Yellow
New-Item -ItemType Directory -Path "backend\scripts" -Force | Out-Null

$scripts = @("erp_setup.ps1", "erp_application.ps1", "erp_infrastructure.ps1", "erp_api.ps1")
foreach ($s in $scripts) {
    $origen = "backend\src\$s"
    $destino = "backend\scripts\$s"
    if (Test-Path $origen) {
        Move-Item $origen $destino -Force
        Write-Host "      Movido: $s" -ForegroundColor Green
    }
}
if (Test-Path "erp_setup.ps1") {
    Move-Item "erp_setup.ps1" "backend\scripts\erp_setup.ps1" -Force
    Write-Host "      Movido: erp_setup.ps1 (raiz)" -ForegroundColor Green
}
Write-Host "      OK" -ForegroundColor Green

# PASO 2: .gitignore para .NET
Write-Host "[2/3] Creando .gitignore..." -ForegroundColor Yellow

Set-Content ".gitignore" @'
# Build
**/bin/
**/obj/
**/out/

# Visual Studio
.vs/
*.suo
*.user
*.userosscache
*.sln.docstates
*.slnx.user

# Rider
.idea/

# NuGet
**/packages/
*.nupkg
!**/[Pp]ackages/build/
.nuget/

# Dotnet tools
.config/dotnet-tools.json

# EF Core Migrations (mantener los archivos, ignorar snapshots temporales)
# **/Migrations/

# Logs
*.log
logs/

# Env / Secrets
appsettings.Development.json
appsettings.Local.json
*.env
.env
secrets.json

# OS
.DS_Store
Thumbs.db
desktop.ini

# Scripts de setup (ya no se necesitan en el repo)
backend/scripts/

# Node (para cuando agreguemos el frontend)
node_modules/
.next/
.nuxt/
dist/
.env.local
.env.production

# Docker
.dockerignore
docker-compose.override.yml

# Test results
TestResults/
coverage/
*.trx
*.coveragexml
'@
Write-Host "      OK" -ForegroundColor Green

# PASO 3: Verificar estructura final
Write-Host "[3/3] Estructura final del proyecto..." -ForegroundColor Yellow
Write-Host ""
Write-Host "erp-saas/" -ForegroundColor White
Write-Host "  backend/" -ForegroundColor White
Write-Host "    scripts/     <- scripts de setup" -ForegroundColor Gray
Write-Host "    src/" -ForegroundColor White
Write-Host "      ERP.API/" -ForegroundColor Green
Write-Host "      ERP.Application/" -ForegroundColor Green
Write-Host "      ERP.Domain/" -ForegroundColor Green
Write-Host "      ERP.Infrastructure/" -ForegroundColor Green
Write-Host "      ERP.slnx" -ForegroundColor Green
Write-Host "  frontend/      <- Next.js (pendiente)" -ForegroundColor Gray
Write-Host "  .gitignore     <- generado" -ForegroundColor Green
Write-Host ""
Write-Host "================================================" -ForegroundColor Cyan
Write-Host " Proyecto limpio" -ForegroundColor Green
Write-Host " Siguiente: Autenticacion JWT" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
