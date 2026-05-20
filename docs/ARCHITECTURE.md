# Architecture

Official architecture of the ERP SaaS platform. Modular monolith: **Clean Architecture + CQRS (MediatR)**.

## Layers

| Layer | Project | Responsibility |
|-------|---------|----------------|
| Domain | `ERP.Domain` | Entities, enums, domain exceptions, interfaces |
| Application | `ERP.Application` | Use cases, behaviors, DTOs, ports |
| Infrastructure | `ERP.Infrastructure` | EF Core, Redis, guards, billing, limits |
| API | `ERP.API` | HTTP, JWT, middleware, authorization policies |

Dependency rule: API → Application → Domain; Infrastructure implements Application ports.

## Hierarchy

```
GlobalSuperAdmin (platform)
  └── Subscriber (SaaS contract: plan, billing, limits)
        └── Company (fiscal / operational entity)
              └── CompanyUserMembership
                    └── ERP modules (Sales, Inventory, Accounting, …)
```

| Actor | Scope key | Pays / governed | Operates ERP |
|-------|-----------|-----------------|--------------|
| GlobalSuperAdmin | none (platform) | — | bypass RLS via `app.is_platform_admin` |
| Subscriber | `subscriber_id` | yes | via companies |
| Company | `company_id` | no | yes (JWT) |

**Rule:** Subscriber pays and is governed. Company operates. SaaS billing never uses `company_id`.

## Bounded contexts (modular monolith)

| Context | Namespace / folder | Scope |
|---------|-------------------|--------|
| Platform / IAM | `Modules/Platform`, `Modules/Access` | `subscriber_id`, `company_id` |
| Subscriptions | `Subscriptions`, `CommercialPlanLimits` | `subscriber_id` |
| Billing | `Billing` | `subscriber_id` only |
| Sales | `Modules/Sales` | `subscriber_id` today → `company_id` target |
| Purchasing | `Modules/Purchasing` | same |
| Inventory | `Modules/Inventario`, `Bodegas` | Wave 1: `company_id` on core tables |
| Accounting | `Modules/Accounting` | `company_id` target |
| Cash | `Modules/Cash` | `company_id` target |
| SRI / electronic docs | `Configuration`, `Sales` | per company settings |

## MediatR pipeline (order)

1. `ValidationBehavior`
2. `BillingGateBehavior` — SaaS account state (suspended / grace)
3. `SubscriptionGateBehavior` — plan features and usage meters
4. `CompanyScopeBehavior` — ERP: JWT `company_id` + membership
5. `CachingBehavior` — read models with explicit cache keys

## Core services

| Concern | Interface | Role |
|---------|-----------|------|
| SaaS context | `ICurrentSubscriber` | Active subscriber from JWT |
| ERP context | `ICurrentCompany` | Active company from JWT |
| Company access | `ICompanyAccessGuard` | Membership + active company |
| Commercial limits | `ICommercialPlanLimitService` | Single enforcement for quotas |
| Entitlements | `ISubscriberEntitlementsSnapshotCache` | Plan features + billing status |
| Billing governance | `IBillingGovernanceService` | Suspend, grace, reactivate |
| Payments (future) | `IPaymentProviderAdapter` | Stripe/Paddle; `NullPaymentProviderAdapter` today |

## CQRS conventions

- Commands/queries under `ERP.Application/Modules/{Module}/UseCases/`
- Handlers are thin: load aggregates, call domain, persist via repositories
- ERP handlers in scoped namespaces implement `ICompanyScopedRequest` or are covered by `CompanyScopeBehavior` namespace rules
- Platform endpoints implement `ISubscriberOnlyRequest` when no company context

## Caching

| Cache | Key pattern | Invalidation |
|-------|-------------|--------------|
| Entitlements | `entitlements:snapshot:{subscriberId}:v{N}` | Plan/billing mutations |
| Permissions | distributed per `(companyId, userId)` | Profile/membership changes |

Redis optional via `ConnectionStrings:Redis`; in-memory fallback when disabled.

## API surface (stable)

| Area | Base route |
|------|------------|
| Auth | `/api/auth/*` (login, refresh, switch-company) |
| Companies | `/api/companies/*` |
| SaaS billing | `/api/saas/billing/*` |
| Entitlements | `/api/saas/entitlements` |
| ERP modules | `/api/{module}/*` (sales, purchases, inventory, …) |
| SuperAdmin | `/api/superadmin/*`, `/api/admin/*` |

## Frontend alignment

- JWT claims: `subscriber_id`, `company_id`
- Flow: login → subscriber → `POST /api/auth/switch-company` when N companies
- SaaS UI: `/saas/companies`, `/select-company`, `CompanySwitcher`
- Some legacy route/i18n aliases still say `tenant` (UX rename deferred)

## Related docs

- [MULTITENANCY.md](./MULTITENANCY.md)
- [SCOPES.md](./SCOPES.md)
- [SECURITY.md](./SECURITY.md)
- [DATABASE/DATABASE-ARCHITECTURE.md](./DATABASE/DATABASE-ARCHITECTURE.md)
- [STATUS.md](./STATUS.md)
- [ROADMAP.md](./ROADMAP.md)
