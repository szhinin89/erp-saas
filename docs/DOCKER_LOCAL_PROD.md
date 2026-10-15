# Docker local production-like — API + Worker + Frontend

## Topología única

`docker-compose.localprod.yml` y `docker-compose.prod.yml` incluyen
`infrastructure/docker/compose.app.yml`. API y Worker usan exactamente `ERP_IMAGE`.
API ejecuta HTTP con `Hangfire__Server__Enabled=false`; Worker usa `--worker`,
`Hangfire__Server__Enabled=true`, una réplica y `restart: unless-stopped`, sin HTTP
ni migraciones. El entrypoint conserva tini y el mismo `ERP.API.dll`.

`docker-compose.yml` y `docker-compose.dev.yml` provisionan solo PostgreSQL/Redis;
no representan otra topología de Background Processing. La aplicación utiliza esos
servicios existentes por una red Docker externa. No crea otra base de datos.

## Preparación

1. Inspeccionar `docker ps` y la red con `docker network inspect erp-saas_default`.
2. Copiar `.env.docker.local.example` a `.env.docker.local` (ignorado por Git).
3. Completar secretos, DNS internos, URLs públicas e identificadores de imágenes.
4. Para el ejemplo local, registrar `127.0.0.1 erp.local.test` en hosts. En demo/prod
   utilizar dominio y HTTPS reales. Frontend consume `PUBLIC_API_URL` en build-time;
   nginx sirve estáticos, sin proxy interno `/api`.

Conexiones dentro de contenedores: PostgreSQL DNS `postgreszh`, puerto **5432**;
Redis DNS `erp-saas-redis`, puerto 6379. El puerto publicado 5435 solo sirve a clientes
fuera de Docker. `Hangfire__StorageConnectionString` es obligatoria, explícita y sin
fallback; puede apuntar a la misma base ERP. No usar localhost ni multiplexing.
Nunca mostrar la salida interpolada de `docker compose config` con secretos reales.

## Configuración compartida y específica

El anchor común de Compose comparte conexión ERP, `Hangfire__StorageConnectionString`,
`Redis__ConnectionString`, `Redis__InstanceName`, configuración SMTP, entorno Production,
URL pública de recuperación y `FileStorage__BasePath=/app/data/files`.
Solo se publica una clave Redis de despliegue. No establecer `DB_CONNECTION_STRING`
ni `ConnectionStrings__Redis` además de las claves canónicas.

Ambos montan **el mismo** volumen `erp-api-files` en `/app/data/files`: certificados,
XML, RIDE y `dataprotection-keys`. Mantener el nombre de proyecto existente
`erp-saas-localprod` para conservar el volumen del piloto. En una transición de Coolify,
identificar y montar el volumen real previo; no crear uno vacío como sustituto.
API tiene volumen de logs histórico; Worker Production registra en consola.

API añade JWT, CORS, URL/puerto HTTP y health. Worker añade únicamente su modo y
Server ON; no publica puertos ni tiene sonda HTTP. Catálogo de siete jobs, cron,
lotes y workers por cola siguen en `appsettings.json`, sin copias en Compose:
core=1, communications=2, sri=2, maintenance=1.

## Construir y arrancar

Antes de activar esta versión, detener cualquier API/worker anterior con Server ON.
El arranque ejecutará migraciones/bootstrap y jobs: usar exclusivamente la base prevista.

```powershell
docker compose -f docker-compose.localprod.yml --env-file .env.docker.local build erp-api erp-frontend
docker compose -f docker-compose.localprod.yml --env-file .env.docker.local up -d --no-build
```

El build de API crea la imagen que consume Worker. En prod, publicar previamente las
imágenes con tags inmutables (o digest), ejecutar `pull` y `up -d --no-build`.
PostgreSQL/Redis deben estar disponibles antes del arranque. Al ser recursos externos,
Compose no puede expresar `depends_on` sobre ellos: API verifica BD/Redis en
`/health/ready`; Worker depende de API `service_healthy`. Este endpoint solo responde
tras terminar migraciones/bootstrap e iniciar HTTP. La sonda SRI opcional se excluye
de esta barrera de dependencias. `/health/live` sigue disponible para liveness.

`depends_on` ordena el arranque inicial; no detiene Worker si API enferma después.
En actualizaciones detener Worker antes de reemplazar API, esperar readiness y luego
recrear Worker con **la misma** imagen. Worker no aplica migraciones.

## Verificación y supervisión

```powershell
docker compose -f docker-compose.localprod.yml --env-file .env.docker.local ps
curl.exe --fail http://localhost:5003/health/ready
docker compose -f docker-compose.localprod.yml --env-file .env.docker.local logs --tail 100 erp-api erp-worker
```

Supervisar salida/reinicios del proceso Worker y heartbeat Hangfire en PostgreSQL.
Esperar cuatro servidores activos del hostname/PID del Worker, cada uno con una cola
core/communications/sri/maintenance y workers 1/2/2/1; siete recurring jobs con las
colas del SSOT y ninguno en default. El hostname de API no debe aparecer como servidor.
Los registros antiguos pueden persistir hasta expirar: distinguir heartbeat activo
de registros históricos. No existe dashboard Hangfire.

Restart actúa al terminar el proceso; no detecta procesos bloqueados. Alertar por
heartbeat vencido y reinicios repetidos. No usar `curl` contra Worker. Locks por ID
existentes limitan concurrencia; no garantizan ejecución exactamente una vez.

## Apagar, respaldar y rollback

`docker compose -f docker-compose.localprod.yml --env-file .env.docker.local down`
detiene API/Worker/frontend sin eliminar los volúmenes ni PostgreSQL/Redis externos.
**No usar `down -v`**: destruye los volúmenes de archivos/keyring y logs del stack.
Backup/restore: `docs/BACKUP_RESTORE_LOCALPROD.md`, scripts `backup-localprod.ps1` y
`restore-check-localprod.ps1`. Confirmar que los scripts cubren el volumen real antes
de una transición fuera de localprod.

Rollback de aplicación: detener Worker, seleccionar el tag anterior compatible en
ERP_IMAGE (ambos procesos), desplegar API, esperar ready y desplegar Worker. API no
revierte migraciones automáticamente. Revisar esquema y Down antes de un downgrade;
si no es seguro, restaurar backup verificado según el runbook.

## Troubleshooting

- Falla interpolación: falta una variable obligatoria de la plantilla.
- API no healthy: revisar migraciones/bootstrap y conectividad BD/Redis/credenciales.
- Worker termina al inicio: revisar storage Hangfire explícito y configuración estricta.
- Archivos no disponibles: comprobar mismo volumen, ruta y permisos en ambos procesos.
- Frontend no alcanza API: revisar PUBLIC_API_URL de build, CORS y dominio público.
- Red inexistente: verificar ERP_NETWORK contra la red de PostgreSQL/Redis.
