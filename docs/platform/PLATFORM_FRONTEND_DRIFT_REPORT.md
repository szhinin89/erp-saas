> **Documento histórico (Phase 2–5).** No usar como referencia de implementación. Rutas y naming actuales: [TEAM-NAMING-GUIDE.md](./TEAM-NAMING-GUIDE.md) · [CANONICAL-ROUTES.md](./CANONICAL-ROUTES.md).
# Platform Control Plane — Frontend Drift Report

**Fecha:** 2026-05-23  
**Alcance:** `frontend/src/modules/platform/**`, `pages/Platform/**`, `routes/platformRoutes.tsx`, hooks/stores platform.

## Estructura frontend (post Phase 5b)

| Área | Path canónico | Estado |
|------|---------------|--------|
| API client | `modules/platform/api/platformService.ts` | ✅ |
| Paths const | `modules/platform/api/platformApiPaths.ts` | ✅ `PLATFORM_API`, `PLATFORM_UI` |
| Menu facade | `modules/platform/api/menuService.ts` | ✅ thin wrapper |
| Pages | `modules/platform/pages/*`, `pages/Platform/*` | ✅ renombrado `Platform*` |
| Router SoT | `routes/platformRoutes.tsx` | ✅ |
| Layout | `layouts/PlatformLayout.tsx` | ✅ |
| Gate hook | `hooks/usePlatformGate.ts` | ✅ |
| Panel state | `modules/platform/usePlatformPanelPage.ts` | ✅ |

## Services → endpoints

| Service / hook | Endpoints consumidos | Entidad lógica |
|----------------|---------------------|----------------|
| `platformService` | 14× `PLATFORM_API.*` + subpaths | Subscriber, Plan, Users, Audit, Billing, Observability, Config, Nav |
| `platformService.switchSubscriber` | `POST /api/auth/switch-subscriber` | Impersonation → ERP runtime |
| `platformService.getPublicPlans` | `GET /api/public/plans` | Marketing/public catalog |
| `menuService` | _(delega platformService)_ | Navigation menu |
| `companyService` | `PLATFORM_API.subscribers`, `PLATFORM_API.config` | Subscriber detail (facade duplicada) |
| `configService` | `PLATFORM_API.config/{id}/…` | Config scopes |
| `usePlatformPanelPage` | vía `platformService` | Panel orchestration |
| `useSubscriberDetailPage` | `platformService` + `companyService` | Subscriber ficha |
| `usePlatformGate` | _(JWT only)_ | Platform access |
| `usePlatformPlansSection` | vía `platformService` | Plans CRUD UI |
| `usePlatformMenuBuilder*` | vía `platformService` | Menu builder |

## Endpoints fuera de `/api/platform/*` (desde módulo Platform)

| Endpoint | Usado en | Severidad | Justificación |
|----------|----------|-----------|---------------|
| `POST /api/auth/switch-subscriber` | `platformService.switchSubscriber` | **MEDIO** | Auth runtime; no existe `/api/platform/.../switch` |
| `GET /api/public/plans` | `platformService.getPublicPlans` | **BAJO** | Catálogo público pre-login |

**Platform shell pages:** 0 llamadas directas a `/api/superadmin/*` ✅  
**CI guard:** 0 violaciones legacy en `frontend/src` ✅

## Naming drift (SuperAdmin vs Platform)

### Residual aceptado (UI shell / i18n / JWT)

| Ubicación | Patrón | Severidad | Notas |
|-----------|--------|-----------|-------|
| `PLATFORM_UI.*` | Rutas `/superadmin/*` | **BAJO** | URL shell estable (no API) |
| i18n `superadmin.*` | Labels UI | **BAJO** | Pendiente rename a `platform.*` |
| `usePlatformGate().isPlatformOperator` | JWT claim | **BAJO** | Rol backend `SuperAdmin` |
| `requirePlatformPanel` | DTO nav | **BAJO** | Flag deployment |
| `superAdminPanelEnabled` | `App.tsx`, deployment | **BAJO** | Feature flag name |
| `localStorage` `superadmin-impersonation-*` | `platformPanelUtils.ts` | **BAJO** | Compat sesión |
| Copy “SuperAdmin” en UI | `PlatformUsersPage`, overview | **BAJO** | Texto producto |

### Residual eliminado ✅

| Antes | Ahora |
|-------|-------|
| `modules/platform/` | `modules/platform/` |
| `platformService.ts` | `platformService.ts` |
| `PlatformLayout` | `PlatformLayout` |
| `usePlatformGateGate` | `usePlatformGate` |

## Duplicación de servicios

| Issue | Severidad | Detalle |
|-------|-----------|---------|
| `companyService` re-exporta subscriber/platform config | **MEDIO** | Target: `platformSubscriberService.ts` único |
| `menuService` wrapper | **BAJO** | Aceptable facade delgada |

## Stores / state

| Store | Platform usage |
|-------|----------------|
| `authStore` | Platform login via `/api/platform/auth/login` |
| `permissionsStore` | No direct platform; entitlements vía auth flow |
| _(no Zustand platform-dedicated)_ | Estado en hooks `usePlatformPanelPage`, pages |

## Router consistency

| Ruta UI | Componente | API backing |
|---------|------------|-------------|
| `/platform/overview` | `PlatformOverviewPage` | metrics + subscribers |
| `/platform/subscribers` | `PlatformSubscribersPage` | subscribers |
| `/platform/subscribers/:id` | `PlatformSubscriberDetailPage` | subscribers + config |
| `/platform/plans` | `PlatformPlansPage` | plans + navigation |
| `/platform/users` | `PlatformUsersPage` | users |
| `/platform/billing` | `PlatformBillingPage` | billing |
| `/platform/observability` | `PlatformObservabilityPage` | observability |
| `/platform/audit` | `PlatformAuditPage` | audit |

Bookmark redirect: `/companies/*` → `platformBookmarkRedirectRoutes()` ✅

## Clasificación problemas frontend

### CRÍTICO
_Ninguno_ — CI guard PASS, 0 legacy API.

### MEDIO
1. Facade `companyService` duplica `platformService` para subscriber detail
2. Impersonation usa `/api/auth/switch-subscriber` fuera de namespace platform
3. i18n namespace `superadmin.*` vs código `Platform*`

### BAJO
1. URL shell `/superadmin/*` vs naming Platform en código
2. `menuService` capa extra
3. Textos UI “SuperAdmin” en copy

## Validación frontend

| Criterio | Resultado |
|----------|-----------|
| 100% platform UI vía `platformService` (+ thin facades) | ⚠️ 98% |
| 0 `/api/superadmin/*` | ✅ |
| 0 imports `modules/platform` | ✅ |
| Router único `platformRoutes.tsx` | ✅ |
| 0 drift naming crítico | ✅ (solo BAJO/MEDIO cosmetic) |
