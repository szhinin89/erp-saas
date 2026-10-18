# Deployment

Topología única API + Worker separados: `../docker/compose.app.yml`, incluida por
`docker-compose.app.yml` en la raíz y operada con `scripts/app-stack.ps1`.

API/Worker usan la misma imagen inmutable; API Server OFF y Worker `--worker` ON.
PostgreSQL/Redis existentes, configuración y almacenamiento durable compartidos.
Secrets externos. No hay infraestructura Coolify activa administrada desde este repo.

Contrato, base por entorno y transición: [runbook](../../docs/deployment/README.md).
