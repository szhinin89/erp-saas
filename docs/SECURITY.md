# Security

## Authentication

| Mechanism | Detail |
|-----------|--------|
| Session | JWT access token (`subscriber_id`, `company_id`, user claims) |
| Refresh | Rotation via `refresh_tokens` |
| First run | `POST /api/setup/superadmin` with one-time setup token |
| SuperAdmin | Role `SuperAdmin`; empty `subscriber_id` |

## Authorization

| Layer | Component |
|-------|-----------|
| Policies | `perm:*` permission claims |
| ERP scope | `CompanyScopeBehavior` + `ICompanyAccessGuard` |
| Billing | `BillingGateBehavior` (fail-closed on suspend) |
| Features | `SubscriptionGateBehavior` + entitlements snapshot |

## Defense in depth

1. JWT validation
2. MediatR behaviors (billing → subscription → company scope)
3. EF global query filters (`ISubscriberScopedEntity`)
4. PostgreSQL RLS (Wave 1 tables)
5. Controlled SuperAdmin bypass (`app.is_platform_admin`)

## PostgreSQL session variables

Set on connection open by `PostgreSqlSessionContextInterceptor`:

```sql
app.subscriber_id
app.company_id
app.is_platform_admin   -- 'true' only for platform admin
```

## Rate limiting

Policy `per-subscriber`: **600 requests/minute** (configurable in `Program.cs`).

## Observability

| Middleware | Purpose |
|------------|---------|
| `EnterpriseDiagnosticMiddleware` | `subscriber_id`, `company_id`, `user_id`, `correlation_id` |
| `ForbiddenAccessLoggingMiddleware` | Audit 401/403 |

## Forbidden patterns

- Accept `company_id` from request body without JWT + membership validation
- `IgnoreQueryFilters()` without documented `PlatformQueryReason`
- Stripe or payment SDK inside MediatR handlers
- SaaS billing tables scoped by `company_id`
- Hardcoded commercial limits (`MAX_COMPANIES`, etc.)
- Hand-written EF migrations (use `dotnet ef migrations add`)

## RLS

Policies and job bypass rules: [DATABASE/RLS.md](./DATABASE/RLS.md).

## Development verification

Smoke checklist (login, switch-company, billing gate, 403 on wrong company) is listed in [DEVELOPMENT-RULES.md](./DEVELOPMENT-RULES.md#verification).
