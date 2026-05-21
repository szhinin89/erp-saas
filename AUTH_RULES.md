# Auth Rules

> Canónico: [`docs/IDENTITY.md`](docs/IDENTITY.md) · Detalle rotation: [`security/auth/refresh-rotation.md`](security/auth/refresh-rotation.md)

## Tokens

| Token | Almacenamiento | Rotación |
|-------|----------------|----------|
| Access JWT | Memoria SPA (+ espejo Zustand no persistido) | Corta (config JWT) |
| Refresh opaco | Cookie **httpOnly** `Path=/api` | Cada uso (family chain) |

## Refresh rotation

- **FamilyId** por sesión/dispositivo
- Reuso benigno (< grace) → 401 sin revocar familia
- Reuso sospechoso → `RevokeFamilyAsync` (no logout global)
- Multi-tab: Web Locks `erp-refresh` + BroadcastChannel `erp.auth`
- **Nunca** compartir refresh token entre tabs

## Logout

`fullLogout()` — stores, sessionStorage, BC `logout`, cookie vía API.

## Platform

SuperAdmin: `/api/platform/auth/login` · cookie compatible con `/api/auth/refresh`.

## Prohibido

- Refresh token en localStorage/sessionStorage
- `?tenantId=` en URLs compartibles
- Revocar todos los tokens del usuario en replay de una sola familia
