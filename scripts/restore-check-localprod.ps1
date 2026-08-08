# =====================================================
# ZH TECHNOLOGIES ERP - RESTORE CHECK (DRILL DESCARTABLE)
# =====================================================
# Restaura un backup de scripts/backup-localprod.ps1 en un contenedor Postgres y un volumen
# FileStorage TEMPORALES Y DESCARTABLES, para validar que el backup realmente sirve para
# recuperar el sistema — sin tocar en ningun momento el stack localprod real:
#   - NO toca postgreszh (usa un contenedor postgres:16 nuevo, con otro nombre).
#   - NO toca dberpsaas (crea una base nueva SOLO dentro del contenedor temporal).
#   - NO toca el volumen erp-api-files (crea un volumen nuevo, con otro nombre).
#   - NO toca erp-api-localprod / erp-frontend-localprod (no los detiene ni reinicia).
#   - Limpia el contenedor/volumen/red temporales al finalizar (salvo -KeepTemp).
#
# No imprime secretos, contenido de certificados ni XML completo — solo rutas, tablas y conteos.
#
# Uso (desde la raiz del repo):
#   .\scripts\restore-check-localprod.ps1
#   .\scripts\restore-check-localprod.ps1 -BackupDir backups\localprod\20260808-141039
#   .\scripts\restore-check-localprod.ps1 -KeepTemp   # deja el entorno temporal vivo para inspeccion manual
#
# Ver docs/BACKUP_RESTORE_LOCALPROD.md para el procedimiento completo.

param(
    [string]$BackupDir,
    [string]$TempContainerName = "erp-restore-check-postgres",
    [string]$TempVolumeName = "erp-restore-check-files",
    [string]$TempNetworkName = "erp-restore-check-net",
    [string]$TempDatabase = "dberpsaas_restore_drill",
    [switch]$KeepTemp
)

$ErrorActionPreference = "Stop"

$RepoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $RepoRoot

# Tablas minimas a contar tras el restore — ajustar si el modelo de datos cambia.
$CheckTables = @(
    "tenants",
    "company",
    "identity_users",
    "sales_invoices",
    "electronic_documents",
    "document_sequence",
    "sri_settings"
)

function Resolve-LatestBackupDir {
    $root = Join-Path $RepoRoot "backups\localprod"
    if (!(Test-Path -LiteralPath $root)) {
        throw "No existe $root. Ejecuta scripts\backup-localprod.ps1 primero."
    }
    $latest = Get-ChildItem -LiteralPath $root -Directory | Sort-Object Name -Descending | Select-Object -First 1
    if (!$latest) {
        throw "No hay backups en $root."
    }
    return $latest.FullName
}

function Remove-TempEnvironment {
    Write-Host ""
    Write-Host "[CLEANUP] Eliminando entorno temporal..." -ForegroundColor Yellow
    & docker stop $TempContainerName 2>$null | Out-Null
    & docker rm $TempContainerName 2>$null | Out-Null
    & docker volume rm $TempVolumeName 2>$null | Out-Null
    & docker network rm $TempNetworkName 2>$null | Out-Null
    Write-Host "[OK] Contenedor '$TempContainerName', volumen '$TempVolumeName' y red '$TempNetworkName' eliminados" -ForegroundColor Green
}

Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "   ZH ERP - RESTORE CHECK (DRILL DESCARTABLE)" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

if (!$BackupDir) {
    $BackupDir = Resolve-LatestBackupDir
}
elseif (!(Test-Path -LiteralPath $BackupDir)) {
    throw "No existe la carpeta de backup: $BackupDir"
}
$BackupDir = (Resolve-Path -LiteralPath $BackupDir).Path

$PostgresDumpFile = Join-Path $BackupDir "postgres.dump"
$FileStorageArchive = Join-Path $BackupDir "filestorage.tar.gz"
$ManifestFile = Join-Path $BackupDir "manifest.json"
$Sha256File = Join-Path $BackupDir "SHA256SUMS.txt"

foreach ($f in @($PostgresDumpFile, $FileStorageArchive, $ManifestFile, $Sha256File)) {
    if (!(Test-Path -LiteralPath $f) -or (Get-Item $f).Length -eq 0) {
        throw "Archivo de backup faltante o vacio: $f"
    }
}
Write-Host "[OK] Backup a usar: $BackupDir" -ForegroundColor Green

# --- Tarea 1: checksums ------------------------------------------------------
$Manifest = Get-Content -LiteralPath $ManifestFile -Raw | ConvertFrom-Json
$ActualPostgresHash = (Get-FileHash -LiteralPath $PostgresDumpFile -Algorithm SHA256).Hash
$ActualFileStorageHash = (Get-FileHash -LiteralPath $FileStorageArchive -Algorithm SHA256).Hash

if ($ActualPostgresHash -ne $Manifest.files.'postgres.dump'.sha256) {
    throw "Checksum de postgres.dump no coincide con manifest.json. Backup posiblemente corrupto."
}
if ($ActualFileStorageHash -ne $Manifest.files.'filestorage.tar.gz'.sha256) {
    throw "Checksum de filestorage.tar.gz no coincide con manifest.json. Backup posiblemente corrupto."
}
Write-Host "[OK] Checksums verificados contra manifest.json" -ForegroundColor Green

# --- Tarea 2/3: entorno descartable + restore Postgres -----------------------
Write-Host ""
Write-Host "[1/3] Levantando Postgres temporal '$TempContainerName'..." -ForegroundColor Yellow

& docker network create $TempNetworkName | Out-Null
# Password temporal, no real, generado solo para este contenedor descartable — nunca se imprime.
$TempPassword = [Convert]::ToBase64String((1..24 | ForEach-Object { Get-Random -Maximum 256 }))
& docker run -d --name $TempContainerName --network $TempNetworkName `
    -e "POSTGRES_PASSWORD=$TempPassword" -e "POSTGRES_DB=postgres" `
    postgres:16 | Out-Null

$ready = $false
for ($i = 0; $i -lt 30; $i++) {
    & docker exec $TempContainerName pg_isready -U postgres *> $null
    if ($LASTEXITCODE -eq 0) { $ready = $true; break }
    Start-Sleep -Seconds 1
}
if (!$ready) {
    Remove-TempEnvironment
    throw "Postgres temporal no quedo listo a tiempo."
}
Write-Host "[OK] Postgres temporal listo" -ForegroundColor Green

Write-Host ""
Write-Host "[2/3] Restaurando postgres.dump en base temporal '$TempDatabase'..." -ForegroundColor Yellow
& docker cp $PostgresDumpFile "${TempContainerName}:/tmp/restore.dump"
& docker exec $TempContainerName psql -U postgres -c "CREATE DATABASE $TempDatabase;" | Out-Null
& docker exec $TempContainerName pg_restore -U postgres -d $TempDatabase /tmp/restore.dump
if ($LASTEXITCODE -ne 0) {
    Remove-TempEnvironment
    throw "pg_restore fallo (codigo $LASTEXITCODE)."
}
Write-Host "[OK] Restore de Postgres completado" -ForegroundColor Green

Write-Host ""
Write-Host "Conteos (base temporal, no afecta dberpsaas):" -ForegroundColor Cyan
$unionSql = ($CheckTables | ForEach-Object { "SELECT '$_' AS tabla, count(*) FROM $_" }) -join "`nUNION ALL`n"
& docker exec $TempContainerName psql -U postgres -d $TempDatabase -c "$unionSql;"

# --- Tarea 3: FileStorage temporal --------------------------------------------
Write-Host ""
Write-Host "[3/3] Restaurando FileStorage en volumen temporal '$TempVolumeName'..." -ForegroundColor Yellow
& docker volume create $TempVolumeName | Out-Null
& docker run --rm -v "${TempVolumeName}:/data" -v "${BackupDir}:/backup" `
    alpine sh -c "tar xzf /backup/filestorage.tar.gz -C /data"
if ($LASTEXITCODE -ne 0) {
    Remove-TempEnvironment
    throw "Extraccion de FileStorage fallo (codigo $LASTEXITCODE)."
}

Write-Host ""
Write-Host "Estructura restaurada (rutas, sin contenido):" -ForegroundColor Cyan
& docker run --rm -v "${TempVolumeName}:/data" alpine find /data -maxdepth 4

$hasCert = (& docker run --rm -v "${TempVolumeName}:/data" alpine sh -c "find /data/certificates -name '*.p12' 2>/dev/null | head -1")
$hasAuthorizedXml = (& docker run --rm -v "${TempVolumeName}:/data" alpine sh -c "find /data/electronic-documents -name 'authorized.xml' 2>/dev/null | head -1")
$hasRide = (& docker run --rm -v "${TempVolumeName}:/data" alpine sh -c "find /data/ride -type f 2>/dev/null | head -1")

Write-Host ""
Write-Host "Presencia de artefactos clave:" -ForegroundColor Cyan
Write-Host "  certificate (.p12):    $(if ($hasCert) { '[OK] presente' } else { '[WARN] no encontrado' })"
Write-Host "  authorized.xml:        $(if ($hasAuthorizedXml) { '[OK] presente' } else { '[WARN] no encontrado' })"
Write-Host "  RIDE (PDF):            $(if ($hasRide) { '[OK] presente' } else { '[WARN] no encontrado' })"

# --- Limpieza ------------------------------------------------------------------
if ($KeepTemp) {
    Write-Host ""
    Write-Host "[SKIP] -KeepTemp activo: entorno temporal NO eliminado." -ForegroundColor Yellow
    Write-Host "  Contenedor: $TempContainerName | Volumen: $TempVolumeName | Red: $TempNetworkName"
    Write-Host "  Limpialo manualmente cuando termines de inspeccionarlo:"
    Write-Host "    docker rm -f $TempContainerName; docker volume rm $TempVolumeName; docker network rm $TempNetworkName"
}
else {
    Remove-TempEnvironment
}

Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "   RESTORE CHECK COMPLETADO" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "El stack localprod real (postgreszh, erp-saas-redis, erp-api-localprod," -ForegroundColor Yellow
Write-Host "erp-frontend-localprod) y sus volumenes no fueron tocados." -ForegroundColor Yellow
Write-Host ""
