# ADR-032 — ICE e IRBPNR como impuestos de línea

Estado: Accepted. Decisión vigente; no es una propuesta de otro motor tributario.

## Decisión y justificación

Representar impuestos de línea en DetailTax, con categoría, procedencia y snapshot
fiscal, evita dos cálculos y dos fuentes de importes por documento. Los consumidores
fiscales/contables usan el dato del documento; GLOBAL es dueño del catálogo oficial.
La configuración del ítem y la política de aplicación por empresa tienen responsabilidades
distintas y no habilitan porcentajes oficiales hardcodeados ni catálogos paralelos.

## Responsabilidad y límites

No reabre Pricing, ItemTypeDefinition, XML/firma/SOAP ni valoración de Inventario.
El costo inventariable no sustituye la base fiscal. La representación GLOBAL de ICE
mixto no acredita cálculo operativo: conservar el límite vigente de Compras.

## SSOT y dependencias

[Configuración tributaria](../architecture/frozen-infrastructure.md),
[GLOBAL](../architecture/architecture.md#global-immutable-schema-global),
[Compras](../architecture/purchases.md), [Accounting](../architecture/accounting.md).
Estado y gates: [STATUS](../../STATUS.md).
