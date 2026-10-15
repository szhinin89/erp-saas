# Deployment

Topología única API + Worker separados: `../docker/compose.app.yml`, incluida por
`docker-compose.localprod.yml` y `docker-compose.prod.yml` en la raíz.

API/Worker usan la misma imagen inmutable; API Server OFF y Worker `--worker` ON.
PostgreSQL/Redis existentes, configuración y almacenamiento durable compartidos.
Secrets externos. No hay infraestructura Coolify activa administrada desde este repo.

Contrato y transición: [runbook](../../docs/deployment/README.md).
