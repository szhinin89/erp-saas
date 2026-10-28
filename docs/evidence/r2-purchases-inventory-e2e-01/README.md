# R2 — Compras XML, retención, NC de proveedor, ajuste y transferencia — E2E

Fecha: 2026-10-28. Clasificación: **PASS 5/5 con un BUG REAL corregido** (#16); aprobado por el
propietario (R2 CLOSED, 2026-10-28), con la corrección H-1 autorizada al cierre. Escenarios #14, #31, #16, #10 y #11 del plan de validación V1, sobre la empresa B y los
datos de [R1](../r1-initial-load-e2e-01/README.md). Comportamiento: [Compras](../../architecture/purchases.md),
[Inventario](../../architecture/inventory.md), [Retenciones](../../architecture/backend.md#retenciones-una-capacidad-una-política-de-emisión-zh-retention-emission-ssot-01-zh-purchase-retention-confirm-01),
[Accounting](../../architecture/accounting.md). Sin commit, push ni deploy; sin migraciones ni cambios de
contrato API.

## Entorno

- Base `erp_zh_test` (baseline vigente, sin recrear); usuario `e2e.admin` del seed E2E con credencial
  efímera no almacenada. Otras bases sin cambios. El token first-run que la API imprime al arrancar no
  se leyó ni se usó.
- API aislada en `:5004` (Release del HEAD, Development, seed E2E, JWT efímero); tras la corrección se
  recompiló y reinició. Chromium (Playwright 1.59) sobre un Vite propio en `:5181` con proxy `/api` →
  `:5004`. Los tres procesos se detuvieron al terminar; `:5003` no se usó.
- **SRI simulado en local.** La descarga del XML autorizado usa el WSDL configurado de la empresa. Se
  configuró en la empresa B (ambiente pruebas, sin certificado) un WSDL `http://127.0.0.1:5099/…`
  servido por un mock local que solo responde `autorizacionComprobante` con XML sintético; recepción y
  consulta responden 503. El mock registró únicamente las 2 consultas de autorización de las facturas
  sintéticas. Ningún tráfico salió hacia el SRI.
- Datos sintéticos: 2 facturas XML (clave de acceso con dígito verificador módulo 11) de los
  proveedores de R1.

## Configuración previa (UI)

- Proveedores de R1: sustento 06, forma de pago 20 y tipo Persona Natural. «Lácteos del Valle R1» además
  con retenciones predeterminadas IVA 725 (30 %) y Renta 312 (2 %), solo en la empresa B.
- Presentación base «UNIDAD X1» en `R1-ARROZ-01`, `R1-ACEITE-01` y `R1-DETER-01` desde Ítems →
  Inventario y presentaciones (ver hallazgo H-1).
- Motivo de ajuste `MERMA_R2` (Egreso, cuenta 5.1.02.001) y bodega «Bodega Trastienda R2» en la
  Sucursal Principal.

## Resultados

| # | Escenario | Resultado |
|---|---|---|
| 14 | Compras XML: importar, vincular, crear pendiente, flete, confirmar y reabrir | **PASS** (hallazgo H-1, precondición) |
| 31 | Retención funcional y CxP neta | **PASS** |
| 16 | NC de proveedor: devolución parcial, Kardex, CxP y asiento | **PASS** tras corregir un **BUG REAL** |
| 10 | Ajuste de egreso, Kardex, contabilización y anulación | **PASS** |
| 11 | Transferencia entre bodegas | **PASS** |

### #14 — Compra desde XML

| Paso | Resultado |
|---|---|
| Importar TXT (factura `001-002-000004501`, 93.34) y «Consultar XML» ([captura](p14-reception-xml-verified.png)) | Recepción VERIFICADO, lectura OK, 3 líneas procesadas; reimportar el mismo TXT no duplica el documento |
| «Crear compra» | Formulario precargado desde el XML: 3 líneas pendientes, total 93.34 |
| Resolver productos pendientes ([captura](p14-pending-products-modal.png)) | Arroz y aceite vinculados a productos de R1; azúcar creado como `R2-AZUCAR-01` (Físico, Marca R1, Abarrotes R1, Unidad, IVA 15 %, PVP 1.84 con IVA). `applied:true`; se aprenden 3 códigos de proveedor |
| Guardar borrador ([captura](p14-draft-saved.png)) | Sustento 06, condición 30 días, bodega por línea; «Listo para confirmar» |
| Flete externo 6.00 a las 3 líneas ([captura](p14-freight-distribution.png)) | Prorrateo por base neta: 1.766355 / 2.383178 / 1.850467 (la última absorbe el residuo); total 99.34 |
| Confirmar ([captura](p14-confirm-modal.png)) | Compra Confirmada, sin retención (proveedor sin defaults) |
| Kardex | Entradas 26.97 / 36.38 / 28.25 = 91.60 (tramos del acumulado); promedio arroz 1.803167, aceite 2.391129, azúcar 1.12375 |
| CxP / asiento | CxP 99.34, 1 cuota al 27/11/2026; asiento N° 7 Posted: Inventario 91.60 + IVA 7.74 / CxP 99.34 |
| Reabrir ([captura](p14-reopened-readonly.png)) | Solo lectura; costos externos 6.00, total 99.34; «Distribuir flete/gasto» y «Guardar presentación» deshabilitados; la recepción queda Confirmada con su compra |

### #31 — Retención funcional

Factura `001-001-000000731` de «Lácteos del Valle R1» (23.00 + IVA 3.45 = 26.45), desde XML.

| Paso | Resultado |
|---|---|
| Vista previa en el borrador ([captura](p31-retention-preview.png)) | IVA 725: 30 % × 3.45 = 1.035 → 1.04; Renta 312: 2 % × 23.00 = 0.46; total 1.50 |
| Emitir al confirmar (punto 001-001, 28/10/2026) | Retención `001-001-000000001` Issued en la misma confirmación; sustento 06 y autorización de la factura en el snapshot |
| CxP ([captura](p31-payables-net.png)) | Total 26.45, retenido 1.50, saldo **24.95** |
| Asientos | N° 8 Retentions/DocumentIssued: CxP 1.50 / Ret. IVA por pagar 1.04 + Ret. renta por pagar 0.46; N° 9 compra: Inventario 23.00 + IVA 3.45 / CxP 26.45 |
| Transmisión | Se detuvo antes de generar el XML (`VALIDATION_ERROR`: la empresa B no tiene dirección matriz ni de establecimiento); estado electrónico «Pendiente». Nada se envió al SRI real |
| Reabrir ([captura](p31-reopened-retention.png)) | Tarjeta con número, base, % y retenido por código |

### #16 — NC de proveedor (devolución parcial)

| Paso | Resultado |
|---|---|
| NC `001-002-000000101` desde la compra #14, devolución de 4 aceites ([captura](p16-nc-return-filled.png)) | Resumen 8.80 + IVA 1.32 = 10.12; guarda borrador y su devolución interna vinculada |
| Autorizar ([captura](p16-nc-authorized.png)) | Kardex: salida −4 al promedio (9.56), saldo 58 / 138.69. CxP: crédito de devolución 10.12, saldo 89.22. Asiento N° 10: CxP 10.12 + diferencia de costo 0.76 / Inventario 9.56 + IVA 1.32 (costo histórico, [accounting.md](../../architecture/accounting.md)) |
| Estado de la CxP | **BUG REAL**: quedaba «Pendiente» con saldo reducido ([captura](p16-bug-payable-pending.png)) |
| Tras la corrección: NC `001-002-000000102`, 1 arroz | CxP «Parcial», saldo 87.52 ([captura](p16-payable-after-fix.png)); Kardex −1 al promedio (1.80); asiento N° 11 balanceado |

**BUG REAL (corregido).** `AccountsPayable.ApplyReturnCredit` aplicaba el crédito por FIFO sin
recalcular el estado, a diferencia de `Apply`/`Reverse`, aunque `RecalculateStatus` ya considera
`ReturnCreditAmount`. Una devolución parcial dejaba la CxP en `Pending` y una devolución que cubriera
el saldo la habría dejado `Pending` con saldo 0, visible así en el listado y en sus filtros.
Corrección de una línea (`RecalculateStatus()`). Regresión focal en
`PurchasePayableReturnCreditTests`: sobre HEAD 2 FAIL / 11 PASS; con la corrección 13/13. La CxP de
#14 quedó corregida por el flujo normal al autorizar la segunda NC; no se modificaron datos.

### #10 — Ajuste de inventario (egreso)

| Paso | Resultado |
|---|---|
| Borrador `ADJ-0003`, Egreso, motivo `MERMA_R2`, 5 × `R1-DETER-01` ([captura](p10-adjustment-draft.png)) | El selector de motivo solo ofrece motivos que admiten Egreso; el borrador no mueve stock |
| Ejecutar ([captura](p10-adjustment-executed.png)) | NegativeAdjust −5 al promedio 1.12 = 5.60 (50 → 45); asiento N° 12: Mermas 5.60 / Inventario 5.60 |
| Anular con motivo ([captura](p10-adjustment-cancelled.png)) | PositiveAdjust +5 por 5.60 (vuelve a 50 / 56.00); asiento N° 13 reverso espejo |
| Rechazos | Anular otra vez y ejecutar el anulado → 422 con su motivo, sin efectos |

### #11 — Transferencia entre bodegas

| Paso | Resultado |
|---|---|
| TR-0001: 10 arroz Principal → Trastienda R2 | TransferExit −10 por 18.03 al promedio 1.803167; TransferEntry +10 por 18.03 en destino |
| TR-0003 en UI: 1 aceite, «Confirmar transferencia» con modal ([captura](p11-transfer-confirm-modal.png), [confirmada](p11-transfer-confirmed.png)) | Salida 2.39 / entrada 2.39, mismo costo; estado Confirmada |
| Contabilidad | Sin asiento: la SSOT no define hecho contable para transferencias (misma cuenta de inventario) |
| Reconfirmar TR-0003 | 400, sin movimientos nuevos |

TR-0001 y TR-0002 se crearon en la UI y se confirmaron por la misma API que usa la pantalla: la
transferencia no tiene listado ni detalle (ver H-3), así que un borrador no se puede reabrir.

### Aislamiento y rechazos (API, token en la empresa A con su sucursal)

GET de las compras #14 y #31, de la NC, del ajuste y de las líneas de recepción de B → 404; autorizar
la NC, anular el ajuste o confirmar la transferencia de B → 404; los listados de compras y CxP de A no
contienen documentos de B. Anular la compra #14 con devolución autorizada → 422 («Cancele primero la
devolución»); reautorizar la NC → 422. Conteos de movimientos, asientos, NC, ajustes, retenciones y
compras confirmadas idénticos antes y después.

## Hallazgos

- **H-1 (corregido al cierre, autorizado).** Los Products creados desde Ítems o la Carga Inicial no tienen
  presentación base, aunque [items.md §5](../../architecture/items.md#5-variantes-empaques-y-conversiones)
  dice que un Product exige exactamente una. Al vincular una línea XML a uno de ellos, el selector de
  presentación queda vacío y el servidor rechaza con «Seleccione la presentación que entrega el
  proveedor…» (`applied:false`, sin efectos). La pantalla de Ítems guía a crear la «presentación base
  X1», y así se resolvió durante el E2E. Corrección autorizada: `CreateItemCommandHandler` registra la
  presentación base «Unidad» de todo Product nuevo (Ítems, IL-1 y Resolver productos pendientes); un
  Service se crea sin presentaciones. Regresión RED → GREEN: `CreateItemCommandHandlerTests`
  (1 FAIL → 4/4) y `ConfirmItemsAtomicPostgreSqlTests.Exito_commit_unico…` (2 FAIL → 2/2). Los
  Products históricos no se migran. No se repitió el E2E.
- **H-2 (diferido, aislamiento sin fuga).** `GET /purchases/{id}/retention` con otra empresa responde 200 con
  `data: null` en lugar de 404; no expone datos.
- **H-3 (diferido, Transferencias).** Sin listado ni detalle: un borrador creado no se puede reabrir ni descartar
  desde la UI. El campo de cantidad no tiene etiqueta accesible.

## Observaciones menores (DEBT, sin ticket)

- Recepción: el archivo importado muestra «0 B»; la lista solo muestra el lote de la sesión.
- Tarjeta de retención: «Estado: Issued» sin traducir; modal de transferencia: «Estado actual: Draft».
- Autorizar la NC no pide confirmación (mueve inventario y CxP).
- Una CxP con solo retención aplicada figura «Parcial» (regla vigente de `RecalculateStatus`).
- El formulario de NC muestra «Saldo pendiente —» antes de guardar.

## Cambios de código

- `backend/src/ERP.Domain/Modules/Payables/Entities/AccountsPayable.cs`: `ApplyReturnCredit` recalcula
  el estado (1+).
- `backend/src/ERP.Domain.Tests/Purchases/PurchasePayableReturnCreditTests.cs`: 2 pruebas focales.
- `backend/src/ERP.Application/Modules/Items/UseCases/CreateItem/CreateItemCommandHandler.cs`: presentación
  base al crear un Product (H-1).
- `backend/src/ERP.Application.Tests/Items/CreateItemCommandHandlerTests.cs`: 2 pruebas focales (Product y
  Service).
- `backend/src/ERP.Infrastructure.Tests/InitialLoad/ConfirmItemsAtomicPostgreSqlTests.cs`: aserción de la
  presentación base de los Products importados.
- `docs/architecture/items.md` §5: regla de la presentación base al crear.

## Gates

Domain `PurchasePayableReturnCreditTests` RED 2/13 → GREEN 13/13; filtro Domain
`PurchasePayable|AccountsPayable` 60/60; Application `PurchaseReturn|PurchaseCreditNote|SupplierCredit|Payable`
387/387; Infrastructure PostgreSQL (Testcontainers) `PurchaseReturnCrossInvariantTests` +
`PurchaseReturnEndToEndTests` 8/8; build Release de ERP.API; `npm run architecture:check` PASS (0
violaciones nuevas); `git diff --check` PASS. H-1: Application `CreateItemCommandHandlerTests` 4/4 y
filtro `Items|InitialLoad|ItemMatching|PurchaseReception|Packaging` 562/562; PostgreSQL
`ConfirmItemsAtomicPostgreSqlTests` + `ResolvePurchaseReceptionLinesIntegrationTests` 62/62 y
`InventoryAdjustmentReasonCompanyIsolationHttpTests`, `InventoryAdjustmentsEndToEndTests`,
`InvoiceItemSearchRepositoryTests`, `ItemCompanyScopeA1Tests`, `ItemRepositorySupplierCodeMatchTests`
31/31. Suite completa no ejecutada.

## Datos que quedan en `erp_zh_test` (empresa B, para R3–R4)

Compras confirmadas #14 (99.34, CxP saldo 87.52, NC 101 y 102 autorizadas) y #31 (26.45, retención
1.50, CxP saldo 24.95); producto `R2-AZUCAR-01` (24 u); presentaciones base en los 3 productos de R1;
3 códigos de proveedor aprendidos; motivo `MERMA_R2`; bodega «Bodega Trastienda R2» con 10 arroz y
3 aceites; ajuste `ADJ-0003` anulado; TR-0001..0003 confirmadas. Asientos 7–13. La configuración SRI
temporal de la empresa B (WSDL del mock local) se retiró al cerrar: la empresa vuelve a no tener
configuración SRI, como antes de R2; ninguna otra empresa se modificó. Stock en
Bodega Principal: arroz 109, aceite 55, azúcar 24, detergente 50.

Detalle: [e2e-results.json](e2e-results.json).
