# Platform Control Plane — Rutas canónicas

**Última sync:** 2026-05-25  
**Ownership:** Platform Control Plane (operador global) — **no** ERP Runtime operativo.

## API canónica (única superficie control plane)

| Prefijo | Ownership | Notas |
|---------|-----------|--------|
| `POST /api/platform/auth/login` | Platform IAM | Login operadores platform |
| `GET/POST/PATCH /api/platform/subscribers/*` | Subscribers | CRUD, lifecycle, menú, entitlements, users |
| `GET/POST/PUT/DELETE /api/platform/plans/*` | Commercial plans | CRUD, menú por plan, reorder |
| `GET/DELETE /api/platform/users/*` | Platform users | Listado + revoke sessions |
| `GET /api/platform/metrics` | Observability | Lifecycle + planes |
| `GET /api/platform/metrics/growth-analytics*` | Observability | Series temporales |
| `GET /api/platform/billing/*` | Billing SaaS | Summary, invoices, overdue |
| `GET /api/platform/observability/*` | Observability | Dashboard |
| `GET /api/platform/audit` | Audit | Log append-only platform |
| `GET/PUT/DELETE /api/platform/config/{subscriberId}/*` | Subscriber config | Global / module / feature |
| `GET/PUT /api/platform/navigation-menu/*` | Menu builder | Menú global platform |
| `GET/POST /api/platform/features/*` | Feature tree | Sync catálogo platform |
| `GET/PUT /api/platform/settings/instance-quota` | Deployment | Cuotas de instancia |

## UI canónica (shell platform)

| Ruta | Pantalla |
|------|----------|
| `/platform/overview` | Dashboard platform |
| `/platform/subscribers` | Listado suscriptores |
| `/platform/subscribers/:id` | Ficha suscriptor (tabs vía `?tab=`) |
| `/platform/plans` | Planes + tab `?tab=menu` |
| `/platform/users` | Platform users |
| `/platform/billing` | Billing agregado SaaS |
| `/platform/observability` | Métricas |
| `/platform/audit` | Audit log platform |

Constantes frontend: `PLATFORM_UI` en `frontend/src/modules/platform/api/platformApiPaths.ts`.

### Subscriber detail — tabs

`?tab=` opcional: `overview` · `subscription` · `entitlements` · `companies` · `users` · `configuration` · `menu-overrides` · `audit` · `metrics`

Flujo UX: listado → **Abrir ficha** → **Entrar al tenant** (impersonación con retorno a ficha).

## Separación Platform vs ERP Runtime

- **Platform:** JWT operador (`PlatformOperator`), rutas `/platform/*`, API `/api/platform/*`.
- **Runtime:** JWT con `subscriberId` + `companyId`, módulos ERP (`/sales`, `/masterdata`, …).
- **Impersonación:** `POST /api/auth/switch-subscriber` → contexto suscriptor → `/saas/*`.
- **Empresas operativas ERP:** `/saas/companies`, `/api/companies/*` (no confundir con panel platform).

## Companion APIs (runtime, no control plane UI)

| API | Uso |
|-----|-----|
| `GET /api/subscribers/entitlements/me` | Gating tenant post-login |
| `GET /api/saas/billing/*` | Billing self-service tenant |
| `POST /api/auth/switch-subscriber` | Impersonación platform → runtime |

Ver también: [TEAM-NAMING-GUIDE.md](./TEAM-NAMING-GUIDE.md), [CLEAN_TARGET_MODEL.md](./CLEAN_TARGET_MODEL.md).
