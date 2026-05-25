# Platform Control Plane — Clean Target Model

**Objetivo:** un solo lenguaje entre dominio, DB, API y frontend.

## Regla 1:1:1:1

```
Domain Entity  →  DB table (snake_case plural)  →  API root  →  frontend module/facade
```

## Mapa canónico

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

## Companion APIs (runtime, no control plane UI)

| API | Uso |
|-----|-----|
| `GET /api/subscribers/entitlements/me` | Tenant runtime gating (post-login ERP) |
| `GET /api/saas/billing/*` | Tenant self-service billing |
| `POST /api/auth/switch-subscriber` | Impersonación platform → tenant runtime |
| `GET/POST /api/companies/*` | Empresas operativas ERP (multiempresa tenant) |

## Naming conventions

| Capa | Convención | Ejemplo |
|------|------------|---------|
| Domain class | PascalCase singular | `CommercialPlan` |
| Domain file | = class name | `CommercialPlan.cs` |
| DB table | snake_case plural | `commercial_plans` |
| API segment | kebab-case plural English | `/api/platform/subscribers` |
| Frontend service | camelCase + `Service` | `platformService` |
| Frontend types | `Platform*` prefix | `PlatformSubscriber` |
| UI routes | `/platform/*` | `/platform/subscribers` |
| JWT platform operator | `PlatformOperator` | `PlatformAuthConstants` / `platformAuth.ts` |
| Deployment flag JSON | `platformPanelEnabled` | GET `/api/public/deployment` |
| Nav menu flag JSON | `requirePlatformPanel` | menú sesión / admin |
| sessionStorage ficha | `erp.saas.platform.detailSubscriberId` | `platformSubscriberDetailNav.ts` |

Ver también: [CANONICAL-ROUTES.md](./CANONICAL-ROUTES.md), [TEAM-NAMING-GUIDE.md](./TEAM-NAMING-GUIDE.md).
