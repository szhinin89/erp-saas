# Backup / Restore — Stack de aplicación

> Nivel 3 (documentación operativa). Complementa [`docs/deployment/README.md`](../deployment/README.md)
> — no lo reemplaza. Procedimiento seguro para respaldar y restaurar el stack `docker-compose.app.yml`
> antes de operaciones riesgosas (upgrades, puestas en marcha, migraciones) sin depender de que el
> volumen Docker nunca falle.

Un solo script, `scripts/backup-restore.ps1`, con `-Action Backup|RestoreCheck` y **`-Database`
obligatorio** (nunca asume una base). En los ejemplos, `<DB>` es la base del entorno:
`erp_zh_prod` en producción, `erp_zh_test` en un ensayo local del stack.

## 1. Qué respalda `-Action Backup`

| Componente | Origen | Destino en el backup |
|---|---|---|
| PostgreSQL (`<DB>`) | `docker exec postgreszh pg_dump -Fc` | `postgres.dump` (formato custom, restaurar con `pg_restore`) |
| FileStorage (certificados P12, XML SRI, RIDE, keyring) | volumen nombrado `erp-saas-localprod_erp-api-files` | `filestorage.tar.gz` |
| Checksums | `Get-FileHash -Algorithm SHA256` sobre ambos archivos | `SHA256SUMS.txt` |
| Metadata | contenedor, base, volumen, tamaños, hashes — **sin secretos** | `manifest.json` |

Cada ejecución crea una carpeta nueva `backups/<DB>/YYYYMMDD-HHMMSS/`, ignorada por Git
(`.gitignore`: `backups/`, `*.dump`, `*.tar.gz`). El script verifica que el contenedor, la base y el
volumen existan antes de empezar.

El volumen conserva el nombre del proyecto Compose histórico (`erp-saas-localprod`); no renombrarlo:
un nombre nuevo crearía un volumen vacío y dejaría huérfanos certificados y documentos.

El backup es de **solo lectura** contra el sistema real: no detiene contenedores, no borra nada, no
hace restore.

## 2. Qué NO respalda

- **`.env.app`** (contiene `POSTGRES_PASSWORD` y `JWT_SECRET_KEY`) — deliberadamente excluido para no
  dejar secretos en texto plano dentro de `backups/`. Ver sección 3.
- Redis (`erp-saas-redis`) — solo cache/sesiones, se reconstruye solo; no contiene datos de negocio
  que no puedan regenerarse.
- Imágenes Docker — se reconstruyen desde el código versionado o se vuelven a obtener del registro por
  su tag inmutable (`ERP_IMAGE`, `FRONTEND_IMAGE`).

## 3. Cómo guardar `.env.app` de forma segura

`.env.app` nunca se versiona ni se incluye en `backups/`. Para preservarlo:

- Cópialo a un gestor de secretos (1Password, Bitwarden, Vault) o cífralo por separado
  (`gpg -c .env.app` → guarda el `.gpg` fuera del repo).
- Si necesitas un respaldo de emergencia en disco, guárdalo **fuera** de `backups/` (que puede
  terminar compartido) y nunca lo imprimas en consola ni lo pegues en un chat/log.
- Alternativa: puedes regenerarlo desde `.env.app.example` siempre que conserves la contraseña real
  de Postgres en tu gestor de secretos (`JWT_SECRET_KEY` puede regenerarse sin costo — solo invalida
  sesiones activas).

## 4. Ejecutar el backup

```powershell
.\scripts\backup-restore.ps1 -Action Backup -Database <DB>
```

Parámetros opcionales (si tus nombres reales difieren de los defaults del proyecto):

```powershell
.\scripts\backup-restore.ps1 -Action Backup -Database <DB> -PostgresContainer postgreszh -FileStorageVolume erp-saas-localprod_erp-api-files -PgUser postgres
```

## 5. Validar integridad sin restaurar (recomendado primero)

No requiere tocar ninguna base de datos ni volumen real:

```powershell
$dir = "backups\<DB>\<TIMESTAMP>"

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

## 6. Restore drill aislado (recomendado antes de una puesta en marcha)

`-Action RestoreCheck` automatiza un drill completo — Postgres **y** FileStorage — en un contenedor,
volumen y red **nuevos y descartables**, sin tocar `postgreszh`, `erp-saas-localprod_erp-api-files`
ni ningún contenedor real:

```powershell
# Usa el backup mas reciente en backups/<DB>/
.\scripts\backup-restore.ps1 -Action RestoreCheck -Database <DB>

# O un backup especifico
.\scripts\backup-restore.ps1 -Action RestoreCheck -Database <DB> -BackupDir backups\<DB>\<TIMESTAMP>

# Deja el entorno temporal vivo para inspeccion manual (limpiar despues a mano)
.\scripts\backup-restore.ps1 -Action RestoreCheck -Database <DB> -KeepTemp
```

El drill:

1. Verifica que `manifest.json` corresponda a `-Database` y los checksums del backup.
2. Levanta un contenedor `postgres:16` nuevo (`erp-restore-check-postgres`, red
   `erp-restore-check-net`, password temporal generado y nunca impreso) y restaura `postgres.dump` en
   una base `<DB>_restore_drill` **dentro de ese contenedor** — la base real nunca se toca.
3. Imprime conteos de las tablas clave (`tenants`, `company`, `identity_users`, `sales_invoices`,
   `electronic_documents`, `document_sequence`, `sri_settings`).
4. Crea un volumen `erp-restore-check-files` y extrae `filestorage.tar.gz` ahí, listando la
   estructura y confirmando la presencia de `certificate.p12`, `authorized.xml` y el RIDE (PDF) — sin
   imprimir contenido.
5. Limpia contenedor, volumen y red temporales al finalizar (salvo `-KeepTemp`).

Nombres temporales configurables por parámetro si necesitas evitar colisiones:
`-TempContainerName`, `-TempVolumeName`, `-TempNetworkName`.

## 7. Restaurar FileStorage en un volumen temporal (inspección manual)

```powershell
$dir = (Resolve-Path "backups\<DB>\<TIMESTAMP>").Path

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

Restaurar sobre la base activa o el volumen `erp-saas-localprod_erp-api-files` es una operación
destructiva sobre datos reales. Antes de hacerlo:

1. Confirma explícitamente con el responsable del sistema que se va a sobrescribir el estado actual.
2. Genera un backup del estado actual (aunque vayas a descartarlo) por si el restore falla a mitad de
   camino.
3. Detén API y Worker (no Postgres) para evitar escrituras concurrentes:
   ```powershell
   .\scripts\app-stack.ps1 -Action Down
   ```
4. Restaura Postgres con `pg_restore --clean --if-exists -U postgres -d <DB>` (usa `--clean` solo si
   aceptas que se recreen los objetos existentes).
5. Restaura FileStorage extrayendo `filestorage.tar.gz` directamente sobre el volumen
   `erp-saas-localprod_erp-api-files` (mismo patrón que la sección 7, sin volumen temporal).
6. Levanta de nuevo el stack (`.\scripts\app-stack.ps1 -Action Up -Force`) y valida (sección 9).

**Nunca** ejecutes `docker compose ... down -v` ni `docker volume rm erp-saas-localprod_erp-api-files`
como parte de un restore — ver [deployment § Apagar, respaldar y rollback](../deployment/README.md#apagar-respaldar-y-rollback).

## 9. Validar después de un restore

```powershell
.\scripts\app-stack.ps1 -Action Health
curl.exe -s http://localhost:5003/health/live
```

Además:

- Login exitoso desde el frontend.
- Consultar un documento electrónico existente (Electronic Documents) y confirmar que se ve.
- Descargar el RIDE (PDF) de una factura autorizada y confirmar que abre correctamente.

## 10. Advertencias

- **Nunca** ejecutes `down -v` pensando que es parte de un backup/restore: contra
  `docker-compose.app.yml` destruye los volúmenes de archivos/keyring y logs; contra
  `docker-compose.yml` (Postgres/Redis) destruye las bases.
- **Nunca** restaures sobre la base activa o el volumen `erp-api-files` sin backup previo verificado
  y confirmación explícita del responsable.
- El backup nunca incluye `.env.app` — sin ese archivo (o su contraseña real de Postgres) el backup de
  datos no sirve para levantar el sistema desde cero.
