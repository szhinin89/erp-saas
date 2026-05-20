# Enterprise Governance Rules

## NEVER

- Use `IgnoreQueryFilters()` without documented `PlatformQueryReason`
- Take `company_id` from request body as authority
- Hardcode commercial limits (`MAX_COMPANIES`, etc.)
- Mix SaaS billing tables with ERP invoices
- Create entities without scope ([SCOPES.md](./SCOPES.md))
- Add migrations without `dotnet ef migrations add`
- Use `Tenant` in new code or schema

## ALWAYS

- `ICurrentSubscriber` / `ICurrentCompany` for context
- `ICompanyAccessGuard` or `CompanyScopeBehavior` for ERP access
- `ICommercialPlanLimitService` for quotas
- `IEntitlementsCacheService` / snapshot for features
- `IBillingGovernanceService` for SaaS billing state
- Invalidate entitlements cache on plan/billing mutations

## New ERP use cases

1. Place under scoped namespace (Sales, Inventory, …) **or** implement `ICompanyScopedRequest`
2. Mark `ISubscriberOnlyRequest` only for true platform endpoints
3. Pass `companyId` from `ICurrentCompany` into domain `Create` (oleada 1+)

## Phase 6 ERP migration

Follow oleadas in [erp-company-id-migration-roadmap.md](./erp-company-id-migration-roadmap.md). No mass migration in one PR.

## Future-ready (stubs documented)

- Transactional outbox for integration events
- Distributed locks for quota enforcement
- Optimistic concurrency tokens on aggregates
