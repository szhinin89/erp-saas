# Identidad, auth y seguridad

IAM único sobre `identity_users`. No existe tabla `users` en el estado final.

Relacionado: [ARCHITECTURE.md](./ARCHITECTURE.md), [SAAS-COMMERCIAL.md](./SAAS-COMMERCIAL.md), [DATABASE.md](./DATABASE.md#rls).

---

## Modelo backend (`identity_users`)

| Columna | Descripción |
|---------|-------------|
| `user_type` | `Platform` \| `Company` \| `Subscriber` (future) |
| `platform_role` | `SuperAdmin` (JWT legacy) \| `Support` \| `BillingAdmin` (nullable) — ver `PlatformAuthConstants` |
| `subscriber_id` | Hint opcional; **Platform debe ser NULL** |
| `email_normalized` | Índice único |
| `security_stamp` | Revocación de sesiones |
| `require_password_reset` | Reset forzado post-migración |

```sql
CHECK (user_type <> 'Platform' OR subscriber_id IS NULL)
```

- Usuarios platform no requieren `company_id`.
- Acceso ERP vía `company_user_memberships` (`user_type=Company`).
- Bypass ERP solo operador platform global (`subscriber_id=Guid.Empty`, rol JWT `SuperAdmin`).

Factories: `CreateCompanyUser`, `CreatePlatformOperator` (tipo dominio; producto = **platform operator**).

---

## Autenticación

| Mecanismo | Detalle |
|-----------|---------|
| Sesión | JWT (`subscriber_id`, `company_id`, `user_type`, claims) |
| Refresh | `refresh_tokens`, `POST /api/auth/refresh` |
| First-run | `POST /api/setup/superadmin` + token efímero (banner / `Crear-SuperAdmin.ps1`) |
| Platform | `POST /api/platform/auth/login` |
| Company ERP | `POST /api/auth/login` → membership + `company_id` |

### Endpoints

| Método | Ruta | Uso |
|--------|------|-----|
| POST | `/api/platform/auth/login` | Operadores platform |
| POST | `/api/auth/login` | Usuarios company |
| POST | `/api/auth/refresh` | Refresh único |
| POST | `/api/auth/forgot-password` | Solicitud reset |
| POST | `/api/auth/reset-password` | Completar reset |
| POST | `/api/setup/superadmin` | First-run operador platform (script `Crear-SuperAdmin.ps1`) |
| POST | `/api/auth/switch-company` | Cambiar empresa activa |
| POST | `/api/admin/iam/switch-subscriber` | Elegir subscriber (alias legacy) |

Aliases `[Obsolete]` delegan a handlers canónicos.

Servicios: `IAccessTokenService`, `IRefreshTokenService`, `IPasswordHasher` (BCrypt).

### JWT claims (session)

| Claim | Operador platform (JWT global) | Company user |
|-------|------------------------------|--------------|
| `sub` | `identity_user_id` | `identity_user_id` |
| `user_type` | `Platform` | `Company` |
| `platform_role` | `SuperAdmin` (literal legacy) | — |
| `subscriber_id` | vacío o impersonation | subscriber activo |
| `company_id` | opcional (impersonation) | empresa operativa |
| `token_type` | `bootstrap` \| `session` | |

---

## Frontend

### `authService`

```typescript
authService.loginUser(credentials)      // POST /api/auth/login
authService.loginPlatform(credentials)  // POST /api/platform/auth/login
authService.refresh(refreshToken?)      // POST /api/auth/refresh
```

### Login (`LoginPage`)

1. `loginUser`
2. Si falla y `platformPanelEnabled` (alias API `superAdminPanelEnabled`) → `loginPlatform`
3. Si falla → bootstrap IAM (`/api/admin/iam/bootstrap-login`)

### Guards

- **`usePlatformGate`**: operador platform (`userType=Platform` + rol JWT o impersonación)
- **ERP**: requiere `companyId` (o `/select-company`)

`/api/platform/auth/login` en `PUBLIC_AUTH_PATHS` (sin refresh loop).

Panel platform: `/api/platform/*` vía **`platformService`** (frontend). Contrato JWT: `frontend/src/constants/platformAuth.ts`.

---

## Autorización

| Capa | Componente |
|------|------------|
| Policies | claims `perm:*` |
| ERP | `CompanyScopeBehavior` + `ICompanyAccessGuard` |
| Billing | `BillingGateBehavior` |
| Features | `SubscriptionGateBehavior` + entitlements |

Pipeline MediatR: billing → subscription → company scope → cache.

---

## Defensa en profundidad

1. Validación JWT
2. Behaviors MediatR
3. Filtros globales EF (`EnterpriseQueryFilterConfigurator`)
4. PostgreSQL RLS
5. Bypass operador platform (`app.is_platform_admin`)

### Variables sesión PostgreSQL

```sql
app.subscriber_id
app.company_id
app.is_platform_admin   -- 'true' solo platform admin
```

Interceptor: `PostgreSqlSessionContextInterceptor`.

### Rate limiting

`per-subscriber`: **600 req/min** (configurable en `Program.cs`).

### Observabilidad

- `EnterpriseDiagnosticMiddleware`: subscriber, company, user, correlation
- `ForbiddenAccessLoggingMiddleware`: audit 401/403

---

## Prohibido

- `company_id` del body sin JWT + membership
- `IgnoreQueryFilters()` sin `PlatformQueryReason`
- SDK de pagos en handlers MediatR
- Tablas billing SaaS con `company_id`
- Límites comerciales hardcodeados
- Migraciones EF escritas a mano

---

## Flujo login multi-empresa

1. Login → `subscriber_id` en token
2. Una company → auto `company_id`
3. Varias → `/select-company` → `switch-company`
4. Módulos ERP requieren `company_id` en JWT

Smoke post-cambios: [DEVELOPMENT.md](./DEVELOPMENT.md#verificación-manual).
