# DECISIONS.md — Reglas de dominio vigentes

Índice de decisiones operativas. Las reglas con SSOT se enlazan sin repetirlas; ante discrepancia prevalece la SSOT o el ADR citado.

- **Representación GLOBAL de ICE mixto (alcance aprobado 2026-10-21):** → [SSOT GLOBAL](../architecture/architecture.md#ice-mixto-representación-global-y-límite-operativo).

- **Ítems:** alcance Company, naturaleza Product/Service, unicidad y catálogo → [items.md](../architecture/items.md).
- **Inventario:** Kardex, valoración, disponibilidad, stock negativo autorizado, ajustes y transferencias → [inventory.md](../architecture/inventory.md).
- **Compras:** estados (incl. Discarded), recepción, confirmación, anulación; entrada única NC del proveedor (devolución o descuento/bonificación), vinculación histórica, reproceso con reversa verificable y saldo a favor → [purchases.md](../architecture/purchases.md).
- **Costo de inventario y COGS en contabilidad:** → [accounting.md § Importes de pagos, costos y apertura](../architecture/accounting.md#importes-de-pagos-costos-y-apertura).

- **BusinessPartner es tenant-wide.** (ADR-017 "subscriber-scoped", confirmado como tenant-wide por ADR-040.)
- **Pricing mantiene ownership propio por Company.** Pricing solamente asocia Items de la misma empresa; su dominio permanece CLOSED (ADR-021) y no se reabre desde otras tareas.
