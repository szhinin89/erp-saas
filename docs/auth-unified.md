# Auth Unified Pipeline

Un solo stack de autenticación sobre `identity_users`.

## Endpoints canónicos

| Método | Ruta | Uso |
|--------|------|-----|
| POST | `/api/platform/auth/login` | SuperAdmin / operadores platform |
| POST | `/api/auth/login` | Usuarios company (ERP runtime) |
| POST | `/api/auth/refresh` | **Único** refresh (platform + company) |
| POST | `/api/auth/forgot-password` | Solicitud reset |
| POST | `/api/auth/reset-password` | Completar reset |
| POST | `/api/setup/superadmin` | First-run → crea platform user |

## Aliases legacy (delegación)

| Alias | Delega a |
|-------|----------|
| `POST /api/auth/superadmin-login` | `PlatformLoginHandler` vía MediatR |
| Rutas `[Obsolete]` platform | handlers MediatR canónicos |

## Servicios

- **`IAccessTokenService`** — emite JWT con `user_type`, `platform_role`, `subscriber_id`, `company_id`.
- **`IRefreshTokenService`** — tipos `Platform` | `Identity` (legacy `SuperAdmin`/`Legacy` rechazados en refresh).
- **`IPasswordHasher`** — BCrypt único.

## Autorización

- Platform API: policy `GlobalSuperAdmin` (`user_type=Platform`, `platform_role=SuperAdmin`, `subscriber_id=Empty`).
- ERP API: membership activa + `company_id` en JWT.

## Seguridad

- Platform users no pueden fijar `subscriber_id`/`company_id` desde body en endpoints platform.
- Setup token one-time (`first_run_setup_state`).
- Refresh rotation en cada uso.
