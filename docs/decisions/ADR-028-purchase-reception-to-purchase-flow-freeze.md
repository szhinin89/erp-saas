# ADR-028 — Separación XML, recepción y compra

Estado: Accepted — Frozen. No reabre el flujo de Compras.

## Decisión y justificación

El XML autorizado recibido es evidencia fiscal inmutable. Su snapshot operativo se
persiste para consumir y conciliar líneas sin reinterpretar XML en cada capa.
La Compra es su agregado transaccional independiente; Item Matching tiene un solo owner.
Separar verificación fiscal y procesamiento operativo conserva líneas sin match y evita
que un fallo de interpretación borre evidencia o modifique silenciosamente una compra.

## Responsabilidad y límites

La relación efectiva recepción/compra y sus estados se documentan en Compras; no se
conserva como pendiente el antiguo diagnóstico de falta de PurchaseId/MarkProcessed.
Aprobaciones futuras no cambian esta decisión ni crean otro flujo operativo hoy.

## SSOT y dependencias

[Compras](../architecture/purchases.md#3-recepción-electrónica-y-conciliación-de-ítems).
[Ítems](../architecture/items.md). Pipeline fiscal: ADR-023/038; sin modificar sus contratos.
