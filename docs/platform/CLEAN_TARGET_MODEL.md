# Platform Control Plane — Clean Target Model

**Objetivo:** un solo lenguaje entre dominio, DB, API y frontend.

## Regla 1:1:1:1

```
Domain Entity  →  DB table (snake_case plural)  →  API root  →  frontend module/facade
```

## Mapa target (canónico)

| Entidad | Tabla | API | Frontend |
|---------|-------|-----|----------|
| `Subscriber` | `subscribers` | `/api/platform/subscribers` | `platformService.subscribers*` |
| `CommercialPlan` | `commercial_plans` | `/api/platform/plans` | `platformService.plans*` |
| `PlatformFeature` | `platform_features` | `/api/platform/features` | `platformService.features*` |
| `SubscriberSubscription` | `subscriber_subscriptions` | `/api/platform/subscribers/{id}/plan` | `platformService` |
| `PlatformAuditLog` | `platform_audit_logs` | `/api/platform/audit` | `PlatformAuditPage` |
| `IdentityUser` (platform) | `identity_users` | `/api/platform/users` | `PlatformUsersPage` |
| SaaS billing | `saas_billing_*` | `/api/platform/billing` | `PlatformBillingPage` |
| Config | `config_*` | `/api/platform/config` | `configService` |
| Nav menu | `ui_nav_*` | `/api/platform/navigation-menu` | `platformService` |
| Metrics | _(read model)_ | `/api/platform/metrics` | `PlatformOverviewPage` |
| Observability | _(métricas + health)_ | `/api/platform/observability` | `PlatformObservabilityPage` |
| Settings | _(KV)_ | `/api/platform/settings` | _(pendiente UI)_ |
| Auth | _(session)_ | `/api/platform/auth/login` | `authService` / login |

## Companion APIs (permitidas, no control plane UI)

| API | Uso |
|-----|-----|
| `GET /api/subscribers/entitlements/me` | Tenant runtime gating (post-login ERP) |
| `GET /api/saas/billing/*` | Tenant self-service billing (`SaasBillingPage`) |
| `POST /api/auth/switch-subscriber` | Impersonation platform → tenant runtime |

## Naming conventions target

| Capa | Convención | Ejemplo |
|------|------------|---------|
| Domain class | PascalCase singular | `CommercialPlan` |
| Domain file | = class name | `CommercialPlan.cs` |
| DB table | snake_case plural | `commercial_plans` |
| API segment | kebab-case plural English | `/api/platform/subscribers` |
| Frontend service | camelCase + `Service` | `platformService` |
| Frontend types | `Platform*` prefix | `PlatformSubscriber` |
| UI routes | `/platform/*` (canónico); `/superadmin/*` redirect legacy |
| JWT platform operator | literal legacy `SuperAdmin` | `PlatformAuthConstants` / `platformAuth.ts` |
| Deployment flag JSON | `platformPanelEnabled` (+ alias `superAdminPanelEnabled`) | GET `/api/public/deployment` |
| Nav menu flag JSON | `requirePlatformPanel` (+ alias `requirePlatformPanel`) | menú sesión / admin |

## Acciones de convergencia

| # | Acción | Estado |
|---|--------|--------|
| 1 | Renombrar archivos dominio `SaasPlan.cs` → `CommercialPlan.cs`, etc. | ✅ (2026-05-25) |
| 2 | Deprecar `SubscribersController` para operadores globales | ✅ runtime-only |
| 3 | Consolidar `companyService` → `platformService` | ✅ |
| 4 | i18n `platform.*` | ✅ (2026-05-23) |
| 5 | Retirar tablas `legacy_usage_*` y telemetría strangler | ✅ (2026-05-23) |
| 6 | UI `/platform/*` + redirect `/superadmin/*` | ✅ (2026-05-23) |
| 7 | Alias JSON API `platformPanelEnabled` / `requirePlatformPanel` | ✅ (2026-05-23) |
| 8 | Renombrar namespace `PlatformSubscribers` → `PlatformSubscribers` | ⏳ backlog |

## Validación target

El Platform Control Plane es **válido operativamente** con drift **BAJO** documentado.  
Pendiente no bloqueante: renames físicos de archivos dominio (item 1) y namespace Application (item 8).
