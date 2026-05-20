# Fase 6 — Oleada 2: Ventas + Kardex + RLS complementario

## Alcance migrado

| Área | Cambio |
|------|--------|
| `sales_bill` | Columna `company_id`, índice `(subscriber_id, company_id)`, RLS empresa |
| `customers` | Columna `company_id`, índice único por empresa + documento, RLS subscriber+empresa |
| `sales_document` | Backfill `company_id`, índice, RLS (schema unificado) |
| `stock_transfer` / `stock_adjustment` | RLS `rls_*_enterprise` (cierre oleada 1) |
| Kardex ventas | `IInventoryPostingService` — única puerta para salidas/devoluciones por venta |
| Handlers ventas | `CreateSale`, `CreateCustomer`, repos filtran por `ICurrentCompany` |

## Integración inventario

- `SalesBill.Authorize` emite `SalesBillAuthorizedEvent` con `CompanyId` y líneas de stock.
- `SalesBillAuthorizedEventHandler` / `SalesNoteAuthorizedEventHandler` delegan en `IInventoryPostingService`.
- Movimientos y `current_stock` reciben el mismo `company_id` de la venta.

## Fuera de alcance (pendiente)

- `sales_note` / retenciones sin `company_id` explícito (aislamiento vía factura original en queries legacy).
- `sales_detail` sin columna `company_id` (aislamiento vía `sales_document` + RLS padre).
- Catálogos SRI / secuenciales por empresa (ya en tablas `company_*`).
- Compras y contabilidad operativa por empresa (oleada 3+).

## RLS y jobs

Política estándar (igual que inventario oleada 1):

```sql
COALESCE(current_setting('app.is_platform_admin', true), '') = 'true'
OR (
  subscriber_id::text = NULLIF(current_setting('app.subscriber_id', true), '')
  AND (company_id IS NULL OR company_id::text = NULLIF(current_setting('app.company_id', true), ''))
)
```

- Conexiones HTTP: interceptor + claim JWT `company_id`.
- Jobs / tests: `JobCompanyContext.Current = <companyId>` (sin hardcodear en handlers).
- SuperAdmin / migraciones: `app.is_platform_admin = true` o bypass documentado en `enterprise-smoke-tests.md`.

## Riesgos

- Datos legacy con `company_id` NULL: visibles solo sin filtro de empresa hasta backfill; migración asigna empresa activa más antigua por suscriptor.
- Clientes duplicados entre empresas: el índice único ahora es por `(subscriber, company, tipo, número)`.
- Tests de integración requieren empresa + membership en seed (`IntegrationSeedData`).

## Verificación

```bash
dotnet build backend/src/ERP.API/ERP.API.csproj
dotnet ef database update --project backend/src/ERP.Infrastructure --startup-project backend/src/ERP.API
cd frontend && npm run build
E2E_API_URL=http://localhost:5003 npm run test:e2e
```

Smoke ventas + switch-company: sección 8 en [enterprise-smoke-tests.md](./enterprise-smoke-tests.md).
