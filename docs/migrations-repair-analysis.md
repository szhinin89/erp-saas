# EF Migrations Repair — Phase 1 Analysis

**Date:** 2026-05-20  
**Scope:** Migration chain only — domain/architecture unchanged.

## 1. ErpDbContext vs snapshot

| Area | `ErpDbContext` + configurations | `ErpDbContextModelSnapshot` (last official: `20260520201817`) |
|------|--------------------------------|---------------------------------------------------------------|
| Company multi-tenant | `ix_company_subscriber_id`, FK `fk_company_subscribers_subscriber_id` | `uq_company_tenant` unique on `subscriber_id` (1:1) |
| Company profile | `timezone`, `currency_code`, `logo_url`, `branding_json` | Missing on `company` |
| `CompanyUserMembership` | `company_id` only | `subscriber_id` + `ux_company_user_memberships_subscriber_identity_user` |
| `RefreshToken` | `company_id` (nullable) | No `company_id` |
| SaaS billing | 6 entities / 6 tables | Not present |
| `CommercialPlanLimit` | Configured + DbSet | Not present |

**Conclusion:** Model is ahead of snapshot by 4 manually authored migration files that were never integrated into the EF chain.

## 2. Orphan migrations (no `.Designer.cs`, not in chain)

| File | Purpose |
|------|---------|
| `20260520210000_EnterpriseMultiCompanyFoundation.cs` | Drop `uq_company_tenant`, FK company→subscriber, backfill companies, `commercial_plan_limits` |
| `20260520220000_Phase1bCompanyMembershipByCompanyId.cs` | Membership `company_id`, drop `subscriber_id`, refresh_tokens `company_id` |
| `20260520230000_CompanyManagementProfileFields.cs` | Company profile columns |
| `20260520240000_SaasBillingFoundation.cs` | SaaS billing tables |

EF `migrations list` only discovers migrations linked via `.Designer.cs` → last in chain: **`20260520201817_RenameTenantToSubscriberDomain`**.

## 3. PostgreSQL state (`dberpsaas`, port 5435)

| Check | Result |
|-------|--------|
| Tables in `dberpsaas` | **Empty** (no relations) |
| `__EFMigrationsHistory` | Not present |
| All 14 official migrations | **(Pending)** when connected explicitly |

**Note:** Another DB on the same instance (`bdzhsoft`) is unrelated legacy schema.

## 4. Root cause

1. Enterprise migrations were written by hand without `dotnet ef migrations add`.
2. `ErpDbContextModelSnapshot.cs` was never updated after `20260520201817`.
3. At startup, `Database.MigrateAsync()` compares live model to snapshot → `PendingModelChangesWarning` → **fatal** (EF Core 10).

## 5. Repair strategy (Phase 2–3)

1. Remove orphan `.cs` files (never applied, not in EF discovery).
2. Run **`dotnet ef migrations add EnterpriseSaasFoundationConsolidated`** — regenerates snapshot + `.Designer.cs`.
3. Augment generated `Up()` with **data-safe SQL** from orphans (company backfill, membership `company_id` backfill) for non-empty databases.
4. **`dotnet ef database update`** — applies full chain without reset.

**Prohibited:** `EnsureCreated`, ignore `PendingModelChangesWarning` in `Program.cs`, drop production tables, domain rewrites.

## 6. Risk matrix

| Risk | Level | Mitigation |
|------|-------|------------|
| Data loss on populated DB at `20260520201817` | Medium | SQL backfill before dropping `subscriber_id` on memberships |
| Duplicate migration IDs in history | Low | Orphans never in `__EFMigrationsHistory` |
| Generated migration differs from manual SQL | Low | Review + embed critical SQL |
| Frontend/API break | None | No contract changes |
