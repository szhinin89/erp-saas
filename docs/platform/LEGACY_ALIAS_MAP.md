# Platform Control Plane — Legacy Alias Map

**Propósito:** registro histórico de aliases SuperAdmin/SaaS → Platform canónico.

**Estado (2026-05-25):** el código de producto ya **no** contiene wire legacy, redirects ni aliases JSON. Este archivo es solo referencia histórica; implementar siempre nombres canónicos (`TEAM-NAMING-GUIDE.md`).

**Implementación actual:** [`TEAM-NAMING-GUIDE.md`](./TEAM-NAMING-GUIDE.md) · [`CANONICAL-ROUTES.md`](./CANONICAL-ROUTES.md)

**Para desarrolladores:** en código de producto usar siempre **platform / operador platform / `PlatformOperator`**. Los literales wire legacy (`SuperAdmin` en JWT antiguo, keys JSON antiguas) viven **solo** en `PlatformAuthConstants.cs` y `frontend/src/constants/platformAuth.ts`.

## API routes

| Legacy (eliminado) | Canónico | Status |
|--------------------|----------|--------|
| `/api/superadmin/*` | `/api/platform/*` | ✅ Eliminado Phase 4 |
| `POST /api/auth/superadmin-login` | `POST /api/platform/auth/login` | ✅ Eliminado |
| `POST /api/setup/superadmin` | `POST /api/setup/platform-operator` | ✅ Eliminado |
| `/api/admin/iam/superadmin/subscribers*` | `/api/platform/subscribers` | ✅ Bloqueado |
| `/api/admin/iam/register-tenant`, `/api/admin/iam/tenant/*` | `/api/admin/iam/register-subscriber`, `subscriber/*` | ✅ Eliminado (2026-05-25) |

## Backend types / files

| Legacy alias | Canónico | Status |
|--------------|----------|--------|
| `SuperAdminController` | `Platform*Controller` | ✅ Eliminado |
| `SuperAdminService` | Application + Platform controllers | ✅ Eliminado |
| JWT literal `SuperAdmin` | `PlatformOperator` (`PlatformAuthConstants.JwtPlatformOperatorRole`) | ✅ Canónico; wire legacy solo lectura |
| `SuperAdminPanelLockMiddleware` | `PlatformPanelLockMiddleware` | ✅ Renombrado |
| `SaasPlan` (file) | `CommercialPlan` (class) | ✅ Archivos admin renombrados (2026-05-25) |
| `TenantSaasSubscription` (file) | `SubscriberSubscription` (class) | ✅ Ya canónico (`SubscriberSubscription.cs`) |
| `SaasFeatureDefinition` (file) | `PlatformFeature` (class) | ✅ Archivo `PlatformFeatureKind.cs` + entidad canónica |
| `SaasPlansAdminService` / `ISaas*` | `CommercialPlansAdminService` / `ICommercial*` | ✅ Renombrado (2026-05-25) |
| `SaasEntitlementsOptions` | `SubscriberEntitlementsOptions` | ✅ Renombrado; sección config `Saas:Entitlements` estable |
| `UpdateSubscriberSaasProfile` | `UpdateSubscriberCommercialProfile` | ✅ Renombrado (2026-05-25) |
| `TenantCustomMenu` (file) | `SubscriberCustomMenu` (class) | ✅ Renombrado (2026-05-25) |

## Frontend (código)

| Legacy | Canónico | Status |
|--------|----------|--------|
| `modules/superadmin/` | `modules/platform/` | ✅ Renombrado Phase 5b |
| `components/superadmin/` | `components/platform/` | ✅ Renombrado |
| `pages/SuperAdmin/` | `pages/Platform/` | ✅ Renombrado |
| `superAdminService` | `platformService` | ✅ Eliminado |
| `useSuperAdmin` / `isSuperAdmin` | `usePlatformGate` / `isPlatformOperator` | ✅ Renombrado (2026-05-23) |
| `SUPERADMIN_UI` | `PLATFORM_UI` (`/platform/*`) | ✅ Renombrado |
| i18n `superadmin.*` | `platform.*` | ✅ Migrado (2026-05-23) |
| `Crear-SuperAdmin.ps1` | `Crear-PlatformOperator.ps1` | ✅ Eliminado alias script |

## Frontend — contrato auth (única fuente)

Archivo: `frontend/src/constants/platformAuth.ts`

| Constante / helper | Uso |
|--------------------|-----|
| `JWT_PLATFORM_OPERATOR_ROLE` | Rol canónico (`PlatformOperator`) |
| `isJwtPlatformOperatorRole()` | Comparación de rol (canónico + wire legacy interno) |
| `NAV_PLATFORM_OPERATOR_ROLE` | Arrays `roles` en navegación |
| `DEPLOYMENT_API_PLATFORM_PANEL_FLAG` | Lee `platformPanelEnabled` del API público |
| `NAV_API_PLATFORM_PANEL_FLAG` | Campo JSON menú `requirePlatformPanel` |
| `readsRequirePlatformPanel()` | Lee flag menú (canónico + legacy wire) |
| `PLATFORM_UI_LEGACY_PATH_PREFIX` | Redirect `/superadmin/*` → `/platform/*` |

## UI / producto (alias activos intencionales)

| Alias activo | Canónico conceptual | Migración |
|--------------|---------------------|-----------|
| URL `/superadmin/*` | Redirect a `/platform/*` | ✅ Redirect en `platformRoutes.tsx` |
| JWT wire `SuperAdmin` | `PlatformOperator` | ✅ Solo lectura en tokens/BD viejos |
| Flag `superAdminPanelEnabled` | `platformPanelEnabled` | ✅ Alias JSON en GET `/api/public/deployment` |
| JSON `requireSuperAdminPanel` | `requirePlatformPanel` | ✅ Alias JSON en menú sesión/admin |
| CSS prefix `sa-*` | Platform shell styles | Cosmético (prefijo histórico) |

## DB (sin rename destructivo)

| Concepto legacy | Tabla actual | Notas |
|-----------------|--------------|-------|
| Tenant | `subscribers` | Terminología unificada a Subscriber |
| Outbox legacy column | `OutboxMessages.SubscriberId` | Renombrado desde `TenantId` (migración `20260525222816`) |
| SaaS plan | `commercial_plans` | No `saas_plans` |
| Platform audit | `platform_audit_logs` | Renombrado Phase 4 |
| Columna `ui_nav_groups.require_platform_panel` | Grupo visible solo con panel platform; propiedad EF `RequirePlatformPanel` |

## CI enforcement

Patrones bloqueados en build: ver [`CI_GUARD_RULES.md`](./CI_GUARD_RULES.md) y `tools/ci/platform-guard-config.json`.

- `isSuperAdmin` — prohibido en `frontend/src`
- Literal `'SuperAdmin'` — solo permitido en `platformAuth.ts`
- Imports `modules/superadmin`, `useSuperAdmin`, `/api/superadmin/` — prohibidos
