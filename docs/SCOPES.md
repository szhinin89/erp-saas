# Scopes

Every new entity must declare one primary scope. See [ARCHITECTURE.md](./ARCHITECTURE.md) for hierarchy.

## SaaS scope (`subscriber_id`)

| Data | Tables (examples) |
|------|-------------------|
| Subscriber root | `subscribers` |
| Plans & features | `commercial_plans`, `commercial_plan_limits`, `platform_features`, `subscriber_subscriptions` |
| Billing | `subscriber_billing_accounts`, `saas_billing_*`, `payment_provider_*` |
| Navigation | `subscriber_custom_menus` |
| Platform config | `config_global`, `config_module`, `config_feature` |

**JWT:** `subscriber_id` required for tenant-scoped platform operations.

## ERP operational scope (`company_id` target)

| Data | Status |
|------|--------|
| Sales, purchases, accounting, cash | `subscriber_id` filter today; migrating to `company_id` |
| Wave 1 | `products`, `warehouse`, `stock_movement`, `current_stock` have nullable `company_id` |

**JWT:** `company_id` required for ERP handlers (`CompanyScopeBehavior`).

## Billing scope (`subscriber_id` only)

- Never `company_id`
- `IBillingGovernanceService`, `/api/saas/billing/*`
- Not ERP `sales_invoice` or `billing_settings` (SRI/tirilla)

## IAM scope (`company_id`)

- `company_user_memberships` → **only** `company_id` + `identity_user_id`
- Permissions resolved per `(companyId, userId)`

## Platform scope (no tenant)

- GlobalSuperAdmin: empty `subscriber_id`, `app.is_platform_admin=true` for controlled RLS bypass
- `PlatformQueryReason` required for `IgnoreQueryFilters()`
