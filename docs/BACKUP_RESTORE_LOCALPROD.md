# Backup / Restore — Docker localprod

> Nivel 3 (documentación técnica especializada). Complementa
> [`docs/DOCKER_LOCAL_PROD.md`](DOCKER_LOCAL_PROD.md) — no lo reemplaza. Procedimiento seguro para
> respaldar y restaurar el stack `docker-compose.localprod.yml` antes de operaciones riesgosas
> (upgrades, pilotos, migraciones) sin depender de que el volumen Docker nunca falle.

## 1. Qué respalda `scripts/backup-localprod.ps1`

| Componente | Origen | Destino en el backup |
|---|---|---|
| PostgreSQL (`dberpsaas`) | `docker exec postgreszh pg_dump -Fc` | `postgres.dump` (formato custom, restaurar con `pg_restore`) |
| FileStorage (certificados P12, XML SRI, RIDE) | volumen nombrado `erp-saas-localprod_erp-api-files` | `filestorage.tar.gz` |
| Checksums | `Get-FileHash -Algorithm SHA256` sobre ambos archivos | `SHA256SUMS.txt` |
| Metadata | contenedor, DB, volumen, tamaños, hashes — **sin secretos** | `manifest.json` |

Cada ejecución crea una carpeta nueva `backups/localprod/YYYYMMDD-HHMMSS/`, ignorada por Git
(`.gitignore`: `backups/`, `*.dump`, `*.tar.gz`).

El script es de **solo lectura** contra el sistema real: no detiene contenedores, no borra nada, no
hace restore.

## 2. Qué NO respalda

- **`.env.docker.local`** (contiene `POSTGRES_PASSWORD` y `JWT_SECRET_KEY`) — deliberadamente excluido
  para no dejar secretos en texto plano dentro de `backups/`. Ver sección 3.
- Redis (`erp-saas-redis`) — solo cache/sesiones, se reconstruye solo; no contiene datos de negocio
  que no puedan regenerarse.
- Imágenes Docker construidas (`erp-api-localprod`, `erp-frontend-localprod`) — se reconstruyen desde
  el código fuente versionado (`docker compose ... build`).

## 3. Cómo guardar `.env.docker.local` de forma segura

`.env.docker.local` nunca se versiona ni se incluye en `backups/`. Para preservarlo:

- Cópialo a un gestor de secretos (1Password, Bitwarden, Vault) o cífralo por separado
  (`gpg -c .env.docker.local` → guarda el `.gpg` fuera del repo).
- Si necesitas un respaldo de emergencia en disco, guárdalo **fuera** de `backups/localprod/` (que
  puede terminar compartido) y nunca lo imprimas en consola ni lo pegues en un chat/log.
- Alternativa: dado que solo contiene `POSTGRES_PASSWORD` + `JWT_SECRET_KEY` + nombres de host/puerto,
  puedes regenerarlo desde `.env.docker.local.example` siempre que conserves la contraseña real de
  Postgres en tu gestor de secretos (`JWT_SECRET_KEY` puede regenerarse sin costo — solo invalida
  sesiones activas).

## 4. Ejecutar el backup

```powershell
cd C:\ProyectCursor\erp-saas
.\scripts\backup-localprod.ps1
```

Parámetros opcionales (si tus nombres reales difieren de los defaults del proyecto):

```powershell
.\scripts\backup-localprod.ps1 -PostgresContainer postgreszh -FileStorageVolume erp-saas-localprod_erp-api-files -Database dberpsaas -PgUser postgres
```

## 5. Validar integridad sin restaurar (recomendado primero)

No requiere tocar ninguna base de datos ni volumen real:

```powershell
$dir = "backups\localprod\<TIMESTAMP>"

# Postgres: valida que el dump es legible y lista su contenido
docker exec -i postgreszh pg_restore --list < "$dir\postgres.dump"

# FileStorage: lista el archivo sin extraerlo
tar -tzf "$dir\filestorage.tar.gz"

# Checksums
Get-FileHash "$dir\postgres.dump" -Algorithm SHA256
Get-FileHash "$dir\filestorage.tar.gz" -Algorithm SHA256
# Compara contra $dir\SHA256SUMS.txt
```

Confirma que `filestorage.tar.gz` incluye `certificates/`, `electronic-documents/` y `ride/` si el
sistema ya tiene documentos electrónicos generados.

## 6. Restaurar PostgreSQL en una base temporal (opción segura, no destructiva)

**Nunca restaures directamente sobre `dberpsaas`** sin confirmación explícita y un backup previo
verificado. Para validar que el dump realmente restaura:

```powershell
$dir = "backups\localprod\<TIMESTAMP>"

# 1. Copiar el dump al contenedor
docker cp "$dir\postgres.dump" postgreszh:/tmp/restore-check.dump

# 2. Crear una base temporal
docker exec postgreszh psql -U postgres -c "CREATE DATABASE dberpsaas_restore_check;"

# 3. Restaurar el dump en la base temporal
docker exec postgreszh pg_restore -U postgres -d dberpsaas_restore_check /tmp/restore-check.dump

# 4. Validaciones básicas (conteos, no contenido)
docker exec postgreszh psql -U postgres -d dberpsaas_restore_check -c "SELECT count(*) FROM \"Tenants\";"

# 5. Limpieza — SOLO de la base temporal creada en este mismo procedimiento
docker exec postgreszh psql -U postgres -c "DROP DATABASE dberpsaas_restore_check;"
docker exec postgreszh rm -f /tmp/restore-check.dump
```

`dberpsaas_restore_check` es una base nueva creada exclusivamente para esta prueba — eliminarla no
afecta a `dberpsaas` (la base activa) en ningún momento.

## 7. Restaurar FileStorage en un volumen temporal (opción segura)

```powershell
$dir = (Resolve-Path "backups\localprod\<TIMESTAMP>").Path

docker volume create erp-api-files-restore-check

docker run --rm `
  -v erp-api-files-restore-check:/data `
  -v "${dir}:/backup" `
  alpine sh -c "tar xzf /backup/filestorage.tar.gz -C /data"

docker run --rm -v erp-api-files-restore-check:/data alpine find /data -maxdepth 2

# Limpieza del volumen temporal de prueba
docker volume rm erp-api-files-restore-check
```

## 8. Restaurar sobre el sistema real (solo con confirmación explícita)

Restaurar sobre `dberpsaas` o el volumen `erp-saas-localprod_erp-api-files` activos es una operación
destructiva sobre datos reales. Antes de hacerlo:

1. Confirma explícitamente con el responsable del sistema que se va a sobrescribir el estado actual.
2. Genera un backup del estado actual (aunque vayas a descartarlo) por si el restore falla a mitad de
   camino.
3. Detén `erp-api-localprod` (no Postgres) para evitar escrituras concurrentes:
   ```powershell
   docker compose -f docker-compose.localprod.yml --env-file .env.docker.local stop erp-api
   ```
4. Restaura Postgres con `pg_restore --clean --if-exists -U postgres -d dberpsaas` (usa `--clean` solo
   si aceptas que se recreen los objetos existentes).
5. Restaura FileStorage extrayendo `filestorage.tar.gz` directamente sobre el volumen
   `erp-saas-localprod_erp-api-files` (mismo patrón que la sección 7, sin volumen temporal).
6. Levanta de nuevo `erp-api` y valida (sección 9).

**Nunca** ejecutes `docker compose -f docker-compose.localprod.yml down -v` ni
`docker volume rm erp-saas-localprod_erp-api-files` como parte de un restore — ver
[`DOCKER_LOCAL_PROD.md`](DOCKER_LOCAL_PROD.md#7-apagar-sin-borrar-datos).

## 9. Validar después de un restore

```powershell
docker compose -f docker-compose.localprod.yml --env-file .env.docker.local ps
curl.exe -s http://localhost:5003/health/live
curl.exe -s http://localhost:5003/health/ready
```

Además:

- Login exitoso desde el frontend (`http://localhost:8080`).
- Consultar un documento electrónico existente (Electronic Documents) y confirmar que se ve.
- Descargar el RIDE (PDF) de una factura autorizada y confirmar que abre correctamente.

## 10. Advertencias

- **Nunca** ejecutes `docker compose -f docker-compose.localprod.yml down -v` pensando que es parte de
  un backup/restore — este compose no declara volúmenes propios ligados a datos de negocio de la
  misma forma que el volumen `erp-api-files`, pero `down -v` contra el compose base
  (`docker-compose.yml`, Postgres/Redis) sí destruye datos reales.
- **Nunca** restaures sobre `dberpsaas` o el volumen `erp-api-files` activos sin backup previo
  verificado y confirmación explícita del responsable.
- El backup nunca incluye `.env.docker.local` — sin ese archivo (o su contraseña real de Postgres) el
  backup de datos no sirve para levantar el sistema desde cero.
