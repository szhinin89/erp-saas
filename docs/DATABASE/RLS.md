# Row Level Security (RLS)

PostgreSQL RLS complements application-layer filters. Session variables are set per connection.

## Session variables

| Variable | Set by | Purpose |
|----------|--------|---------|
| `app.subscriber_id` | `PostgreSqlSessionContextInterceptor` | Subscriber isolation |
| `app.company_id` | same | Company isolation |
| `app.is_platform_admin` | same | SuperAdmin bypass when `'true'` |

Components: `ISessionContext`, `HttpSessionContext`, `DbSessionContextApplicator`, `PostgreSqlSessionContextInterceptor`.

## Tables with RLS (baseline)

Policies named `rls_{table}_enterprise`. `FORCE ROW LEVEL SECURITY` is on.

| Table | Policy logic |
|-------|----------------|
| `products`, `warehouse`, `stock_movement`, `current_stock`, `stock_transfer`, `stock_adjustment`, `sales_bill`, `sales_document` | Platform admin OR (`subscriber_id` match AND (`company_id` null OR matches)) |
| `customers` | Platform admin OR (`subscriber_id` + optional `company_id` match) |
| `sales_invoice` | Platform admin OR `company_id` match |

Defined in `EnterpriseBaselineRowLevelSecurity.Apply()` at end of `InitialEnterpriseBaseline.Up()`.

## Platform admin bypass

```sql
COALESCE(current_setting('app.is_platform_admin', true), '') = 'true'
```

Only for authenticated GlobalSuperAdmin flows. Do not set in normal tenant sessions.

## Background jobs (Hangfire)

Before DB access:

1. Set `JobSubscriberContext` with target subscriber
2. Set `JobCompanyContext` with target company when ERP work is company-specific

Without this, RLS may deny all rows.

## Application layer (still required)

RLS does not replace:

- `CompanyScopeBehavior`
- `EnterpriseQueryFilterConfigurator` (EF global filters)
- `ICompanyAccessGuard`
- JWT validation

## Troubleshooting

| Symptom | Check |
|---------|--------|
| Empty result for tenant user | `app.subscriber_id` / `app.company_id` set on connection |
| Job sees no rows | Job context not set |
| SuperAdmin cannot query | `app.is_platform_admin` must be `'true'` |
