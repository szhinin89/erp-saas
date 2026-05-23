> **Documento histórico (Phase 2–5).** No usar como referencia de implementación. Rutas y naming actuales: [TEAM-NAMING-GUIDE.md](./TEAM-NAMING-GUIDE.md) · [CANONICAL-ROUTES.md](./CANONICAL-ROUTES.md).
# Platform Control Plane — Contrato final

**Fecha:** 2026-05-23  
**Estado:** Consolidado — superficie única `/api/platform/*`

## Resumen ejecutivo

El Platform Control Plane queda como **una única superficie API** sin duplicidad funcional activa en frontend. Los endpoints legacy `/api/subscribers/*` que representaban gestión SaaS (SuperAdmin) están **deprecados** en backend con `[DeprecatedApi]` + middleware de telemetría. El ERP Runtime conserva sus rutas cross-cutting sin cambios.

## Modelo 1:1:1:1

| Capa | Artefacto canónico |
|------|-------------------|
| Domain | `Subscriber` (`ERP.Domain.Subscribers`) |
| DB | tabla `subscribers` |
| API Control Plane | `/api/platform/subscribers/*` |
| Frontend Control Plane | `platformService` + `subscriberService` |

## Superficie API oficial (Control Plane)

| Dominio | Ruta base | Controller |
|---------|-----------|------------|
| Suscriptores | `/api/platform/subscribers` | `PlatformSubscribersController` |
| Planes | `/api/platform/plans` | `PlatformPlansController` |
| Auth plataforma | `/api/platform/auth` | `PlatformAuthController` |
| Billing | `/api/platform/billing` | `PlatformBillingController` |
| Audit | `/api/platform/audit` | `PlatformAuditController` |
| Config | `/api/platform/config` | `PlatformConfigController` |
| Métricas | `/api/platform/metrics` | `PlatformMetricsController` |

## Runtime ERP — NO movidos (whitelist)

| Endpoint | Propósito |
|----------|-----------|
| `GET /api/subscribers/entitlements/me` | Gating de módulos en sesión |
| `POST /api/auth/switch-subscriber` | Cambio de contexto tenant |
| `GET /api/public/plans` | Catálogo público pre-login |
| `GET /api/subscribers/{id}/public-settings` | Password reset anónimo |
| `GET/PATCH /api/subscribers/{id}/company` | Tenant Admin (runtime) |
| `PATCH /api/subscribers/{id}/operational-settings` | Tenant Admin (runtime) |
| `PATCH /api/subscribers/{id}/password-reset-mode` | Tenant Admin (runtime) |

## Legacy deprecado (backend)

**Eliminados** — ya no existen rutas duplicadas en `/api/subscribers`:

| Legacy (removed) | Canónico |
|------------------|----------|
| `POST /api/subscribers` | `POST /api/platform/subscribers` |
| `PATCH /api/subscribers/{id}/global-parameters` | `PATCH /api/platform/subscribers/{id}/global-parameters` |
| `PATCH /api/subscribers/{id}/subscription` | `PATCH /api/platform/subscribers/{id}/plan` |

## Frontend — clientes HTTP

| Cliente | Alcance | API |
|---------|---------|-----|
| `platformService` | Control plane general | `/api/platform/*` |
| `subscriberService` | Detalle/config suscriptor (SuperAdmin UI) | `/api/platform/subscribers` + `/api/platform/config` |
| `tenantSubscriberService` | Config empresa tenant Admin | `/api/subscribers/*` (runtime) |
| `entitlementsService` | Snapshot sesión | `/api/subscribers/entitlements/me` |

## CI guards

- `tools/ci/run-platform-guard.mjs` — falla en `companyService`, `/api/superadmin`, imports legacy
- `validate-subscriber-api-surface.mjs` — `/api/subscribers` solo en archivos runtime whitelist
- `PlatformControlPlaneGuardTests.cs` — backend sin `/api/superadmin`, deprecaciones en `SubscribersController`

## Criterios de aceptación

| Criterio | Estado |
|----------|--------|
| 0 duplicidad activa frontend control plane → `/api/platform/*` | ✅ |
| 0 `companyService` en código productivo | ✅ (eliminado) |
| Runtime ERP no afectado | ✅ |
| Legacy SuperAdmin deprecado con telemetría | ✅ |
| CI fail-fast | ✅ |

## Referencias

- `API_DUPLICATION_REMOVAL_MAP.md`
- `FRONTEND_CONSOLIDATION_REPORT.md`
- `PLATFORM_SINGLE_SOURCE_OF_TRUTH.md`
