# ADR-041 — Venta sin existencia y obligaciones de costo

Estado: Accepted. Fecha: 2026-10-06. Entrega: A2 CLOSED.

## Alcance aprobado

Addendum de Inventory y su integración con Accounting. A1 permanece CLOSED en `43cce57a9`; no cambia ItemNature ni la infraestructura de ItemTypeDefinition. Product participa siempre en inventario y Service nunca participa. El promedio ponderado sigue siendo el método de valoración. No se introduce FIFO/LIFO ni se modifica el modelo base de Accounting.

## Disponibilidad y concurrencia

La política central exige disponibilidad solamente si el producto participa en Inventory, el control de Company y del Item están activos y AllowSellWithoutStock es false. Sales/POS y los selectores usan esa política; el precheck compara AvailableQuantity, incluyendo reservas. La validación definitiva ocurre bajo lock transaccional PostgreSQL por Tenant + Company + Item + Warehouse. Una SaleExit autorizada puede dejar saldo negativo; otras salidas mantienen su protección. Una entrada puede compensar parcialmente un saldo negativo.

## Valoración y regularización

CostBasis nullable distingue costo desconocido de costo real cero. El costo conocido se conserva al llegar a cero o negativo; RunningStockValue conserva signo. Una venta con base conocida reconoce costo provisional; si no hay historia de costo, TotalCost queda null y se registra una obligación pendiente sin inventar COGS. CostPending se expone junto con la base en las consultas de stock y movimientos.

SaleCostObligation es una proyección por movimiento y línea original. SaleCostAllocation registra las compensaciones y devoluciones sin modificar destructivamente el Kardex histórico. Una entrada con costo real cubre primero obligaciones vigentes en SequenceNumber ascendente dentro de Company + Item + Warehouse, hasta agotar su cantidad. Las obligaciones anuladas, devueltas o ya cubiertas quedan excluidas. Esta prioridad ordena exclusivamente la compensación de obligaciones, no la valoración de inventario.

Las asignaciones conservan cantidad, origen, obligación y costo; las claves únicas previenen duplicados.

## Anulación y devolución

La anulación extingue las obligaciones operativas pendientes. La devolución se vincula con unidades de la línea original y conserva su trazabilidad de cantidades y costo; las unidades extinguidas no vuelven a regularizarse. Integración contable: [Accounting](../architecture/accounting.md).

Una devolución aún sin costo resuelto devuelve cantidad conservando la incertidumbre de valoración. Las unidades devueltas con costo real pueden compensar otras obligaciones vigentes del mismo recurso; las asignaciones quedan registradas. No se reescribe el movimiento original.

## Pendientes contables y operación

[Accounting](../architecture/accounting.md).

## Persistencia y gates

Migración generada `20261006120230_NegativeSaleCostObligations`: base/estado de valoración, obligaciones, asignaciones y pendientes contables, con constraints y correlación de movimientos por documento/línea. Aplicada sin errores a desarrollo `dberpsaas` durante el cierre operativo autorizado; smoke de persistencia PASS (historial de migración y lectura de tablas/columnas nuevas). PostgreSQL de Testcontainers verifica migraciones completas, stock negativo, reservas, concurrencia, aislamiento, compensación parcial/completa, costo desconocido, reintento, anulaciones y devoluciones con redondeo. Los resultados finales se registran en STATUS.md.

## Addendum PURCHASE-RETURN-COST-FIX-01 — devolución a proveedor (2026-10-10)

Reabre Purchases solo para la valoración de `PurchaseReturn`; no cambia PostingRules, cuentas ni el cálculo comercial/fiscal.

- **NC comercial = factura original.** Subtotal, descuento, IVA/ICE/IRBPNR y total acreditado se prorratean de la línea original (cantidad devuelta / cantidad original, en la unidad comercial de la factura). El costo de inventario nunca interviene en la NC.
- **Kardex = promedio ponderado.** La salida `PurchaseReturn` se registra en unidad base (`QuantityInBaseUom` vía el `ConversionFactor` original, `BaseUomCode`) y sin costo propio: se valora al promedio vigente del recurso. `LandedUnitCost` de la factura no es costo de salida — la unidad pierde identidad al promediarse, y usarlo podía dejar el valor del inventario negativo. El invariante de costo no negativo no se relaja.
- **HistoricalCost = valor real de Kardex.** `PurchaseReturnDetail.HistoricalCostAmount` = valor retirado por la salida (2 decimales); `UnitCost` = costo de valoración por unidad base de esa salida.
- **Cancelación.** Reingresa exactamente la cantidad base, la unidad y el costo valorado de la salida registrada al autorizar.

Fuera de alcance: anulación completa de `PurchaseInvoice` (sigue saliendo al `LandedUnitCost`) y la conciliación flete/ICE entre Inventario contable y Kardex.
