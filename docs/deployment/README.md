# Deployment — API + Worker separados

Documento único del stack de aplicación (ensayo local y producción). Backups:
[`docs/operations/BACKUP_RESTORE.md`](../operations/BACKUP_RESTORE.md). Operación diaria:
[`docs/operations/DAILY_RUNBOOK.md`](../operations/DAILY_RUNBOOK.md).

## Topología única

`docker-compose.app.yml` (raíz) incluye `infrastructure/docker/compose.app.yml`: API HTTP y Worker
`--worker`, misma imagen/tag `ERP_IMAGE`, una réplica Worker sin HTTP ni migraciones, más el
frontend. API ejecuta HTTP con `Hangfire__Server__Enabled=false`; Worker usa `--worker`,
`Hangfire__Server__Enabled=true` y `restart: unless-stopped`. El entrypoint conserva tini y el mismo
`ERP.API.dll`. API migra/bootstrap; Worker nunca migra. No usar host combinado para despliegue.

`docker-compose.yml` provisiona solo PostgreSQL/Redis locales; no representa otra topología de
Background Processing. La aplicación utiliza servicios PostgreSQL/Redis existentes por la red
Docker externa `ERP_NETWORK`; Compose no crea bases.

El nombre de proyecto `erp-saas-localprod` se conserva a propósito: identifica los volúmenes
existentes (`erp-saas-localprod_erp-api-files`, `erp-saas-localprod_erp-api-logs`). Renombrarlo
crearía volúmenes vacíos y dejaría huérfanos certificados, XML, RIDE y keyring.

## Base de datos por entorno

| Entorno | Base | Origen de la conexión |
|---|---|---|
| Desarrollo (`dotnet run`, dev-launcher) | `erp_zh_dev` | user-secrets de ERP.API |
| Tests / E2E / ensayo local del stack app | `erp_zh_test` | entorno (`run-e2e.ps1`, `.env.app`) |
| Producción | `erp_zh_prod` (solo en su host) | entorno / gestor de secretos |

`appsettings.json` no contiene connection strings; fuera de Development no se leen user-secrets.
El stack app **nunca** usa `erp_zh_dev`: `scripts/app-stack.ps1` lo rechaza. El ensayo local y el
E2E comparten `erp_zh_test` y no deben correr a la vez: ambos se niegan a arrancar si la base tiene
conexiones ajenas. El ensayo usa `Redis__InstanceName=ERP_TEST_` para no mezclar caché con desarrollo
en el Redis local compartido.

## Preparación

1. Inspeccionar `docker ps` y la red con `docker network inspect erp-saas_default`.
2. Copiar `.env.app.example` a `.env.app` (ignorado por Git). En producción, provisionar las mismas
   variables desde el gestor de secretos.
3. Completar secretos, DNS internos, URLs públicas e identificadores de imágenes. `POSTGRES_DB` y la
   base de `Hangfire__StorageConnectionString` deben coincidir (`app-stack.ps1` lo valida).
4. Para el ensayo local, registrar `127.0.0.1 erp.local.test` en hosts. En producción utilizar
   dominio y HTTPS reales. Frontend consume `PUBLIC_API_URL` en build-time; nginx sirve estáticos,
   sin proxy interno `/api`.

Conexiones dentro de contenedores: PostgreSQL DNS `postgreszh`, puerto **5432**; Redis DNS
`erp-saas-redis`, puerto 6379. El puerto publicado 5435 solo sirve a clientes fuera de Docker.
`Hangfire__StorageConnectionString` es obligatoria, explícita y sin fallback; apunta a la misma base
ERP. No usar localhost ni multiplexing. Nunca mostrar la salida interpolada de
`docker compose config` con secretos reales (`app-stack.ps1 -Action Config` usa `--quiet`).

## Redis de autorización (requisito de seguridad, SEC-06)

Todo Redis configurado es el de la caché de permisos. API y worker **no arrancan** si no cumple o si
no se puede verificar (contrato en [security.md](../architecture/security.md)):

- `maxmemory-policy noeviction`, `save ""`, `appendonly no`; nodo primario sin réplicas conectadas.
- El usuario de conexión debe poder ejecutar `CONFIG GET` e `INFO` (la verificación abre una conexión
  efímera en modo admin). Un proveedor que bloquee `CONFIG` necesita una verificación equivalente
  aprobada antes de usarse.
- Con más de una instancia de API: `Redis__Required=true` (caché local solo en instancia única).
- Operación: no restaurar respaldos de Redis ni promover réplicas; no cambiar la configuración en
  caliente; monitorear memoria (`used_memory` frente a `maxmemory`) y alertar por indisponibilidad.
  Con `noeviction`, memoria llena = escrituras rechazadas (cambios de permisos responden 503 sin
  guardar), nunca permisos obsoletos.

El Redis local (`infrastructure/docker/compose.base.yml`) ya declara esa configuración; el contenedor
existente conserva la anterior hasta recrearlo (`docker compose up -d redis`). Redis solo contiene
cachés y contadores efímeros (permisos, consultas, rate limits): perder su contenido es seguro.

## Configuración compartida y específica

El anchor común de Compose comparte conexión ERP, `Hangfire__StorageConnectionString`,
`Redis__ConnectionString`, `Redis__InstanceName`, configuración SMTP, entorno Production, URL pública
de recuperación y `FileStorage__BasePath=/app/data/files`. Solo se publican las claves canónicas
(`ConnectionStrings__DefaultConnection`, `Redis__ConnectionString`); no existen alias.

Ambos montan **el mismo** volumen `erp-api-files` en `/app/data/files`: certificados, XML, RIDE y
`dataprotection-keys`. En una transición de Coolify, identificar y montar el volumen real previo; no
crear uno vacío como sustituto. API tiene volumen de logs; Worker Production registra en consola.

API añade JWT, CORS, URL/puerto HTTP y health. Worker añade únicamente su modo y Server ON; no publica
puertos ni tiene sonda HTTP. Catálogo de siete jobs, cron, lotes y workers por cola siguen en
`appsettings.json`, sin copias en Compose: core=1, communications=2, sri=2, maintenance=1.

## Construir y arrancar

Antes de activar una versión, detener cualquier API/worker anterior con Server ON. El arranque
ejecutará migraciones/bootstrap y jobs: usar exclusivamente la base prevista.

```powershell
.\scripts\app-stack.ps1 -Action Config   # valida .env.app e interpolación, sin imprimir secretos
.\scripts\app-stack.ps1 -Action Build    # construye erp-api (imagen de API y Worker) y erp-frontend
.\scripts\app-stack.ps1 -Action Up       # up -d --no-build
```

Equivalente directo: `docker compose -f docker-compose.app.yml --env-file .env.app up -d --no-build`.
En producción, publicar previamente las imágenes con tags inmutables (o digest), ejecutar `pull` y
`up -d --no-build`. PostgreSQL/Redis deben estar disponibles antes del arranque. Al ser recursos
externos, Compose no puede expresar `depends_on` sobre ellos: API verifica BD/Redis en
`/health/ready`; Worker y frontend dependen de API `service_healthy`. Este endpoint solo responde tras
terminar migraciones/bootstrap e iniciar HTTP. La sonda SRI opcional se excluye de esta barrera.
`/health/live` sigue disponible para liveness.

`depends_on` ordena el arranque inicial; no detiene Worker si API enferma después. En
actualizaciones detener Worker antes de reemplazar API, esperar readiness y luego recrear Worker con
**la misma** imagen. Worker no aplica migraciones.

## Verificación y supervisión

```powershell
.\scripts\app-stack.ps1 -Action Health   # ps + /health/ready
.\scripts\app-stack.ps1 -Action Logs
```

Supervisar salida/reinicios del proceso Worker y heartbeat Hangfire en PostgreSQL. Esperar cuatro
servidores activos del hostname/PID del Worker, cada uno con una cola core/communications/sri/
maintenance y workers 1/2/2/1; siete recurring jobs con las colas del SSOT y ninguno en default. El
hostname de API no debe aparecer como servidor. Los registros antiguos pueden persistir hasta
expirar: distinguir heartbeat activo de registros históricos. No existe dashboard Hangfire.

Restart actúa al terminar el proceso; no detecta procesos bloqueados. Alertar por heartbeat vencido y
reinicios repetidos. No usar `curl` contra Worker. Locks por ID existentes limitan concurrencia; no
garantizan ejecución exactamente una vez.

## Apagar, respaldar y rollback

`.\scripts\app-stack.ps1 -Action Down` detiene API/Worker/frontend sin eliminar los volúmenes ni
PostgreSQL/Redis externos. **No usar `down -v`**: destruye los volúmenes de archivos/keyring y logs.

Backup y restore drill: `.\scripts\backup-restore.ps1 -Action Backup|RestoreCheck -Database <DB>`
(detalle en [`BACKUP_RESTORE.md`](../operations/BACKUP_RESTORE.md)).

Rollback de aplicación: detener Worker, seleccionar el tag anterior compatible en `ERP_IMAGE` (ambos
procesos), desplegar API, esperar ready y desplegar Worker. API no revierte migraciones
automáticamente. Revisar esquema y Down antes de un downgrade; si no es seguro, restaurar backup
verificado según el runbook.

## Troubleshooting

- `app-stack.ps1` rechaza el archivo: falta una variable, conserva `CHANGE_ME`, usa `erp_zh_dev` o la
  base de Hangfire no coincide con `POSTGRES_DB`.
- Falla interpolación: falta una variable obligatoria de la plantilla.
- API no healthy: revisar migraciones/bootstrap y conectividad BD/Redis/credenciales.
- Worker termina al inicio: revisar storage Hangfire explícito y configuración estricta.
- Archivos no disponibles: comprobar mismo volumen, ruta y permisos en ambos procesos.
- Frontend no alcanza API: revisar `PUBLIC_API_URL` de build, CORS y dominio público.
- Red inexistente: verificar `ERP_NETWORK` contra la red de PostgreSQL/Redis.

## Transición real de Coolify (pendiente de inspección)

No se inspeccionó ni modificó Coolify activo. Secrets en el gestor de secretos de Coolify, nunca en
Git. TLS/routing público se configura en Coolify. Antes de ejecutar:

1. Inventariar recursos actuales, imágenes/tags, comandos, variables efectivas, redes,
   volúmenes/keyring y procesos con Hangfire Server ON. Respaldar BD/archivos.
2. Preparar la topología Compose canónica en Coolify, misma imagen ERP para API y Worker, una réplica
   Worker. Conectar ambos a la red real de PostgreSQL/Redis; montar el volumen durable real existente
   en ambos, sin sustituirlo por uno vacío. El mismo volumen local exige el mismo host; no separar en
   nodos sin storage compartido.
3. Cargar conexiones externas explícitas (base `erp_zh_prod`) y secretos compartidos. Storage Hangfire
   usa DNS PostgreSQL interno y puerto 5432, sin localhost/5435/multiplexing. Publicar solo
   `Redis__ConnectionString` y el mismo `Redis__InstanceName`. Eliminar variables legacy.
4. Detener todos los workers antiguos y API antiguas con `Server.Enabled=true`. No dejar procesos
   viejos sincronizando recurring jobs durante la transición.
5. Desplegar primero API con `Server.Enabled=false`; confirmar migraciones/bootstrap completos y
   `/health/ready` healthy (BD/Redis). Verificar la tabla `background_job_progress` (baseline
   `20261021162651_InitialProductionBaseline`).
6. Desplegar Worker con `--worker`, `Server.Enabled=true` y exactamente la imagen de API. En Compose
   depende de API healthy; si la plataforma no respeta esa dependencia, ejecutar estos pasos
   secuencialmente antes de habilitar el servicio Worker.
7. Confirmar proceso vivo, sin listener HTTP, cuatro servidores Hangfire activos con colas
   core/communications/sri/maintenance (1/2/2/1 workers), siete recurring jobs y ningún servidor
   perteneciente al hostname API. Revisar heartbeat/logs y ausencia de procesos antiguos activos; un
   registro histórico no equivale a worker vivo.
8. Habilitar alertas de heartbeat/reinicios, verificar archivos/keyring compartidos y realizar smoke
   controlado. En actualizaciones: detener Worker → API ready → Worker.

Los locks existentes evitan concurrencia por ID; no sustituyen la retirada de procesos antiguos ni
garantizan ejecución exactamente una vez. ADR-043 permanece sin cambios.

## SMTP (correo de factura autorizada)

El envío de correo (ej. "factura autorizada") nunca bloquea una venta ni una autorización SRI — el
encolado (`CommunicationOutbox`) está desacoplado del flujo comercial, y si no hay SMTP configurado
el correo simplemente no se envía (queda `Pending`/`Failed` en el outbox, reintentable, sin afectar
la factura ya emitida).

`CommunicationSettingsResolver` (`backend/src/ERP.Infrastructure/Communications/CommunicationSettingsResolver.cs`)
resuelve la configuración SMTP en dos capas, en este orden:

1. **`OrgSettings` por empresa** (claves `communications.email.*` — `OrgSettingKeys.Communications` en
   `backend/src/ERP.Domain/Modules/Configuration/Constants/OrgSettingKeys.cs`). Hoy **no existe un
   endpoint/pantalla de administración** para escribir estas claves vía la app — solo el resolver las
   lee. Exponerlas en un endpoint/pantalla de configuración es trabajo funcional nuevo.
2. **Fallback por variables de entorno** (`Communications:Email:*` en `appsettings`/env vars) — el
   camino recomendado para una instalación de una sola empresa:

   | Variable | Ejemplo (Zoho) |
   |---|---|
   | `Communications__Email__Enabled` | `true` |
   | `Communications__Email__SmtpHost` | `smtp.zoho.com` |
   | `Communications__Email__SmtpPort` | `587` |
   | `Communications__Email__SmtpUsername` | `facturacion@tudominio.com` |
   | `Communications__Email__SmtpPassword` | *(credencial real — nunca en el repo)* |
   | `Communications__Email__SenderEmail` | `facturacion@tudominio.com` |
   | `Communications__Email__SenderName` | `Nombre Comercial` |
   | `Communications__Email__UseSsl` | `true` |
   | `Communications__Email__MaxRetries` | `3` |

   Configurar las variables `SMTP_*` de `.env.app.example`; el bloque común de
   `infrastructure/docker/compose.app.yml` las transmite a API y Worker. **Nunca** comitear la
   contraseña SMTP real en ningún archivo versionado.

**Smoke real de envío pendiente únicamente por la credencial SMTP real** — el flujo ya está
implementado y probado (ERP-CORE-CLOSEOUT-06/07).

## SRI / Certificado electrónico

- **Factura física NO depende del certificado**: `PhysicalSalesInvoiceEmissionStrategy` no tiene ninguna dependencia de `IElectronicDocumentIssuer`/certificado — garantía estructural, no solo de configuración (confirmado en ERP-CORE-CLOSEOUT-07). Una empresa puede operar 100% con facturación física sin ningún dato SRI configurado.
- **Venta electrónica SÍ requiere certificado + settings**: ambiente SRI + WSDL (`SriSettings`) y certificado `.p12` subido vía `PUT`/`POST /api/v1/electronic-invoicing/sri-configuration` y `.../sri-configuration/certificate` (pantalla `/settings/electronic-invoicing`). Sin esto, la emisión electrónica devuelve un error de validación claro ("La empresa no tiene un certificado digital (P12) registrado..." / "no tiene configuración SRI registrada..."), nunca un 500 — confirmado en ERP-CORE-CLOSEOUT-06/07.
- **Readiness**: `GET /api/companies/operational-readiness` indica exactamente qué falta (`sriConfig`, `certificate`, `emissionPoint`) antes de intentar una venta electrónica.
- **Proveedor de sistema en el XML — pendiente de fuente normativa**: `SystemProviderSettings` (RUC/razón social/CIIU de ZH Technologies como proveedor de sistema, ERP-CORE-CLOSEOUT-09) ya tiene configuración dinámica lista (`GET`/`PUT /api/v1/system/provider-settings`), pero **todavía no se inyecta en ningún XML de comprobante** — falta el texto de la Resolución NAC-DGERCGC26-00000027 o la ficha técnica SRI que confirme el campo/elemento exacto antes de tocar `InvoiceXmlBuilder`/`CreditNoteXmlBuilder`. No inventar esa estructura sin esa fuente.

## Dominio / SSL

**Pendiente externo, no resuelto en este repo.** `docker-compose.app.yml` publica los puertos
API/frontend sin terminación TLS integrada. Para producción falta: dominio registrado, terminación SSL
(reverse proxy tipo Caddy/Traefik/nginx+certbot delante de `erp-frontend`) y actualizar
`PUBLIC_FRONTEND_URL`/`PUBLIC_API_URL` al dominio real. No inventar aquí una configuración de SSL sin
el dominio real confirmado.

## Pendientes externos (resumen)

- SMTP real (Zoho u otro) — falta la credencial real para completar el smoke de envío.
- Impresora térmica física — falta hardware para la prueba física del Print Agent (ver `print-agent/README.md`).
- Dominio y SSL (ver sección anterior).
- Certificado `.p12` SRI real y confirmación de ambiente (pruebas/producción) por empresa.
- Confirmación normativa de la Resolución NAC-DGERCGC26-00000027 (proveedor de sistema) antes de tocar el XML electrónico.
- Backups con periodicidad automatizada: `backup-restore.ps1` ya existe; falta agendarlo y copiar
  `backups/<DB>/` fuera del host.
