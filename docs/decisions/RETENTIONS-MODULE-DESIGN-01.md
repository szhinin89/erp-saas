# Retentions — capacidad tributaria transversal

Decisión de diseño conservada por referencias existentes; no es especificación funcional.

## Decisión y justificación

RetentionDocument es un agregado independiente, consumido por Compras y Gastos;
no se modela la retención como campos de ExpenseLine ni se duplica el cálculo por origen.
Company y el proveedor aportan su configuración fiscal existente, sin nuevos flags
paralelos. La elegibilidad tributaria precede a la emisión, no es un permiso de usuario.
La emisión pertenece a la unidad de trabajo del origen y sus efectos sobre CxP.

## Límites y dependencias

Corregir una retención emitida usa el ciclo del origen; no habilita reconfirmar una
retención Cancelled. El documento electrónico tiene su propio agregado y pipeline.
No altera Company/SupplierRoleConfig ni requisitos fiscales por interpretación de docs.
No se conservan E1–E9 como estado actual ni el diagnóstico de ausencia de retenciones.

## SSOT

[Backend — Retenciones](../architecture/backend.md#retenciones-una-capacidad-una-política-de-emisión-zh-retention-emission-ssot-01-zh-purchase-retention-confirm-01),
[Accounting](../architecture/accounting.md), [Compras](../architecture/purchases.md).
Anulación fiscal: [ADR-036](ADR-036-retention-electronic-document-cancellation.md).
Planificación de Gastos: [ROADMAP](../ROADMAP.md#etapa-1--completar-erp-core).
Estado y gates: [STATUS](../../STATUS.md).
