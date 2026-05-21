# Infrastructure — ERP SaaS

Infraestructura local y plantillas de despliegue. **No** mezclar con código de aplicación.

## Estructura

| Carpeta | Contenido |
|---------|-----------|
| [`docker/`](docker/) | Compose base, imágenes locales |
| [`postgres/`](postgres/) | Notas tuning, scripts ops (no init Docker obligatorio) |
| [`redis/`](redis/) | Configuración Redis local |
| [`nginx/`](nginx/) | Plantillas reverse proxy (futuro) |
| [`environments/`](environments/) | Plantillas `.env` por entorno |
| [`backups/`](backups/) | Procedimientos backup PostgreSQL |
| [`monitoring/`](monitoring/) | Enlaces a `../monitoring/` raíz |
| [`deployment/`](deployment/) | Runbooks deploy |
| [`migrations/`](migrations/) | SQL ops fuera de EF (excepcional) |

## Arranque local

Desde la raíz del repo:

```powershell
docker compose up -d
# o
docker compose -f docker-compose.dev.yml up -d
```

Puertos por defecto: PostgreSQL **5435**, Redis **6379**.

## Compose

- **Canónico raíz:** `docker-compose.yml` → incluye `docker/compose.base.yml`
- **Dev:** `docker-compose.dev.yml`
- **Prod (plantilla):** `docker-compose.prod.yml`
