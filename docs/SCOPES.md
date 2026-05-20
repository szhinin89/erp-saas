# Official Scopes

Every new entity **must** declare one primary scope.

## SaaS scope (`subscriber_id`)

- `Subscriber`, `CommercialPlan`, `CommercialPlanLimit`
- `SubscriberSubscription`, billing (`subscriber_billing_accounts`, `saas_billing_*`)
- `SubscriberCustomMenu`, platform config

**JWT:** `subscriber_id` claim required for tenant operations.

## ERP operational scope (`company_id`)

- Target for all operational data (Sales, Inventory, Accounting, …)
- **JWT:** `company_id` claim required for ERP handlers (`CompanyScopeBehavior`)
- Oleada 1: `products`, `warehouse`, `stock_movement`, `current_stock` have nullable `company_id` + backfill

Legacy rows may still filter by `subscriber_id` until Phase 6 completes.

## Billing scope (`subscriber_id`)

- Never `company_id`
- `IBillingGovernanceService`, `/api/saas/billing/*`

## IAM scope (`subscriber_id` + `company_id`)

- `company_user_memberships` → **only** `company_id`
- Permissions resolved per `(companyId, userId)`

## Platform scope (no tenant)

- GlobalSuperAdmin: `subscriber_id` empty, `app.is_platform_admin=true` for RLS bypass
