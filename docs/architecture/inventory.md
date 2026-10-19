# Inventario — SSOT funcional

Única fuente funcional del módulo Inventario: bodegas, stock, Kardex, valoración, ajustes, transferencias y saldo inicial. Describe solo el comportamiento implementado y cubierto por tests vigentes. Asientos contables derivados: [accounting.md](./accounting.md). Rationale: [ADR-040](../decisions/ADR-040-item-company-scope-inventory-nature.md), [ADR-041](../decisions/ADR-041-negative-sale-inventory-cost-obligations.md).

Código: `ERP.Domain/Modules/Inventory`, `ERP.Application/Modules/Inventory`, `ERP.Infrastructure/Persistence/Repositories/Inventory/StockRepository.cs`. API: `/api/v1/inventory/{stock,kardex,warehouses,adjustment-reasons}`.

## 1. Bodegas

- `Warehouse` pertenece a una Branch de la empresa actual (Branch Ownership Rule); la sucursal debe existir y pertenecer a la empresa. El código se genera (`WH-…`) y es único dentro de la sucursal.
- Capacidad opcional con la escala de `WarehousePrecision.Capacity`; un valor con más decimales se rechaza. Admite marcar como principal, habilitar y deshabilitar; no se borra.

## 2. Participación y alcance

- Stock y Kardex solo aceptan un `Item` Product de la empresa actual ([items.md § 3](./items.md#3-naturaleza-operativa-itemnature)). `StockRepository.AppendMovementAsync` lo valida y los triggers `a1_stock_product_scope` / `a1_movement_product_scope` lo garantizan en PostgreSQL. Las pruebas que escriben stock o Kardex deben sembrar ese Item real.
- Todo movimiento exige contexto de empresa igual a la empresa del movimiento. Su `BranchId` es el de la sucursal dueña de la bodega afectada, no el de la sesión.
- Consultas: stock por bodega y movimientos por bodega exigen una bodega de la sucursal activa. Kardex por producto, por documento y detalle de movimiento son de alcance empresa.

## 3. Kardex (`StockMovement`)

- Tipos registrados por flujos vigentes: `PurchaseEntry`, `PurchaseCancelled`, `PurchaseReturn` ([purchases.md](./purchases.md)); `SaleExit`, `SaleReturn` (Ventas); `PositiveAdjust`, `NegativeAdjust`; `TransferExit`, `TransferEntry`; `InitialBalance`.
- Append-only, con secuencia por Company + Product + Warehouse (`uq_stock_movements_company_product_warehouse_sequence`). Cada movimiento se serializa con `pg_advisory_xact_lock` por Tenant + Company + Product + Warehouse dentro de la transacción. Ante conflicto de secuencia se reintenta hasta 3 veces desde una unidad de trabajo restaurada.
- `CurrentStock` es una proyección derivada del mismo movimiento, nunca su origen.

## 4. Valoración (promedio ponderado)

- La secuencia, el costo promedio corrido y el valor corrido salen del último movimiento real de la clave, nunca de `CurrentStock`.
- Las entradas usan el costo unitario del llamador. Las salidas sin costo propio consumen el promedio corrido y no lo alteran.
- Escalas: el costo promedio usa `AverageCostDecimals` de la política de precisión de la empresa, que es obligatoria y falla cerrada; el valor de stock usa `StockMovement.StockValueScale` ([data-standards.md](./data-standards.md#estándar-de-precisión-numérica)).
- Sin base de costo conocida, el costo queda null (no cero) y la venta registra `SaleCostObligation`. Nunca se deriva costo desde el precio de venta. Una entrada con costo real cubre obligaciones pendientes en orden de secuencia (`SaleCostAllocation`). Las obligaciones pendientes se consultan en `GET /api/v1/inventory/stock/costs/pending`.

## 5. Disponibilidad y stock negativo

- Se exige disponibilidad (`SaleStockPolicy`) solo si el Item es Product, `inventory.stock_control_enabled` de la empresa está ON, el `StockControlEnabled` del Item está ON y `AllowSellWithoutStock` está OFF. Con cualquier control OFF el Product sigue registrando movimientos.
- Solo una `SaleExit` autorizada por esa política puede dejar saldo negativo. Cualquier otra salida que deje saldo negativo se rechaza.

## 6. Ajustes de inventario

- `StockAdjustment` (`ADJ-NNNN`): Draft → Executed → Cancelled. Solo Draft se edita o ejecuta; solo Executed se anula, con motivo obligatorio.
- El motivo (`InventoryAdjustmentReason`) debe existir, estar activo y admitir la dirección (Ingreso, Egreso o Ambos); si `RequiresNotes`, exige observaciones. La cuenta contable del motivo se configura por empresa (`InventoryAdjustmentReasonAccount`).
- Líneas: solo Products de la empresa; cantidad > 0; factor de conversión > 0; costo ≥ 0. Un Ingreso exige costo unitario > 0 y registra `PositiveAdjust` a ese costo. Un Egreso valida disponibilidad cuando la política la exige y registra `NegativeAdjust` al promedio corrido.
- La fecha efectiva es el día de la empresa. La anulación registra el movimiento contrario por línea. Un documento de saldo inicial no se anula desde Ajustes.

## 7. Transferencias y saldo inicial

- `StockTransfer` (`TR-NNNN`): origen distinto de destino, al menos una línea, solo Products de la empresa y acceso inter-sucursal validado. Confirmar (Draft → Confirmed) registra `TransferExit` al promedio corrido de origen y `TransferEntry` en destino con ese mismo costo, cada uno con la sucursal de su bodega.
- Saldo inicial (`PostInitialBalance`, Carga Inicial): bodega activa de la sucursal activa, motivo de apertura que admita Ingreso, fecha de corte no posterior al día de la empresa, Product activo sin lotes ni series, costo > 0 y sin stock ni movimientos previos en la bodega. Registra `InitialBalance` con la fecha de corte.

## 8. Pendiente (no operativo)

- Lotes y series: existen entidades `Lot`/`SerialNumber` y repositorios, pero ningún flujo los usa.
- Reservas de stock: la entidad `StockReservation` no tiene flujo.
- `StockMovementType.SupplierCreditNote` / `SupplierDebitNote`: definidos, pero ningún flujo los registra.
- Anulación de transferencias: `StockTransfer.Cancel` existe en el dominio, pero no hay caso de uso ni endpoint.

## 9. Evidencia

Domain/Application (filtro Items/Inventory/Purchases, 2026-10-19): 354/354 y 551/551 PASS. PostgreSQL 2026-10-19: `StockRepositoryCompanyScopeIntegrationTests`, `StockMovementBranchOwnershipIntegrationTests`, `PurchaseCancelledStockMovementIntegrationTests`, `ResolvePurchaseReceptionLinesIntegrationTests` y `PurchaseInvoiceConfirmedPostingIntegrationTests` 39/39 PASS; `ItemCompanyScopeA1Tests` 16/16 PASS. Cobertura adicional: `NegativeSaleCostA2Tests`, `SaleStockPolicyTests`, `SaleCostObligationTests`, `StockMovementValuationCostTests`, `StockAdjustmentExecuteCancelTests`, `InventoryAdjustmentsEndToEndTests`, `WarehouseBranchOwnershipTests`, `GetStockMovementsBranchScopeTests`, `ConfirmInitialStockAtomicPostgreSqlTests`.
