# Multi-Tenancy & Multi-Company

## Model

| Concept | Table | Role |
|---------|-------|------|
| **Subscriber** | `subscribers` | SaaS tenant: subscription, billing, commercial limits |
| **Company** | `company` | Fiscal/operational entity (RUC, branding, SRI config) |
| **Membership** | `company_user_memberships` | User ↔ company (IAM); **only** `company_id` |

One subscriber has **N companies** (`ix_company_subscriber_id`).

## Isolation (defense in depth)

| Layer | Mechanism |
|-------|-----------|
| JWT | `subscriber_id`, `company_id` claims |
| MediatR | `CompanyScopeBehavior`, `BillingGateBehavior`, `SubscriptionGateBehavior` |
| Application | `ICompanyAccessGuard`, `ICurrentSubscriber`, `ICurrentCompany` |
| EF Core | Global query filter on `ISubscriberScopedEntity` |
| PostgreSQL | RLS on Wave 1 tables (see [DATABASE/RLS.md](./DATABASE/RLS.md)) |

## Context switching

1. Login / bootstrap → token with `subscriber_id`
2. If one company → auto `company_id` in JWT
3. If many → `/select-company` → `POST /api/auth/switch-company`
4. Handlers read `ICurrentCompany` — never trust body `company_id` as authority

## Background jobs (Hangfire)

Before DB work set:

- `JobSubscriberContext`
- `JobCompanyContext`

So `PostgreSqlSessionContextInterceptor` applies `app.subscriber_id` and `app.company_id`.

## Commercial limits

All quotas via `ICommercialPlanLimitService` — never manual `COUNT(*)` in handlers.

## Retired terminology

Do not use `Tenant`, `tenant_id`, or `memberships` in new code/schema. Use **Subscriber** / **subscriber_id**.
