# Security Boundaries

## Authentication

- JWT session tokens (`subscriber_id`, `company_id`, user claims)
- Refresh token rotation
- GlobalSuperAdmin: role `SuperAdmin` + empty `subscriber_id`

## Authorization

- Permission policies: `perm:*`
- `CompanyScopeBehavior` for ERP modules (namespace + `ICompanyScopedRequest`)
- `BillingGateBehavior` for billing state
- `SubscriptionGateBehavior` for plan features

## Defense in depth

1. JWT validation
2. MediatR behaviors
3. EF global filters
4. **PostgreSQL RLS** (wave 1 tables)
5. `app.is_platform_admin` bypass (controlled, SuperAdmin only)

## Session variables (PostgreSQL)

```sql
app.subscriber_id
app.company_id
app.is_platform_admin  -- 'true' for platform admin
```

Set by `PostgreSqlSessionContextInterceptor` on connection open.

## Rate limiting

Policy `per-subscriber`: 600 req/min (configurable in code).

## Logging

- `EnterpriseDiagnosticMiddleware`: `subscriber_id`, `company_id`, `user_id`, `correlation_id`
- `ForbiddenAccessLoggingMiddleware`: 401/403 audit

## Forbidden patterns

- Accept `company_id` from body without JWT check
- `IgnoreQueryFilters()` without `PlatformQueryReason`
- Stripe SDK inside handlers
- Billing scoped by `company_id`
