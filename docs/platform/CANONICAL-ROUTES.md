# Platform Control Plane — Rutas canónicas

**Fecha:** 2026-05-23 · **Phase 5 COMPLETE** (naming platform + CI guard)  
**Ownership:** Platform Control Plane (operador global) — **no** ERP Runtime operativo.

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

## UI canónica (shell platform)

| Ruta | Pantalla |
|------|----------|
| `/platform/overview` | Dashboard platform |
| `/platform/subscribers` | Listado suscriptores |
| `/platform/subscribers/:id` | Ficha única — tabs (ver abajo) |
| `/platform/plans` | Planes + tab `?tab=menu` (menu builder fusionado) |
| `/platform/users` | Platform users (listado, revoke sessions) |
| `/platform/billing` | Billing agregado SaaS |
| `/platform/observability` | Métricas + legacy usage histórico |
| `/platform/audit` | Audit log platform |

Constantes frontend: `PLATFORM_UI` en `frontend/src/modules/platform/api/platformApiPaths.ts`.

### Subscriber detail — tabs canónicos

`?tab=` opcional: `overview` · `subscription` · `entitlements` · `companies` · `users` · `configuration` · `menu-overrides` · `audit` · `metrics`

Flujo UX: listado → **Abrir ficha** → sección **Entrar al tenant** (impersonación con retorno a ficha).

## Legacy UI → redirect (compat bookmarks)

| Legacy | Redirect |
|--------|----------|
| `/superadmin/*` | `/platform/*` (misma subruta) |
| `/companies` | `/platform/subscribers` o ficha vía `sessionStorage` |
| `/superadmin/companies` | `/platform/subscribers` |
| `/superadmin/menu-plans` | `/platform/plans?tab=menu` |
| `/superadmin/navigation-menu` | `/platform/plans?tab=menu` |

Implementación: `PlatformLegacyUiRedirect` en `frontend/src/routes/platformRoutes.tsx`.

## Phase 4 — API legacy eliminada

Los siguientes prefijos **ya no existen** en el backend (greenfield; sin strangler):

- `/api/superadmin/*`
- `/api/auth/superadmin-login`
- `/api/admin/iam/superadmin/subscribers*`

Controllers eliminados: `SuperAdminController`, `SuperAdminConfigController`, `SaasPlansAdminController`, `SuperAdminPlanesMenuController`, `SuperAdminEmpresasMenuController`, `SuperAdminAppFeaturesController`.

## Separación Platform vs ERP Runtime

- **Platform:** JWT operador platform (`PlatformOperator` — ver `platformAuth.ts` / `PlatformAuthConstants`), rutas `/platform/*`, API `/api/platform/*`.
- **Runtime:** JWT con `subscriberId` + `companyId`, módulos ERP (`/sales`, `/masterdata`, …).
- **Impersonación:** `switch-subscriber` → contexto tenant → `/saas/*` (banner con retorno a ficha platform).
- **Intacto (Phase 4):** query filters multiempresa, `switch-company`, tablas legacy BD, `/api/subscribers/*` runtime (entitlements, public-settings, tenant admin).

Ver también: [TEAM-NAMING-GUIDE.md](./TEAM-NAMING-GUIDE.md), [ROUTE-MIGRATION.md](./ROUTE-MIGRATION.md), [PHASE4-LEGACY-REMOVAL-COMPLETE.md](./PHASE4-LEGACY-REMOVAL-COMPLETE.md), [LEGACY_ALIAS_MAP.md](./LEGACY_ALIAS_MAP.md), [LEGACY_SURFACE_REPORT.md](./LEGACY_SURFACE_REPORT.md).
