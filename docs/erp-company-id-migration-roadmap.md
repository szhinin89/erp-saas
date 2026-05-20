# ERP Runtime — Migration Roadmap: `subscriber_id` → `company_id`

**Status:** Analysis only — no bulk migration executed.

## Scope rule

| Scope | Tables / modules |
|-------|------------------|
| **Stay `subscriber_id`** | SaaS billing, subscriptions, commercial plans, subscriber config, navigation |
| **Migrate to `company_id`** | Sales, purchases, inventory, accounting, cash, products, customers, suppliers, SRI docs |

`company` already has `subscriber_id` (platform). ERP rows should reference `company_id` for operational isolation.

## Entity inventory (subscriber-scoped today)

High-volume ERP aggregates (~75+ entities) implementing `ISubscriberScopedEntity` / `MasterEntity`:

- **Sales:** `SalesBill`, `SalesNote`, `Customer`, payments, retentions, electronic docs
- **Purchasing:** `PurchBill`, `PurchaseOrder`, `Supplier`, etc.
- **Inventory:** `Product`, `Warehouse`, `StockMovement`, `Kardex*`, transfers, adjustments
- **Accounting:** `Account`, `JournalEntry`, setup
- **Cash:** `BankAccount`, `PettyCash`, statements
- **Config ERP:** `BillingSettings` (SRI/tirilla — rename to avoid SaaS confusion), `SriSettings`, branches

**Already company-aware:** `Company`, `CompanyUserMembership`, future SRI certificates per company.

## Phased strategy

### Phase 6a — Dual write (read company, write both)

1. Add nullable `company_id` FK to top-level ERP documents
2. Backfill from JWT `company_id` / default company per subscriber
3. Update repositories to filter `company_id` when present
4. Keep `subscriber_id` for query filters during transition

### Phase 6b — Read switch

1. Handlers use `ICurrentCompany` only
2. Global query filter: `company_id = CurrentCompanyId` for `ICompanyScopedEntity`
3. Deprecate subscriber filter on ERP tables

### Phase 6c — Cleanup

1. `subscriber_id` nullable → drop on ERP tables
2. RLS policies: `company_id = current_setting('app.company_id')::uuid`

## Risks

| Risk | Impact |
|------|--------|
| Cross-company data leak | High — requires membership checks on every query |
| Report historical data | Medium — subscriber-level reports must aggregate companies |
| Performance | Medium — indexes on `(company_id, ...)` per table |
| SRI RUC per company | Already modeled on `company` |

## Detection commands (dev)

```sql
-- Tables with subscriber_id (platform catalog)
SELECT table_name FROM information_schema.columns
WHERE column_name = 'subscriber_id' AND table_schema = 'public'
ORDER BY table_name;
```

## Do not migrate (SaaS platform)

- `subscriber_billing_accounts`
- `saas_billing_invoices`
- `subscriber_subscriptions`
- `commercial_plan_*`
- `subscription_*`
- `payment_provider_*`
