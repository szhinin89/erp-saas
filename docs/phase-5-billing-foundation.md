# Phase 5 — Enterprise Billing + Governance Foundation

## 1. Impact analysis

| Layer | Change | ERP impact |
|-------|--------|------------|
| **SaaS Billing Domain** | New tables `subscriber_billing_accounts`, `saas_billing_*`, `payment_provider_*` | None |
| **ERP Financial** | `sales_invoice`, `journal_entry`, SRI unchanged | Isolated |
| **Governance** | `IBillingGovernanceService` + `BillingGateBehavior` | Blocks platform when suspended |
| **Entitlements** | Versioned distributed cache wrapper | Faster reads; invalidation on billing events |
| **RLS** | `PostgreSqlSessionContextInterceptor` | Prep only — policies not enabled |

**Rule enforced:** Subscriber pays (`subscriber_id`). Company operates (`company_id`). Billing never scoped by `company_id`.

## 2. Billing domain design

```
ERP.Domain.Billing
├── Entities
│   ├── SubscriberBillingAccount   (1:1 subscriber)
│   ├── SaasBillingInvoice         (≠ sales_invoice)
│   ├── SaasBillingInvoiceLine
│   ├── BillingEvent               (audit trail)
│   ├── PaymentProviderCustomer
│   └── PaymentProviderSubscription
├── Enums (status, provider, trial, renewal)
└── Exceptions (BillingAccessDeniedException)
```

**Application**

- `IBillingGovernanceService` — grace, suspend, reactivate, events
- `IPaymentProviderAdapter` — Stripe/Paddle-ready; `NullPaymentProviderAdapter` today
- CQRS: `GET /api/saas/billing/account|invoices|events`

## 3. Dependency analysis

```
SaasBillingController
  → MediatR (Billing UseCases)
    → ISubscriberBillingRepository
    → IBillingGovernanceService
    → ICurrentSubscriber

BillingGateBehavior (all MediatR)
  → IBillingGovernanceService

SubscriberEntitlementsService
  → ISubscriberEntitlementsSnapshotCache (IDistributedCache / Redis)
  → IBillingGovernanceService (billing status in snapshot)

CompanyProvisioning / limits
  → ICommercialPlanLimitService (unchanged — single source of truth)
```

## 4. Security analysis

- Billing tables use global query filter via `ISubscriberScopedEntity` + `subscriber_id`
- Membership + JWT unchanged for ERP routes
- `BillingGateBehavior` fails closed on suspension (403)
- `BillingEvent` append-only audit for governance traceability
- No card data stored — only `default_payment_method_ref` + provider external IDs

## 5. PostgreSQL RLS readiness

On each connection open:

```sql
SELECT set_config('app.subscriber_id', '<uuid>', true);
SELECT set_config('app.company_id', '<uuid>', true);
```

Components: `ISessionContext`, `HttpSessionContext`, `DbSessionContextApplicator`, `PostgreSqlSessionContextInterceptor`.

**Not enabled:** RLS policies on tables (Phase 6+).

## 6. Entitlements cache strategy

| Key | Purpose |
|-----|---------|
| `entitlements:version:{subscriberId}` | Monotonic version; bump on invalidation |
| `entitlements:snapshot:{subscriberId}:v{N}` | Serialized `CachedSubscriberEntitlements` |

Config: `SaasEntitlementsCache:Enabled`, `TtlSeconds` (default 300).

Invalidation triggers: billing suspend/reactivate/grace, plan change (extend as needed).

`SubscriberEntitlementsSnapshot` extended: `SubscriberId`, `BillingAccountStatus`, `Version`, `ResolvedAtUtc`.

## 7. Commercial plan limits (extended seeds)

| Code | Starter | Business | Professional | Enterprise |
|------|---------|----------|--------------|------------|
| MAX_COMPANIES | 1 | 3 | 10 | ∞ |
| MAX_USERS | 5 | 25 | 100 | ∞ |
| MAX_BRANCHES | 2 | 10 | 50 | ∞ |
| MAX_WAREHOUSES | 2 | 10 | 50 | ∞ |

Usage providers: `MaxCompanies`, `MaxUsers`, `MaxBranches`, `MaxWarehouses`.

Reserved in domain (providers TBD): `MAX_STORAGE_MB`, `MAX_AI_TOKENS`, `MAX_API_REQUESTS`.

## 8. EF migration

`20260520240000_SaasBillingFoundation.cs` — apply with `dotnet ef database update`.

## 9. API endpoints

| Method | Route | Permission |
|--------|-------|------------|
| GET | `/api/saas/billing/account` | `perm:saas.billing.view` |
| GET | `/api/saas/billing/invoices` | `perm:saas.billing.view` |
| GET | `/api/saas/billing/events` | `perm:saas.billing.view` |

## 10. Frontend impact

- New route recommended: `/saas/billing` (read-only account + invoices + audit)
- Reuse entitlements API — snapshot now includes `billingAccountStatus`
- No ERP accounting UI changes

## 11. Risks

| Risk | Mitigation |
|------|------------|
| Confusion SaaS vs ERP invoices | Table prefix `saas_billing_*`, docs, API namespace `/api/saas/billing` |
| Billing gate blocks all MediatR | Only when `SubscriberId` set and account suspended |
| Cache stale entitlements | Version bump on governance mutations |
| Stripe SDK in handlers | Forbidden — only `IPaymentProviderAdapter` implementations |

## 12. Future Stripe integration strategy

1. Implement `StripePaymentProviderAdapter : IPaymentProviderAdapter`
2. Webhook controller `POST /api/saas/billing/webhooks/stripe` (signature verify)
3. Map webhooks → `BillingEvent` + update `PaymentProviderSubscription`
4. Sync `SubscriberBillingAccount.ExternalCustomerId` via `EnsureCustomerAsync`
5. Never import Stripe types into Domain or Application handlers

## 13. ERP migration preparation

See [erp-company-id-migration-roadmap.md](./erp-company-id-migration-roadmap.md).
