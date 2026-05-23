# Platform Control Plane — Legacy Alias Map

**Propósito:** mapa de aliases históricos SuperAdmin/SaaS → Platform canónico.

**Para desarrolladores nuevos:** en código de producto usar siempre **platform / operador platform**. El literal JWT `SuperAdmin` vive **solo** en `frontend/src/constants/platformAuth.ts` (y tests que validan el contrato auth).

## API routes

| Legacy (eliminado) | Canónico | Status |
|--------------------|----------|--------|
| `/api/superadmin/*` | `/api/platform/*` | ✅ Eliminado Phase 4 |
| `POST /api/auth/superadmin-login` | `POST /api/platform/auth/login` | ✅ Eliminado |
| `/api/admin/iam/superadmin/subscribers*` | `/api/platform/subscribers` | ✅ Bloqueado |

## Backend types / files

| Legacy alias | Canónico | Status |
|--------------|----------|--------|
| `SuperAdminController` | `Platform*Controller` | ✅ Eliminado |
| `SuperAdminService` | Application + Platform controllers | ✅ Eliminado |
| JWT literal `SuperAdmin` | `PlatformOperator` (`PlatformAuthConstants`) | ✅ Canónico; legacy aceptado en lectura |
| `SuperAdminPanelLockMiddleware` | `PlatformPanelLockMiddleware` | ✅ Renombrado |
| `SaasPlan` (file) | `CommercialPlan` (class) | ⚠️ Archivo pendiente rename |
| `TenantSaasSubscription` (file) | `SubscriberSubscription` (class) | ⚠️ Archivo pendiente rename |
| `SaasFeatureDefinition` (file) | `PlatformFeature` (class) | ⚠️ Archivo pendiente rename |
| `TenantCustomMenu` (file) | `SubscriberCustomMenu` (class) | ⚠️ Archivo pendiente rename |

## Frontend (código)

| Legacy | Canónico | Status |
|--------|----------|--------|
| `modules/superadmin/` | `modules/platform/` | ✅ Renombrado Phase 5b |
| `components/superadmin/` | `components/platform/` | ✅ Renombrado |
| `pages/SuperAdmin/` | `pages/Platform/` | ✅ Renombrado |
| `platformService.ts` | `platformService.ts` | ✅ Eliminado |
| `usePlatformGateGate` / `isSuperAdmin` | `usePlatformGate` / `isPlatformOperator` | ✅ Renombrado (2026-05-23) |
| `PlatformLayout` | `PlatformLayout` | ✅ Renombrado |
| `SUPERADMIN_UI` | `PLATFORM_UI` (`/platform/*`) | ✅ Renombrado |
| i18n `superadmin.*` | `platform.*` | ✅ Migrado (2026-05-23) |
| `LEGACY_PLATFORM_API` | `PLATFORM_API` | ✅ Eliminado |

## Frontend — contrato auth (única fuente)

Archivo: `frontend/src/constants/platformAuth.ts`

| Constante / helper | Uso |
|--------------------|-----|
| `JWT_PLATFORM_OPERATOR_ROLE` | Literal JWT del backend (`SuperAdmin`) |
| `isJwtPlatformOperatorRole()` | Comparación de rol en UI |
| `NAV_PLATFORM_OPERATOR_ROLE` | Arrays `roles` en navegación |
| `DEPLOYMENT_API_PLATFORM_PANEL_FLAG` | Lee `platformPanelEnabled` del API público |
| `NAV_API_PLATFORM_PANEL_FLAG` | Campo JSON menú `requirePlatformPanel` |
| `readsRequirePlatformPanel()` | Lee flag menú (canónico + legacy) |
| `PLATFORM_UI_LEGACY_PATH_PREFIX` | Redirect `/superadmin/*` → `/platform/*` |

## UI / producto (alias activos intencionales)

| Alias activo | Canónico conceptual | Migración |
|--------------|---------------------|-----------|
| URL `/superadmin/*` | Redirect a `/platform/*` | ✅ Redirect en `platformRoutes.tsx` |
| JWT role `SuperAdmin` | `PlatformOperator` | ✅ Legacy aceptado en tokens/menús |
| Flag `superAdminPanelEnabled` | `platformPanelEnabled` | ✅ Alias JSON en GET `/api/public/deployment` |
| JSON `requirePlatformPanel` | `requirePlatformPanel` | ✅ Alias JSON en menú sesión/admin |
| CSS prefix `sa-*` | Platform shell styles | Cosmético |

## DB (sin rename destructivo)

| Concepto legacy | Tabla actual | Notas |
|-----------------|--------------|-------|
| Tenant | `subscribers` | Terminología unificada a Subscriber |
| SaaS plan | `commercial_plans` | No `saas_plans` |
| SuperAdmin audit | `platform_audit_logs` | Renombrado Phase 4 |

## CI enforcement

Patrones bloqueados en build: ver [`CI_GUARD_RULES.md`](./CI_GUARD_RULES.md) y `tools/ci/platform-guard-config.json`.

- `isSuperAdmin` — prohibido en `frontend/src`
- Literal `'SuperAdmin'` — solo permitido en `platformAuth.ts`
- Imports `modules/superadmin`, `usePlatformGate`, `/api/superadmin/` — prohibidos
