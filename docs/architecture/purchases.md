# Compras — SSOT funcional

Única fuente funcional del módulo Compras: recepción electrónica, conciliación de ítems, compra, nota de crédito del proveedor y saldo a favor. Efectos de stock y costo: [inventory.md](./inventory.md). Asientos: [accounting.md](./accounting.md). Retenciones: [backend.md § Retenciones](./backend.md#retenciones-una-capacidad-una-política-de-emisión-zh-retention-emission-ssot-01-zh-purchase-retention-confirm-01). Rationale de anulación de compras: [ADR-044](../decisions/ADR-044-purchase-cancellation-and-global-sri-policy.md).

Código: `ERP.Domain/Modules/Purchases`, `ERP.Application/Modules/Purchases`, `ERP.Application/Modules/Inventory/ItemMatching`. API operativa: `/api/v1/purchases`, `/api/v1/purchases/{reception,credit-notes}`, `/api/v1/finance/supplier-credits`. `/api/v1/purchases/returns` conserva exclusivamente consultas del historial y de líneas devolvibles.

## 1. Alcance y estados

- La compra (`PurchaseInvoice`) pertenece a Tenant + Company + Branch; se opera desde la sucursal activa y los Items deben ser de la empresa.
- `PurchaseStatus` persistido: Draft=1, Confirmed=2, Cancelled=3, Discarded=4. Activos = Draft/Confirmed; históricos = Cancelled/Discarded. Solo Draft se edita.
- Unicidad entre compras activas: clave de acceso SRI por Tenant (`uq_purchase_invoices_tenant_access_key`) y número por Tenant + Company + proveedor (`uq_purchase_invoices_tenant_company_supplier_number`). Una misma clave de acceso no puede registrarse como compra y como gasto.

## 2. Borrador

- Requiere un proveedor activo con configuración SRI y una condición de pago (cuotas ≥ 1). Desde una recepción solo una factura genera compra; la NC sigue su propio flujo.
- Líneas: la bodega debe pertenecer a la sucursal de la compra; el Item debe ser de la empresa; la presentación debe pertenecer al Item y estar activa; el factor de conversión debe ser representable con `ConversionFactorDecimals`; los códigos IVA/ICE seleccionados manualmente deben estar activos en el catálogo SRI. Si el XML trae un impuesto que no está en el catálogo, se rechaza.
- ICE: el snapshot fiscal de una línea de recepción conserva la tarifa, la base y el importe declarados en el XML (`Source=Xml`); recálculos, prorrateos, confirmación y reapertura no los alteran. Un código ICE inactivo se reconoce solo como dato del XML, nunca como selección manual. La selección manual admite únicamente ICE porcentual activo; ICE específico o mixto sin importe XML se rechaza (`PURCHASE_ICE_DATA_REQUIRED`) y nunca se calcula $0 ni se deriva de la cantidad comercial. No existe cálculo operativo de ICE mixto. Editar cantidad, precio o descuento (de línea o global) de una línea XML con ICE específico, mixto o inactivo se rechaza (`PURCHASE_ICE_XML_EDIT_BLOCKED`) por falta de bases físicas verificables; con ICE porcentual activo la línea pasa a cálculo por tarifa. Las devoluciones prorratean cada fila del snapshot (tarifa conservada).
- Costos: descuento global 0–100. Flete y otros costos ≥ 0 se prorratean según la base imponible de cada línea (equitativo si la base es 0) y la última línea absorbe el residuo. "Distribuir flete/gasto" suma un monto > 0 solo a las líneas elegidas (con cantidad base > 0).
- Las cuotas manuales del borrador se guardan solo con validación estructural (número ≥ 1 sin duplicados, monto > 0).
- Descartar: Draft → Discarded, con motivo opcional (`Borrador descartado.` por defecto). Sin reversos; libera la clave de acceso, el número y la recepción de origen.

## 3. Recepción electrónica y conciliación de ítems

- Importar el TXT del SRI crea un `PurchaseReceptionDocument` por comprobante en la sucursal activa. Descargar el XML autorizado solo opera sobre documentos Imported; si falla, el documento conserva su estado. Al descargarlo se persisten sus líneas con la conciliación automática.
- La NC se interpreta con su parser propio, sin Item Matching de factura. Si el XML autorizado no tiene líneas utilizables, se conserva como `Verified` con procesamiento `Failed`, conteos y diagnóstico; `Processed`/`ProcessedWithWarnings` requieren al menos una línea procesada. Una descarga repetida sobre `Verified` se rechaza sin volver a consultar SRI ni reemplazar el snapshot. La NC continúa por la entrada única de §6.
- En el detalle de NC, `descuento` es opcional según los XSD SRI 1.0.0/1.1.0 existentes: su ausencia representa cero descuento y no descarta la línea. Si el elemento está presente, su valor debe ser válido. Se conservan los importes e impuestos del XML, incluido IVA 0 %.
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

## 6. Nota de crédito del proveedor — entrada única

- Única entrada operativa: **Nota de crédito del proveedor**, desde Compras o recepción electrónica, con dos modalidades: **Devolución de productos** (`Return`) y **Descuento / bonificación** (`Discount`). Ambas se crean, consultan, autorizan y cancelan desde `/purchases/credit-notes`. No existen altas independientes de devolución ni un flujo operativo «Devolución pendiente de NC».
- `PurchaseCreditNote`: Draft → Authorized | Cancelled; Authorized → Cancelled. Las operaciones financieras son idempotentes y se serializan por factura. La identidad tributaria activa es única: emisor, tipo NC, establecimiento, punto de emisión, secuencial y clave de acceso cuando corresponda. El número se captura como `001-001-000000001`; una clave electrónica debe corresponder al tipo, serie, secuencial y emisor. Se conservan los índices únicos de NC activas y la unicidad del documento de recepción.
- **Devolución de productos nueva:** guardar la NC crea y vincula su `PurchaseReturn` interno en un mismo guardado atómico. Este motor no tiene altas ni modificaciones operativas independientes. La cantidad no puede exceder el remanente de la línea original; la bodega y la unidad provienen de la factura. Se exige que la factura no tenga una retención emitida. Al autorizar la NC, el motor captura su número interno con `CaptureNextAsync`, registra una sola salida por línea (promedio vigente, unidad base), aplica a la CxP hasta su saldo y crea `SupplierCredit` por el excedente. Prorratea subtotal, descuento e IVA/ICE/IRBPNR desde el snapshot original; `HistoricalCostTotal` conserva el valor real retirado por Kardex. Los efectos y la finalización fiscal se confirman en una transacción. La obligación contable se origina exclusivamente en el evento del motor, sin duplicarla desde la NC.
- **Descuento / bonificación:** ajuste de CxP y obligación contable; sin `PurchaseReturn` ni movimientos de inventario. Se bloquea si el total excede el saldo pendiente.
- **Vinculación histórica:** dentro de la misma modalidad `Return` se puede elegir una devolución histórica autorizada, pendiente de NC, sin otro vínculo y compatible en factura, proveedor, empresa, sucursal, líneas e importes. La NC se construye desde su snapshot congelado. Al completar el vínculo se revalida bajo el lock financiero; el XML, si existe, debe estar verificado y coincidir en proveedor, moneda, factura y total. Solo completa la relación y el estado fiscal: no crea otra devolución, no mueve inventario, CxP ni `SupplierCredit`, y no emite hechos contables. No constituye otra modalidad ni pantalla operativa.
- **Historial:** `/purchases/returns` y `/purchases/returns/:id` son exclusivamente de consulta. Conservan documentos, vínculos, saldos, Kardex y asientos existentes. `/purchases/returns/new`, los endpoints de escritura y los contratos independientes antiguos se retiran de la superficie operativa.

## 7. Cancelación interna, reproceso y saldo a favor

- Cancelar una NC en el ERP es **cancelación interna**; no solicita ni acredita anulación tributaria ante el SRI. Desde Draft no hay efectos que revertir. Desde Authorized, `Return` utiliza el motor existente: exige crédito íntegro, reingresa cantidad/unidad/costo registrados, revierte el crédito de CxP y cancela el saldo a favor mediante su movimiento de sistema. `Discount` revierte su aplicación de CxP. La reversa contable utiliza la continuidad durable existente ([accounting.md](./accounting.md)). Una cancelación operativa no demuestra por sí sola que el reverso contable ya esté publicado.
- El mismo comprobante puede reprocesarse únicamente después de cancelación interna con **reversa íntegra verificable**. Se comprueban todas las NC canceladas de esa identidad antes de crear, editar o autorizar un nuevo borrador. Bajo los locks financieros se concilian CxP contra los créditos que siguen autorizados; cantidad, bodega y costo de cada salida/reingreso deben coincidir; `SupplierCredit` debe quedar en cero con su movimiento de cancelación y las aplicaciones/reembolsos revertidos. Un asiento publicado requiere su espejo publicado, sin obligación pendiente/fallida ni reversa del reverso. Una obligación que nunca se publicó debe estar cancelada sin asiento. La ausencia de obligación/asiento de un origen autorizado solo se acepta cuando la política contable canónica demuestra que no aplicaba.
- Para reprocesar documentos electrónicos se consulta **ConsultaComprobante**, fuera de la transacción financiera. Solo `AUTORIZADO`, con clave y tipo NC coincidentes, confirma vigencia. `ANULADO` bloquea; `PENDIENTE DE ANULAR`, estado desconocido, falta de configuración o fallo de consulta también bloquean. No se infiere vigencia tributaria desde la cancelación local ni desde un XML descargado anteriormente. Los comprobantes manuales sin clave conservan su identidad por emisor/tipo/serie/secuencial.
- Si falta evidencia o hay inconsistencias, se bloquea el reproceso y se informa qué NC y qué reversa requieren revisión; nunca se reparan saldos, se fabrican asientos o se borran históricos en ese intento. La nueva NC tiene identidad interna propia; la cancelada y sus relaciones permanecen consultables. Los vínculos fiscales históricos del mecanismo anterior que no puedan verificarse no se reutilizan automáticamente.
- `SupplierCredit` se origina en una devolución o en el remanente no aplicado de un pago (exactamente uno). Su saldo es la suma de sus movimientos: aplicación a CxP, reembolso, sus reversas y los movimientos de sistema por cancelación del origen.

## 8. Pendiente (no operativo)

- Órdenes de compra: existe la columna `PurchaseOrderId`, pero no hay agregado, flujo ni endpoint.
- Compras mixtas y costos capitalizables: FUTURE; solo existe el flujo manual vigente.
- No existe calendario oficial de días hábiles: el plazo SRI ordinario es informativo y se verifica en SRI en Línea. No existe ventana interna de anulación por empresa.

## 9. Evidencia

ICE en Compras (2026-10-21): `PurchaseIceSnapshotTests`, `PurchaseIceSpecificResolutionTests`, `PurchaseInvoiceDetailXmlTaxSnapshotTests`, `PurchaseReturnTests` (prorrateo ICE XML) y `PurchaseIceXmlSnapshotPostgreSqlTests` (PostgreSQL real 5/5: persistencia, reapertura, recálculo y descuento bloqueado); frontend `purchaseLineReadiness.test.ts`. Sin migración ni ejecución sobre datos existentes.

Unificación de NC (2026-10-21): `PurchaseReturnEndToEndTests.UnifiedCreditNotes.cs` y `.Reprocessing.cs`, PostgreSQL real 21/21 con SRI simulado; contratos API 24/24; Application focal 79/79; contabilización PostgreSQL de devolución/descuento 6/6; frontend focal 32/32. Cubren salida única, descuento sin inventario, idempotencia, concurrencia, vínculo histórico sin efectos, duplicados, cancelación interna, reversas incompletas y SRI anulado/pendiente/no verificable. Build/lint/TypeScript y gate de arquitectura PASS; límite externo del guardrail general en [entrega](../agent/PURCHASE_CREDIT_NOTE_UNIFICATION_DELIVERY.md). Sin migración ni ejecución sobre datos de negocio.

Domain/Application (filtro Items/Inventory/Purchases, 2026-10-19): 354/354 y 551/551 PASS. PostgreSQL 2026-10-19: `PurchaseCancelledStockMovementIntegrationTests`, `ResolvePurchaseReceptionLinesIntegrationTests` y `PurchaseInvoiceConfirmedPostingIntegrationTests` (dentro de 39/39 PASS). Cobertura adicional: `ConfirmPurchaseHandlerTests`, `CancelPurchaseHandlerTests`, `DiscardPurchaseDraftTests`, `PurchaseDraftRecoveryTests`, `PurchaseXmlConfirmationGuardTests`, `PurchaseInvoiceCostProrationConsistencyTests`, `ItemMatchFinderTests`, `ItemMatchConfirmationServiceTests`, `AuthorizePurchaseReturnHandlerTests`, `CancelPurchaseReturnUseCasesTests`, `PurchaseCreditNoteDiscountIntegrationTests`, `SupplierCreditMovementTests`, `PurchaseRetentionConfirmIntegrationTests`.
