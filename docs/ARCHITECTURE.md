# ERP SaaS — Official Architecture

Modular monolith: **Clean Architecture + CQRS (MediatR)**.

## Layers

| Layer | Responsibility |
|-------|----------------|
| `ERP.Domain` | Entities, enums, domain exceptions |
| `ERP.Application` | Use cases, behaviors, DTOs, interfaces |
| `ERP.Infrastructure` | EF, Redis cache, governance, guards |
| `ERP.API` | HTTP, JWT, middleware, authorization |

## Hierarchy

```
GlobalSuperAdmin
  → Subscriber (SaaS billing, plan, limits)
    → Company (fiscal, branding, ERP scope)
      → CompanyUserMembership
        → ERP modules (Sales, Inventory, …)
```

## MediatR pipeline (order)

1. `ValidationBehavior`
2. `BillingGateBehavior` — billing SaaS
3. `SubscriptionGateBehavior` — features/usage
4. `CompanyScopeBehavior` — subscriber + company + membership
5. `CachingBehavior`

## Migrations

Single baseline + incremental: see [Migrations/README.md](../backend/src/ERP.Infrastructure/Migrations/README.md).

## Related

- [SCOPES.md](./SCOPES.md)
- [SECURITY-BOUNDARIES.md](./SECURITY-BOUNDARIES.md)
- [final-enterprise-architecture.md](./final-enterprise-architecture.md)
