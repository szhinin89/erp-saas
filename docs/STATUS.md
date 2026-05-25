# Project Status

**Single source of truth** for delivery state. Updated: **2026-05-24**.

## Documentation map (canonical — `AI-RULES/` + 7 files in `docs/` + índices)

| Topic | File |
|-------|------|
| **Agent rules (canonical)** | `AI-RULES/README.md` |
| Index | `CONTEXT.md` |
| Repo structure (2026-05) | `README.md`, `infrastructure/`, `scripts/`, `tools/` |
| Product summary | `README.md` |
| Agent adapters | `CLAUDE.md`, `.cursor/rules/` → `AI-RULES/*` |
| Delivery state | `docs/STATUS.md` (this file) |
| Priorities | `docs/ROADMAP.md` |
| Architecture | `docs/ARCHITECTURE.md` |
| Architecture rules (PR blocking) | `AI-RULES/PR-RULES-CATALOG.md` (entry: `docs/ARCHITECTURE-RULES.md`) |
| ADRs (architectural rationale) | `docs/adr/README.md` |
| Development + stack | `docs/DEVELOPMENT.md` |
| Identity + security | `docs/IDENTITY.md` |
| SaaS plans + billing | `docs/SAAS-COMMERCIAL.md` |
| Database | `docs/DATABASE.md` |

Consolidated 2026-05-21: former `MULTITENANCY`, `SCOPES`, `SECURITY`, `BILLING`, `DATABASE/*`, etc. merged into the files above. **2026-05-21:** `AI-RULES/` centralizes implementation rules for Cursor, Claude and future agents.

## Architecture (current)

| Area | State |
|------|--------|
| Modular monolith (Clean + CQRS) | ✅ |
| Single EF baseline `20260521034018_InitialEnterpriseBaseline` | ✅ |
| Subscriber / Company / Membership model | ✅ |
| SaaS billing domain (isolated tables) | ✅ |
| Commercial plan limits service | ✅ |
| CompanyScopeBehavior + BillingGate + SubscriptionGate | ✅ |
| Entitlements distributed cache | ✅ |
| Wave 1 `company_id` (inventory core) | ✅ (in baseline) |
| PostgreSQL RLS (enterprise tables) | ✅ (in baseline) |
| Rate limit per subscriber (600/min) | ✅ |
| Architecture guardrails CI (scripts + NetArchTest) | ✅ (2026-05-21) |
| **Frontend architecture checks (Node ESM)** | ✅ 12/12, score 100/100 (2026-05-24) — controllers backend ≤150 líneas |
| **Architecture governance v2** (ADRs, backend Node checks, score, PR annotations) | ✅ (2026-05-21) |
| Architecture baseline v1.0 remediation (lint, E2E smoke, legacy platform controller, SYSTEM_TRUTH) | ✅ (2026-05-21) |
| Post-audit remediation (session SEC, Sales unify, Kardex CQRS, Cash validators) | ✅ (2026-05-21) |
| Post-audit wave 2 (menu builder split, services→modules, access/security pages) | ✅ (2026-05-21) |
| Post-audit wave 3 (menu builder modular split, test sessionStorage) | ✅ (2026-05-21) |
| Enterprise monorepo root (`infrastructure/`, `scripts/`, `tools/`, docs stubs) | ✅ (2026-05-21) |
| Post-reorg stabilization (paths, CI green, company-scoped inventory movements) | ✅ (2026-05-21) |
| Post-audit P2 + wave 4 (services eliminados, AppLayout/Companies split) | ✅ (2026-05-21) |
| Post-audit wave 5 (PR-7 TSX: catálogo, clientes, contabilidad, menu builder, platform shell) | ✅ (2026-05-21) |
| Post-audit wave 6 (handlers C-03, lazy routes, grandfather vacío) | ✅ (2026-05-21) |
| **AI-RULES multi-agent governance** (`AI-RULES/*` canonical; `CLAUDE.md` + `.mdc` adapters) | ✅ (2026-05-21) |

Details: [ARCHITECTURE.md](./ARCHITECTURE.md), [DATABASE.md](./DATABASE.md).

### Post-audit remediation (2026-05-21)

| Item | Estado |
|------|--------|
| Frontend: tokens en memoria + perfil/bootstrap/permisos en `sessionStorage`; `SessionBootstrap` + cookie refresh | ✅ |
| Backend: `ERP.Application/Sales` consolidado bajo `Modules/Sales` + validators Notas/Retenciones | ✅ |
| Backend: `EnqueueKardexReportCommand` (controller sin `SaveChangesAsync`) | ✅ |
| Backend: validators Cash (caja/bancos/conciliación) | ✅ |
| Pendiente PR-7 TSX >500 | ✅ (grandfather `tsxMaxLines500` vacío 2026-05-21) |

### Post-audit wave 5 (2026-05-21)

| Item | Estado |
|------|--------|
| `MenuBuilder` + `NavigationMenuEditorPanel` modularizados (controller + subpaneles) | ✅ |
| `PlatformPanelPage` + `PlatformPlansSection` en hook + tabs/modales | ✅ |
| `AccountingPage`, `BranchesPage`, `CustomersPage`, `SriConfigPage`, `BodegasPage` | ✅ |
| `CatalogPages`, `CatalogStructurePage`, categorías/subcategorías | ✅ |
| `architecture-grandfather.json`: `tsxMaxLines500` vacío | ✅ (`tools/architecture/`) |

### Post-audit wave 6 (2026-05-21)

| Item | Estado |
|------|--------|
| Handlers C-03: `CrearVenta`, `CreateProduct`, `UpdateProduct`, `EmitirFactura`, `EnviarNotaSri` (Handle ≤150) | ✅ |
| `ProductCommandMutationHelper` compartido create/update | ✅ |
| Rutas lazy: `accessRoutes`, `companiesRoutes`, `companyManagementRoutes`, `publicRoutes`, `mainRoutes` (placeholder) | ✅ |
| Grandfather vacío (`handlerHandleMaxLines150`, `tsxMaxLines500`, `tsxPageWrapperMaxLines15`) | ✅ |
| Chunk `index-*.js` ~362 KB (límite 650 KB) | ✅ |

### Post-audit P2 (2026-05-21)

| Item | Estado |
|------|--------|
| Carpeta `frontend/src/services/` eliminada (cero consumidores; API solo en `modules/*/api`) | ✅ |
| `SalesReportPage` → `modules/reportes/pages/` + wrapper 1 línea | ✅ |
| Placeholders → `modules/shared/pages/` + wrappers delgados | ✅ |
| `components/ui` sustituido por ZH en company-management, access, security, companies | ✅ |

### Post-audit wave 4 (2026-05-21)

| Item | Estado |
|------|--------|
| `AppLayout.tsx` (~634 → ~216) + `AppLayoutMainMenu`, `useAppLayoutNavigation`, banner | ✅ |
| `CompaniesPage.tsx` (~820 → ~252) + `useCompaniesPage`, `CompaniesPageDataTab` | ✅ |
| Grandfather: retirados `AppLayout`, `CompaniesPage`, `SalesReportPage` | ✅ |

### Post-audit wave 3 (2026-05-21)

| Item | Estado |
|------|--------|
| `usePlatformGateMenuBuilder` (~844 → ~371 líneas) + effects/actions/persist extraídos | ✅ |
| `PlatformMenuBuilderCrmWorkspace` (~934 → ~259 líneas) + panels/preview/audit/modals | ✅ |
| Test `syncSessionEntitlements` con stub `sessionStorage`/`localStorage` | ✅ |
| Grandfather: `PlatformMenuBuilderCrmWorkspace` retirado de PR-7 | ✅ |

### Post-audit wave 2 (2026-05-21)

| Item | Estado |
|------|--------|
| `PlatformMenuBuilderSection` dividido en entry + hook + CRM/legacy panels | ✅ |
| Imports `services/` → `modules/*/api` (cero consumidores directos en `src/`) | ✅ |
| `ProfilesPage`, `SubscriberAccessPage`, `SecuritySettingsPage` en `modules/` + wrappers delgados | ✅ |
| Re-exports `@deprecated` en `frontend/src/services/` para compatibilidad | ✅ (carpeta eliminada 2026-05-21) |
| Grandfather JSON actualizado (CRM workspace, sin legacy service imports) | ✅ |

## SaaS platform

| Component | Status |
|-----------|--------|
| Subscribers / plans / features | ✅ |
| Platform UI naming + API JSON aliases + middleware rename | ✅ (2026-05-23) |
| Subscriber ficha unificada + impersonación con retorno | ✅ (2026-05-23) |
| Company management API + UI (`/saas/companies`) | ✅ |
| Switch company + JWT claims | ✅ |
| Commercial limits (companies, users, branches, warehouses) | ✅ |
| Entitlements snapshot API | ✅ |
| Billing governance + API | ✅ backend |
| Billing UI | ⏳ not built |
| Stripe / real payment provider | ⏳ `NullPaymentProviderAdapter` |

## ERP backend

| Module | Status |
|--------|--------|
| Products, catalogs, customers, suppliers | ✅ |
| Inventory, transfers, adjustments, kardex | ✅ |
| Purchases (OC, bills, expenses) | ✅ |
| Sales + electronic invoice (SRI code) | ✅ code / 🟡 real SRI validation pending |
| **Sales commercial pipeline** (quote → order → invoice, `DocumentRelation`) | ✅ API + UI + E2E (2026-05-24) |
| Accounting, cash | ✅ |
| Retenciones / guía remisión | 🟡 partial / placeholder UI |

### Backend architecture hardening (audit 2026-05-21)

| Item | Status |
|------|--------|
| SRI post-auth atomic transactions (`IUnitOfWork` ambient + journal entry nested) | ✅ |
| `SriSettings.CertPassword` encrypted at rest (Data Protection, legacy plaintext fallback) | ✅ |
| `Company` → `ISubscriberScopedEntity` + global EF subscriber filter | ✅ |
| `AccountingService` orchestration in Application layer | ✅ |
| API DbContext leakage → CQRS (`GetAppFeatureTree`, `ListPendingSriRetry`, `IAppFeatureRepository`) | ✅ |

## Frontend

| Area | Status |
|------|--------|
| Auth, subscriber select, company select | ✅ |
| Core ERP modules (sales, purchases, inventory, settings) | ✅ |
| **Ventas pipeline UI** (`/sales/quotes`, `/sales/orders`, `/sales/invoices`, credit notes) | ✅ (2026-05-24) |
| **`fullLogout()` centralizado** (stores + localStorage + `erp.saas.*`) | ✅ |
| **Products/customers — fuente única en `modules/*`** (`apiEnvelope`, adapters `@deprecated`) | ✅ |
| **Consolidación modular P3** (auth, branches, accounting, dashboard, platform API + pages) | ✅ |
| **Catálogo + bodegas + auth UI** en `modules/catalog`, `modules/inventario/warehouses`, `modules/auth/pages` | ✅ |
| **Lazy routes P4** (`routes/lazyPage.tsx`, main/catalog/platform split) | ✅ |
| **Platform naming cleanup** (`/platform/*`, `platformAuth.ts`, sin `isPlatformOperator`) | ✅ (2026-05-23) |
| **ZH UI estándar** (`components/ui` delega clases ZH; catálogo usa `ZHCard`/`ZHSearchBar`) | ✅ |
| Company management module | ✅ |
| SaaS billing pages | ⏳ |
| Kardex / stock dedicated UI | ⏳ placeholder routes |
| Legacy `tenant` i18n aliases | 🟡 rename deferred |

## PostgreSQL

| Item | Status |
|------|--------|
| Schema from single baseline | ✅ |
| Naming `_subscriber_` on indexes/FK | ✅ |
| RLS enabled (inventory, sales core) | ✅ |
| Session vars via interceptor | ✅ |
| Company scope on operational entities | ✅ (baseline + query filters) |

## Security

| Item | Status |
|------|--------|
| JWT + refresh rotation (FamilyId, grace configurable, revocación por familia, rate limit IP/user/family, audit logs) | ✅ |
| Multi-tab SPA (Web Locks + BroadcastChannel + bootstrap retry) | ✅ |
| Permission policies | ✅ |
| Company isolation (app layer) | ✅ |
| SRI certificate password encryption (Data Protection) | ✅ |
| RLS (DB layer) | ✅ |
| Platform operator bypass (JWT global) | ✅ controlled |
| Permissions cache in handler hot path | ⏳ service exists, wiring partial |
| SPA session cleanup (`fullLogout`) | ✅ frontend |

## Cache

| Cache | Status |
|-------|--------|
| Entitlements snapshot (Redis-ready) | ✅ |
| Permissions (distributed impl) | ✅ registered |
| Dedicated `commercial-limits:{id}` cache | ⏳ optional future |

## Tests

| Project | Status (2026-05-21) |
|---------|---------------------|
| `ERP.Infrastructure.Tests` (limits/entitlements + optional Postgres unified-doc) | ✅ 23/23 |
| `ERP.Domain.Tests` | ✅ 24/24 |
| `ERP.Application.Tests` | ✅ 95/95 |
| `ERP.API.Tests` | ✅ 174/174 |
| `ERP.Architecture.Tests` (NetArchTest + controller guardrails) | ✅ 7/7 |
| Frontend ESLint (`npm run lint`) | ✅ 0 errors (2026-05-21 remediation) |
| Frontend Vitest | ✅ 22/22 |
| Frontend build | ✅ |
| Playwright smoke | ✅ PASS |
| Playwright enterprise E2E | 🟡 requiere API local; skip controlado sin backend |

### Sales commercial pipeline greenfield (2026-05-24)

| Item | Estado |
|------|--------|
| API: quotes (list/detail/create/approve/cancel), orders (list/detail/create/confirm/cancel/invoice) | ✅ |
| API: invoices (list/detail/validar/emitir/reintentar/anular) + permisos `sales.invoices.*` | ✅ |
| API: `DocumentRelation` (`QUOTE_TO_ORDER`, `ORDER_TO_INVOICE`) en detalle | ✅ |
| UI: `/sales/quotes`, `/sales/orders`, `/sales/invoices` + legacy redirects | ✅ |
| UI: trazabilidad cotización↔pedido↔factura; factura directa walk-in | ✅ |
| UI: filtros servidor en listado facturas; permiso `sales.credit-notes.send` | ✅ |
| E2E: `SalesCommercialPipelineEndToEndTests`, `SalesOrderInvoiceEndToEndTests`, `SalesCommercialCancelEndToEndTests` | ✅ |
| Tenants con perfil Facturador anterior al seed | 🟡 re-seed o migración manual de permisos `sales.quotes.*`, `sales.orders.*` |

Flujo canónico: **Cotización → Aprobar → Pedido → Confirmar → Factura → Validar/Emitir SRI**.

## MVP commercial (~85–90%)

**Done:** Core ERP operational flows, platform control plane, plans, multi-company foundation.

**Blocking / high priority:**

1. Validate SRI in `celcer.sri.gob.ec` with test certificate
2. Billing + retenciones UI gaps
3. Playwright enterprise E2E con API en CI (smoke ya verde)

See [ROADMAP.md](./ROADMAP.md) for prioritized backlog.

### Enterprise hardening — MasterData + security (2026-05-23)

| Item | Estado |
|------|--------|
| Explicit scope markers (`ICompanyScopedRequest` / CI AR-SEC-4) | ✅ |
| PostgreSQL unique violation → `Result.Conflict` (409) | ✅ |
| Testcontainers concurrency tests | ✅ (`Category=PostgreSql`) |
| Security metrics wired (refresh, 403, dual-write, namespace fallback) | ✅ |
| MasterData reconciliation (READ-ONLY) + health + Hangfire job | ✅ |
| SRI foundation (`SupplierProfile` retention defaults) | ✅ |
| Docs: [security/MULTI-TENANT-HARDENING.md](./security/MULTI-TENANT-HARDENING.md), [observability/METRICS.md](./observability/METRICS.md) | ✅ |

## Risks

| Risk | Mitigation |
|------|------------|
| Cross-company data leak | `CompanyScopeBehavior` + RLS + EF query filters |
| Production migration from old chain | Use baseline + planned data migration — never `DROP SCHEMA` in prod |
| Billing suspend without UI visibility | Entitlements snapshot exposes status; build `/saas/billing` |
| Test drift | Fix controller/DTO names before release gate |

## Quick start

```powershell
docker compose up -d
cd backend/src/ERP.Infrastructure
dotnet ef database update --startup-project ../ERP.API/ERP.API.csproj
cd ../ERP.API
dotnet run
```

First-run operador platform: banner en consola al arrancar API, o **`scripts/setup/Crear-PlatformOperator.ps1`**.

## Related

- [ROADMAP.md](./ROADMAP.md) — what’s next
- [DEVELOPMENT.md](./DEVELOPMENT.md) — how to contribute safely
