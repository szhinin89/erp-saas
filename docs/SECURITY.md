# Security

## Authentication

| Mechanism | Detail |
|-----------|--------|
| Session | JWT access token (`subscriber_id`, `company_id`, `user_type`, claims) |
| Refresh | Rotation via `refresh_tokens` (`POST /api/auth/refresh`) |
| First run | `POST /api/setup/superadmin` with one-time setup token (banner en startup) |
| Platform login | `POST /api/platform/auth/login` → `identity_users` con `user_type=Platform` |
| Company login | `POST /api/auth/login` → membership + `company_id` en JWT |

Detalle IAM: [identity-model.md](./identity-model.md).

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
3. EF global query filters (`EnterpriseQueryFilterConfigurator`)
4. PostgreSQL RLS (baseline enterprise tables)
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
