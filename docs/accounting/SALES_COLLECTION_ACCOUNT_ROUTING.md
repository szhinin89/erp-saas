# Ruteo de cuenta contable para cobros de venta

**Ticket:** SALES-COLLECTION-ACCOUNT-SSOT-CLEANUP-01
**Módulo:** Ventas → autorización de factura (`AuthorizeSalesInvoiceHandler`), Contabilidad → Configuración → Destinos contables → Cobros de ventas.

## Regla arquitectónica

> Una forma de cobro no puede tener más de una fuente de verdad para resolver su cuenta contable.

Antes de este ticket, `PaymentMethodAccount` (una fila por `PaymentMethod`) podía configurarse para **cualquier** forma de cobro, incluyendo Efectivo y Transferencia — que ya tenían su propia fuente de verdad real (`CashRegister.AccountingAccountId` y `CompanyBankAccount.AccountingAccountId`, respectivamente, introducidas en tickets previos). Esto dejaba dos configuraciones posibles para el mismo método, con el riesgo de que quedaran desincronizadas o de que el código de posting eligiera la incorrecta silenciosamente.

## Modelo final — una fuente por forma de cobro

| Forma de cobro | `PaymentMethod.DetailType` | Fuente de la cuenta contable | ¿Configurable en "Cobros de ventas"? |
|---|---|---|---|
| Efectivo | `None` (y `IsCreditAllowed = false`) | `CashRegister.AccountingAccountId` — la caja de la sesión (`SalesInvoice.CashSessionId`) que autoriza la venta | No — solo lectura ("Según caja registradora") |
| Transferencia | `Transfer` | `CompanyBankAccount.AccountingAccountId` — la cuenta bancaria elegida en el pago (`SalesInvoicePayment.TransferDetail.CompanyBankAccountId`) | No — solo lectura ("Según cuenta bancaria") |
| Tarjeta | `Card` | `PaymentMethodAccount.AccountingAccountId` | Sí — única configuración manual posible |
| Cheque | `Check` | `PaymentMethodAccount.AccountingAccountId` | Sí — única configuración manual posible |
| Crédito | — (`PaymentMethod.IsCreditAllowed = true`) | Regla contable de Cuentas por Cobrar (`SalesReceivable`, resuelta al momento del cobro real vía `RegisterCollectionCommand`, nunca al autorizar la factura) | No — solo lectura ("Según regla de Cuentas por cobrar") |

`PaymentMethodAccount` es, desde este ticket, **exclusivo de Tarjeta/Cheque**. No existe fila, consulta, ni fallback hacia esta tabla para Efectivo/Transferencia/Crédito.

## Resolución en autorización de venta (`AuthorizeSalesInvoiceHandler`)

Para cada pago no-Crédito de la factura, `AuthorizeSalesUseCases.cs` resuelve la cuenta exactamente una vez, según `PaymentMethod.DetailType`:

1. **Transfer** → carga `CompanyBankAccount` desde `payment.TransferDetail.CompanyBankAccountId`, valida que exista, pertenezca a la Company activa, esté activa, y que su cuenta contable esté activa/postable.
2. **Card / Check** → busca en el mapa de `PaymentMethodAccount` de la Company activa (`GetMapAsync`). Sin fila configurada, bloquea la autorización.
3. **None (Efectivo)** → carga la `CashSession` de `inv.CashSessionId`, de ahí el `CashRegister`, valida que exista, pertenezca a la Company activa, esté activo, y que `CashRegister.AccountingAccountId` esté configurado y sea una cuenta activa/postable.

Todo el proceso es **fail-closed incondicional**: sin la cuenta correspondiente resuelta, la autorización se rechaza antes de capturar secuencial o generar el asiento contable — nunca cae en un default oculto ("Caja general" u otro).

Las cuentas resueltas se acumulan en `cashByAccount` (`Dictionary<AccountingAccountId, Amount>`) y viajan en `SalesInvoiceAuthorizedEvent.CashByAccount` hacia `SalesInvoiceAuthorizedPostingTranslator`, que las postea como `PostingAllocation` (mismo mecanismo ya usado por Gastos para N cuentas dinámicas) — el traductor es agnóstico a la fuente, solo consume el desglose ya resuelto.

## Resolución en el cobro real de una CxC (Crédito)

El Crédito nunca resuelve cuenta contable al autorizar la factura — genera una `SalesReceivable` pendiente. Cuando esa cuenta por cobrar se cobra de verdad, `RegisterCollectionCommandHandler` (`PaymentUseCases.cs`) resuelve la cuenta de la MISMA manera que una venta de contado: `CompanyBankAccountId` o `CashRegisterId` explícitos en el comando, nunca `PaymentMethodId`/`PaymentMethodAccount`.

## Consecuencias del cambio

- `SetPaymentMethodAccountCommand` (`PUT /api/v1/payment-methods/{id}/account`) rechaza cualquier método de pago cuya fuente contable (`PaymentMethodDto.AccountSource`) no sea `PaymentMethodAccount` — validado tanto en `SetPaymentMethodAccountValidator` como en el handler (defensa en profundidad).
- `GetPaymentMethodsQuery`/`GetPaymentMethodByIdQuery` exponen `PaymentMethodDto.AccountSource` (`CashRegister` | `CompanyBankAccount` | `PaymentMethodAccount` | `AccountingRule`) — la pantalla "Cobros de ventas" usa este campo para decidir si muestra el botón "Configurar cuenta" o un texto de solo lectura.
- Los dos backfills/seeds que antes vinculaban Efectivo → "Caja general" vía `PaymentMethodAccount` (`AccountingBootstrapStep` en el bootstrap de una Company nueva, y `PaymentMethodAccountBackfillService`/`dotnet run -- backfill-payment-method-account` para companies existentes) se eliminaron — Efectivo ya no necesita, ni debe tener, esa fila.
- Migración `SalesCollectionAccountSsotCleanup01` borra cualquier fila de `payment_method_accounts` que no corresponda a un método Tarjeta/Cheque (dato legacy de Efectivo/Transferencia/Crédito, si existía). No hay columnas ni constraints que retirar del esquema — el problema era de datos, no de estructura.

## Si se necesita una fuente adicional en el futuro

Cualquier cambio a este ruteo (nueva forma de cobro, cambio de fuente para una existente) requiere una migración/ticket nuevo — nunca reabrir `PaymentMethodAccount` para un método que ya tiene su propia fuente de verdad. La regla arquitectónica de este documento (una forma de cobro, una sola fuente) es la que se audita.
