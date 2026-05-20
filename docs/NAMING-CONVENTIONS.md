# Naming Conventions (Official)

## Domain (C#)

| Use | Avoid |
|-----|-------|
| `Subscriber` | `Tenant` |
| `Company` | `Empresa` as type name |
| `CompanyUserMembership` | `Membership` alone |
| `SubscriberSubscription` | `TenantSaasSubscription` |
| `CommercialPlanLimit` | hardcoded limits |

## Database

- Tables: `snake_case` plural (`subscribers`, `company_user_memberships`)
- Columns: `snake_case` (`subscriber_id`, `company_id`)
- PK: `PK_{table}`
- FK: `fk_{child}_{parent}`
- Indexes: `ix_`, `ux_`, `uq_` with **`_subscriber_`** not `_tenant_`

## SaaS vs ERP billing

| SaaS | ERP fiscal |
|------|------------|
| `saas_billing_invoices` | `sales_bill`, `sales_invoice` |
| `SubscriberBillingAccount` | `BillingSettings` (SRI/tirilla) |

## API routes

- Preferred: `/api/companies`, `/api/saas/billing`
- Legacy alias (compat): `/api/admin/iam/tenant/*` — do not use for new endpoints

## Cache keys

- `entitlements:version:{subscriberId}`
- `entitlements:snapshot:{subscriberId}:v{N}`
- `permissions:{companyId}:{userId}`
