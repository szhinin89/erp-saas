# Devolución de venta con CxC cobrada — E2E

Fecha: 2026-10-27. Clasificación: **PASS**: 14/14 comprobaciones en navegador y 23/23 de rechazos y
aislamiento (37/37), con verificación en PostgreSQL. Sin BUG REAL ni cambios de código. Aprobado
por el propietario (2026-10-27). CLOSED solo para la devolución parcial con crédito a una CxC
parcialmente cobrada.
Comportamiento: [SSOT de CxC § Devolución y anulación](../../architecture/receivables.md#devolución-y-anulación),
[caminos excluyentes](../../architecture/backend.md#revertir-una-venta-un-solo-camino-por-factura-zh-sales-cancel-authorized-return-rule-01),
[contabilización](../../architecture/accounting.md); cobertura:
[matriz QA](../../QA/RECEIVABLES-COLLECTIONS-CLOSEOUT.md). No repite los E2E de cobro
([receivables-e2e-01](../receivables-e2e-01/README.md) a [-03](../receivables-e2e-03/README.md)).
Sin commit, push ni deploy; sin migraciones ni cambios de contrato API.

## Regla aplicada

El reembolso se asigna de forma explícita (sin prorrateo automático): «Efectivo (Caja)», «Crédito a
Cuenta por Cobrar» o una combinación, cuya suma debe igualar el total de la devolución. Se eligió
**crédito a CxC por el total**. La SSOT define que el crédito reduce `OriginalAmount` (nunca lo
cobrado), que no puede exceder el saldo y que las cuotas se reconstruyen sobre el saldo con sus
números y vencimientos. Se validó tal cual, sin inventar política ni cambiar reglas.

## Entorno

- Base `erp_zh_test` sin recrear; usuario `e2e.admin` del seed E2E con credencial efímera, borrada
  al terminar. Otras bases sin cambios.
- API aislada en `:5004` (Release del HEAD `c1046783a`, Development, seed E2E, JWT efímero),
  detenida al terminar. La API de desarrollo `:5003` no se tocó (0 solicitudes).
- Chromium (Playwright) sobre un Vite propio en `:5181` con `/api/*` redirigido a `:5004`; detenido.

## Datos preparados vía API (flujos ya aprobados)

- Producto `RET-PRD-758825` (IVA de venta 15% configurado en el ítem, precio base 20.00) e ingreso
  de stock `ADJ-0001`: 10 u a costo 8.00 en «Bodega Principal», con el motivo `E2E-ING`.
- Factura `001-001-000000005`: 5 u × 20.00 = 100.00 + IVA 15.00 = **115.00**, al cliente
  `Cliente E2E ZH`, método `CREDITO`, cronograma manual de 2 cuotas de 57.50 (26/11 y 26/12/2026).
- Cobro parcial de **40.00** sin cuota (FIFO), referencia `E2E-DEV-COBRO-40`. CxC: 115.00 / 40.00 /
  75.00 «Parcial»; cuota #1 con 40.00 cobrado (`partial`) y cuota #2 `pending`.

## Flujo verificado en navegador

| Paso | Resultado |
|---|---|
| `/sales/returns/new`: elegir la factura, motivo y 2 u ([captura](d1-draft-form.png)) | Borrador: subtotal 40.00, IVA 6.00, total 46.00; CxC sin cambios (115 / 40 / 75) |
| «Autorizar devolución» con «Crédito a Cuenta por Cobrar» por 46.00 ([captura](a1-authorize-modal-credit.png)) | `Authorized`; asignación `ReceivableCredit` 46.00 |
| CxC tras autorizar (API) | 69.00 / 40.00 / **29.00** «Parcial», «Vence» 26/11/2026; cuotas 14.50 + 14.50, ambas `pending` |
| Recargar el detalle ([captura](r1-return-reloaded.png)) | «Autorizada», líneas y reembolso de solo lectura, sin acciones de borrador |
| Listado de devoluciones ([captura](r2-returns-list.png)) | `DEV-0a5d40ad43` «Autorizada», 46.00 |
| `/finance/receivables` y recarga ([captura](c1-receivables-after-return.png)) | Fila 69.00 / 40.00 / 29.00 «Parcial», igual tras recargar |
| Modal de cobro ([captura](c2-collection-modal-cuotas.png)) | «Cuota #1 — 14.50 pendiente», «Cuota #2 — 14.50 pendiente» |

## PostgreSQL

- `sales_returns`: `Authorized`; subtotal 40.00, IVA 6.00, ICE 0, descuento 0, total 46.00; Nota de
  Crédito `001-001-000000001`. Detalle congelado: 2 u, `vat_code` 4, tarifa 15%, IVA 6.00 (impuesto
  heredado de la factura). Asignación: `ReceivableCredit` 46.00.
- `sales_receivables`: original **69.00** (115 − 46), cobrado **40.00** (sin cambio), saldo 29.00,
  `status` `pending`. `sales_receivable_installments`: #1 26/11 14.50 cobrado 0 `pending`; #2 26/12
  14.50 cobrado 0 `pending`. `payments`: el cobro de 40.00 sigue `Applied`.
- Kardex: `ADJ-0001` +10 a 8.00 → venta −5 (costo 40.00) → devolución `SaleReturn` **+2** (costo
  16.00 al costo de la venta). Stock final 7 u, valor 56.00, costo 8.00. Sin movimientos de caja
  para la devolución.
- Asientos `Posted`, balanceados y con cuentas de la empresa A:

| N.º | Evento | Debe | Haber |
|---|---|---|---|
| 28 | `Sales/InvoiceIssued` | 1.1.03.001 CxC 115.00 | 4.1.01.001 Ventas 100.00; 2.1.02.001 IVA 15.00 |
| 29 | `Sales/CostOfGoodsSold` | 5.1.01.001 Costo 40.00 | 1.1.04.001 Inventario 40.00 |
| 30 | `Finance/CollectionApplied` | 1.1.01.001 Caja general 40.00 | 1.1.03.001 CxC 40.00 |
| 31 | `Sales/CostOfGoodsSoldReversed` | 1.1.04.001 Inventario 16.00 | 5.1.01.001 Costo 16.00 |
| 32 | `Sales/SalesReturn` | 4.1.03.001 Devoluciones en ventas 40.00; 2.1.02.001 IVA 6.00 | 1.1.03.001 CxC 46.00 |

Saldo contable de CxC de la factura: 115 − 40 − 46 = 29.00, igual al saldo de la CxC.

## Rechazos y aislamiento empresarial (API y UI)

| Caso | Resultado |
|---|---|
| Empresa A: remanente devolvible | 3 u (5 vendidas − 2 devueltas) |
| Empresa A: borrador por 4 u | 422 «la cantidad solicitada (4) excede el remanente devolvible (3…)» |
| Empresa A: autorizar 3 u (69.00) todo a crédito CxC con saldo 29.00 | 422 «El monto del crédito excede el saldo pendiente de la cuenta por cobrar.»; borrador sigue `Draft` |
| Empresa A: anular la factura con devolución autorizada | 422 «La factura tiene una devolución autorizada y no puede anularse.» |
| Empresa A: cancelar ese borrador | `Cancelled`, sin Nota de Crédito |
| Empresa B: listado de devoluciones | No incluye la de A |
| Empresa B: detalle, autorizar o cancelar la devolución de A | 404 |
| Empresa B: líneas devolvibles o borrador sobre la factura de A | 404 |
| Empresa B: CxC de la factura de A | 404 |
| UI de la empresa B: `/sales/returns` y el detalle de A ([captura](b1-company-b-returns.png), [detalle](b2-company-b-return-detail.png)) | Listado vacío; «Devolución no encontrada.» |

Tras los rechazos: 3 movimientos de Kardex, último asiento n.º 32, CxC 69.00 / 40.00 y devolución de
A `Authorized`; ningún rechazo dejó efectos.

Detalle: [e2e-results.json](e2e-results.json).

## Límites del cierre

- **Emisión electrónica SRI no acreditada:** la Nota de Crédito queda en «Falló la generación»
  porque la empresa E2E no tiene configuración SRI ([captura](r1-return-reloaded.png)). Se acredita
  la autorización interna de la devolución y su número de NC, no la generación, firma ni
  autorización ante el SRI.
- **Cuotas reconstruidas conforme a la SSOT:** la devolución reconstruye las cuotas sobre el saldo
  con sus números y vencimientos; arrancan con cobrado 0 y el cobro de 40.00 de la cuota #1 queda
  reflejado en la CxC (cobrado 40.00), no en las cuotas. Es el comportamiento documentado, coherente
  con el límite ya descrito para la reversa de cobros.
- No acredita reembolso en efectivo o mixto, devolución total ni mora.

## Observaciones fuera de alcance (sin cambios)

- **Observación UX no bloqueante:** la columna «Cliente» del listado `/sales/returns` muestra el
  `customerId` (GUID) en vez del nombre ([captura](r2-returns-list.png)). `SalesReturnListItemDto`
  no expone el nombre. No afecta datos ni contabilidad; se registra sin ticket ni cambio de
  contrato.
- El número interno de devolución (`DEV-` + 10 caracteres del Guid) no es correlativo; el número
  fiscal es el de la Nota de Crédito. La referencia del Kardex lleva doble prefijo
  (`DEV-DEV-0a5d40ad43`), fijado por `AuthorizeSalesReturnHandlerTests`. Ambos son comportamiento
  vigente.
- Errores esperados en consola: `401` de refresh antes del login y `403` de
  `electronic-invoicing/status` antes de seleccionar empresa.

## Gates

Sin cambios de código de producción ni de tests: no aplican `dotnet test`, lint, `tsc -b` ni build
del frontend. Se compiló el API en Release para la instancia aislada. Suite completa no ejecutada.
