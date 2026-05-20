# EF Migrations Repair — Final Report

**Date:** 2026-05-20  
**Status:** Complete — `has-pending-model-changes` = false, `ERP.API` starts, `MigrateAsync` succeeds.

## What was broken

| Issue | Impact |
|-------|--------|
| 4 manual migrations without `.Designer.cs` | Not in EF chain; snapshot stuck at `20260520201817` |
| `ErpDbContextModelSnapshot` out of date | `PendingModelChangesWarning` → crash at `Program.cs:182` |
| EF scaffold used `RenameColumn` for memberships | Would map `subscriber_id` values as `company_id` (data corruption) |
| `Subscriber` mapped shadow `SubscriberId` | Runtime SQL referenced non-existent column |
| Legacy `TenantId` on `subscribers` | NOT NULL column blocked demo seed inserts |

Architecture (Subscriber, Company, Billing, limits) was **correct** — only EF metadata and one schema orphan were wrong.

## Root cause

Enterprise schema changes were authored as hand-written `Migration.cs` files without running `dotnet ef migrations add`, so:

- `ErpDbContextModelSnapshot` never advanced past `RenameTenantToSubscriberDomain`
- EF Core 10 treats model/snapshot drift as a **fatal** error on `Database.MigrateAsync()`

## Repair actions (commands)

```powershell
cd backend\src\ERP.Infrastructure

# 1. Removed orphan files (not in __EFMigrationsHistory):
#    20260520210000, 20260520220000, 20260520230000, 20260520240000

# 2. Official consolidated migration
dotnet ef migrations add EnterpriseSaasFoundationConsolidated --startup-project ..\ERP.API\ERP.API.csproj

# 3. Manual correction in Up(): company backfill + membership company_id (add/backfill/drop), NOT RenameColumn

# 4. Apply to PostgreSQL
dotnet ef database update --startup-project ..\ERP.API\ERP.API.csproj

# 5. Subscriber root mapping (Ignore SubscriberId) + snapshot sync
#    TenantConfiguration.cs → builder.Ignore(t => t.SubscriberId)
dotnet ef migrations add FixSubscriberRootEntityMapping --startup-project ..\ERP.API\ERP.API.csproj
#    Up/Down empty (column never existed in DB)

# 6. Drop legacy TenantId column on subscribers
dotnet ef migrations add DropLegacySubscriberTenantIdColumn --startup-project ..\ERP.API\ERP.API.csproj
#    SQL: ALTER TABLE subscribers DROP COLUMN IF EXISTS "TenantId";

dotnet ef database update --startup-project ..\ERP.API\ERP.API.csproj
dotnet ef migrations has-pending-model-changes --startup-project ..\ERP.API\ERP.API.csproj
```

## Final migration chain (enterprise tail)

| MigrationId | Role |
|-------------|------|
| `20260520201817_RenameTenantToSubscriberDomain` | Last previously valid snapshot |
| `20260520212433_EnterpriseSaasFoundationConsolidated` | Multi-company, membership `company_id`, company profile, `commercial_plan_limits`, SaaS billing tables |
| `20260520212801_FixSubscriberRootEntityMapping` | Snapshot-only: ignore `SubscriberId` on `Subscriber` |
| `20260520213009_DropLegacySubscriberTenantIdColumn` | SQL cleanup legacy `TenantId` column |

## PostgreSQL validation (`dberpsaas`)

Tables confirmed present:

- `subscribers`, `company`, `company_user_memberships`
- `commercial_plans`, `commercial_plan_limits`, `subscriber_subscriptions`
- `subscriber_billing_accounts`, `saas_billing_invoices`, `saas_billing_invoice_lines`
- `saas_billing_events`, `payment_provider_customers`, `payment_provider_subscriptions`

`company_user_memberships` columns: `company_id` (no `subscriber_id`).  
`company` indexes: `ix_company_subscriber_id` (no `uq_company_tenant`).

Note: SaaS audit table is `saas_billing_events` (not `billing_events`) — matches domain naming.

## Build / runtime validation

| Check | Result |
|-------|--------|
| `dotnet build` | OK (0 errors) |
| `dotnet ef migrations has-pending-model-changes` | **No pending changes** |
| `Database.MigrateAsync()` at startup | OK |
| `ERP.API` listen | `https://localhost:5001`, `http://localhost:5003` |
| Demo seed (`Development:SeedDemoTenant`) | OK — onboarding + profiles |
| Infrastructure tests (limits/entitlements) | 8/8 pass |

Not re-run in this session: Swagger UI click, login HTTP, switch-company HTTP (API process verified up).

## Files touched (migration repair only)

- **Removed:** 4 orphan migration `.cs` files
- **Added:** `20260520212433_EnterpriseSaasFoundationConsolidated` (+ Designer)
- **Added:** `20260520212801_FixSubscriberRootEntityMapping` (+ Designer)
- **Added:** `20260520213009_DropLegacySubscriberTenantIdColumn` (+ Designer)
- **Updated:** `ErpDbContextModelSnapshot.cs` (auto)
- **Updated:** `TenantConfiguration.cs` — `Ignore(SubscriberId)` (EF mapping, not domain)
- **Docs:** `migrations-repair-analysis.md`, this report

**Not changed:** `Program.cs` (no warning suppress), domain entities, handlers, frontend.

## Risks for other environments

If a database was already at `20260520201817` **with data**:

1. Run `database update` — consolidated migration includes **idempotent** company INSERT and membership `company_id` backfill SQL.
2. If `TenantId` exists on `subscribers`, migration `20260520213009` drops it safely.
3. Do **not** apply orphan migration IDs manually to `__EFMigrationsHistory`.

If a DB had partially applied hand migrations outside EF history, compare schema to snapshot before update.

## Architecture preserved

- Subscriber pays (`subscriber_id` on SaaS/billing)
- Company operates (`company_id` on memberships)
- No ERP billing mixed with SaaS billing
- No tenant rollback, no DB reset

See also: [migrations-repair-analysis.md](./migrations-repair-analysis.md), [phase-5-billing-foundation.md](./phase-5-billing-foundation.md).
