# Docker Local "Production-Like" — API + Frontend

> Nivel 3 (documentación técnica especializada). No reemplaza `docs/DEVELOPMENT.md` (modo desarrollo
> normal, sigue funcionando exactamente igual). Esto es un stack **adicional** para correr API y
> Frontend compilados en Docker, contra el PostgreSQL y Redis que **ya tienes corriendo**.

## Qué es y qué NO es

- Levanta `ERP.API` (compilada, `dotnet publish`) y el Frontend (compilado con Vite, servido por Nginx)
  dentro de contenedores Docker.
- **NO** crea un PostgreSQL nuevo. **NO** crea un Redis nuevo. **NO** duplica bases de datos.
- Se conecta a los contenedores `postgreszh` y `erp-saas-redis` que ya administra
  `docker-compose.yml` / `infrastructure/docker/compose.base.yml`, vía la red Docker
  `erp-saas_default` (declarada como `external: true` en `docker-compose.localprod.yml`).
- Las migraciones EF Core se aplican automáticamente al arrancar `ERP.API` (`db.Database.MigrateAsync()`,
  igual que en desarrollo) — no se ejecuta ningún seed destructivo (`InstallData.Enabled` permanece en
  `false`, valor por defecto en `appsettings.json`, salvo que tú mismo lo actives).

Archivos que agrega esta funcionalidad:

| Archivo | Propósito |
|---|---|
| `backend/Dockerfile` | Multi-stage build de `ERP.API` (SDK 10.0.203 → runtime `aspnet:10.0-alpine`). |
| `frontend/Dockerfile` | Multi-stage build del frontend (`node:22-alpine` → `nginx:1.27-alpine`). |
| `frontend/nginx.conf` | Sirve el SPA con fallback a `index.html` + proxy `/api/` → `erp-api:8080`. |
| `docker-compose.localprod.yml` | Orquesta `erp-api` + `erp-frontend`, sin declarar `postgres`/`redis`. |
| `.env.docker.local.example` | Plantilla de variables (versionada). Copiar a `.env.docker.local` (ignorado por Git). |

---

## 1. Confirmar el PostgreSQL y Redis existentes

Antes de tocar nada, identifica los contenedores y la red reales en tu máquina — pueden diferir de los
nombres por defecto de este repo si los renombraste.

```powershell
docker ps --format "table {{.Names}}\t{{.Image}}\t{{.Ports}}"
```

Deberías ver algo como:

```
NAMES            IMAGE                PORTS
postgreszh       postgres:16          0.0.0.0:5435->5432/tcp
erp-saas-redis   redis:7-alpine       0.0.0.0:6379->6379/tcp
```

Confirma en qué red Docker están (por defecto, la que crea `docker-compose.yml` de este repo):

```powershell
docker network ls
docker network inspect erp-saas_default
```

Si tus contenedores están en una red con **otro nombre**, ajusta `name: erp-saas_default` dentro de
`docker-compose.localprod.yml` (bloque `networks:`) para que coincida.

Si el nombre de contenedor de Postgres/Redis es distinto de `postgreszh` / `erp-saas-redis`, ajusta
`POSTGRES_HOST` / `REDIS_HOST` en tu `.env.docker.local` (paso 2) — no hace falta tocar el compose.

---

## 2. Configurar `.env.docker.local`

```powershell
Copy-Item .env.docker.local.example .env.docker.local
```

Edita `.env.docker.local` y completa, como mínimo:

- `POSTGRES_PASSWORD` — debe coincidir **exactamente** con la contraseña real del Postgres que ya
  corre (revísala en tu `appsettings.Development.json` local o en cómo arrancaste ese contenedor).
- `JWT_SECRET_KEY` — genera un secreto propio para este stack, no reutilices el de desarrollo:
  ```powershell
  [Convert]::ToBase64String((1..48 | ForEach-Object { Get-Random -Maximum 256 }))
  ```

`.env.docker.local` está en `.gitignore` — nunca se versiona. `.env.docker.local.example` sí, sin
secretos reales.

---

## 3. Construir las imágenes

Desde la raíz del repo (los build contexts están definidos en `docker-compose.localprod.yml`:
`backend/` para la API, `.` para el frontend, porque su `npm run build` referencia `../tools/ci`):

```powershell
docker compose -f docker-compose.localprod.yml --env-file .env.docker.local build
```

---

## 4. Levantar el stack

```powershell
docker compose -f docker-compose.localprod.yml --env-file .env.docker.local up -d --build
```

Esto:

1. Construye `erp-api-localprod` y `erp-frontend-localprod` si hay cambios.
2. Los conecta a la red externa `erp-saas_default` (donde ya están `postgreszh` y `erp-saas-redis`).
3. Publica:
   - API en `http://localhost:5003` (dentro del contenedor escucha en `:8080`, ver `ASPNETCORE_URLS`).
   - Frontend en `http://localhost:8080` (Nginx, puerto `80` interno).
4. Al arrancar, `ERP.API` aplica migraciones pendientes contra el Postgres existente y sirve tráfico.

Verificar:

```powershell
docker compose -f docker-compose.localprod.yml ps
curl http://localhost:5003/health/live
```

Abrir `http://localhost:8080` en el navegador — el frontend llama a la API vía proxy Nginx
(`/api/*` → `erp-api:8080`, mismo patrón relativo que usa Vite en desarrollo), así que no hace falta
configurar CORS adicional.

---

## 5. Aplicar migraciones contra la DB existente

No requiere ningún paso manual: `ERP.API` ejecuta `db.Database.MigrateAsync()` en el arranque
(`Program.cs`), igual que en modo desarrollo. Si necesitas verlo en los logs:

```powershell
docker compose -f docker-compose.localprod.yml logs -f erp-api
```

Si por alguna razón necesitas aplicar migraciones manualmente contra la misma base (fuera del
contenedor, con `dotnet ef`), usa la connection string apuntando al puerto publicado del Postgres
existente (`localhost:5435` si sigues el valor por defecto de `infrastructure/docker/compose.base.yml`)
— exactamente como ya lo haces en desarrollo. Esta imagen Docker no cambia ese flujo.

---

## 6. Ver logs

```powershell
docker compose -f docker-compose.localprod.yml logs -f erp-api
docker compose -f docker-compose.localprod.yml logs -f erp-frontend
docker compose -f docker-compose.localprod.yml logs -f          # ambos
```

---

## 7. Apagar sin borrar datos

```powershell
docker compose -f docker-compose.localprod.yml down
```

Esto elimina **solo** `erp-api-localprod` y `erp-frontend-localprod`. `postgreszh` y `erp-saas-redis`
siguen corriendo (no son parte de este compose) y sus datos persisten en los volúmenes de
`infrastructure/docker/compose.base.yml` (`erp_saas_pgdata`, `erp_saas_redisdata`).

> ⚠️ **Nunca ejecutes `docker compose -f docker-compose.localprod.yml down -v`** pensando que borra
> algo de este stack: este compose no declara volúmenes propios, así que `-v` no tiene efecto aquí —
> pero tampoco ejecutes `down -v` sobre `docker-compose.yml` (el de Postgres/Redis) salvo que
> **realmente** quieras destruir los datos. **Jamás** ejecutes `DROP DATABASE`, `TRUNCATE` masivo, ni
> `docker volume rm erp_saas_pgdata` contra la base de datos compartida — este stack fue diseñado
> explícitamente para no requerir ninguna de esas operaciones.

---

## Troubleshooting rápido

| Síntoma | Causa probable | Acción |
|---|---|---|
| `erp-api` no arranca, error de conexión a Postgres | `POSTGRES_HOST`/`POSTGRES_PASSWORD` no coinciden con el contenedor real | Revisar paso 1 y 2 |
| `erp-api` no arranca, error de conexión a Redis | `REDIS_HOST` no coincide con el contenedor real | Revisar paso 1 y 2 |
| Frontend carga pero las llamadas a `/api/*` fallan | `erp-api` no está healthy o el nombre de servicio cambió en el compose | `docker compose -f docker-compose.localprod.yml ps` y logs de `erp-api` |
| `docker compose ... build` falla en el stage de frontend por `run-platform-guard` | Violación real de las reglas de `tools/ci/` (mismo gate que corre en desarrollo) | Corregir el código, no el gate |
| Error "network erp-saas_default not found" | El PostgreSQL/Redis existente aún no está levantado, o está en otra red | `docker compose up -d` (compose base) primero, o ajustar `name:` de la red en `docker-compose.localprod.yml` |
