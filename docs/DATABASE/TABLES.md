# Tables Reference

Grouped by bounded context. **Scope** = primary isolation key today. **Target** = post Phase 6.

## Platform & IAM

| Table | Purpose | Scope | Relations |
|-------|---------|-------|-----------|
| `subscribers` | SaaS tenant root | platform / `subscriber_id` | → `company`, subscriptions, billing |
| `company` | Fiscal company | `subscriber_id` | → `establishment`, ERP config |
| `company_user_memberships` | User access to company | `company_id` | → `users` |
| `users` | Identity | `subscriber_id` | memberships, refresh tokens |
| `refresh_tokens` | Session refresh | user | |
| `access_profiles` | Role templates | `subscriber_id` | → permissions |
| `access_profile_permissions` | Profile ↔ perm | profile | |
| `user_activity` | Audit | `subscriber_id` | |

## Subscriptions & limits

| Table | Purpose | Scope |
|-------|---------|-------|
| `commercial_plans` | Plan catalog | platform |
| `commercial_plan_features` | Plan ↔ feature | plan |
| `commercial_plan_limits` | Numeric caps | plan |
| `platform_features` | Feature registry | platform |
| `subscriber_subscriptions` | Active subscription | `subscriber_id` |
| `subscriber_subscription_events` | Subscription audit | `subscriber_id` |
| `subscription_feature_overrides` | Per-subscriber override | `subscriber_id` |
| `subscription_usages` | Usage meters | `subscriber_id` |

## SaaS billing (never `company_id`)

| Table | Purpose | Scope |
|-------|---------|-------|
| `subscriber_billing_accounts` | Account status, grace | `subscriber_id` |
| `saas_billing_invoices` | Platform invoices | `subscriber_id` |
| `saas_billing_invoice_lines` | Lines | invoice |
| `saas_billing_events` | Governance audit | `subscriber_id` |
| `payment_provider_customers` | External customer id | `subscriber_id` |
| `payment_provider_subscriptions` | External sub id | `subscriber_id` |

## Company structure

| Table | Purpose | Scope | Target |
|-------|---------|-------|--------|
| `establishment` | SRI establishment | `company_id` | ✅ |
| `emission_point` | Emission point | establishment | |
| `branches` | Operational branch | `subscriber_id` | `company_id` |

## ERP — Sales

| Table | Purpose | Scope today | Target |
|-------|---------|-------------|--------|
| `customers` | Customers | `subscriber_id` + RLS | `company_id` |
| `sales_bill` | Sales document | `subscriber_id` | `company_id` |
| `sales_bill_line` | Lines | bill | |
| `sales_invoice` | Invoice header | `company_id` RLS | ✅ |
| `sales_note` | Credit/debit notes | `subscriber_id` | `company_id` |
| `sales_document` | Unified doc model | `subscriber_id` | `company_id` |
| `sales_withholding` | Withholdings | `subscriber_id` | `company_id` |
| `electronic_doc` | SRI XML/archive | document | |

## ERP — Purchasing

| Table | Purpose | Scope today | Target |
|-------|---------|-------------|--------|
| `purchase_order` | Purchase orders | `subscriber_id` | `company_id` |
| `purch_bill` | Purchase bills | `subscriber_id` | `company_id` |
| `purch_note` | Debit notes | `subscriber_id` | `company_id` |
| `suppliers` | Suppliers | `subscriber_id` | `company_id` |
| `expense_invoice` | Expenses | `subscriber_id` | `company_id` |

## ERP — Inventory (Wave 1)

| Table | Purpose | Scope today | Target |
|-------|---------|-------------|--------|
| `products` | Product master | `subscriber_id` + nullable `company_id` + RLS | `company_id` |
| `warehouse` | Warehouses | same | `company_id` |
| `stock_movement` | Movements | same | `company_id` |
| `current_stock` | On-hand | same | `company_id` |
| `stock_transfer` | Transfers | `subscriber_id` | `company_id` |
| `stock_adjustment` | Adjustments | `subscriber_id` | `company_id` |
| `kardex_snapshot` | Valuation snapshots | `subscriber_id` | `company_id` |

Product child tables (`product_barcodes`, `product_images`, …) hang off `products`.

## ERP — Accounting & cash

| Table | Purpose | Scope today | Target |
|-------|---------|-------------|--------|
| `accounts` | Chart of accounts | `subscriber_id` | `company_id` |
| `journal_entries` | GL entries | `subscriber_id` | `company_id` |
| `accounting_setup` | Posting rules | `subscriber_id` | `company_id` |
| `bank_account` | Banks | `subscriber_id` | `company_id` |
| `petty_cash` | Petty cash | `subscriber_id` | `company_id` |

## ERP — Configuration (per company)

| Table | Purpose | Note |
|-------|---------|------|
| `billing_settings` | SRI RIDE / tirilla | **Not** SaaS billing |
| `sri_*` | Tax catalogs | mostly shared |
| `config_global` / `config_module` / `config_feature` | Platform config | `subscriber_id` |

## Navigation

| Table | Purpose | Scope |
|-------|---------|-------|
| `subscriber_custom_menus` | Custom menu JSON | `subscriber_id` |

## Naming traps

| Name | Meaning |
|------|---------|
| `billing_settings` | ERP electronic billing config |
| `saas_billing_invoices` | SaaS platform invoices |
| `subscribers` | SaaS tenant (not `tenants`) |

Full column-level detail: `ErpDbContextModelSnapshot.cs` or `\d table` in psql.
