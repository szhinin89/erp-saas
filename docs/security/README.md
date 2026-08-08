# Security — ERP SaaS

Documentación de seguridad (no contiene secretos). Normativa canónica: [`docs/architecture/security.md`](../../docs/architecture/security.md) · detalle identidad: [`docs/IDENTITY.md`](../IDENTITY.md).

| Área | Documento |
|------|-----------|
| Multi-tenant hardening | [`MULTI-TENANT-HARDENING.md`](MULTI-TENANT-HARDENING.md) |
| Auth & refresh rotation | [`auth/refresh-rotation.md`](auth/refresh-rotation.md) |

## Secrets

- No commitear `.env`, tokens, certificados ni publish profiles.
- Usar variables de entorno / secret store del entorno de despliegue.
- Ver [`docs/DEVELOPMENT.md`](../DEVELOPMENT.md).

## Pendiente (futuro, por release)

Threat models, auditorías de seguridad y notas de compliance (SRI, retención de datos) se documentarán aquí cuando aplique.
