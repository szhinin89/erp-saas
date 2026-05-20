# SaaS Billing

SaaS billing is **isolated** from ERP financial documents (`sales_invoice`, journals, SRI).

## Domain (`ERP.Domain.Billing`)

| Entity | Table | Purpose |
|--------|-------|---------|
| `SubscriberBillingAccount` | `subscriber_billing_accounts` | 1:1 subscriber; status, grace, trial |
| `SaasBillingInvoice` | `saas_billing_invoices` | Platform invoices (≠ ERP sales) |
| `SaasBillingInvoiceLine` | `saas_billing_invoice_lines` | Line items |
| `BillingEvent` | `saas_billing_events` | Append-only audit trail |
| `PaymentProviderCustomer` | `payment_provider_customers` | External customer id |
| `PaymentProviderSubscription` | `payment_provider_subscriptions` | External subscription id |

Scope: **`subscriber_id` only** — never `company_id`.

## Application

| Component | Role |
|-----------|------|
| `IBillingGovernanceService` | Grace, suspend, reactivate; emits events |
| `BillingGateBehavior` | Blocks MediatR when account suspended |
| `IPaymentProviderAdapter` | Provider abstraction; `NullPaymentProviderAdapter` default |
| `ISubscriberBillingRepository` | Persistence |

## API

| Method | Route | Permission |
|--------|-------|------------|
| GET | `/api/saas/billing/account` | `perm:saas.billing.view` |
| GET | `/api/saas/billing/invoices` | `perm:saas.billing.view` |
| GET | `/api/saas/billing/events` | `perm:saas.billing.view` |

No Stripe SDK in handlers. Webhooks and real adapter are roadmap items ([ROADMAP.md](./ROADMAP.md)).

## Entitlements integration

`SubscriberEntitlementsSnapshot` includes `BillingAccountStatus`. Cache invalidation on governance mutations.

Keys: `entitlements:version:{subscriberId}`, `entitlements:snapshot:{subscriberId}:v{N}`.

## Security

- Global filter via `ISubscriberScopedEntity`
- Fail-closed on suspension (403)
- No card PAN storage — only `default_payment_method_ref` and provider external ids

## ERP distinction

| Table | Context |
|-------|---------|
| `billing_settings` | SRI / RIDE / tirilla per company — **not** SaaS billing |
| `sales_invoice` | ERP sales — **not** `saas_billing_invoices` |
