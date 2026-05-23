# Platform Control Plane — API Consistency Report

**Fecha:** 2026-05-23  
**Alcance:** `/api/platform/*`, `/api/subscribers/*` (SaaS), billing SaaS companion.

## Inventario API Platform (`Controllers/Platform/*`)

| Controller | Route prefix | Entidad / agregado | Verbos principales |
|------------|--------------|-------------------|-------------------|
| `PlatformAuthController` | `api/platform/auth` | Platform login | `POST login` |
| `PlatformSubscribersController` | `api/platform/subscribers` | `Subscriber`, subscription, menu | CRUD + lifecycle + entitlements |
| `PlatformPlansController` | `api/platform/plans` | `CommercialPlan`, menu JSON | CRUD + reorder + menu |
| `PlatformFeaturesController` | `api/platform/features` | `PlatformFeature`, `AppFeature` sync | `GET tree`, `POST sync` |
| `PlatformNavigationController` | `api/platform/navigation-menu` | `ui_nav_*` | CRUD + reorder |
| `PlatformConfigController` | `api/platform/config` | Config scopes | CRUD por subscriber/scope |
| `PlatformSettingsController` | `api/platform/settings` | Instance quota | `GET/PUT instance-quota` |
| `PlatformMetricsController` | `api/platform/metrics` | Read-model KPIs | `GET`, growth analytics |
| `PlatformAuditController` | `api/platform/audit` | `PlatformAuditLog` | `GET` |
| `PlatformBillingController` | `api/platform/billing` | SaaS billing aggregates | summary, invoices, overdue |
| `PlatformObservabilityController` | `api/platform/observability` | Legacy telemetry + health | dashboard, legacy-* |
| `PlatformUsersController` | `api/platform/users` | `IdentityUser` (platform ops) | list, sessions, impersonation |

**Total rutas Platform:** 12 controllers, **0** prefijos `/api/superadmin` (✅ CI guard).

## APIs SaaS companion (in scope, fuera de `/api/platform`)

| Controller | Route | Propósito | Relación Platform |
|------------|-------|-----------|-------------------|
| `SubscriberEntitlementsController` | `api/subscribers/entitlements/me` | Snapshot tenant sesión | Complementa `GET /api/platform/subscribers/{id}/entitlements` |
| `SubscribersController` | `api/subscribers` | CRUD tenant (Admin/SuperAdmin legacy path) | **Duplica** subset de PlatformSubscribers |
| `SaasBillingController` | `api/saas/billing` | Vista tenant facturación | Complementa `PlatformBillingController` (operador) |

## Duplicidad detectada

### CRÍTICO → reclasificado **MEDIO** (coexistencia intencional pendiente de deprecación)

| Recurso | API canónica (control plane) | API paralela | Riesgo |
|---------|------------------------------|--------------|--------|
| Subscriber detail/update | `/api/platform/subscribers/{id}` | `/api/subscribers/{id}` | Drift de contrato; frontend Platform usa solo platform |
| Subscriber create | `POST /api/platform/subscribers` | _(solo platform en UI)_ | Backend dual surface |

**Recomendación:** marcar `SubscribersController` como `@Obsolete` para operadores globales; reservar `/api/subscribers` solo para tenant Admin scope.

### Endpoints sin entidad 1:1 (read-models válidos)

| Endpoint | Justificación |
|----------|---------------|
| `/api/platform/metrics` | Agregación KPI cross-subscriber |
| `/api/platform/metrics/growth-analytics*` | Analytics |
| `/api/platform/observability/dashboard` | Health index compuesto |
| `/api/platform/billing/summary` | DTO agregado billing |

## Endpoints mal nombrados vs dominio

| Endpoint | Issue | Severidad |
|----------|-------|-----------|
| `/api/platform/features/sync` | Sincroniza **AppFeature** (`app_features`), no solo `PlatformFeature` | **MEDIO** |
| `/api/platform/observability/legacy-*` | Nombre “legacy” en API canónica | **BAJO** (observabilidad de migración) |
| `/api/platform/subscribers/{id}/company` | “company” en ruta pero entidad es **Subscriber** | **MEDIO** |

## Frontend ↔ API alignment

| Client | Endpoints Platform | Fuera de scope |
|--------|-------------------|----------------|
| `platformService.ts` | 14 roots `PLATFORM_API.*` | `POST /api/auth/switch-subscriber`, `GET /api/public/plans` |
| `menuService.ts` | Delega 100% a `platformService` | — |
| `companyService.ts` | `PLATFORM_API.subscribers`, `PLATFORM_API.config` | Facade en módulo `companies` |
| `configService.ts` | `PLATFORM_API.config` | — |
| `entitlementsService.ts` | — | `GET /api/subscribers/entitlements/me` (tenant runtime) |

## Clasificación de problemas

### CRÍTICO
_Ninguno activo_ — no hay `/api/superadmin/*` ni controllers `SuperAdmin*`.

### MEDIO
1. Dual surface `SubscribersController` vs `PlatformSubscribersController`
2. Ruta `/company` en subscriber patch (semántica tenant vs subscriber)
3. `features/sync` mezcla catálogos `AppFeature` + `PlatformFeature`
4. Companion APIs `/api/subscribers/entitlements/me` y `/api/auth/switch-subscriber` consumidas desde platform UI (impersonation)

### BAJO
1. Comentarios XML en controllers Platform citan rutas `/api/superadmin/*` históricas (solo docs)
2. `PlatformObservabilityController` endpoints `legacy-*` en surface canónica

## Validación API

| Check | Estado |
|-------|--------|
| 100% control plane bajo `/api/platform/*` | ✅ (12/12 controllers) |
| 0 rutas `/api/superadmin` activas | ✅ |
| Endpoints duplicados legacy vs platform | ⚠️ `/api/subscribers` (backend only) |
| Cada endpoint Platform mapea a agregado de dominio | ✅ |
