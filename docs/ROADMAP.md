# Roadmap

Enterprise priorities. For current delivery status see [STATUS.md](./STATUS.md).

## P0 — Production readiness

| Item | Bounded context |
|------|-----------------|
| SRI real environment validation | Sales / SRI |
| Repair CI test suite post-refactor | All |
| Frontend: retenciones emitidas/recibidas | Sales / Purchasing |
| SaaS billing UI (`/saas/billing`) | Billing |

## P1 — ERP company isolation (Phase 6)

Migrate operational data from `subscriber_id` filter to **`company_id`** as primary scope.

| Wave | Modules | Tables (representative) |
|------|---------|-------------------------|
| 1 ✅ | Inventory | `products`, `warehouse`, `stock_movement`, `current_stock` |
| 2 | Sales | `customers`, `sales_bill`, `sales_invoice`, documents |
| 3 | Purchasing | `purch_bill`, `purchase_order`, `suppliers` |
| 4 | Accounting | `accounts`, `journal_entries`, setup |
| 5 | Cash / config | `bank_account`, `billing_settings`, branches |

Strategy per wave:

1. Add nullable `company_id` + backfill from default company
2. Dual-write (subscriber + company)
3. Read switch to `ICurrentCompany` only
4. Drop redundant `subscriber_id` on ERP tables when safe

## P1 — Security hardening

| Item | Detail |
|------|--------|
| RLS wave 2+ | Extend policies to sales/purchasing/accounting |
| Permissions cache | Wire `IPermissionsCacheService` in `PermissionHandler` read path |
| Rename legacy `tenant` routes/i18n | UX consistency |

## P2 — Payments

| Item | Detail |
|------|--------|
| Stripe / Paddle adapter | Implement `IPaymentProviderAdapter` |
| Webhooks | Idempotent billing event ingestion |
| Customer portal | Payment method update (provider-hosted) |

## P2 — Observability

| Item | Detail |
|------|--------|
| Structured metrics | Per-subscriber request rate, limit denials |
| Distributed tracing | OpenTelemetry across API + Hangfire |
| SRI failure dashboard | Authorization retries, rejections |

## P3 — Platform expansion

| Item | Detail |
|------|--------|
| Usage meters | `MAX_STORAGE_MB`, `MAX_AI_TOKENS`, `MAX_API_REQUESTS` |
| AI features | Token accounting per subscriber |
| Analytics | Cross-company reporting within subscriber |
| Marketplace | Third-party modules / integrations catalog |

## P4 — Architecture evolution (optional)

| Item | Detail |
|------|--------|
| Transactional outbox | Integration events |
| Microservices split | Only if bounded context scale demands — start from modular monolith |
| Multi-region | Read replicas + subscriber pinning |

## Deferred (non-blocking)

- OC recepción física sin factura
- Liquidación de compra (tipo 03)
- Partitioning `electronic_doc` / `stock_movement` at scale
- Impersonación operador platform audit log
