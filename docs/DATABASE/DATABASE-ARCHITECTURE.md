# Database Architecture

PostgreSQL **15+**. ORM: **EF Core 10**. One official migration baseline.

## Principles

| Principle | Implementation |
|-----------|----------------|
| Single schema | `public` |
| Naming | `snake_case` tables/columns |
| SaaS vs ERP | Separate table families — see [TABLES.md](./TABLES.md) |
| Tenant key | `subscriber_id` on platform and transitional ERP rows |
| ERP target key | `company_id` on operational rows (migration in progress) |
| Migrations | EF Core only — [MIGRATIONS.md](./MIGRATIONS.md) |
| Row security | RLS Wave 1 — [RLS.md](./RLS.md) |

## Context diagram

```
subscribers
  ├── company (1:N)
  │     ├── company_user_memberships
  │     └── ERP operational tables → company_id (target)
  ├── subscriber_subscriptions
  ├── subscriber_billing_accounts
  ├── saas_billing_*
  └── commercial_plan_*
```

## EF Core

| Artifact | Path |
|----------|------|
| DbContext | `ERP.Infrastructure/Persistence/ErpDbContext.cs` |
| Configurations | `Persistence/Configurations/**` |
| Interceptor | `PostgreSqlSessionContextInterceptor` |
| Snapshot | `Migrations/ErpDbContextModelSnapshot.cs` |

Global query filters: entities implementing `ISubscriberScopedEntity` (except `Subscriber` root).

## Connection

Configured via `ConnectionStrings:DefaultConnection`. API applies `Database.MigrateAsync()` on startup in Development.

## Environments

| Environment | Policy |
|-------------|--------|
| Development | May drop/recreate DB; single baseline apply |
| Staging / Production | Forward-only migrations; no schema drop |

## Related

- [MIGRATIONS.md](./MIGRATIONS.md)
- [RLS.md](./RLS.md)
- [TABLES.md](./TABLES.md)
