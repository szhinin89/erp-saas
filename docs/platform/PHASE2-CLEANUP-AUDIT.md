> **Documento histórico (Phase 2–5).** No usar como referencia de implementación. Rutas y naming actuales: [TEAM-NAMING-GUIDE.md](./TEAM-NAMING-GUIDE.md) · [CANONICAL-ROUTES.md](./CANONICAL-ROUTES.md).
# Phase 2 — Cleanup audit (Platform Control Plane)

**Fecha:** 2026-05-23  
**Alcance:** canonicalización UI/API platform sin eliminar legacy ni tablas ERP Runtime.

---

## Resumen ejecutivo

| Área | Estado Phase 2 |
|------|----------------|
| UI canónica `/superadmin/*` (8 rutas) | ✅ |
| Redirects legacy UI | ✅ `/companies`, `/superadmin/companies`, `/superadmin/menu-plans` |
| Subscriber detail (9 tabs) | ✅ absorbe ficha `CompaniesPage` |
| Frontend platform → `/api/platform/*` | ✅ (sin consumo activo de `/api/superadmin/*` en shell) |
| Deprecation instrumentation | ✅ logs + `X-Deprecated-Endpoint` + tracker in-memory |
| Legacy usage dashboard | ✅ `/platform/observability` |
| Platform Users UI | ✅ listado + revoke sessions |
| Billing foundation UI | ✅ resumen agregado |
| Observability UI | ✅ health index + legacy endpoints |
| BusinessPartner legacy tables | ✅ intactas — roadmap abajo |
| Build / tests | ✅ `dotnet build`, `npm run build`, API + Architecture tests |

---

## Endpoints duplicados restantes (backend)

Legacy **mantener** hasta métricas de uso en cero (ver Observability → Legacy endpoints).

| Legacy | Canónico | Controller legacy |
|--------|----------|-------------------|
| `GET/PUT /api/superadmin/*` | `/api/platform/subscribers`, `/metrics`, `/navigation-menu`, `/plans` | `SuperAdminController` |
| `GET/POST /api/superadmin/commercial-plans*` | `/api/platform/plans` | `SaasPlansAdminController`, `SuperAdminPlanesMenuController` |
| `GET/PUT /api/superadmin/empresas/*` | `/api/platform/subscribers/{id}/menu` | `SuperAdminEmpresasMenuController` |
| `GET/PUT /api/superadmin/config/*` | `/api/platform/config/*` | `SuperAdminConfigController` |
| `GET/POST /api/superadmin/app-features*` | `/api/platform/features` | `SuperAdminAppFeaturesController` |
| `PATCH /api/subscribers/{id}/subscription` | `/api/platform/subscribers/{id}/plan` | `SubscribersController` |
| `GET /api/admin/iam/superadmin/subscribers*` | `/api/platform/subscribers` | `AccessController` |
| `POST /api/auth/superadmin-login` | `/api/platform/auth/login` | `AuthController` |

**Runtime ERP (NO migrar — contratos operativos):**

- `GET /api/subscribers/entitlements/me`
- `GET /api/subscribers/{id}/public-settings`
- `POST /api/auth/switch-subscriber`
- `GET/POST /api/companies/*` (multiempresa tenant)

---

## Páginas / rutas UI legacy restantes

| Legacy | Destino | Eliminación Phase 3 |
|--------|---------|---------------------|
| `/companies/*` | `CompaniesLegacyRedirect` → subscribers o ficha | Cuando hits = 0 (60d) |
| `modules/companies/pages/CompaniesPage.tsx` | Duplicada por `PlatformSubscriberDetailPage` | Deprecar export; no borrar hasta métricas |
| `pages/CompaniesPage.tsx` re-export | Compat imports viejos | Phase 3 |
| `/superadmin/features`, `/growth`, `/forms` | Redirect overview/plans | Phase 3 |
| `PlatformPlaceholderPage.tsx` | Sin rutas activas | Borrar si orphan confirmado |

---

## Servicios frontend duplicados / adapters

| Servicio | Rol | Phase 3 |
|----------|-----|---------|
| `platformService.ts` | Facade platform (canónico) | Mantener |
| `companyService.ts` | CRUD ficha suscriptor (platform paths) | Fusionar helpers en `platformService` opcional |
| `platformApiPaths.ts` `LEGACY_*` | Documentación + auth refresh compat | Eliminar cuando backend legacy off |
| `companiesSubscriberDetailNav.ts` | sessionStorage + redirect compat | Mantener hasta cero hits `/companies` |
| `useCompaniesPage.ts` | Hook legacy CompaniesPage | Eliminar con página |
| `PlatformPanelCompaniesTab.tsx` | Tab panel legacy | Revisar callers overview |

---

## Instrumentación deprecación

- **Filtro:** `DeprecatedApiAttribute` → header `X-Deprecated-Endpoint`, log warning, registro en `ILegacyEndpointUsageTracker`.
- **Dashboard:** `GET /api/platform/observability/legacy-endpoints` + UI en `/platform/observability`.
- **Criterio borrado endpoint:** 0 calls en ventana acordada (30–60d) + CI verde sin alias.

---

## Deuda removible Phase 3

1. Eliminar controllers legacy listados arriba (por dominio, con strangler completo).
2. Borrar `CompaniesPage` y rutas `/companies` tras métricas.
3. Portar `enabledModules` PATCH a `/api/platform/subscribers/{id}/subscription/modules` (gap Phase 2).
4. Platform roles extendidos: `BillingManager`, `Auditor` (dominio hoy: `SuperAdmin`, `Support`, `BillingAdmin`).
5. UI impersonation logs dedicada (audit platform parcial en tab Audit).
6. Billing: facturas SaaS, grace monitor detallado, usage/limits panel (Phase 2 = resumen agregado).
7. Prometheus scrape directo en UI (Phase 2 = enlaces + health index).
8. Persistir `LegacyEndpointUsageTracker` en Redis/DB para multi-instancia prod.

---

## BusinessPartner — roadmap gradual (sin drop tables)

| Paso | Acción |
|------|--------|
| 1 | Telemetría uso legacy `customers` / `suppliers` pickers vs `BusinessPartner` |
| 2 | Adapters finales ID mapping BP ↔ legacy en capa Application |
| 3 | CRUD UI gradual en módulos masterdata (feature flag entitlements) |
| 4 | Coexistencia strangler: lectura dual, escritura canónica BP |
| 5 | Phase 4+: deprecar tablas legacy solo con métricas + migración datos |

**Regla:** no eliminar tablas legacy en Phase 2.

---

## Validación ejecutada

```bash
dotnet build backend/src/ERP.API/ERP.API.csproj -c Release
dotnet test backend/src/ERP.API.Tests -c Release
dotnet test backend/src/ERP.Architecture.Tests -c Release
cd frontend && npm run build
```

---

Ver también: [CANONICAL-ROUTES.md](./CANONICAL-ROUTES.md), [ROUTE-MIGRATION.md](./ROUTE-MIGRATION.md).
