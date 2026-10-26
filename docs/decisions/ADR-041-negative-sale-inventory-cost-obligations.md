# ADR-041 — Costo desconocido, provisional y obligaciones de venta

Estado: Accepted. Decisión arquitectónica vigente; no es especificación funcional.

## Decisión

Mantener promedio ponderado y representar explícitamente costo desconocido frente a
costo conocido cero. Las obligaciones y asignaciones trazan la regularización sin
reescribir destructivamente el Kardex.

## Justificación

Permitir ventas autorizadas sin existencia no permite inventar COGS ni derivarlo del
precio de venta. La secuencia de compensación ordena obligaciones, no introduce FIFO/LIFO.
El valor operativo registrado es la fuente del efecto contable.

## Responsabilidad y límites

Ítems conserva ADR-040. El importe y sus residuos/reversas vigentes están en Inventario;
los asientos y continuidad están en Accounting. La devolución comercial conserva sus
importes fiscales de origen; su salida física y la anulación completa no son la misma operación.

## SSOT y dependencias

Comportamiento: [inventory.md](../architecture/inventory.md).
Integración contable: [Accounting](../architecture/accounting.md).
Instalación vigente: [GLOBAL](../architecture/architecture.md#global-immutable-schema-global).
Gates/cierres: [STATUS](../../STATUS.md). PostgreSQL real es obligatorio para validar persistencia,
concurrencia y aislamiento; este ADR no acredita nuevas ejecuciones.
