# =====================================================
# ZH TECHNOLOGIES ERP - BACKUP LOCALPROD
# =====================================================
# Respalda, sin destruir nada:
#   1. PostgreSQL (pg_dump formato custom, vía docker exec en el contenedor real).
#   2. FileStorage (certificados P12, XML SRI, RIDE) desde el volumen nombrado erp-api-files.
#
# NO respalda .env.docker.local (secretos) — ver docs/BACKUP_RESTORE_LOCALPROD.md para
# cómo resguardarlo por separado.
# NO hace restore. NO borra nada. NO imprime secretos.
#
# Uso (desde la raíz del repo):
#   .\scripts\backup-localprod.ps1
#
# Ver docs/BACKUP_RESTORE_LOCALPROD.md para el procedimiento completo de restore.

param(
    [string]$PostgresContainer = "postgreszh",
    [string]$FileStorageVolume = "erp-saas-localprod_erp-api-files",
    [string]$Database = "dberpsaas",
    [string]$PgUser = "postgres"
)

$ErrorActionPreference = "Stop"

$RepoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $RepoRoot

function Assert-ContainerRunning {
    param([Parameter(Mandatory)][string]$Name)

    $running = & docker container inspect --format '{{.State.Running}}' $Name 2>$null
    if ($LASTEXITCODE -ne 0 -or $running.Trim() -ne "true") {
        throw "Contenedor '$Name' no esta corriendo. Verifica 'docker ps' antes de continuar."
    }
}

function Assert-VolumeExists {
    param([Parameter(Mandatory)][string]$Name)

    & docker volume inspect $Name 2>$null | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Volumen '$Name' no existe. Verifica 'docker volume ls' antes de continuar."
    }
}

Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "   ZH ERP - BACKUP LOCALPROD" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

Assert-ContainerRunning -Name $PostgresContainer
Assert-VolumeExists -Name $FileStorageVolume
Write-Host "[OK] Contenedor '$PostgresContainer' y volumen '$FileStorageVolume' verificados" -ForegroundColor Green

$Timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$BackupDir = Join-Path $RepoRoot "backups\localprod\$Timestamp"
New-Item -ItemType Directory -Path $BackupDir -Force | Out-Null
Write-Host "[OK] Carpeta de backup: backups\localprod\$Timestamp" -ForegroundColor Green

# --- 1. PostgreSQL ---------------------------------------------------------
Write-Host ""
Write-Host "[1/2] Respaldando PostgreSQL ($Database)..." -ForegroundColor Yellow

$RemoteDumpPath = "/tmp/localprod-backup-$Timestamp.dump"
$PostgresDumpFile = Join-Path $BackupDir "postgres.dump"

& docker exec $PostgresContainer pg_dump -Fc -U $PgUser -d $Database -f $RemoteDumpPath
if ($LASTEXITCODE -ne 0) {
    throw "pg_dump fallo (codigo $LASTEXITCODE)."
}

& docker cp "${PostgresContainer}:${RemoteDumpPath}" $PostgresDumpFile
if ($LASTEXITCODE -ne 0) {
    throw "docker cp del dump fallo (codigo $LASTEXITCODE)."
}

& docker exec $PostgresContainer rm -f $RemoteDumpPath
if ($LASTEXITCODE -ne 0) {
    Write-Host "[WARN] No se pudo limpiar el dump temporal dentro del contenedor ($RemoteDumpPath)" -ForegroundColor Yellow
}

if (!(Test-Path -LiteralPath $PostgresDumpFile) -or (Get-Item $PostgresDumpFile).Length -eq 0) {
    throw "postgres.dump no se genero correctamente o esta vacio."
}
Write-Host "[OK] postgres.dump generado" -ForegroundColor Green

# --- 2. FileStorage ----------------------------------------------------------
Write-Host ""
Write-Host "[2/2] Respaldando FileStorage (certificados, XML, RIDE)..." -ForegroundColor Yellow

$FileStorageArchive = Join-Path $BackupDir "filestorage.tar.gz"

& docker run --rm `
    -v "${FileStorageVolume}:/data:ro" `
    -v "${BackupDir}:/backup" `
    alpine sh -c "tar czf /backup/filestorage.tar.gz -C /data ."
if ($LASTEXITCODE -ne 0) {
    throw "Exportacion de FileStorage fallo (codigo $LASTEXITCODE)."
}

if (!(Test-Path -LiteralPath $FileStorageArchive) -or (Get-Item $FileStorageArchive).Length -eq 0) {
    throw "filestorage.tar.gz no se genero correctamente o esta vacio."
}
Write-Host "[OK] filestorage.tar.gz generado" -ForegroundColor Green

# --- 3. Checksums + manifest -------------------------------------------------
Write-Host ""
Write-Host "[3/3] Generando checksums y manifest..." -ForegroundColor Yellow

$PostgresHash = (Get-FileHash -LiteralPath $PostgresDumpFile -Algorithm SHA256).Hash
$FileStorageHash = (Get-FileHash -LiteralPath $FileStorageArchive -Algorithm SHA256).Hash

$PostgresSize = (Get-Item $PostgresDumpFile).Length
$FileStorageSize = (Get-Item $FileStorageArchive).Length

$Manifest = [ordered]@{
    createdAtUtc      = (Get-Date).ToUniversalTime().ToString("o")
    postgresContainer = $PostgresContainer
    database          = $Database
    fileStorageVolume = $FileStorageVolume
    files             = [ordered]@{
        "postgres.dump"     = [ordered]@{ sizeBytes = $PostgresSize; sha256 = $PostgresHash }
        "filestorage.tar.gz" = [ordered]@{ sizeBytes = $FileStorageSize; sha256 = $FileStorageHash }
    }
    notes = @(
        "No incluye .env.docker.local (secretos) — ver docs/BACKUP_RESTORE_LOCALPROD.md",
        "postgres.dump generado con pg_dump -Fc (formato custom, restaurar con pg_restore)",
        "filestorage.tar.gz contiene certificates/, electronic-documents/, ride/ desde el volumen $FileStorageVolume"
    )
}

$ManifestFile = Join-Path $BackupDir "manifest.json"
$Manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $ManifestFile -Encoding utf8

$Sha256File = Join-Path $BackupDir "SHA256SUMS.txt"
@(
    "$PostgresHash  postgres.dump"
    "$FileStorageHash  filestorage.tar.gz"
) | Set-Content -LiteralPath $Sha256File -Encoding ascii

Write-Host "[OK] manifest.json y SHA256SUMS.txt generados" -ForegroundColor Green

# --- Resumen (sin contenidos, sin secretos) ----------------------------------
Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "   BACKUP COMPLETADO" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Carpeta:  backups\localprod\$Timestamp"
Write-Host ("  postgres.dump        {0,10:N0} bytes  sha256:{1}" -f $PostgresSize, $PostgresHash.Substring(0,12))
Write-Host ("  filestorage.tar.gz   {0,10:N0} bytes  sha256:{1}" -f $FileStorageSize, $FileStorageHash.Substring(0,12))
Write-Host ""
Write-Host "Recuerda: .env.docker.local NO esta incluido en este backup." -ForegroundColor Yellow
Write-Host "Guardalo por separado en un gestor de secretos seguro (ver docs/BACKUP_RESTORE_LOCALPROD.md)." -ForegroundColor Yellow
Write-Host ""
