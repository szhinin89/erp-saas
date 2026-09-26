# ADR-035 — Remanente no aplicado de SupplierPayment como anticipo (SupplierCredit)

**Status:** Accepted · **Fecha:** 2026-09-26 · **Ticket:** ZH-SUPPLIER-PAYMENT-UNAPPLIED-ADVANCE-02C
**Relacionado:** ADR-026 (Accounting Core — PostingRule/PostingFact), P0-02 (SupplierCredit), 02B-FINAL (semántica de reversa).

## Contexto

`SupplierPayment` exigía Σ aplicaciones == total: no se podía pagar más que la deuda ni pagar sin una CxP. El dinero pagado de más no tenía dónde vivir. Ya existía un libro de saldo a favor del proveedor (`SupplierCredit` + `SupplierCreditMovement`, con aplicación, reembolso y sus reversas, y posting propio sobre `1.1.03.004 Anticipos a proveedores`), pero su origen estaba fijado a `PurchaseReturn`.

## Decisión

1. **Σ aplicaciones ≤ total.** `AppliedAmount`/`UnappliedAmount` se derivan en el agregado; no se persisten.
2. **Confirmación explícita.** Si queda remanente, `RegisterSupplierPaymentCommand.ConfirmUnappliedAmount` debe ser `true`. Application lo rechaza antes de abrir la transacción y el dominio lo revalida.
3. **Pago sin CxP** (cero aplicaciones) solo si la empresa lo permite: `org_settings` `payables.allow_supplier_payment_without_payable` (default `false`), resuelto por `IOperationalPreferencesResolver` (grupo propio `Payables`, en Configuración > Operaciones > Cuentas por pagar; una sola key, sin alias). El anticipo por sobrepago **no** depende de esta política.
4. **SupplierCredit es el SSOT del anticipo.** No hay un agregado `SupplierAdvance` ni otro libro de saldo. El origen pasa a ser `SourcePurchaseReturnId?` XOR `SourceSupplierPaymentId?` (CHECK en BD + guard de dominio + índice único filtrado por pago). `SourceType` se deriva y no se persiste (se mantiene el criterio del diseño §6.1). El crédito nace por exactamente `UnappliedAmount`, en la misma transacción que el pago.
5. **Posting.** `SupplierPayment` sigue siendo el único asiento financiero. La `PostingRule` `Payables/SupplierPaymentConfirmed` pasa de 1 línea (CxP `GrandTotal`) a 2 líneas: CxP `AppliedToPayable` + `1.1.03.004` `SupplierCredit`. `SupplierPaymentReversed` es el espejo con naturaleza Credit. El Haber (o el Debe en la reversa) por medio de pago sigue siendo dinámico vía `Allocations`. `SupplierCredit` **no** postea al crearse desde un pago, porque duplicaría el Debe a Anticipos. Conserva sus postings existentes al aplicarse, reembolsarse y en sus reversas.
6. **Reglas existentes.** `AccountingBootstrapStep.TryCorrectLegacySupplierPaymentRule` corrige solo la forma previa exacta: conserva el Id de la línea, cambia su `AmountKind` a `AppliedToPayable` y agrega la línea de anticipos. Las reglas editadas por un admin no se tocan. Para un pago sin remanente, ambas formas generan el mismo asiento. Con remanente y una regla sin la línea `SupplierCredit`, los traductores rechazan el pago fail-closed (`IPostingEngine.IsAmountKindConfiguredAsync`, mismo mecanismo que IRBPNR). Nunca se debita CxP por dinero que no se aplicó.
7. **Matriz de allocations.** Cada aplicación queda cubierta al 100% y ningún medio se distribuye por encima de su monto. Lo que falta distribuir de cada medio (`Amount − Σ allocations(medio)`) es lo que financió el anticipo. No se agrega otra tabla.
8. **Reversa** (semántica 02B intacta). Si el pago originó un anticipo, la reversa exige que ese crédito siga íntegro (`AvailableAmount == OriginalAmount`, `SupplierCredit.IsIntact`). Se toma Lock B (`SupplierCredit.Lock`) antes de los locks de caja, en el mismo orden que el reembolso. El crédito se anula con el movimiento de sistema `SourcePaymentReversed` (saldo a 0, nunca se borra) y se genera el asiento inverso exacto.

## Consecuencias

- Migración `SupplierPaymentUnappliedAdvance`: `source_purchase_return_id` pasa a nullable, se agrega `source_supplier_payment_id` (FK Restrict), el CHECK de origen único y un índice único filtrado. No mueve datos: las filas existentes cumplen el CHECK.
- **Empresas nuevas:** `AccountingBootstrapStep` (el bootstrap canónico de creación de empresa) siembra ya la forma vigente. No hace falta ningún upgrade.
- **Instalaciones existentes, incluida Production:** se actualizan con el comando de despliegue explícito `dotnet run -- backfill-supplier-payment-posting-rules [apply]`, el mismo mecanismo que `backfill-sales-invoice-posting-rule`.
  - Sin `apply` es dry-run.
  - Cada empresa corre en una transacción Serializable.
  - Diagnóstico por regla: `Canonical`, `Legacy`, `Legacy; InvalidAccounts: …`, `Custom: …` o `MissingRule`.
  - Solo `Legacy` (la forma canónica anterior exacta, con todas las cuentas disponibles) se actualiza, conservando el Id de la línea.
  - Es idempotente: una segunda corrida reporta `Canonical` y no cambia nada.
- **Reglas personalizadas:** nunca se sobrescriben. Quedan registradas como warning, con empresa y regla, para revisión manual. Los pagos exactos siguen funcionando; los que tienen remanente siguen rechazándose fail-closed. En entornos no productivos, el backfill automático de arranque usa el mismo reconocimiento.
- `GET /api/v1/supplier-payments/policy` (permiso `supplier-payments.create`) expone la política al formulario. Quien registra pagos no necesita `settings.operations.view`. Es solo UX: el handler vuelve a aplicar la política.
- `SupplierCreditDto` suma `SourceType`, `SourceSupplierPaymentId` y `SourceDocumentNumber`, y `SourcePurchaseReturnId` pasa a nullable.

## Alternativas consideradas

- **Agregado `SupplierAdvance` nuevo:** descartado, porque sería un segundo libro de saldo a favor del mismo proveedor, con su propia aplicación, reembolso y posting duplicados.
- **Columnas `AppliedAmount`/`UnappliedAmount` persistidas:** descartadas, porque se derivan siempre de las líneas y guardarlas abriría la puerta a contradicciones.
- **Mantener `GrandTotal` y enviar en él el monto aplicado:** descartada, porque `GrandTotal` dejaría de significar "total" y rompería la semántica del `PostingFact`.
- **Posting propio de `SupplierCredit` al crearse:** descartado, porque duplicaría el Debe a Anticipos que ya genera el pago.
