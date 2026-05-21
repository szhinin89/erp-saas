# ERP.API.Tests — failure map (enterprise consolidation)

**Final run:** `156 passed / 0 failed` (`api-tests-final.trx`, Release, EF InMemory, `Testing` environment)

This document maps the original ~80 failures to root causes and the fixes applied during the `/api/platform/*` + company-scope migration.

## Test harness (deterministic host)

| Component | Role |
|-----------|------|
| `IntegrationTestWebAppFactory` | `Testing` env, InMemory DB, Hangfire/demo seed off, `AllowAllSubscriptionService` + `AllowAllEntitlementsService` |
| `TestJwtFactory` | JWT with optional `company_id` + `user_type=subscriber` |
| `TestAuthClient` | HTTP client with seed + mutable company |
| `TestDataFactory` / `IntegrationSeedData` | Subscriber, company, branch, product, warehouse, accounts; sets `JobCompanyContext.Current` |
| `MutableCurrentCompany` | Enables `ForOperationalScope` queries in repositories |

## Root-cause categories → fixes

### 1. Company scope (`company_id` / `ICurrentCompany`)

**Symptom:** Stock/kardex empty, transfers with 0 origin stock, purchase approval not visible in `GetCurrentStock`.

**Fix:**
- Seed `CurrentStock` / `StockMovement` with `companyId: seed.CompanyId`
- `PurchBillApprovedEventHandler` / `PurchNoteApprovedEventHandler`: inject `ICurrentCompany`, stamp stock + movements
- `StockRepository` InMemory increment: create `CurrentStock` with company when context exists
- Tests: `WarehouseTestHelpers`, `TransferenciasEndToEndTests`, `AjustesInventarioEndToEndTests`, `KardexConmutacionTests` (explicit UTC dates + `companyId`)

### 2. English domain statuses vs Spanish handler checks

**Symptom:** `"Solo se puede enviar una OC en Borrador (estado actual: Draft)"`, transfer/adjustment/PO/note flows failing on valid entities.

**Fix (handlers aligned to domain):**
- PO: `Draft` / `Sent` / `Closed` / `Cancelled`
- Purch notes: `Draft` → approve
- Issued retention: `Draft` / `Validated` / `Authorized`
- Sales notes SRI send: `Draft` / `Validated`
- Transfer/adjustment cancel: already `Draft` (prior fix)

### 3. Note type normalization (`CREDIT`/`DEBIT` vs `CREDITO`/`DEBITO`)

**Symptom:** Supplier note import/approve failures; sales note SRI called `AuthorizeDebitNote` for `CREDIT`.

**Fix:**
- `SriFacturaParser`: emit `CREDIT` / `DEBIT`
- Handlers accept both legacy and canonical where needed (`AprobarCompraNotaProveedor`, `EnviarVentasNotaSri`, event handlers)

### 4. Legacy routes & DTOs

**Symptom:** 404 on Spanish paths (`/api/gastos`, `/api/compras`, …).

**Fix:** Tests updated to canonical routes (`/api/expenses`, `/api/purchases/invoices`, `/api/inventory/kardex`, `/api/admin/iam/me/permissions`, etc.) and English payload fields.

### 5. Assertion drift (numbers, labels, defaults)

| Area | Was | Now |
|------|-----|-----|
| Transfer confirm/cancel | `Confirmado` / `Cancelado` | `Confirmed` / `Cancelled` |
| Stock adjustment | `AJ-*`, `Ejecutado` | `ADJ-*`, `Executed` |
| PO link complete | `Cerrada` | `Closed` |
| Purch note approved | `IsApproved` | `Approved` |
| Issued retention | `Autorizado` | `Authorized` |
| Sales note authorized | `Autorizado` | `Authorized` |
| Tirilla sin config | `EMPRESA DEMO` | `DEMO COMPANY` (from `BillingSettings.CreateDefault`) |
| Kardex movement labels | raw enum **or** Spanish | Spanish labels via `KardexService.DescripcionTipo` (English enum keys mapped) |

### 6. Kardex date boundaries

**Symptom:** `OpeningQuantity` 0 when using `DateTime.Today` vs `UtcNow` movements.

**Fix:** `KardexConmutacionTests` uses explicit `DateTime.UtcNow.Date` window (same pattern as `KardexInventarioTests`).

### 7. Retention & accounting test data

**Symptom:** No supplier retention rates; retention received missing liability account name match.

**Fix:**
- `RetentionSettings.Create(..., "Supplier", ...)` → stored as `SUPPLIER`; repository filters `SUPPLIER` / `AMBOS`
- Liability account name includes `IMPUESTOS` for IVA pasivo lookup

### 8. Entitlements / subscription (fail-closed)

**Symptom:** Feature gates blocking inventory/purchasing in tests.

**Fix:** `AllowAllEntitlementsService` + existing `AllowAllSubscriptionService`.

## Production bugs fixed (minimal, required for runtime/tests)

| File | Issue |
|------|--------|
| `CreateSaleCommandHandler` | Missing `_currentCompany` assignment (NRE) |
| `CancelTransferCommandHandler` / `CancelStockAdjustmentCommandHandler` | Status check `Borrador` → `Draft` |
| `Program.cs` | Skip FirstRunSetup / CommercialPlansBootstrap in Testing; Swagger in Testing |
| `SriFacturaParser` | Note types `CREDIT` / `DEBIT` |
| `KardexService.DescripcionTipo` | Map English `StockMovementType` names to UI labels |
| `ConfiguracionRetencionRepository` | Match `SUPPLIER` subject type (uppercase storage) |
| Event handlers + `StockRepository` | Company-scoped stock creation |

## Commands

```powershell
cd erp-saas\backend\src
dotnet test ERP.API.Tests\ERP.API.Tests.csproj -c Release `
  --logger "trx;LogFileName=api-tests.trx" `
  --results-directory TestResults
```

## Not in scope (follow-ups)

- **Shared `WebApplicationFactory` collection fixture** (reduce ~4–5s per test startup) — not implemented
- **`IntegrationExternal` suite** for real Postgres — no tests required it after InMemory + company-scope fixes
- Remaining Spanish statuses in **sales invoices** domain (`Borrador`, `Autorizado`) — unchanged; sales tests still use those where domain has not migrated
