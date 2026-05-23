# Platform Control Plane — Rutas canónicas

**Fecha:** 2026-05-23 · **Phase 5 COMPLETE** (hardening + CI guard)  
**Ownership:** Platform SaaS (Super Admin global) — **no** ERP Runtime operativo.

## API canónica (única superficie control plane)

| Prefijo | Ownership | Notas |
|---------|-----------|--------|
| `POST /api/platform/auth/login` | Platform IAM | Login operadores platform |
| `GET/POST/PATCH /api/platform/subscribers/*` | Subscribers | CRUD, lifecycle, menú, entitlements, users tenant |
| `GET/POST/PUT/DELETE /api/platform/plans/*` | Commercial plans | CRUD, menú por plan, reorder |
| `GET/DELETE /api/platform/users/*` | Platform users | Listado + revoke sessions |
| `GET /api/platform/metrics` | Observability | Lifecycle + planes |
| `GET /api/platform/metrics/growth-analytics*` | Observability | Series temporales |
| `GET /api/platform/billing/*` | Billing SaaS | Summary, invoices, overdue |
| `GET /api/platform/observability/*` | Observability | Dashboard, legacy usage histórico (PostgreSQL) |
| `GET /api/platform/audit` | Audit | Log append-only platform |
| `GET/PUT/DELETE /api/platform/config/{subscriberId}/*` | Tenant config | Global / module / feature |
| `GET/PUT /api/platform/navigation-menu/*` | Menu builder | Menú global platform |
| `GET/POST /api/platform/features/*` | Feature tree | Sync catálogo platform |
| `GET/PUT /api/platform/settings/instance-quota` | Deployment | Cuotas de instancia (`App_Data/instance-quota.json`) |

## UI canónica (Super Admin shell)

| Ruta | Pantalla |
|------|----------|
| `/superadmin/overview` | Dashboard platform |
| `/superadmin/subscribers` | Listado suscriptores |
| `/superadmin/subscribers/:id` | Ficha única — 9 tabs (ver abajo) |
| `/superadmin/plans` | Planes + tab `?tab=menu` (menu builder fusionado) |
| `/superadmin/users` | Platform users (listado, revoke sessions) |
| `/superadmin/billing` | Billing agregado SaaS |
| `/superadmin/observability` | Métricas + legacy usage histórico |
| `/superadmin/audit` | Audit log platform |

### Subscriber detail — tabs canónicos

`?tab=` opcional: `overview` · `subscription` · `entitlements` · `companies` · `users` · `configuration` · `menu-overrides` · `audit` · `metrics`

## Legacy UI → redirect (compat bookmarks)

| Legacy | Redirect |
|--------|----------|
| `/companies` | `/superadmin/subscribers` o `/superadmin/subscribers/:id` si `sessionStorage` legacy |
| `/superadmin/companies` | `/superadmin/subscribers` |
| `/superadmin/menu-plans` | `/superadmin/plans?tab=menu` |
| `/superadmin/navigation-menu` | `/superadmin/plans?tab=menu` |

## Phase 4 — API legacy eliminada

Los siguientes prefijos **ya no existen** en el backend (greenfield; sin strangler):

- `/api/superadmin/*`
- `/api/auth/superadmin-login`
- `/api/admin/iam/superadmin/subscribers*`

Controllers eliminados: `SuperAdminController`, `SuperAdminConfigController`, `SaasPlansAdminController`, `SuperAdminPlanesMenuController`, `SuperAdminEmpresasMenuController`, `SuperAdminAppFeaturesController`.

## Separación Platform vs ERP Runtime

- **Platform:** JWT global SuperAdmin, rutas `/superadmin/*`, API `/api/platform/*`.
- **Runtime:** JWT con `subscriberId` + `companyId`, módulos ERP (`/sales`, `/masterdata`, …).
- **Impersonación:** `switch-subscriber` → contexto tenant → `/saas/*` (no confundir con platform shell).
- **Intacto (Phase 4):** query filters multiempresa, `switch-company`, tablas legacy BD, `/api/subscribers/*` runtime (entitlements, public-settings, tenant admin).

Ver también: [ROUTE-MIGRATION.md](./ROUTE-MIGRATION.md), [PHASE4-LEGACY-REMOVAL-COMPLETE.md](./PHASE4-LEGACY-REMOVAL-COMPLETE.md), [LEGACY_SURFACE_REPORT.md](./LEGACY_SURFACE_REPORT.md).
