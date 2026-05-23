# Platform Control Plane — Rutas canónicas

**Fecha:** 2026-05-23 (Phase 2)  
**Ownership:** Platform SaaS (Super Admin global) — **no** ERP Runtime operativo.

## API canónica

| Prefijo | Ownership | Notas |
|---------|-----------|--------|
| `POST /api/platform/auth/login` | Platform IAM | Login operadores platform |
| `GET/POST/PATCH /api/platform/subscribers/*` | Subscribers | CRUD, lifecycle, menú, entitlements, users tenant |
| `GET/POST/PUT/DELETE /api/platform/plans/*` | Commercial plans | CRUD, menú por plan, reorder |
| `GET/DELETE /api/platform/users/*` | Platform users | Listado + revoke sessions |
| `GET /api/platform/metrics` | Observability | Lifecycle + planes |
| `GET /api/platform/metrics/growth-analytics*` | Observability | Series temporales (Phase 2) |
| `GET /api/platform/billing/summary` | Billing SaaS | Resumen agregado grace/suspend/overdue |
| `GET /api/platform/observability/*` | Deprecation | Legacy usage dashboard, health index |
| `GET /api/platform/audit` | Audit | Log append-only platform |
| `GET/PUT/DELETE /api/platform/config/{subscriberId}/*` | Tenant config | Global / module / feature |
| `GET/PUT /api/platform/navigation-menu/*` | Menu builder | Menú global platform |
| `GET/POST /api/platform/features/*` | Feature tree | Sync catálogo platform |

Headers en rutas legacy: `Deprecation`, `X-Api-Deprecated`, `X-Deprecated-Endpoint`, `Link` (RFC 8594).

## UI canónica (Super Admin shell)

| Ruta | Pantalla |
|------|----------|
| `/superadmin/overview` | Dashboard platform |
| `/superadmin/subscribers` | Listado suscriptores |
| `/superadmin/subscribers/:id` | Ficha única — 9 tabs (ver abajo) |
| `/superadmin/plans` | Planes + tab `?tab=menu` (menu builder fusionado) |
| `/superadmin/users` | Platform users (listado, revoke sessions) |
| `/superadmin/billing` | Billing agregado SaaS |
| `/superadmin/observability` | Métricas + legacy endpoint usage |
| `/superadmin/audit` | Audit log platform |

### Subscriber detail — tabs canónicos

`?tab=` opcional: `overview` · `subscription` · `entitlements` · `companies` · `users` · `configuration` · `menu-overrides` · `audit` · `metrics`

Absorbe la ficha legacy `CompaniesPage` (datos empresa, config, menú custom, impersonación).

## Legacy → redirect (UI)

| Legacy | Redirect |
|--------|----------|
| `/companies` | `/superadmin/subscribers` o `/superadmin/subscribers/:id` si `sessionStorage` legacy |
| `/superadmin/companies` | `/superadmin/subscribers` |
| `/superadmin/menu-plans` | `/superadmin/plans?tab=menu` |
| `/superadmin/navigation-menu` | `/superadmin/plans?tab=menu` |

## Legacy API (compat — no consumir desde platform UI)

| Legacy | Sucesor |
|--------|---------|
| `/api/superadmin/*` | `/api/platform/*` (por dominio) |
| `/api/admin/iam/superadmin/subscribers*` | `/api/platform/subscribers` |
| `/api/subscribers/{id}/subscription` | `/api/platform/subscribers/{id}/plan` |
| `/api/superadmin/planes/*` | `/api/platform/plans/{id}/menu` |
| `/api/superadmin/empresas/*` | `/api/platform/subscribers/{id}/menu` |
| `/api/superadmin/config/*` | `/api/platform/config/*` |
| `/api/auth/superadmin-login` | `/api/platform/auth/login` |

## Separación Platform vs ERP Runtime

- **Platform:** JWT global SuperAdmin, rutas `/superadmin/*`, API `/api/platform/*`.
- **Runtime:** JWT con `subscriberId` + `companyId`, módulos ERP (`/sales`, `/masterdata`, …).
- **Impersonación:** `switch-subscriber` → contexto tenant → `/saas/*` (no confundir con platform shell).

Ver también: [ROUTE-MIGRATION.md](./ROUTE-MIGRATION.md), [PHASE2-CLEANUP-AUDIT.md](./PHASE2-CLEANUP-AUDIT.md).
