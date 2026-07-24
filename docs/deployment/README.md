# Deployment — ERP SaaS

Plantillas y guías de despliegue (placeholder enterprise).

| Recurso | Ubicación |
|---------|-----------|
| Docker Compose prod | [`docker-compose.prod.yml`](../../docker-compose.prod.yml) |
| Base de servicios | [`infrastructure/docker/compose.base.yml`](../../infrastructure/docker/compose.base.yml) |
| Ops / deployment IaC | [`infrastructure/deployment/`](../../infrastructure/deployment/) |
| CI deploy opcional | [`.github/workflows/build-and-deploy.yml`](../../.github/workflows/build-and-deploy.yml) |

> Antes de producción: secrets, redes, volúmenes, réplicas y variables de entorno según [`docs/DEVELOPMENT.md`](../DEVELOPMENT.md).
