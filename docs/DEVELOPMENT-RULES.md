# Development Rules

Official rules for contributors and agents. Violations break tenant isolation or billing boundaries.

## Naming

| Area | Convention |
|------|------------|
| Tables / columns | `snake_case` |
| Indexes | `ix_*`, `ux_*`, `uq_*` |
| Foreign keys | `fk_*` with `_subscriber_` (not `_tenant_`) |
| Domain types | PascalCase; map to DB via EF configurations |
| Retired | `Tenant`, `tenant_id`, `PK_tenants` |

## Entity scope

Declare scope before adding tables — see [SCOPES.md](./SCOPES.md).

## NEVER

- `IgnoreQueryFilters()` without `PlatformQueryReason`
- `company_id` from request body as authority
- Hardcoded `MAX_*` limits in handlers
- Mix SaaS billing with ERP invoices
- Stripe SDK inside MediatR handlers
- Hand-written migration `.cs` without `dotnet ef migrations add`
- `Tenant` in new code or schema

## ALWAYS

- `ICurrentSubscriber` / `ICurrentCompany` for context
- `ICompanyAccessGuard` or `CompanyScopeBehavior` for ERP
- `ICommercialPlanLimitService` for quotas
- `IBillingGovernanceService` for billing state
- Invalidate entitlements cache on plan/billing changes
- `dotnet ef migrations add <Name>` for schema changes

## New ERP use case checklist

1. Handler under scoped namespace (Sales, Inventory, …) **or** implement `ICompanyScopedRequest`
2. Pass `companyId` from `ICurrentCompany` into domain factories (Wave 1+)
3. Mark `ISubscriberOnlyRequest` only for true platform endpoints
4. Add permission seed if new `perm:*` required

## Local setup

```powershell
docker compose up -d
cd backend/src/ERP.Infrastructure
dotnet ef database update --startup-project ../ERP.API/ERP.API.csproj
cd ../ERP.API
dotnet run
# https://localhost:5001  http://localhost:5003

cd ../../../frontend
npm run dev
# http://localhost:5173
```

Copy `appsettings.Development.json.example` → `appsettings.Development.json`.

PostgreSQL (default): `Host=localhost;Port=5435;Database=dberpsaas`.

## Migrations (dev)

Fresh DB:

```sql
DROP SCHEMA public CASCADE;
CREATE SCHEMA public;
GRANT ALL ON SCHEMA public TO postgres;
GRANT ALL ON SCHEMA public TO public;
```

Then `dotnet ef database update`. Policy: [DATABASE/MIGRATIONS.md](./DATABASE/MIGRATIONS.md).

Pre-production: prefer **one baseline** in repo; squash incremental migrations before first production deploy.

## Tests

```powershell
cd backend/src
dotnet test ERP.Domain.Tests
dotnet test ERP.Infrastructure.Tests
dotnet test ERP.Application.Tests
dotnet test ERP.API.Tests
```

## E2E (Playwright)

Requisitos: Docker (Postgres `:5435`, Redis `:6379`), .NET SDK, Node 22+, `npx playwright install chromium`.

```powershell
# Recomendado (desde raíz del repo)
pwsh -File scripts/run-e2e.ps1

# Variantes
pwsh -File scripts/run-e2e.ps1 -SkipDocker
pwsh -File scripts/run-e2e.ps1 -SkipMigrations
pwsh -File scripts/run-e2e.ps1 -PlaywrightArgs "e2e/smoke.spec.ts"
```

Credenciales demo (`Development:SeedDemoTenant: true`): `admin@erp.com` / `Admin123!`, API `http://localhost:5003`.

Suites: `e2e/smoke.spec.ts` (solo UI); `e2e/enterprise-*.spec.ts` requieren API en `/health/live`.

## Verification

Manual smoke (after schema or auth changes):

| # | Check |
|---|--------|
| 1 | API starts; `GET /health/live` → 200 |
| 2 | `dotnet ef migrations has-pending-model-changes` → false |
| 3 | Login → JWT has `subscriber_id` |
| 4 | Switch company → JWT has `company_id` |
| 5 | ERP endpoint without `company_id` → 403 |
| 6 | `GET /api/saas/billing/account` with permission |
| 7 | Create company over `MAX_COMPANIES` → 403 |
| 8 | SuperAdmin bypass (controlled) |

## Future patterns (documented, not implemented)

- Transactional outbox for integration events
- Distributed locks for quota enforcement under high concurrency
- Optimistic concurrency on hot aggregates
