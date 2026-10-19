# Compras — SSOT funcional

Única fuente funcional del módulo Compras: recepción electrónica, conciliación de ítems, compra, devolución, nota de crédito de proveedor y saldo a favor. Describe solo el comportamiento implementado y cubierto por tests vigentes. Efectos de stock y costo: [inventory.md](./inventory.md). Asientos: [accounting.md](./accounting.md). Retenciones: [backend.md § Retenciones](./backend.md#retenciones-una-capacidad-una-política-de-emisión-zh-retention-emission-ssot-01-zh-purchase-retention-confirm-01). Rationale: [ADR-044](../decisions/ADR-044-purchase-cancellation-and-global-sri-policy.md).

Código: `ERP.Domain/Modules/Purchases`, `ERP.Application/Modules/Purchases`, `ERP.Application/Modules/Inventory/ItemMatching`. API: `/api/v1/purchases`, `/api/v1/purchases/{reception,returns,credit-notes}`, `/api/v1/finance/supplier-credits`.

## 1. Alcance y estados

- La compra (`PurchaseInvoice`) pertenece a Tenant + Company + Branch; se opera desde la sucursal activa y los Items deben ser de la empresa.
- `PurchaseStatus` persistido: Draft=1, Confirmed=2, Cancelled=3, Discarded=4. Activos = Draft/Confirmed; históricos = Cancelled/Discarded. Solo Draft se edita.
- Unicidad entre compras activas: clave de acceso SRI por Tenant (`uq_purchase_invoices_tenant_access_key`) y número por Tenant + Company + proveedor (`uq_purchase_invoices_tenant_company_supplier_number`). Una misma clave de acceso no puede registrarse como compra y como gasto.

## 2. Borrador

- Requiere un proveedor activo con configuración SRI y una condición de pago (cuotas ≥ 1). Desde una recepción solo una factura genera compra; la NC sigue su propio flujo.
- Líneas: la bodega debe pertenecer a la sucursal de la compra; el Item debe ser de la empresa; la presentación debe pertenecer al Item y estar activa; el factor de conversión debe ser representable con `ConversionFactorDecimals`; los códigos IVA/ICE deben estar activos en el catálogo SRI. Si el XML trae un impuesto que no está en el catálogo, se rechaza.
- Costos: descuento global 0–100. Flete y otros costos ≥ 0 se prorratean según la base imponible de cada línea (equitativo si la base es 0) y la última línea absorbe el residuo. "Distribuir flete/gasto" suma un monto > 0 solo a las líneas elegidas (con cantidad base > 0).
- Las cuotas manuales del borrador se guardan solo con validación estructural (número ≥ 1 sin duplicados, monto > 0).
- Descartar: Draft → Discarded, con motivo opcional (`Borrador descartado.` por defecto). Sin reversos; libera la clave de acceso, el número y la recepción de origen.

## 3. Recepción electrónica y conciliación de ítems

- Importar el TXT del SRI crea un `PurchaseReceptionDocument` por comprobante en la sucursal activa. Descargar el XML autorizado solo opera sobre documentos Imported; si falla, el documento conserva su estado. Al descargarlo se persisten sus líneas con la conciliación automática.
- "Actualizar estados" recalcula el estado de compra, gasto y documento sin reimportar, guardar ni consultar al SRI. La recepción refleja la compra por `PurchaseState` (None/Draft/Confirmed/Cancelled/Discarded) + `PurchaseId`.
- "Crear compra" desde la recepción precarga el formulario a partir del XML guardado y la conciliación persistida. La compra se crea solo al guardar el borrador. Una recepción puede generar compra o gasto, nunca ambos.
- Conciliación (`ItemMatchStatus`: Pending, NeedsReview, AutoMatched, ManuallyMatched):
  - Código principal o auxiliar de proveedor exacto → vínculo automático.
  - Si la línea trae código de proveedor desconocido → revisión manual; nunca se reemplaza por descripción.
  - Sin código de proveedor → candidatos por descripción normalizada (score 95) o por similitud pg_trgm (umbral 0.80, score mínimo 75), solo como sugerencia.
  - Confirmar (individual o en lote) crea el `ItemSupplierCode` si no existía. Desvincular desactiva el código aprendido no principal. La conciliación nunca crea Items ni infiere impuestos.

## 4. Confirmación

En una sola transacción, todo o nada:

- **Bloquean la confirmación:**
  - Estado distinto de Draft, o compra sin líneas.
  - Condición de pago inactiva.
  - Bodega de otra sucursal.
  - Línea de recepción sin Item.
  - Product de una línea XML sin presentación.
  - Product sin bodega, o Service con bodega.
  - Cantidad ≤ 0 o línea sin IVA.
  - Descuadre contra el XML autorizado que afecte cantidades, costos, totales o trazabilidad (`PURCHASE_XML_RECONCILIATION_REQUIRED`).
  - Margen contra el precio de venta resuelto < −50 % (presentación o costo sospechoso).
- **Requisito por empresa:** con `purchases.allow_confirm_without_reception_xml` OFF, al menos una línea debe venir de recepción.
- **Efectos:**
  - Congela costos y resúmenes de impuestos.
  - Cronograma de pagos: el enviado; si no, el del borrador si cuadra con el total y no vence antes de la emisión; si no, el generado desde la condición. Nunca se ajusta en silencio.
  - `PurchaseEntry` por línea Product al costo unitario con flete/gastos incluidos, con la fecha de emisión.
  - CxP (`AccountsPayable`) si el total > 0.
  - Retención opcional ([backend.md § Retenciones](./backend.md#retenciones-una-capacidad-una-política-de-emisión-zh-retention-emission-ssot-01-zh-purchase-retention-confirm-01)).
  - Actualiza `Item.BaseSalePrice` con el PVP de la línea cuando difiere.
  - Obligaciones contables durables. La transmisión de la retención ocurre después del commit.

## 5. Anulación (Confirmed → Cancelled)

- Requiere motivo (≤ 500). **Se rechaza si:**
  - La compra no está Confirmed.
  - Tiene pagos aplicados.
  - Tiene una devolución autorizada.
  - Tiene saldo a favor aplicado.
  - Tiene una NC o un crédito de devolución aplicados.
  - Existe cualquier salida de inventario posterior a la entrada original del mismo Item + Bodega, aunque esa salida se haya revertido (`CancellationSubsequentInventoryExit`). Las entradas posteriores solas no bloquean.
- La comprobación y el reverso ocurren bajo los mismos advisory locks de inventario. Efectos: anula la retención emitida (si requiere anulación SRI, sigue el flujo de Retenciones), cancela la CxP, registra `PurchaseCancelled` por línea al costo histórico con la fecha de la empresa y libera la recepción de origen.
- Cancelar localmente una compra no anula ante el SRI la factura del proveedor. La política global de anulación SRI se resuelve por fecha de emisión desde `sri_annulment_policy_version`, con su fuente normativa (0 o varias versiones vigentes fallan cerrado). Solo se carga vía HasData + migraciones EF. La solicitud de anulación de la retención guarda el plazo ordinario como snapshot (`OrdinaryDeadline`).

## 6. Devolución de compra

- `PurchaseReturn`: Draft → Authorized | Cancelled; Authorized → Cancelled. La cantidad no puede exceder el remanente devolvible de la línea original. Autorizar y cancelar son idempotentes y se serializan con un lock financiero por factura.
- **Autorizar:** exige líneas y que la factura no tenga una retención emitida. El número se captura con `CaptureNextAsync`. Registra `PurchaseReturn` (salida al promedio vigente, en unidad base). Prorratea subtotal, descuento e impuestos (IVA/ICE/IRBPNR) del snapshot de la factura. `HistoricalCostTotal` es el valor retirado por el Kardex. Aplica a la CxP hasta su saldo; el excedente crea `SupplierCredit`. Queda pendiente de NC del proveedor.
- **Cancelar desde Authorized:** exige crédito íntegro. Revierte la salida con su costo registrado, revierte el crédito aplicado a la CxP y registra el movimiento de sistema en el saldo a favor.

## 7. Nota de crédito de proveedor y saldo a favor

- `PurchaseCreditNote`: Draft → Authorized | Cancelled; Authorized → Cancelled.
  - Tipo `Return`: se vincula a una devolución autorizada; inventario, CxP y crédito quedan en la devolución.
  - Tipo `Discount`: se aplica contra la CxP sin inventario; se bloquea si el total excede el saldo pendiente. Genera obligación contable ([accounting.md](./accounting.md)).
- `SupplierCredit` se origina en una devolución o en el remanente no aplicado de un pago (exactamente uno). Su saldo es la suma de sus movimientos: aplicación a CxP, reembolso, sus reversas y los movimientos de sistema por cancelación del origen.

## 8. Pendiente (no operativo)

- Órdenes de compra: existe la columna `PurchaseOrderId`, pero no hay agregado, flujo ni endpoint.
- Compras mixtas y costos capitalizables: FUTURE; solo existe el flujo manual vigente.
- No existe calendario oficial de días hábiles: el plazo SRI ordinario es informativo y se verifica en SRI en Línea. No existe ventana interna de anulación por empresa.

## 9. Evidencia

Domain/Application (filtro Items/Inventory/Purchases, 2026-10-19): 354/354 y 551/551 PASS. PostgreSQL 2026-10-19: `PurchaseCancelledStockMovementIntegrationTests`, `ResolvePurchaseReceptionLinesIntegrationTests` y `PurchaseInvoiceConfirmedPostingIntegrationTests` (dentro de 39/39 PASS). Cobertura adicional: `ConfirmPurchaseHandlerTests`, `CancelPurchaseHandlerTests`, `DiscardPurchaseDraftTests`, `PurchaseDraftRecoveryTests`, `PurchaseXmlConfirmationGuardTests`, `PurchaseInvoiceCostProrationConsistencyTests`, `ItemMatchFinderTests`, `ItemMatchConfirmationServiceTests`, `AuthorizePurchaseReturnHandlerTests`, `CancelPurchaseReturnUseCasesTests`, `PurchaseCreditNoteDiscountIntegrationTests`, `SupplierCreditMovementTests`, `PurchaseRetentionConfirmIntegrationTests`.
