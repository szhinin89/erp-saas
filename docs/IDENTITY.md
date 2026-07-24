# Identidad, auth y seguridad

IAM único sobre `identity_users`. No existe tabla `users` en el estado final.

Relacionado: [ARCHITECTURE.md](./ARCHITECTURE.md), [DATABASE.md](./DATABASE.md#rls).

---

## Modelo backend (`identity_users`)

Entidad `IdentityUser` (`ERP.Domain.Access.Entities`):

| Columna | Descripción |
|---------|-------------|
| `first_name` / `last_name` | Nombre del usuario |
| `email` / `email_normalized` | Email (índice único en normalizado) |
| `password_hash` | Hash BCrypt |
| `is_active` | Habilita/deshabilita login |
| `security_stamp` | Revocación de sesiones (refresh tokens) |
| `require_password_reset` | Reset forzado post-migración |
| `tenant_id` (`[Obsolete]`) | Legado single-tenant; no usar en guards/policies nuevos |

- Único factory: `IdentityUser.Create(firstName, lastName, email, passwordHash, createdBy)`.
- Acceso ERP vía `company_user_memberships` (`IdentityUser` → `CompanyUserMembership` → `Company` → tenant/subscriber).
- `IdentityUserType` (enum `Platform=0`, `Tenant=1`, `Company=2`) existe en el dominio pero **no es columna de `identity_users`** y no se usa en guards/policies — reservado para una futura plataforma externa (ver [`docs/future-platform/`](./future-platform/)).

---

## Autenticación

| Mecanismo | Detalle |
|-----------|---------|
| Sesión | JWT (`sub`, `tenant_id`, `role`, opcional `tenant_ids`) |
| Refresh | `refresh_tokens`, `POST /api/auth/refresh` |
| First-run | `GET /api/setup/status` + `POST /api/setup/admin` (banner consola en arranque API, token-gated) |
| Login ERP | `POST /api/auth/login` → membership + `companyId` en `AuthResponseDto` (no en el JWT) |

### Endpoints (`AuthController`, base `/api/auth`)

| Método | Ruta | Uso |
|--------|------|-----|
| POST | `/api/auth/login` | Login |
| POST | `/api/auth/refresh` | Refresh único |
| POST | `/api/auth/logout` | Cerrar sesión |
| POST | `/api/auth/forgot-password` | Solicitud reset (token por email) |
| POST | `/api/auth/reset-password` | Completar reset con token |
| GET | `/api/auth/my-companies` | Empresas accesibles del usuario |
| POST | `/api/auth/switch-company` | Cambiar empresa activa |

**Fase S1 (hardening, 2026-07-17) — eliminados permanentemente:**
- `POST /api/auth/register` (creaba un usuario con `TenantId`/`Role` arbitrarios del body, sin autenticación — permitía crear un Admin en cualquier tenant existente). El alta del primer usuario/tenant vive únicamente en el flujo First-run (`POST /api/setup/admin`), que nunca acepta `TenantId`/`Role` del cliente y requiere el token de instalación de un solo uso.
- `POST /api/auth/password-reset` (cambiaba la contraseña de cualquier usuario solo con `TenantId`+`Email`, sin contraseña actual, token ni OTP). El reset de contraseña queda únicamente vía `forgot-password`+`reset-password` (token por email, de un solo uso).
- Guardado como regresión permanente por `ERP.Architecture.Tests.AuthAttackSurfaceGuardTests`.

Sesión runtime: menú `GET /api/me/menu` (`IRequiresCompanyContext` — requiere empresa activa validada por `CompanyScopeBehavior`, ver [Flujo login multi-empresa](#flujo-login-multi-empresa)); permisos `GET /api/admin/iam/me/permissions`; first-run `GET /api/setup/status`, `POST /api/setup/admin`.

Servicios: `IAccessTokenService`, `IRefreshTokenService`, `IPasswordHasher` (BCrypt).

> Rutas `/api/platform/auth/*`, `/api/platform/subscribers`, `/api/auth/switch-subscriber` y `/api/admin/iam/bootstrap-switch-subscriber` **no existen** en el código actual — eliminadas/renombradas en FASE 1 / FASE 4. No usar como referencia.

### JWT claims (session)

Generados por `AccessTokenService` (`ERP.Infrastructure.Services`):

| Claim | Valor | Cuándo |
|-------|-------|--------|
| `sub` | `identity_user_id` | Siempre |
| `tenant_id` | tenant activo (`Guid.Empty` si pendiente de selección de empresa) | Siempre |
| `role` (`ClaimTypes.Role`) | rol de membresía (`membership.Role`) o `Bootstrap` | Siempre |
| `tenant_ids` | tenants accesibles del usuario (lista separada por comas) | Solo token bootstrap (rol `Bootstrap`) |

No existen claims `user_type`, `platform_role`, `company_id`, `company_role` ni `token_type` en el JWT actual.

---

## Frontend

### `authService` (`frontend/src/modules/auth/api/authService.ts`)

```typescript
authService.loginUser(credentials)      // POST /api/v1/auth/login
authService.refresh(refreshToken?)      // POST /api/v1/auth/refresh
authService.listMyCompanies()           // GET  /api/v1/auth/my-companies
authService.switchCompany(companyId)    // POST /api/v1/auth/switch-company
```

### Login (`LoginPage`)

1. `loginUser` → si `RequiresCompanySelection`, redirige a `/select-company`.
2. Si falla → bootstrap IAM (`/api/admin/iam/bootstrap-login`).

### Guards

- **ERP**: requiere `companyId` válido en sesión (o redirige a `/select-company`).

---

## Autorización

| Capa | Componente |
|------|------------|
| Policies | claims `perm:*` |
| ERP | `CompanyScopeBehavior` + `ICompanyAccessGuard` |
| Billing | `BillingGateBehavior` |
| Features | `SubscriptionGateBehavior` + entitlements |

Pipeline MediatR: billing → subscription → company scope → cache.

**Fase S1 (hardening, 2026-07-17)**: `GetCompanyUserMembershipsAdminQuery`, `GetCompanyUserPreferencesAdminQuery`, `UpdateCompanyUserPreferencesAdminCommand`, `GetCompanyUserBranchesAdminQuery` y `UpdateCompanyUserBranchesAdminCommand` no implementaban ningún marker de `CompanyScopeBehavior` — su única defensa era un chequeo manual contra `ICurrentCompany.CompanyId` (header `X-Company-Id`, no un claim firmado), sin pasar nunca por `ICompanyAccessGuard`, y sin que el bypass de rol Admin (`RuntimePermissionAuthorizer`) revalidara tenant/membership real (hallazgo 5C de la auditoría de cierre de Access/IAM). Los cinco ahora implementan `IRequiresCompanyContext` — mismo marker que `UpsertCompanyUserMembershipAdminCommand`/`RevokeCompanyUserMembershipAdminCommand` (Fase I-A), sin inventar un mecanismo nuevo. El chequeo manual original se mantiene como defensa adicional, no se retiró.

---

## Defensa en profundidad

1. Validación JWT
2. Behaviors MediatR
3. Filtros globales EF (`EnterpriseQueryFilterConfigurator`)
4. PostgreSQL RLS

### Variables sesión PostgreSQL

```sql
app.tenant_id
app.company_id
```

Interceptor / applicator: `DbSessionContextApplicator` (`ISessionContext`).

### Rate limiting

Política `per-tenant` (`Program.cs`).

### Observabilidad

- `EnterpriseDiagnosticMiddleware`: tenant, company, user, correlation
- `ForbiddenAccessLoggingMiddleware`: audit 401/403

---

## Prohibido

- `company_id` del body sin JWT + membership
- `IgnoreQueryFilters()` nuevo fuera de la allowlist de `IgnoreQueryFiltersAuditTests`
- SDK de pagos en handlers MediatR
- Migraciones EF escritas a mano

---

## Flujo login multi-empresa

1. Login → `tenant_id` / `tenant_ids` en el JWT
2. Una company → `AuthResponseDto.CompanyId` con la empresa activa
3. Varias → `AuthResponseDto.CompanyId = null`, `RequiresCompanySelection=true` → `/select-company` → `switch-company` → `AuthResponseDto.CompanyId` con la empresa elegida
4. Frontend envía `companyId` activo como header `X-Company-Id`; módulos ERP lo validan vía `CompanyScopeBehavior` (`ICompanyAccessGuard`) contra la membresía antes de llegar a los handlers — el JWT no transporta `company_id`

```
Login
 |
 +-- Una empresa ------------------------------> Empresa activa
 |
 +-- Varias empresas -> /select-company -> switch-company -> CompanyScopeBehavior -> /api/me/menu -> ERP operativo
```

### Estado "pendiente de selección de empresa" (`RequiresCompanySelection`)

- Estado de sesión separado del ERP operativo: usuario autenticado pero sin `company_id` validado en el JWT.
- Frontend: `/select-company` está en `publicRoutes`, fuera de `ProtectedRoute`/`AppLayout` — en este estado no se invoca `GET /api/me/menu` ni ningún caso de uso operativo del ERP.
- `GET /api/me/menu` (navegación server-driven) implementa `IRequiresCompanyContext`: `CompanyScopeBehavior` valida `X-Company-Id` contra la membresía activa del usuario (`ICompanyAccessGuard.RequireCurrentCompanyAsync`) **antes** de que el handler construya el menú.
- La navegación server-driven (menú lateral) solo está disponible con una empresa activa y validada (post `switch-company`); no existe un "menú limitado" para sesiones pendientes de selección.

Smoke post-cambios: [DEVELOPMENT.md](./DEVELOPMENT.md#verificación-manual).

---

## UserSession (Contexto Operativo del Usuario)

Registro de la sesión operativa (empresa + sucursal + terminal) asociada a un login, independiente del `RefreshToken` (`ERP.Domain.Access.Entities.UserSession`, tabla `user_sessions`).

- **Relación con RefreshToken**: unidireccional — `UserSession.RefreshTokenId` referencia al refresh token vigente; `RefreshToken` nunca conoce `UserSession`. `IRefreshTokenService.CreateWithoutSaveAsync` permite crear ambos en una sola transacción (`CreateAuthenticatedSessionCommand`).
- **Invariante**: máximo una `UserSession` con `Status=Active` por `(tenant, empresa, usuario)` — índice único parcial `ux_user_sessions_active_per_company`. Un nuevo login en la misma empresa cierra la anterior (`CloseByNewLogin`), nunca por tenant completo.
- **Alta**: `LoginHandler`/`SwitchCompanyHandler` resuelven la sucursal vía `CompanyUserPreferencesLoginResolver` (ver sección siguiente): si `CompanyUserPreferences.LoginMode=DirectToDefault` y la sucursal sigue autorizada, se usa esa sucursal directamente; en cualquier otro caso (AskBranch, sin preferencias, o revalidación fallida) cae al heurístico interino (única `Branch.IsMainBranch=true` activa de la empresa). Crean la sesión internamente vía `CreateAuthenticatedSessionCommand` (mediator interno, nunca expuesto por un controller público); si ninguna de las dos vías resuelve una sucursal, el login continúa igual sin crear `UserSession`.
- **Baja**: expiración por antigüedad (`ExpireUserSessionsJob`, Hangfire diario, `SessionExpirationOptions.MaxSessionAgeDays`) o cierre administrativo (`AdminUserSessionController`, ver abajo). No revoca el `RefreshToken` — sigue siendo responsabilidad exclusiva de `/auth/logout`.
- **Administración**: `AdminUserSessionController` (`GET/POST /api/v1/admin/access/sessions*`, permisos `access.sessions.view`/`access.sessions.close`), UI en `/admin/access/sessions`. Única superficie HTTP de este dominio — es la fuente de verdad para consultar/cerrar sesiones.
- **Hardening (Fase 12, 2026-07-17)**: existió un `UserSessionController` self-service (`api/v1/access/sessions*`, `api/v1/access/company-user-branches*`) que aceptaba `TenantId`/`CompanyId`/`IdentityUserId` como datos del cliente bajo solo `[Authorize]` (IDOR) y no tenía consumidores reales (ni frontend, ni Login/SwitchCompany, que siempre usaron `CreateAuthenticatedSessionCommand` directo). Se eliminó por completo — controller, sus 6 Commands/Queries exclusivos y sus DTOs huérfanos (`CompanyUserBranchDto`, `CurrentUserSessionDto`) — en vez de endurecerlo, por no tener ningún caso de uso real que justificara mantenerlo. La entidad `CompanyUserBranch` (Domain/Infrastructure/migración) se dejó intacta por no ser en sí un riesgo de seguridad y por la regla de no tocar Domain sin necesidad estricta.
  - **Actualización (2026-07-17, ciclo CompanyUserPreferences)**: `CompanyUserBranch` dejó de estar huérfana — `UpsertCompanyUserMembershipHandler` es hoy el único flujo de producción que la escribe (autoriza sucursales al dar de alta/editar una membresía), y `CompanyUserPreferencesDefaultBranchValidation` la usa para validar `DefaultBranchId` en cada creación/actualización/relogin. Sigue siendo la única fuente de verdad de "sucursales autorizadas" — nunca se le agregó comportamiento nuevo, solo consumidores. Ver sección siguiente.

## CompanyUserPreferences (preferencias operativas de login)

Ciclo completo cerrado (Fases A–H, 2026-07-17). Entidad `ERP.Domain.Access.Entities.CompanyUserPreferences` (tabla `company_user_preferences`), **única fuente de verdad** de dos datos: `DefaultBranchId` (`Guid?`) y `LoginMode` (`CompanyUserLoginMode`: `AskBranch` | `DirectToDefault`). Relación 1:1 con `CompanyUserMembership` (índice único `ux_company_user_preferences_membership`). No existe ninguna copia de estos dos campos en `CompanyUserMembership`, `UserSession`, claims del JWT, DTOs de frontend ni stores Zustand — verificado explícitamente en la auditoría de Fase H.

**Separación de responsabilidad con `CompanyUserBranch`** (INMUTABLE, no reabrir sin ADR): `CompanyUserBranch` responde únicamente "¿a qué sucursales puede ingresar el usuario?" (autorización). `CompanyUserPreferences` responde únicamente "¿cómo debe iniciar sesión?" (preferencia de arranque). `CompanyUserPreferences` **nunca** concede autorización — su `DefaultBranchId` se valida en cada escritura/relectura contra `CompanyUserBranch` y contra `Branch.IsActive` (ver hallazgo de Fase H más abajo).

- **Escritura**: dos entradas, ambas delegando en los mismos UseCases (`CreateCompanyUserPreferencesCommand`/`UpdateCompanyUserPreferencesCommand`), sin duplicar su validación:
  - `UpsertCompanyUserMembershipHandler` — al dar de alta una membresía nueva crea preferencias con default `AskBranch`/sin sucursal si el llamador no informó nada (nunca deja una membresía sin fila); al reactivar/editar una existente, solo toca preferencias si se informó explícitamente `LoginMode`/`DefaultBranchId` (editar rol/perfil nunca resetea una configuración de login ya definida). También es el único flujo de producción que autoriza sucursales en `CompanyUserBranch` (mismo handler, todo-o-nada, idempotente).
  - `UpdateCompanyUserPreferencesAdminHandler` (`PUT /api/v1/admin/iam/company-users/{companyUserId}/preferences`, permiso `access.company_user_memberships.view`) — delega 100% en `UpdateCompanyUserPreferencesCommand`; el único agregado propio es verificar que la membresía pertenezca a la empresa operativa actual del administrador (aislamiento multi-tenant, ver abajo).
- **Lectura**: patrón único, sin heurísticas compitiendo — `GetCompanyUserPreferencesQuery` es la única consulta; `CompanyUserPreferencesLoginResolver` (`ERP.Application.Auth.UseCases`) es el único punto donde Login/SwitchCompany la invocan, justo después de resolver `CompanyUserMembership` y antes de crear la `UserSession`. `GetCompanyUserPreferencesAdminHandler` (`GET` del mismo endpoint) delega en la misma Query, agregando solo el chequeo de empresa.
- **Aislamiento multi-tenant**: verificado en Fase H — un administrador nunca puede leer/modificar preferencias de una membresía de otra empresa (`GetCompanyUserPreferencesAdminHandler`/`UpdateCompanyUserPreferencesAdminHandler` comparan `membership.CompanyId` contra `ICurrentCompany.CompanyId`; mismo mensaje `NotFound` para "no existe" y "es de otra empresa", sin fuga de información). Login/SwitchCompany no pasan por este chequeo (usan las Queries/Commands de Fase C directamente, sin marcador de scope) — deliberado, porque en el momento de Login/SwitchCompany el contexto de empresa activo puede no coincidir todavía con la empresa destino.
- **Estados y su comportamiento** (auditoría de Fase H):
  1. `LoginMode=DirectToDefault` con `DefaultBranchId=null` — **imposible por invariante de dominio**: `CompanyUserPreferences.Create`/`ChangeLoginMode` lanzan `ArgumentException` si se intenta esa combinación.
  2. `DefaultBranchId` apunta a una sucursal desactivada (soft-delete, nunca DELETE físico) — **corregido en Fase H**: `CompanyUserPreferencesDefaultBranchValidation` ahora exige `Branch.IsActive`, no solo existencia (antes de este fix, una sucursal inactiva pasaba la validación).
  3. El usuario pierde su autorización en `CompanyUserBranch` después de configurar `DirectToDefault` — Login/SwitchCompany revalidan en cada intento (reenviando `UpdateCompanyUserPreferencesCommand` con los mismos valores, mutación idempotente) y **fallan explícitamente con `ValidationFailure`** si la sucursal ya no está autorizada, en vez de caer silenciosamente al heurístico. Aceptado como comportamiento correcto (fail-closed); una futura fase de UX podría ofrecer un fallback más amigable, no es un bug.
  4. Membresía histórica sin fila de preferencias (creada antes de Fase D) — `GetCompanyUserPreferencesQuery` devuelve `null`; el resolver de login lo trata igual que `AskBranch` (heurístico interino). Se "autosana" la próxima vez que `UpsertCompanyUserMembershipHandler` procese esa membresía (crea la fila con los defaults).
- **Frontend**: dos superficies independientes reutilizan el mismo schema (`companyUserPreferencesSchema`) y servicio (`companyUserPreferencesService`), sin componente compartido: la sección "Preferencias" original de `SecuritySettingsPage.tsx` (`/admin/security`) y el modal de preferencias de `UsersPage.tsx` (`/admin/users`, Fase I-C) — ambas gateadas por `access.company_user_memberships.view`. `SecurityUserDto` se amplió con `CompanyUserMembershipId` (antes solo exponía `IdentityUser.Id`) porque era el único dato que faltaba para poder invocar el endpoint administrativo desde `SecuritySettingsPage`. Ver `docs/STATUS.md` (Fase I-A/I-B/I-C) para la administración completa de `CompanyUserMembership`/`CompanyUserBranch` agregada después de este cierre, no documentada en detalle en esta sección.
- **JWT**: sin cambios — no se agregó `branch_id` ni ningún claim nuevo; la sucursal resuelta viaja únicamente como parámetro de `CreateAuthenticatedSessionCommand` hacia `UserSession`.
