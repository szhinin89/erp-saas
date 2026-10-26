# ADR-033 — PaymentTerm, defaults por empresa/rol y cronograma documental

Estado: Accepted. Decisión vigente; no acredita cierres de las fases de implementación.

## Decisión y justificación

Un catálogo PaymentTerm representa plazos; el default Company/rol solo propone una
selección. El cronograma final pertenece al agregado documental y origina la cartera.
Separar estas responsabilidades evita que editar un catálogo o el default de un tercero
cambie documentos históricos, o que el cliente invente una condición por orden del catálogo.

## Responsabilidad y límites

Defaults independientes para Cliente y Proveedor. No hay cronograma compartido entre
módulos ni tercer catálogo. CreditTerm queda fuera de esta capacidad; no se introduce
riesgo crediticio ni anticipos complejos. No se mantienen diagnósticos antiguos de
SupplierRoleConfig o simulador desconectado como estado actual.

## SSOT y dependencias

[Backend](../architecture/backend.md#condiciones-de-pago-y-cronogramas),
[Compras](../architecture/purchases.md) y [Accounting](../architecture/accounting.md).
Capacidades: [FEATURES](../../FEATURES.md). Gates: [STATUS](../../STATUS.md).
