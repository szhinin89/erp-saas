# ADR-035 — Remanente no aplicado de SupplierPayment como anticipo (SupplierCredit)

**Status:** Accepted · **Fecha:** 2026-09-26 · **Ticket:** ZH-SUPPLIER-PAYMENT-UNAPPLIED-ADVANCE-02C

## Contexto

`SupplierPayment` exigía Σ aplicaciones == total: no se podía pagar más que la deuda ni pagar sin una CxP. El dinero pagado de más no tenía dónde vivir. Ya existía un libro de saldo a favor del proveedor (`SupplierCredit` + `SupplierCreditMovement`, con aplicación, reembolso y sus reversas), pero su origen estaba fijado a `PurchaseReturn`.

## Decisión

1. **Σ aplicaciones ≤ total.** `AppliedAmount`/`UnappliedAmount` se derivan en el agregado; no se persisten.
2. **Confirmación explícita.** Si queda remanente, `RegisterSupplierPaymentCommand.ConfirmUnappliedAmount` debe ser `true`. Application lo rechaza antes de abrir la transacción y el dominio lo revalida.
3. **Pago sin CxP** (cero aplicaciones) solo si la empresa lo permite: `org_settings` `payables.allow_supplier_payment_without_payable` (default `false`), resuelto por `IOperationalPreferencesResolver` (grupo propio `Payables`, en Configuración > Operaciones > Cuentas por pagar; una sola key, sin alias). El anticipo por sobrepago **no** depende de esta política.
4. **SupplierCredit es el SSOT del anticipo.** No hay un agregado `SupplierAdvance` ni otro libro de saldo. El origen pasa a ser `SourcePurchaseReturnId?` XOR `SourceSupplierPaymentId?` (CHECK en BD + guard de dominio + índice único filtrado por pago). `SourceType` se deriva y no se persiste (se mantiene el criterio del diseño §6.1). El crédito nace por exactamente `UnappliedAmount`, en la misma transacción que el pago.
5. Integración contable: [Accounting](../architecture/accounting.md).
6. **Matriz de allocations.** Cada aplicación queda cubierta al 100% y ningún medio se distribuye por encima de su monto. Lo que falta distribuir de cada medio (`Amount − Σ allocations(medio)`) es lo que financió el anticipo. No se agrega otra tabla.
7. **Reversa** (semántica 02B intacta). Si el pago originó un anticipo, la reversa exige que ese crédito siga íntegro (`AvailableAmount == OriginalAmount`, `SupplierCredit.IsIntact`). Se toma Lock B (`SupplierCredit.Lock`) antes de los locks de caja, en el mismo orden que el reembolso. El crédito se anula con el movimiento de sistema `SourcePaymentReversed` (saldo a 0, nunca se borra).

## Consecuencias

- Migración `SupplierPaymentUnappliedAdvance`: `source_purchase_return_id` pasa a nullable, se agrega `source_supplier_payment_id` (FK Restrict), el CHECK de origen único y un índice único filtrado. No mueve datos: las filas existentes cumplen el CHECK.
- `GET /api/v1/supplier-payments/policy` (permiso `supplier-payments.create`) expone la política al formulario. Quien registra pagos no necesita `settings.operations.view`. Es solo UX: el handler vuelve a aplicar la política.
- `SupplierCreditDto` suma `SourceType`, `SourceSupplierPaymentId` y `SourceDocumentNumber`, y `SourcePurchaseReturnId` pasa a nullable.

## Alternativas consideradas

- **Agregado `SupplierAdvance` nuevo:** descartado, porque sería un segundo libro de saldo a favor del mismo proveedor, con su propia aplicación, reembolso y posting duplicados.
- **Columnas `AppliedAmount`/`UnappliedAmount` persistidas:** descartadas, porque se derivan siempre de las líneas y guardarlas abriría la puerta a contradicciones.
