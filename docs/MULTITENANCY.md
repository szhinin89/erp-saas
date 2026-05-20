# Multi-Tenancy & Multi-Company

## Model

- **Subscriber** = contrato SaaS (paga, límites, billing).
- **Company** = entidad fiscal/operativa (RUC, branding).
- Un subscriber tiene **N companies** (`ix_company_subscriber_id`).

## Isolation mechanisms

| Layer | Mechanism |
|-------|-----------|
| Application | `CompanyScopeBehavior`, `ICompanyAccessGuard` |
| EF | Global query filter `ISubscriberScopedEntity` |
| PostgreSQL | RLS wave 1 (products, warehouse, stock_movement, customers, sales_invoice) |
| JWT | `subscriber_id` + `company_id` |

## Switching context

1. Login / bootstrap → `subscriber_id`
2. `POST /api/auth/switch-company` → `company_id` in JWT
3. `ICurrentSubscriber` / `ICurrentCompany` in handlers

## Jobs (Hangfire)

Set `JobSubscriberContext` + `JobCompanyContext` before DB work so interceptors apply session vars.

## Limits

`ICommercialPlanLimitService` — never `COUNT(*)` manual en handlers.
