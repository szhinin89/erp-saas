# Auth Rules (adaptador)

> **Canónico:** [`AI-RULES/SECURITY.md`](AI-RULES/SECURITY.md) · Detalle: [`docs/IDENTITY.md`](docs/IDENTITY.md)

## Resumen

| Token | Almacenamiento | Rotación |
|-------|----------------|----------|
| Access JWT | Memoria SPA | Corta |
| Refresh opaco | Cookie httpOnly `Path=/api` | Family chain |

- Refresh rotation: Web Locks + BroadcastChannel; ver `authRefreshManager`
- Login unificado: `/api/auth/login` (detalle en `docs/IDENTITY.md`)
- Prohibido: refresh en localStorage/sessionStorage; `?tenantId=` en URLs

Detalle completo: [`AI-RULES/SECURITY.md`](AI-RULES/SECURITY.md).
