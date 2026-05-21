# Project Status

**Single source of truth** for delivery state. Updated: **2026-05-21**.

## Documentation map (canonical)

| Topic | File |
|-------|------|
| Index | `CONTEXT.md` |
| Agent / coding rules | `CLAUDE.md`, `.cursor/rules/` |
| Stack allowlist | `docs/HERRAMIENTAS-ERP-SAAS.md` |
| Development setup | `docs/DEVELOPMENT-RULES.md` |

Legacy or duplicate docs outside this map were removed 2026-05-21 (`_verify_build_out/`, audit snapshots, READMEs de migración en código).

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

Details: [ARCHITECTURE.md](./ARCHITECTURE.md), [DATABASE/](./DATABASE/).

## SaaS platform

| Component | Status |
|-----------|--------|
| Subscribers / plans / features | ✅ |
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
| Accounting, cash | ✅ |
| Retenciones / guía remisión | 🟡 partial / placeholder UI |

## Frontend

| Area | Status |
|------|--------|
| Auth, subscriber select, company select | ✅ |
| Core ERP modules (sales, purchases, inventory, settings) | ✅ |
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
| JWT + refresh rotation | ✅ |
| Permission policies | ✅ |
| Company isolation (app layer) | ✅ |
| RLS (DB layer) | ✅ |
| SuperAdmin platform bypass | ✅ controlled |
| Permissions cache in handler hot path | ⏳ service exists, wiring partial |

## Cache

| Cache | Status |
|-------|--------|
| Entitlements snapshot (Redis-ready) | ✅ |
| Permissions (distributed impl) | ✅ registered |
| Dedicated `commercial-limits:{id}` cache | ⏳ optional future |

## Tests

| Project | Status (2026-05-20) |
|---------|---------------------|
| `ERP.Infrastructure.Tests` (limits/entitlements) | ✅ 8/8 |
| `ERP.Domain.Tests` | ✅ |
| `ERP.Application.Tests` | ✅ |
| `ERP.API.Tests` | ✅ 156/156 |
| Playwright E2E | 🟡 align with subscriber/company flow |

## MVP commercial (~85–90%)

**Done:** Core ERP operational flows, SuperAdmin, plans, multi-company foundation.

**Blocking / high priority:**

1. Validate SRI in `celcer.sri.gob.ec` with test certificate
2. Billing + retenciones UI gaps
3. Playwright E2E hardening for CI

See [ROADMAP.md](./ROADMAP.md) for prioritized backlog.

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

First-run super admin: banner en consola al arrancar API, o `scripts/create-superadmin.ps1` / `Crear-SuperAdmin.ps1`.

## Related

- [ROADMAP.md](./ROADMAP.md) — what’s next
- [DEVELOPMENT-RULES.md](./DEVELOPMENT-RULES.md) — how to contribute safely
