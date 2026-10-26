# ADR-040 — Item por empresa y naturaleza operativa

Estado: Accepted. Decisión arquitectónica vigente; no es especificación funcional.

## Decisión

Item tiene ownership Tenant + Company, sin ownership Branch. La clasificación
ItemTypeDefinition y la naturaleza operativa Product/Service son conceptos distintos.
La participación en inventario no depende de un flag de control de disponibilidad.

## Justificación

Separar catálogo compartido por sucursales de su empresa, clasificación administrable
y comportamiento operativo evita cruzar empresas o confundir desactivar controles con
suprimir movimientos. BusinessPartner conserva su decisión tenant-wide independiente.

## Responsabilidad y límites

No reabre ItemTypeDefinition ni Pricing CLOSED; Digital no está implementado.
No prescribe migraciones sustituidas ni modifica datos existentes.

## SSOT y dependencias

Comportamiento: [items.md](../architecture/items.md).
Integración contable: [Accounting](../architecture/accounting.md).
Instalación vigente: [GLOBAL](../architecture/architecture.md#global-immutable-schema-global).
Gates/cierres: [STATUS](../../STATUS.md). PostgreSQL real es obligatorio para validar persistencia,
concurrencia y aislamiento; este ADR no acredita nuevas ejecuciones.
