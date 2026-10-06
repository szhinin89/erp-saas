# DECISIONS.md — Reglas de dominio vigentes

Espejo condensado de decisiones operativas. Cada regla enlaza a su fuente de autoridad; ante discrepancia prevalece el ADR / STATUS.md citado.

- **BusinessPartner es tenant-wide.** (ADR-017 "subscriber-scoped", confirmado como tenant-wide por ADR-040.)
- **Item es Tenant + Company.** (ADR-040.)
- **Branch no posee Items.** (ADR-040.)
- **ItemNature:**
  - `Product` = participa Inventory
  - `Service` = no Inventory
  - `Digital` = FUTURE
  (ADR-040.)
- **Product siempre registra Inventory.** (ADR-040 / ADR-041.)
- **Control Company e Item solo gobiernan bloqueo por disponibilidad.** No desactivan el registro de movimientos de inventario. (ADR-040: `StockControlEnabled` a nivel Company + Item.)
- **AllowSellWithoutStock resuelto en A2.** (ADR-041.)
- **Negativos autorizados solo según política efectiva.** La venta que deja saldo negativo requiere que la política central lo permita (participación en Inventory + controles + AllowSellWithoutStock), bajo lock transaccional PostgreSQL por Tenant + Company + Item + Warehouse. (ADR-041.)
- **COGS pendiente/regularización ya resuelto en ADR-041.** SaleCostObligation / SaleCostAllocation / InventoryCostPosting; no reinventar el mecanismo.
- **No inventar costos desde precio de venta.** Sin base de costo conocida, TotalCost queda null y se registra obligación pendiente; nunca se deriva COGS del precio de venta. (ADR-041.)
- **Multiwarehouse por línea.** Los Products conservan Warehouse por línea y el caso multiwarehouse. (ADR-040.)
- **Pricing mantiene ownership propio por Company.** Pricing solamente asocia Items de la misma empresa; su dominio permanece CLOSED (ADR-021) y no se reabre desde otras tareas.
