# =====================================================
# ZH TECHNOLOGIES ERP - BACKUP / RESTORE CHECK
# =====================================================
# -Action Backup (sin destruir nada):
#   1. PostgreSQL (pg_dump formato custom, via docker exec en el contenedor real).
#   2. FileStorage (certificados P12, XML SRI, RIDE, keyring) desde el volumen del stack app.
#   3. manifest.json + SHA256SUMS.txt en backups/<Database>/<yyyyMMdd-HHmmss>/.
#   NO respalda .env.app (secretos). NO restaura. NO borra. NO imprime secretos.
#
# -Action RestoreCheck (drill descartable y aislado):
#   Verifica checksums y que el backup sea de -Database; restaura Postgres y FileStorage en un
#   contenedor/volumen/red NUEVOS y temporales. Nunca toca el contenedor, la base ni el volumen reales.
#
# -Database es obligatorio: nunca se asume una base por defecto.
#
# Uso (desde cualquier ubicacion):
#   .\scripts\backup-restore.ps1 -Action Backup -Database erp_zh_prod
#   .\scripts\backup-restore.ps1 -Action RestoreCheck -Database erp_zh_prod
#   .\scripts\backup-restore.ps1 -Action RestoreCheck -Database erp_zh_prod -BackupDir backups\erp_zh_prod\20261017-120000 -KeepTemp
#
# Procedimiento completo (incluye restore real): docs/operations/BACKUP_RESTORE.md

param(
    [Parameter(Mandatory)]
    [ValidateSet("Backup", "RestoreCheck")]
    [string]$Action,
    [Parameter(Mandatory)]
    [ValidatePattern('^[a-z0-9_]+$')]
    [string]$Database,
    [string]$PostgresContainer = "postgreszh",
    # Volumen del proyecto Compose del stack app (nombre conservado: erp-saas-localprod).
    [string]$FileStorageVolume = "erp-saas-localprod_erp-api-files",
    [string]$PgUser = "postgres",
    [string]$BackupDir,
    [string]$TempContainerName = "erp-restore-check-postgres",
    [string]$TempVolumeName = "erp-restore-check-files",
    [string]$TempNetworkName = "erp-restore-check-net",
    [switch]$KeepTemp
)

$ErrorActionPreference = "Stop"

$RepoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $RepoRoot

$BackupRoot = Join-Path $RepoRoot "backups\$Database"
$TempDatabase = "${Database}_restore_drill"

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

function Write-Banner {
    param([Parameter(Mandatory)][string]$Text)

    Write-Host ""
    Write-Host "============================================" -ForegroundColor Cyan
    Write-Host "   $Text" -ForegroundColor Cyan
    Write-Host "============================================" -ForegroundColor Cyan
    Write-Host ""
}

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

function Assert-DatabaseExists {
    $exists = (& docker exec $PostgresContainer psql -U $PgUser -d postgres -At -c "SELECT 1 FROM pg_database WHERE datname = '$Database';").Trim()
    if ($LASTEXITCODE -ne 0 -or $exists -ne "1") {
        throw "La base '$Database' no existe en '$PostgresContainer'."
    }
}

function Invoke-Backup {
    Write-Banner "ZH ERP - BACKUP ($Database)"

    Assert-ContainerRunning -Name $PostgresContainer
    Assert-DatabaseExists
    Assert-VolumeExists -Name $FileStorageVolume
    Write-Host "[OK] Contenedor '$PostgresContainer', base '$Database' y volumen '$FileStorageVolume' verificados" -ForegroundColor Green

    $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $targetDir = Join-Path $BackupRoot $timestamp
    New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
    Write-Host "[OK] Carpeta de backup: backups\$Database\$timestamp" -ForegroundColor Green

    # --- 1. PostgreSQL -------------------------------------------------------
    Write-Host ""
    Write-Host "[1/3] Respaldando PostgreSQL ($Database)..." -ForegroundColor Yellow

    $remoteDumpPath = "/tmp/$Database-backup-$timestamp.dump"
    $postgresDumpFile = Join-Path $targetDir "postgres.dump"

    & docker exec $PostgresContainer pg_dump -Fc -U $PgUser -d $Database -f $remoteDumpPath
    if ($LASTEXITCODE -ne 0) {
        throw "pg_dump fallo (codigo $LASTEXITCODE)."
    }
    & docker cp "${PostgresContainer}:${remoteDumpPath}" $postgresDumpFile
    if ($LASTEXITCODE -ne 0) {
        throw "docker cp del dump fallo (codigo $LASTEXITCODE)."
    }
    & docker exec $PostgresContainer rm -f $remoteDumpPath
    if ($LASTEXITCODE -ne 0) {
        Write-Host "[WARN] No se pudo limpiar el dump temporal dentro del contenedor ($remoteDumpPath)" -ForegroundColor Yellow
    }
    if (!(Test-Path -LiteralPath $postgresDumpFile) -or (Get-Item $postgresDumpFile).Length -eq 0) {
        throw "postgres.dump no se genero correctamente o esta vacio."
    }
    Write-Host "[OK] postgres.dump generado" -ForegroundColor Green

    # --- 2. FileStorage --------------------------------------------------------
    Write-Host ""
    Write-Host "[2/3] Respaldando FileStorage (certificados, XML, RIDE)..." -ForegroundColor Yellow

    $fileStorageArchive = Join-Path $targetDir "filestorage.tar.gz"
    & docker run --rm `
        -v "${FileStorageVolume}:/data:ro" `
        -v "${targetDir}:/backup" `
        alpine sh -c "tar czf /backup/filestorage.tar.gz -C /data ."
    if ($LASTEXITCODE -ne 0) {
        throw "Exportacion de FileStorage fallo (codigo $LASTEXITCODE)."
    }
    if (!(Test-Path -LiteralPath $fileStorageArchive) -or (Get-Item $fileStorageArchive).Length -eq 0) {
        throw "filestorage.tar.gz no se genero correctamente o esta vacio."
    }
    Write-Host "[OK] filestorage.tar.gz generado" -ForegroundColor Green

    # --- 3. Checksums + manifest -----------------------------------------------
    Write-Host ""
    Write-Host "[3/3] Generando checksums y manifest..." -ForegroundColor Yellow

    $postgresHash = (Get-FileHash -LiteralPath $postgresDumpFile -Algorithm SHA256).Hash
    $fileStorageHash = (Get-FileHash -LiteralPath $fileStorageArchive -Algorithm SHA256).Hash
    $postgresSize = (Get-Item $postgresDumpFile).Length
    $fileStorageSize = (Get-Item $fileStorageArchive).Length

    $manifest = [ordered]@{
        createdAtUtc      = (Get-Date).ToUniversalTime().ToString("o")
        postgresContainer = $PostgresContainer
        database          = $Database
        fileStorageVolume = $FileStorageVolume
        files             = [ordered]@{
            "postgres.dump"      = [ordered]@{ sizeBytes = $postgresSize; sha256 = $postgresHash }
            "filestorage.tar.gz" = [ordered]@{ sizeBytes = $fileStorageSize; sha256 = $fileStorageHash }
        }
        notes             = @(
            "No incluye .env.app (secretos) — ver docs/operations/BACKUP_RESTORE.md",
            "postgres.dump generado con pg_dump -Fc (formato custom, restaurar con pg_restore)",
            "filestorage.tar.gz contiene certificates/, electronic-documents/, ride/ desde el volumen $FileStorageVolume"
        )
    }
    $manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $targetDir "manifest.json") -Encoding utf8

    # LF-only (no el CRLF de Set-Content) para que 'sha256sum -c' funcione tal cual desde Linux/WSL/Git Bash.
    $sha256Content = "$postgresHash  postgres.dump`n$fileStorageHash  filestorage.tar.gz`n"
    [System.IO.File]::WriteAllText((Join-Path $targetDir "SHA256SUMS.txt"), $sha256Content, [System.Text.Encoding]::ASCII)
    Write-Host "[OK] manifest.json y SHA256SUMS.txt generados" -ForegroundColor Green

    Write-Banner "BACKUP COMPLETADO"
    Write-Host "Carpeta:  backups\$Database\$timestamp"
    Write-Host ("  postgres.dump        {0,10:N0} bytes  sha256:{1}" -f $postgresSize, $postgresHash.Substring(0, 12))
    Write-Host ("  filestorage.tar.gz   {0,10:N0} bytes  sha256:{1}" -f $fileStorageSize, $fileStorageHash.Substring(0, 12))
    Write-Host ""
    Write-Host "Recuerda: .env.app NO esta incluido en este backup." -ForegroundColor Yellow
    Write-Host "Guardalo por separado en un gestor de secretos seguro (ver docs/operations/BACKUP_RESTORE.md)." -ForegroundColor Yellow
    Write-Host ""
}

function Resolve-LatestBackupDir {
    if (!(Test-Path -LiteralPath $BackupRoot)) {
        throw "No existe $BackupRoot. Ejecuta primero: .\scripts\backup-restore.ps1 -Action Backup -Database $Database"
    }
    $latest = Get-ChildItem -LiteralPath $BackupRoot -Directory | Sort-Object Name -Descending | Select-Object -First 1
    if (!$latest) {
        throw "No hay backups en $BackupRoot."
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

function Invoke-RestoreCheck {
    Write-Banner "ZH ERP - RESTORE CHECK ($Database, DRILL DESCARTABLE)"

    $sourceDir = if ($BackupDir) { $BackupDir } else { Resolve-LatestBackupDir }
    if (!(Test-Path -LiteralPath $sourceDir)) {
        throw "No existe la carpeta de backup: $sourceDir"
    }
    $sourceDir = (Resolve-Path -LiteralPath $sourceDir).Path

    $postgresDumpFile = Join-Path $sourceDir "postgres.dump"
    $fileStorageArchive = Join-Path $sourceDir "filestorage.tar.gz"
    $manifestFile = Join-Path $sourceDir "manifest.json"
    $sha256File = Join-Path $sourceDir "SHA256SUMS.txt"

    foreach ($f in @($postgresDumpFile, $fileStorageArchive, $manifestFile, $sha256File)) {
        if (!(Test-Path -LiteralPath $f) -or (Get-Item $f).Length -eq 0) {
            throw "Archivo de backup faltante o vacio: $f"
        }
    }
    Write-Host "[OK] Backup a usar: $sourceDir" -ForegroundColor Green

    # --- Checksums y base del manifest -------------------------------------------
    $manifest = Get-Content -LiteralPath $manifestFile -Raw | ConvertFrom-Json
    if ($manifest.database -ne $Database) {
        throw "El backup es de la base '$($manifest.database)', no de '$Database'."
    }
    if ((Get-FileHash -LiteralPath $postgresDumpFile -Algorithm SHA256).Hash -ne $manifest.files.'postgres.dump'.sha256) {
        throw "Checksum de postgres.dump no coincide con manifest.json. Backup posiblemente corrupto."
    }
    if ((Get-FileHash -LiteralPath $fileStorageArchive -Algorithm SHA256).Hash -ne $manifest.files.'filestorage.tar.gz'.sha256) {
        throw "Checksum de filestorage.tar.gz no coincide con manifest.json. Backup posiblemente corrupto."
    }
    Write-Host "[OK] Checksums y base verificados contra manifest.json" -ForegroundColor Green

    # --- Entorno descartable + restore Postgres ------------------------------------
    Write-Host ""
    Write-Host "[1/3] Levantando Postgres temporal '$TempContainerName'..." -ForegroundColor Yellow

    & docker network create $TempNetworkName | Out-Null
    # Password temporal, no real, generado solo para este contenedor descartable — nunca se imprime.
    $tempPassword = [Convert]::ToBase64String((1..24 | ForEach-Object { Get-Random -Maximum 256 }))
    & docker run -d --name $TempContainerName --network $TempNetworkName `
        -e "POSTGRES_PASSWORD=$tempPassword" -e "POSTGRES_DB=postgres" `
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
    & docker cp $postgresDumpFile "${TempContainerName}:/tmp/restore.dump"
    & docker exec $TempContainerName psql -U postgres -c "CREATE DATABASE $TempDatabase;" | Out-Null
    & docker exec $TempContainerName pg_restore -U postgres -d $TempDatabase /tmp/restore.dump
    if ($LASTEXITCODE -ne 0) {
        Remove-TempEnvironment
        throw "pg_restore fallo (codigo $LASTEXITCODE)."
    }
    Write-Host "[OK] Restore de Postgres completado" -ForegroundColor Green

    Write-Host ""
    Write-Host "Conteos (base temporal, no afecta $Database):" -ForegroundColor Cyan
    $unionSql = ($CheckTables | ForEach-Object { "SELECT '$_' AS tabla, count(*) FROM $_" }) -join "`nUNION ALL`n"
    & docker exec $TempContainerName psql -U postgres -d $TempDatabase -c "$unionSql;"

    # --- FileStorage temporal ------------------------------------------------------
    Write-Host ""
    Write-Host "[3/3] Restaurando FileStorage en volumen temporal '$TempVolumeName'..." -ForegroundColor Yellow
    & docker volume create $TempVolumeName | Out-Null
    & docker run --rm -v "${TempVolumeName}:/data" -v "${sourceDir}:/backup" `
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

    Write-Banner "RESTORE CHECK COMPLETADO"
    Write-Host "El contenedor '$PostgresContainer', la base '$Database' y el volumen '$FileStorageVolume' no fueron tocados." -ForegroundColor Yellow
    Write-Host ""
}

switch ($Action) {
    "Backup" { Invoke-Backup }
    "RestoreCheck" { Invoke-RestoreCheck }
}
