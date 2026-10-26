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
- Escalas: el costo promedio usa `AverageCostDecimals` de la política de precisión de la empresa, que es obligatoria y falla cerrada ([data-standards.md](./data-standards.md#estándar-de-precisión-numérica)).
- Importe monetario único: `StockMovement.TotalCost`, con `AccountingDecimals` de la misma política. El valor corrido es la suma exacta de esos importes; Contabilidad (costo de ventas, ajustes, devoluciones) y las obligaciones A2 usan el mismo importe.
- Cantidad cero implica valor cero: la salida que vacía la bodega se lleva el valor restante (absorbe el residuo). Si el último movimiento quedó en cantidad cero, el siguiente parte de valor cero; los movimientos históricos no se modifican.
- La entrada por compra registra como importe el tramo en `AccountingDecimals` del acumulado de `TotalLineCost` de las líneas Product en su orden persistido: la suma de las entradas es exactamente el costo inventariable que contabiliza InvoiceReceived y el residuo de las asignaciones a 6 decimales queda en la línea donde el acumulado cruza el centavo. `LandedUnitCost` es solo el costo unitario y nunca reconstruye el importe.
- Las reversas (anulación de ajuste, anulación de compra, reingreso por anulación de devolución de compra) y la entrada de una transferencia conservan el importe del movimiento original. La anulación de compra usa la entrada original de cada línea: con `SourceDocLineId` se valida contra su detalle; una entrada histórica sin él solo se acepta si producto, bodega, cantidad base y costo unitario la identifican con una única línea. Faltante, sobrante o ambigüedad ⇒ falla cerrada con rollback. Las comparaciones de reversa íntegra se hacen en `AccountingDecimals`, compatibles con movimientos históricos de más decimales.
- Sin base de costo conocida, el costo queda null (no cero) y la venta registra `SaleCostObligation`. Nunca se deriva costo desde el precio de venta. Una venta que cruza a negativo valora el tramo en stock con el valor restante y el tramo sin stock con el costo provisional redondeado. Una entrada con costo real cubre obligaciones pendientes en orden de secuencia (`SaleCostAllocation`); cada tramo cubierto toma su parte del importe de la entrada y el Kardex se ajusta con el mismo delta de costo que se reconoce en Contabilidad. Las obligaciones pendientes se consultan en `GET /api/v1/inventory/stock/costs/pending`.

## 5. Disponibilidad y stock negativo

- Se exige disponibilidad (`SaleStockPolicy`) solo si el Item es Product, `inventory.stock_control_enabled` de la empresa está ON, el `StockControlEnabled` del Item está ON y `AllowSellWithoutStock` está OFF. Con cualquier control OFF el Product sigue registrando movimientos.
- Solo una `SaleExit` autorizada por esa política puede dejar saldo negativo. Cualquier otra salida que deje saldo negativo se rechaza.

## 6. Ajustes de inventario

- `StockAdjustment` (`ADJ-NNNN`): Draft → Executed → Cancelled. Solo Draft se edita o ejecuta; solo Executed se anula, con motivo obligatorio. Ejecutar y anular deciden y registran en una transacción con el ajuste bloqueado (`GetByIdForUpdateAsync`): una solicitud concurrente ve el estado ya aplicado y no duplica movimientos.
- `InventoryAdjustmentReason` es dato de empresa (`ICompanyOperationalEntity`, `CompanyId` obligatorio; código único por Tenant + Company): nunca se comparte entre empresas del tenant. Se crea, lista, edita y activa/desactiva solo en la empresa activa validada (`ICompanyScopedRequest`); un `companyId` enviado en body o query debe coincidir con ella o se rechaza. Carga Inicial crea su motivo `CARGA_INICIAL` por empresa.
- El motivo debe ser de la empresa del ajuste, existir, estar activo y admitir la dirección (Ingreso, Egreso o Ambos); si `RequiresNotes`, exige observaciones. La cuenta contable del motivo se configura por empresa (`InventoryAdjustmentReasonAccount`).
- Líneas: solo Products de la empresa; cantidad > 0; factor de conversión > 0; costo ≥ 0. Un Ingreso exige costo unitario > 0 y registra `PositiveAdjust` a ese costo. Un Egreso valida disponibilidad cuando la política la exige y registra `NegativeAdjust` al promedio corrido.
- La fecha efectiva es el día de la empresa. La anulación registra el movimiento contrario por línea. Un documento de saldo inicial no se anula desde Ajustes.

## 7. Transferencias y saldo inicial

- `StockTransfer` (`TR-NNNN`): origen distinto de destino, al menos una línea, solo Products de la empresa y acceso inter-sucursal validado. Confirmar (Draft → Confirmed) registra `TransferExit` al promedio corrido de origen y `TransferEntry` en destino con ese mismo costo, cada uno con la sucursal de su bodega. La confirmación ocurre en una transacción con la transferencia bloqueada (`GetByIdForUpdateAsync`); una confirmación concurrente no vuelve a mover stock.
- Saldo inicial (`PostInitialBalance`, Carga Inicial): bodega activa de la sucursal activa, motivo de apertura que admita Ingreso, fecha de corte no posterior al día de la empresa, Product activo sin lotes ni series, costo > 0 y sin stock ni movimientos previos en la bodega. Registra `InitialBalance` con la fecha de corte.

## 8. Pendiente (no operativo)

- Lotes y series: existen entidades `Lot`/`SerialNumber` y repositorios, pero ningún flujo los usa.
- Reservas de stock: la entidad `StockReservation` no tiene flujo.
- `StockMovementType.SupplierCreditNote` / `SupplierDebitNote`: definidos, pero ningún flujo los registra.
- Anulación de transferencias: `StockTransfer.Cancel` existe en el dominio, pero no hay caso de uso ni endpoint.

## 9. Evidencia

B4 Product: commit `8a2c3692d`; cobertura existente en [PurchaseRetentionConfirmIntegrationTests.InventoryRounding.cs](../../backend/src/ERP.Infrastructure.Tests/Modules/Purchases/PurchaseRetentionConfirmIntegrationTests.InventoryRounding.cs): seis pruebas sobre importe de compra/asiento/Kardex, costo externo, reparto de flete y anulación actual/histórica inequívoca o ambigua. Esta referencia identifica cobertura, no acredita una nueva ejecución. Estado y evidencia CI: [STATUS](../../STATUS.md#inventario-v1--evidencia-ci-de-referencia).

Domain/Application (filtro Items/Inventory/Purchases, 2026-10-19): 354/354 y 551/551 PASS. PostgreSQL 2026-10-19: `StockRepositoryCompanyScopeIntegrationTests`, `StockMovementBranchOwnershipIntegrationTests`, `PurchaseCancelledStockMovementIntegrationTests`, `ResolvePurchaseReceptionLinesIntegrationTests` y `PurchaseInvoiceConfirmedPostingIntegrationTests` 39/39 PASS; `ItemCompanyScopeA1Tests` 16/16 PASS. Cobertura adicional: `NegativeSaleCostA2Tests`, `SaleStockPolicyTests`, `SaleCostObligationTests`, `StockMovementValuationCostTests`, `StockAdjustmentExecuteCancelTests`, `InventoryAdjustmentsEndToEndTests`, `WarehouseBranchOwnershipTests`, `GetStockMovementsBranchScopeTests`, `ConfirmInitialStockAtomicPostgreSqlTests`. Concurrencia de documentos (2026-10-25): `InventoryDocumentConcurrencyPostgreSqlTests` 3/3 PASS. Importe monetario canónico (2026-10-25): `InventoryValuationPostgreSqlTests` 6/6 y `PurchaseReturnEndToEndTests.UnifiedReprocessing_Fractional_inventory_cost_reversal_allows_reprocessing` 2/2 PASS.
