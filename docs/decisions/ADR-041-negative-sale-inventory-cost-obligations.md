# ADR-041 — Venta sin existencia y obligaciones de costo

Estado: Accepted. Fecha: 2026-10-06. Entrega: A2 CLOSED.

## Alcance aprobado

Addendum de Inventory y su integración con Accounting. A1 permanece CLOSED en `43cce57a9`; no cambia ItemNature ni la infraestructura de ItemTypeDefinition. Product participa siempre en inventario y Service nunca participa. El promedio ponderado sigue siendo el método de valoración. No se introduce FIFO/LIFO ni se modifica el modelo base de Accounting.

## Disponibilidad y concurrencia

La política central exige disponibilidad solamente si el producto participa en Inventory, el control de Company y del Item están activos y AllowSellWithoutStock es false. Sales/POS y los selectores usan esa política; el precheck compara AvailableQuantity, incluyendo reservas. La validación definitiva ocurre bajo lock transaccional PostgreSQL por Tenant + Company + Item + Warehouse. Una SaleExit autorizada puede dejar saldo negativo; otras salidas mantienen su protección. Una entrada puede compensar parcialmente un saldo negativo.

## Valoración y regularización

CostBasis nullable distingue costo desconocido de costo real cero. El costo conocido se conserva al llegar a cero o negativo; RunningStockValue conserva signo. Una venta con base conocida reconoce costo provisional; si no hay historia de costo, TotalCost queda null y se registra una obligación pendiente sin inventar COGS. CostPending se expone junto con la base en las consultas de stock y movimientos.

SaleCostObligation es una proyección por movimiento y línea original. SaleCostAllocation registra las compensaciones y devoluciones sin modificar destructivamente el Kardex histórico. Una entrada con costo real cubre primero obligaciones vigentes en SequenceNumber ascendente dentro de Company + Item + Warehouse, hasta agotar su cantidad. Las obligaciones anuladas, devueltas o ya cubiertas quedan excluidas. Esta prioridad ordena exclusivamente la compensación de obligaciones, no la valoración de inventario.

La diferencia entre costo real y provisional se reconoce con ajustes COGS append-only mediante las reglas y el motor contable existentes. Los importes reconocidos se concilian a la precisión monetaria existente de dos decimales, incluyendo el remanente de redondeo en devoluciones sucesivas. Las asignaciones conservan cantidad, origen, obligación y costo; las claves únicas previenen duplicados.

## Anulación y devolución

La anulación extingue las obligaciones pendientes y reversa tanto el COGS original como sus ajustes; no deja COGS residual de la venta. La devolución se vincula explícitamente con unidades de la línea original: extingue primero unidades pendientes y luego unidades regularizadas. Las primeras no reversan COGS inexistente; las segundas reversan el costo efectivamente reconocido, asignado proporcionalmente entre regularizaciones de la misma línea. No se usa indiscriminadamente el promedio vigente. Las unidades extinguidas no vuelven a regularizarse.

Una devolución aún sin costo resuelto devuelve cantidad conservando la incertidumbre de valoración. Las unidades devueltas con costo real pueden compensar otras obligaciones vigentes del mismo recurso; las asignaciones quedan registradas. No se reescribe el movimiento original.

## Pendientes contables y operación

InventoryCostPosting persiste importe, factura, evento, estado, intentos, error y asiento. Los errores de configuración contable quedan Failed, visibles y reintentables; no se pierden ni se marcan falsamente como contabilizados. El procesamiento respeta el reconocimiento original antes de sus ajustes y utiliza idempotencia del motor existente. La anulación revierte los asientos realmente publicados y cancela los pendientes.

- GET `/api/v1/inventory/stock/costs/pending`: obligaciones de costo y contabilizaciones pendientes/fallidas; permiso StockView.
- POST `/api/v1/inventory/stock/costs/retry`: reintento idempotente de pendientes contables de la empresa activa; permiso StockManage.

Ambos requests requieren contexto autenticado de Company. No se introduce un worker ni una nueva pantalla; el costo pendiente legítimo requiere una base real de valoración y no se resuelve con un reintento contable.

## Persistencia y gates

Migración generada `20261006120230_NegativeSaleCostObligations`: base/estado de valoración, obligaciones, asignaciones y pendientes contables, con constraints y correlación de movimientos por documento/línea. Aplicada sin errores a desarrollo `dberpsaas` durante el cierre operativo autorizado; smoke de persistencia PASS (historial de migración y lectura de tablas/columnas nuevas). PostgreSQL de Testcontainers verifica migraciones completas, stock negativo, reservas, concurrencia, aislamiento, compensación parcial/completa, costo desconocido, reintento, anulaciones y devoluciones con redondeo. Los resultados finales se registran en STATUS.md.
