# ADR-036 — Ciclo fiscal y anulación de retenciones

Estado: Accepted. Decisión vigente; no contiene una variante histórica de procesamiento.

## Decisión y justificación

Una retención anulada no puede seguir avanzando su documento electrónico. El gate del
origen y la decisión de anulación se coordinan con el pipeline único de ElectronicDocuments,
sin que ese módulo dependa de Retentions. Registrar el reclamo antes del envío distingue
un procesamiento incierto de uno nunca intentado; una respuesta inconcluyente no autoriza
reenvío ni reversos locales. Una retención autorizada requiere ANULADO verificado por SRI
antes de finalizar idempotentemente la anulación de su origen.

La consulta tipada separa fallo técnico de estado fiscal y evita que timeout o RECHAZADA
se interpreten como NO AUTORIZADO/ANULADO. No existe resolución manual como alternativa.

## Responsabilidad y límites

Permisos y XML/RIDE se autorizan por el origen. La compra recibida sigue siendo documento
del proveedor. El plazo global y la elegibilidad de compra pertenecen a ADR-044/037.
NO AUTORIZADO sobre un comprobante autorizado no produce finalización automática.
ADR-043 es la decisión independiente y única de orquestación recurrente; Retentions no
recupera un documento ya registrado en ElectronicDocuments, incluido Draft.

## SSOT y dependencias

Contrato fiscal: [Backend](../architecture/backend.md#verificación-fiscal-de-anulación-de-retenciones).
Emisión y permisos: [Backend — Retenciones](../architecture/backend.md#retenciones-una-capacidad-una-política-de-emisión-zh-retention-emission-ssot-01-zh-purchase-retention-confirm-01).
Efectos contables: [Accounting](../architecture/accounting.md).
Recuperación: [ADR-043](ADR-043-background-processing-ssot.md).
Estado de entrega: [STATUS](../../STATUS.md).
