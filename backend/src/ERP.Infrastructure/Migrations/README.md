# EF Core Migrations — Enterprise Policy

## Single baseline

This project uses **one official baseline migration** representing the full enterprise schema (including Wave 1 `company_id` columns and PostgreSQL RLS):

- `20260520215307_InitialEnterpriseBaseline`

Do **not** add hand-written migration `.cs` files without:

```bash
dotnet ef migrations add <Name> --startup-project ../ERP.API/ERP.API.csproj
```

Every migration must include `.cs`, `.Designer.cs`, and an updated `ErpDbContextModelSnapshot.cs`.

## New environments

```bash
cd backend/src/ERP.Infrastructure
dotnet ef database update --startup-project ../ERP.API/ERP.API.csproj
```

Or rely on `Database.MigrateAsync()` at API startup.

## Fresh database (dev)

```sql
DROP SCHEMA public CASCADE;
CREATE SCHEMA public;
GRANT ALL ON SCHEMA public TO postgres;
GRANT ALL ON SCHEMA public TO public;
```

Then `dotnet ef database update`.

## Naming

- Tables/columns: `snake_case`
- Indexes/FK: `ix_*`, `ux_*`, `uq_*`, `fk_*` with **`_subscriber_`** (not `_tenant_`)
- SaaS billing tables: `saas_billing_*`, `subscriber_billing_accounts`

Legacy migration history before 2026-05-20 was **removed** intentionally. See `docs/final-enterprise-architecture.md`.
