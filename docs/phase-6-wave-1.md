# Fase 6 — Oleada 1: Warehouse / Inventory / Product

## Alcance migrado

| Entidad / tabla | `company_id` | Filtro repositorio | `CompanyScopeBehavior` | RLS |
|-----------------|-------------|-------------------|------------------------|-----|
| `products` | baseline + backfill | `ProductRepository` | `ERP.Application.Products` | baseline |
| `warehouse` | baseline + backfill | `WarehouseRepository` | `ERP.Application.Modules.Inventory` | baseline |
| `stock_movement` | baseline + backfill | (kardex / handlers) | inventario | baseline |
| `current_stock` | baseline + backfill | `StockRepository` | inventario | **oleada 1** (`rls_current_stock_enterprise`) |
| `stock_transfer` | migración `Wave1InventoryCompanyScope` | `StockTransferRepository` | inventario | RLS en `Wave2SalesCompanyScope` |
| `stock_adjustment` | migración `Wave1InventoryCompanyScope` | `StockAdjustmentRepository` | inventario | RLS en `Wave2SalesCompanyScope` |

## Cambios aplicados

- Dominio: `StockTransfer`, `StockAdjustment` implementan `ICompanyOperationalEntity`.
- Migración incremental: `20260520223659_Wave1InventoryCompanyScope` (columnas, índices, backfill SQL, RLS `current_stock`).
- `CompanyOperationalQueryExtensions` + repos de inventario/productos filtran por `ICurrentCompany`.
- Handlers de creación asignan `company_id` desde `ICurrentCompany` y validan bodegas/productos de la misma empresa.
- `CompanyScopeBehavior`: namespaces `ERP.Application.Products` y `ERP.Application.Modules.Products`.
- `CreateCompanyHandler`: `CommercialPlanLimitExceededException` propaga → HTTP **403** (middleware).
- Seed demo: bodega principal con `company_id` de la empresa por defecto.

## Fuera de oleada 1 (sin cambios operativos)

- Ventas, compras, contabilidad, caja (siguen reglas previas / fases posteriores).
- Catálogos de producto (líneas, marcas, UoM) siguen en scope **subscriber** (maestros compartidos).
- RLS global para `stock_transfer` / `stock_adjustment` (siguiente oleada).
- Event handlers de ventas/compras que crean movimientos: deben recibir `company_id` en oleada 2 al unificar kardex transaccional.

## Verificación

```bash
dotnet build backend/src/ERP.API/ERP.API.csproj
dotnet ef database update --project backend/src/ERP.Infrastructure --startup-project backend/src/ERP.API
cd frontend && npm run build
E2E_API_URL=http://localhost:5003 npm run test:e2e
```

Smoke manual: [enterprise-smoke-tests.md](./enterprise-smoke-tests.md).
