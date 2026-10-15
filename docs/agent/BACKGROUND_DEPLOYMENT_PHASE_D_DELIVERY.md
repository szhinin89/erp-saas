# Fase D — Deployment Background Worker (2026-10-15)

Implementado y validado focalmente. Sin commit/push ni cambios en Coolify real.
ADR-043 KEEP; lógica de jobs intacta.

## Resultado

- EXTEND: `infrastructure/docker/compose.app.yml` es la definición única de API,
  Worker y frontend, incluida por localprod/prod. Root/dev provisionan dependencias.
- API/Worker usan exactamente ERP_IMAGE; API Server OFF explícito, Worker
  `--worker`, Server ON explícito, una réplica, restart unless-stopped, sin HTTP.
- Bloque común: conexión ERP, storage Hangfire explícito, Redis/prefijo, FileStorage,
  entorno, SMTP y URL de recuperación. JWT/CORS/puerto/health quedan en API.
- Mismo volumen `erp-api-files` en `/app/data/files`, incluido keyring. Se conserva
  el nombre de proyecto/volumen localprod previo. Coolify debe mapear su volumen real.
- API `/health/ready` verifica BD/Redis tras migraciones/bootstrap; Worker espera API
  healthy. Los recursos externos no permiten depends_on directo en el modelo final.
- Eliminado fallback localhost de storage y clave Redis redundante del despliegue.
  No se eliminó soporte interno de Redis alternativo. Configuración de colas/jobs
  permanece únicamente en el SSOT existente.
- BUG REAL reproducido: build Docker copiaba obj de Windows encima de restore Linux
  y fallaba MSB4018 por fallback NuGet Windows. `backend/.dockerignore` excluye
  bin/obj y configuraciones locales; build repetido PASS.

## Validación focal

| Gate | Resultado |
|---|---|
| Compose config root/dev/localprod/prod | PASS 4/4, plantillas sin secretos reales |
| Roles, imagen, conexiones, Redis único, volumen, readiness, sin catálogo duplicado | PASS localprod/prod |
| Falta storage explícito | PASS: interpolación Compose rechaza vacío; opciones API rechazan ausencia |
| HangfireRecurringJobsTests + WorkerHostRegistrationTests | PASS 21/21 |
| PostgreSQL 16 real: API ready sin Hangfire, Worker separado, sync y lock entre instancias | PASS 4/4 |
| Worker no migra ERP | PASS: arranca sobre BD nueva sin __EFMigrationsHistory |
| Docker build Linux del artefacto actual | PASS, imagen erp-phase-d-smoke:20261015 |
| Compose smoke aislado con PostgreSQL/Redis nuevos, sin puertos publicados | PASS |
| API ready / PostgreSQL / Redis | PASS |
| Worker vivo, cero reinicios, misma imagen por digest, sin listener 8080 | PASS |
| 4 servidores Worker, colas core/communications/sri/maintenance, workers 1/2/2/1 | PASS |
| 7 recurring jobs y 0 servidores del hostname API | PASS |
| Escritura API visible desde Worker en FileStorage | PASS |
| Compose up --wait tras recrear Worker | PASS |
| Diff review / git diff --check | PASS |

Compose 2.27 rechazó inicialmente `--wait` con `healthcheck: disable: true`.
Se retiró la declaración redundante (imagen sin HEALTHCHECK) y se repitió PASS.
No se añadió HTTP al Worker. Su supervisión es proceso/restart y heartbeat Hangfire.
El smoke no prueba entrega real SMTP/SRI ni garantiza ejecución exactamente una vez.
La exclusión concurrente existente se verificó con PostgreSQL real, sin rediseñarla.

Proyecto temporal `erp-phase-d-smoke-20261015` retirado con sus contenedores,
volúmenes y red; piloto intacto. Imagen de smoke permanece disponible localmente.
Sin suites completas ni llamadas de negocio contra datos del piloto.

## Archivos

- `infrastructure/docker/compose.app.yml`, `docker-compose.localprod.yml`,
  `docker-compose.prod.yml`: topología y configuración común.
- `docker-compose.yml`, `infrastructure/docker/compose.base.yml`: comentarios de alcance.
- `.env.docker.local.example`, `.env.example`: contrato y referencias de variables.
- `backend/Dockerfile`, `backend/.dockerignore`: artefacto compartido/contexto Linux.
- `backend/src/ERP.API/appsettings.json`, `appsettings.Development.json.example`:
  storage externo obligatorio y plantilla Redis canónica.
- `backend/src/ERP.API.Tests/Hangfire/HangfireRecurringJobsTests.cs`,
  `RecurringJobRunnerPostgreSqlTests.cs`,
  `backend/src/ERP.API.Tests/Support/PostgreSqlTestWebAppFactory.cs`:
  storage explícito en fixtures y regresión de ausencia de migraciones Worker.
- `docs/deployment/README.md`, `docs/DOCKER_LOCAL_PROD.md`,
  `infrastructure/README.md`, `infrastructure/deployment/README.md`: operación única.
- `STATUS.md`, `docs/agent/PROJECT_STATE.md`, `docs/agent/WORK_QUEUE.md` y esta entrega:
  estado de avance. PROGRESS.html no existe; no se creó un espejo nuevo.

## Transición pendiente

Inspeccionar Coolify real y respaldar; identificar imágenes/red/volumen/secrets;
detener procesos anteriores Server ON; desplegar API OFF y confirmar migraciones/ready;
desplegar Worker ON con la misma imagen y volumen; comprobar cuatro servidores,
siete recurring jobs, cero servidores API y heartbeat. Configurar routing/TLS y alertas.
Runbook: `docs/deployment/README.md`. No se ejecutó esta transición.
