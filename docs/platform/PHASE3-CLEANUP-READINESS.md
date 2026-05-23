# Phase 3 — Cleanup readiness report

**Fecha:** 2026-05-23  
**Objetivo:** estabilización Platform ↔ Runtime, telemetría legacy persistente, producción enterprise.

---

## Entregado Phase 3

| Prioridad | Entrega | Estado |
|-----------|---------|--------|
| 1 | Playwright `phase3-smoke.spec.ts` + helpers platform | ✅ (full stack vía `e2e-manual.yml`) |
| 2 | Telemetría legacy PostgreSQL (`legacy_usage_stats`, `legacy_usage_hits`) | ✅ |
| 2 | Beacons UI + MasterData fallback | ✅ |
| 3 | Platform login multi-rol + Users UI revoke + impersonation history | ✅ parcial |
| 4 | Billing platform invoices/overdue APIs + UI | ✅ foundation |
| 5 | Observability dashboard + migration % | ✅ |
| 6 | BP facade telemetry + readiness doc | ✅ telemetría (CRUD cutover pendiente) |
| 7 | Production readiness doc | ✅ |
| 8 | Este reporte | ✅ |

---

## Endpoints removibles (gated — NO eliminar aún)

Requiere **0 hits 30–60d** en `/api/platform/observability/legacy-endpoints`.

| Legacy | Canónico |
|--------|----------|
| `/api/superadmin/*` | `/api/platform/*` |
| `/api/auth/superadmin-login` | `/api/platform/auth/login` |
| `/api/admin/iam/superadmin/*` | `/api/platform/subscribers` |
| `/api/subscribers/{id}/subscription` | `/api/platform/subscribers/{id}/plan` |

**Runtime ERP — NO tocar:** `entitlements/me`, `switch-subscriber`, `/api/companies/*`, pickers legacy APIs.

---

## Páginas removibles

| Legacy | Criterio |
|--------|----------|
| `CompaniesPage` | 0 hits beacon `/companies` + 0 sessionStorage legacy |
| `/companies` redirect | Idem |
| `SuperAdminPlaceholderPage` | Sin rutas activas |

---

## Tablas legacy candidatas (Phase 4+)

**Drop-readiness actual BP:** ~18/100 — ver `docs/masterdata/LEGACY-DROP-READINESS.md`.

| Tabla | Condición borrado |
|-------|-------------------|
| `customers` / `suppliers` standalone | BP shadow FK 100% + dual-write 0 failures |
| Columnas FIXME subscribers | Migración companies completa |

---

## Adapters temporales

| Adapter | Remover cuando |
|---------|----------------|
| `businessPartnerFacade` | MasterData flags 100% tenants + legacy hits = 0 |
| `companiesSubscriberDetailNav` | `/companies` hits = 0 |
| `LEGACY_PLATFORM_API` constants | Backend legacy controllers off |

---

## Riesgo residual

| Riesgo | Mitigación Phase 3 |
|--------|---------------------|
| Telemetría single-writer queue overflow | Channel drop-oldest + PostgreSQL |
| Platform Support/BillingAdmin sin policies granulares | `PlatformAuthorizationRoles` — mutaciones SuperAdmin only |
| MRR/churn no calculado | Dashboard note + payment provider Phase 4 |
| BP MasterData sin reverse dual-write | Telemetría + roadmap FRONTEND-MIGRATION-STATUS |
| MFA / brute-force prod | Documentado en PRODUCTION-READINESS |

---

## Validación

```bash
dotnet build backend/src/ERP.API/ERP.API.csproj -c Release
dotnet test backend/src/ERP.API.Tests -c Release
cd frontend && npm run build
# Full E2E: GitHub Actions → E2E Manual (workflow_dispatch)
```

Credenciales E2E platform (seed dev): `superadmin@erp.com` / `Admin123!`  
Tenant demo: `admin@erp.com` / `Admin123!`

Ver también: [PRODUCTION-READINESS.md](./PRODUCTION-READINESS.md), [PHASE2-CLEANUP-AUDIT.md](./PHASE2-CLEANUP-AUDIT.md).
