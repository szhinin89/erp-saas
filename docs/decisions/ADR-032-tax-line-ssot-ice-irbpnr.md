# ADR-032: ICE e IRBPNR como impuestos de línea — SSOT en `*DetailTax` (propuesta técnica)

**Estado:** 🟢 APROBADO (dirección general) — pre-implementación, con ajustes pendientes de esta revisión antes de codificar (ver §3.2 y §7)
**Fecha:** 2026-08-29 (revisión de aprobación: 2026-08-29)
**Autor:** Sebastian Zhinin (auditoría + diseño guiado)
**Ticket:** TAX-LINE-SSOT-ICE-IRBPNR-01
**Contexto previo:** [ADR-021](./ADR-021-pricing-engine-ssot.md) (SSOT de precio, patrón de referencia), [ADR-023](./ADR-023-electronic-documents-v1-closure.md) / [ADR-025](./ADR-025-ride-design-freeze.md) (ElectronicDocuments/RIDE — infraestructura CLOSED que este ADR no reabre), [ADR-028](./ADR-028-purchase-reception-to-purchase-flow-freeze.md) (Recepción XML de Compras — infraestructura CLOSED que este ADR no reabre)

---

## 1. Contexto

El manejo actual de ICE e IRBPNR como impuestos de línea tiene **doble fuente de verdad en Compras** y **ausencia total de modelo en Ventas**, con brechas funcionales reales en devoluciones, notas de crédito y XML de venta. Este ADR es la propuesta técnica (diseño, no implementación) para consolidar la fuente de verdad en las tablas `*DetailTax`, siguiendo el mismo espíritu de ADR-021: una entidad delegada como única autoridad, snapshot histórico inmutable por documento, sin doble fuente de verdad.

No reabre infraestructura CLOSED (Recepción XML→Compra de ADR-028, ElectronicDocuments v1.0 de ADR-023, RIDE de ADR-025): este ADR opera **dentro** de esas fronteras — cambia quién alimenta los datos que esas infraestructuras ya consumen, no sus contratos ni su diseño.

## 2. Auditoría de consumidores actuales

### 2.1 Compras — doble fuente de verdad ya presente

`PurchaseInvoiceDetail` (`ERP.Domain/Modules/Purchases/Entities/PurchaseInvoiceDetail.cs`):
- Campos escalares `IceCode` (L63), `IceRate` (L64), `IceAmount` (L65), `SnapshotIceName` (L66), `IceCalculationType` (L74-75) — hoy son **autoridad real** de `TaxInclusiveTotal` (L142) y de los totales de factura (esto es exactamente lo que este ADR revierte: pasan a ser un *legacy compatibility mirror*, nunca la fuente de una decisión nueva — ver §3.3).
- Colección `_taxes`/`Taxes` de `PurchaseInvoiceDetailTax` (L78-79) — **ya existe y ya es genérica** (soporta IVA/ICE/IRBPNR indistintamente), pero es **puramente aditiva**: `ReplaceTaxes()` (L300-305) la reemplaza sin tocar los escalares Ice*, y `ApplyTaxes()`/`RecalcTaxes()` (L251-291, L384-411) siguen escribiendo/recalculando solo los escalares.
- **Excepción:** IRBPNR no tiene campos escalares propios — `IrbpnrCode`/`IrbpnrRate`/`IrbpnrAmount`/`SnapshotIrbpnrName` (L85-91) ya se derivan 100% de `_taxes`. Es decir: **IRBPNR ya sigue el patrón objetivo de este ADR**; ICE es el que hay que migrar a ese mismo patrón.

`PurchaseInvoiceDetailTax` (`ERP.Domain/Modules/Purchases/Entities/PurchaseInvoiceDetailTax.cs`, tabla `purchase_invoice_detail_taxes`): columnas `TaxCode, TaxRateCode, TaxName, Rate, CalculationType, TaxableBase, TaxAmount, Source` (L26-40) — shape ya adecuado como SSOT, no requiere cambios de estructura.

También existe `PurchaseReceptionLineTax` (`ERP.Domain/Modules/Purchases/PurchaseReception/Entities/PurchaseReceptionLineTax.cs`) — snapshot crudo del XML a nivel de **Recepción** (previo a crear la compra). Es infraestructura CLOSED (ADR-028): este ADR no la toca, solo confirma que ya captura todos los `<impuesto>` del XML (código "5" IRBPNR incluido) vía `PurchaseXmlDraftParser` (`ParsedPurchaseXmlLineTax`, L51-57 y L212-234).

### 2.2 Ventas — sin tabla de impuestos de línea, sin soporte de IRBPNR

`SalesInvoiceDetail` (`ERP.Domain/Modules/Sales/Entities/SalesInvoiceDetail.cs`):
- Solo campos escalares `IceCode/IceRate/IceAmount/SnapshotIceName` (L52-55), sin colección equivalente a `PurchaseInvoiceDetailTax`.
- `TaxInclusiveTotal` (L72) **no incluye IRBPNR** (a diferencia de Compras).
- `ApplyTaxes()` (L170-194) no acepta `iceCalculationType`/monto fijo → **Ventas no soporta ICE "Specific"** (tarifas ICE por unidad, no solo porcentaje).
- No existe `SalesInvoiceDetailTax`. Consecuencia: **IRBPNR no puede modelarse en ventas hoy, ni como escalar ni como snapshot genérico.**

### 2.3 Devoluciones y Notas de Crédito — replican escalares, nunca *DetailTax, nunca IRBPNR

- Compras — Devolución: `PurchaseReturnDetail` (campos `IceCode/IceRate`, `ReturnedIceAmount`) y `AuthorizePurchaseReturnUseCases.cs` (L223-227) copian escalares de la línea original.
- Compras — Nota de Crédito: `PurchaseCreditNoteTaxSummary` (comentario propio L9-11: "heredado siempre del resumen fiscal de la factura") y `PurchaseCreditNoteDraftUseCases.cs` (L576-587) copian desde `PurchaseInvoiceTaxSummary` (otro resumen escalar).
- Ventas — Devolución: `SalesReturnDraftUseCases.cs` (L350-370) copia `IceCode/IceRate` posicionalmente a `SalesReturnDetail`.
- Ventas — NC (RIDE/XML): `SalesReturnCreditNoteDataProvider.cs` (L210-217) arma el nodo XML de ICE solo si `line.IceCode` no está vacío, desde escalares.

**Ninguna devolución/NC de ningún módulo propaga IRBPNR.** Es la brecha funcional más concreta detectada: una NC de compra sobre una factura con IRBPNR no lo revierte, ni contable ni fiscalmente.

### 2.4 Recepción electrónica (Compras) — ya lista, no requiere cambios

`PurchaseXmlDraftParser.cs` ya captura todos los `<impuesto>` del XML SRI (VAT/ICE/IRBPNR) en una lista genérica `ParsedPurchaseXmlLineTax` (L212-234), independiente de los campos legacy puntuales (L196-209). El cuello de botella está aguas abajo, no en el parseo.

### 2.5 XML builder / RIDE de venta — el builder es agnóstico, el data provider no

`InvoiceXmlBuilder.cs` (`BuildTotalImpuesto` L295-302, `BuildImpuestoDetalle` L332-340) ya itera una lista abstracta `ElectronicDocumentDetailTax` — no hardcodea ICE ni IVA, aceptaría IRBPNR sin cambios si se lo dieran.

El problema está en `SalesInvoiceElectronicDocumentDataProvider.cs` (`BuildDetailLine`, L206-232): arma la lista manualmente desde los escalares `line.VatCode/VatRate/VatAmount` + `line.IceCode/IceRate/IceAmount` — **nunca agrega IRBPNR** porque `SalesInvoiceDetail` no lo tiene. Este es el punto de integración que cambia cuando exista `SalesInvoiceDetailTax`.

### 2.6 Reportes

No hay una carpeta de reportes fiscales que lea `IceAmount`/`IceRate` directamente salvo:
- `GetItemFullReportQuery.cs` / `GetItemByIdQuery.cs` — resuelven tarifa ICE del catálogo para mostrar en ficha de ítem (no reporte fiscal).
- `GetPurchaseInvoiceTaxSummariesUseCases.cs` (L73-82) — lee `PurchaseInvoiceTaxSummary` (resumen escalar por factura, no `*DetailTax`).
- `GetDailySalesReportQueryHandler.cs` — **no desglosa ICE ni IRBPNR en absoluto** (gap preexistente, no lo introduce este ADR, pero queda evidenciado).

### 2.7 Contabilidad

`PostingFact.cs` ya tiene `TotalVat/TotalIce/TotalIrbpnr` (L29-30, L40) como categorías independientes, y `JournalFactory.cs` (L96) ya mapea `PostingAmountKind.TaxIrbpnr` a su propia cuenta contable — **el contrato de posteo ya distingue IRBPNR de ICE**, no hay nada que rediseñar ahí. Lo que cambia es el origen del dato: hoy los traductores (`PurchaseInvoiceConfirmedPostingTranslator.cs`, etc.) reciben esos totales ya agregados desde el evento de dominio, que a su vez agrega los escalares/derivados de cada línea — no consultan `PurchaseInvoiceDetailTax` directamente. `ConfirmPurchaseUseCases.cs` (L160-183) ya tiene un guard que bloquea el Confirm si hay `IrbpnrAmount > 0` sin regla de posteo configurada — este guard se preserva.

### 2.8 Catálogos SRI — ya existen, no se tocan en este ADR

`SriVatRate`, `SriIceRate` (`Code, Name, Percentage, UnitValue, CalculationType`), `SriIrbpnrRate` (mismo shape, entidad separada "a propósito" según su propio comentario) ya son dinámicos, sin hardcodeo, resueltos vía `ISriTaxResolver`. `SriTaxCategoryCodes` centraliza los códigos de categoría del protocolo SRI (`Vat="2"`, `Ice="3"`, `Irbpnr="5"`). **No se propone consolidar en un catálogo `TaxType` único** — sería una refactorización de infraestructura ya funcional, sin necesidad funcional que la motive dentro del alcance de este ticket (ver §6, Alternativas consideradas).

### 2.9 Configuración tributaria del ítem — sin soporte de IRBPNR

`ItemTaxConfig` (VO embebido en `Item`, `ERP.Domain/Modules/Items/ValueObjects/ItemTaxConfig.cs`): `SaleVatCode, PurchaseVatCode, ExciseTaxCode` — `ExciseTaxCode` se resuelve **únicamente** contra el catálogo ICE (`GetPurchaseItemContextQueryHandler.cs` L104). No existe ningún campo para IRBPNR a nivel de ítem.

---

## 3. Diseño propuesto

### 3.1 Catálogos SRI globales (sin cambios de estructura)

Se reutilizan `SriVatRate`/`SriIceRate`/`SriIrbpnrRate` y `SriTaxCategoryCodes` tal como están. No forman parte del alcance de este ADR.

### 3.2 Configuración tributaria dinámica del producto — revisado tras retroalimentación

**Cambio respecto al borrador inicial:** no se aprueba agregar `IrbpnrCode` como columna fija adicional a `ItemTaxConfig`. Ese patrón ("una columna nueva por cada impuesto especial que aparezca") es justo el problema que ya tiene `SriVatRate`/`SriIceRate`/`SriIrbpnrRate` como tres entidades paralelas (§2.8) — replicarlo en `ItemTaxConfig` extiende la deuda en vez de contenerla, y no escala si el SRI agrega un tercer impuesto especial en el futuro.

`ItemTaxConfig` (VO) se mantiene **sin cambios** para IVA — `SaleVatCode`/`PurchaseVatCode` ya están estables, no hay motivo funcional para tocarlos:

```
ItemTaxConfig { SaleVatCode, PurchaseVatCode }   // sin cambios
```

Se crea una colección nueva **`ItemSpecialTaxConfiguration`** (entidad 1:N de `Item`, no VO embebido) para impuestos especiales — ICE, IRBPNR y cualquier impuesto especial futuro que el SRI defina, sin volver a tocar el esquema de `Item`:

```
ItemSpecialTaxConfiguration {
  Id, TenantId, ItemId,
  SriTaxCategoryCode,   // "3" ICE, "5" IRBPNR — mismo catálogo que SriTaxCategoryCodes
  TaxCatalogCode,       // código de tarifa dentro del catálogo (SriIceRate.Code / SriIrbpnrRate.Code)
  IsActive
}
```

- Índice único `(ItemId, SriTaxCategoryCode)` — a lo sumo una configuración activa por impuesto especial por ítem (mismo principio de unicidad que `PricingRule` en ADR-021).
- Sin fila para un `SriTaxCategoryCode` dado = el ítem no está gravado con ese impuesto (comportamiento por defecto; alineado al criterio de aceptación "línea sin ICE/IRBPNR no tiene ICE/IRBPNR falso por default"). Reemplaza tanto al `ExciseTaxCode` actual (que se migra a una fila `SriTaxCategoryCode="3"`) como al `IrbpnrCode` descartado.
- Migración: `ExciseTaxCode` existente en `Item` se migra a filas `ItemSpecialTaxConfiguration` (backfill, no pérdida de dato); la columna `ExciseTaxCode` sigue existiendo como *legacy compatibility mirror* bajo el mismo régimen que los campos `Ice*` de línea (§3.3) — no se elimina en esta fase.
- `GetPurchaseItemContextQueryHandler.cs` (hoy resuelve `ExciseTaxCode` solo contra ICE) y el flujo de Ventas (§5) pasan a resolver impuestos especiales consultando `ItemSpecialTaxConfiguration` por `SriTaxCategoryCode`, no un campo fijo por nombre. En Ventas esta configuración se combina con `CompanySpecialTaxResponsibility` (§3.4) — ambas son necesarias para que el impuesto se aplique.

### 3.3 Snapshot de impuestos por línea — `*DetailTax` como SSOT real

**Compras:** `PurchaseInvoiceDetailTax` ya existe con el shape correcto y pasa a ser la **única fuente de verdad** ("SSOT" en el sentido estricto del ticket — la única tabla desde la que se decide, calcula o reporta):
- `ApplyTaxes()`/`RecalcTaxes()` en `PurchaseInvoiceDetail` pasan a escribir siempre a través de `ReplaceTaxes()` (la colección). Los escalares `IceCode/IceRate/IceAmount/SnapshotIceName/IceCalculationType` dejan de ser fuente de escritura: pasan a ser un **legacy compatibility mirror** — propiedades de solo lectura, recalculadas/sincronizadas automáticamente cada vez que cambia `_taxes`, y **nunca** deben usarse como entrada de ninguna decisión, cálculo o validación nueva (mismo patrón que ya usa `IrbpnrCode`/`IrbpnrAmount` hoy — L85-91, que ya no se toca).
- `TaxInclusiveTotal` sigue leyendo de `_taxes` (sin cambio de fórmula, cambia de dónde vienen los valores fuente — nunca cambió, de hecho: ya se calculaba a partir de la colección para IRBPNR).

**Ventas:** se crea `SalesInvoiceDetailTax`, entidad espejo de `PurchaseInvoiceDetailTax` (mismas columnas: `TaxCode, TaxRateCode, TaxName, Rate, CalculationType, TaxableBase, TaxAmount, Source`), tabla `sales_invoice_detail_taxes`, FK 1:N hacia `SalesInvoiceDetail`. Es la fuente de verdad desde el día uno — no pasa por una fase "aditiva" intermedia como Compras, porque no hay escalares previos que migrar salvo `Ice*` (ver abajo).
- `SalesInvoiceDetail.ApplyTaxes()` se extiende para aceptar `iceCalculationType` y monto fijo (paridad con Compras — cierra el gap de ICE "Specific" en Ventas) y para recibir impuestos IRBPNR opcionales.
- `IceCode/IceRate/IceAmount/SnapshotIceName` pasan a ser legacy compatibility mirror de solo lectura de `_taxes`, igual que en Compras — no existen equivalentes `Irbpnr*` escalares nuevos en `SalesInvoiceDetail`; IRBPNR en Ventas vive únicamente en `SalesInvoiceDetailTax`, sin mirror legacy (no hay campo previo que espejar).
- `TaxInclusiveTotal` se actualiza para incluir el monto de IRBPNR agregado desde `_taxes` (paridad con Compras).

**Devoluciones y Notas de Crédito (ambos módulos):** en vez de copiar campos escalares punto a punto, copian las filas `*DetailTax` de la línea/documento original (nueva colección `PurchaseReturnDetailTax`/`SalesReturnDetailTax` y equivalente en el resumen fiscal de NC). Esto es lo que permite que IRBPNR se propague correctamente a devoluciones y NC por primera vez, sin lógica ad-hoc adicional por tipo de impuesto — un solo mecanismo de copia sirve para IVA, ICE e IRBPNR.

**Campos legacy `Ice*` en `PurchaseInvoiceDetail`/`SalesInvoiceDetail` y `ExciseTaxCode` en `Item`:** durante todas las fases de este ADR son **legacy compatibility mirror**, no SSOT ni fuente — de solo lectura, sincronizados automáticamente desde `*DetailTax`/`ItemSpecialTaxConfiguration`, nunca escritos directamente ni usados como entrada de una decisión nueva. Nunca se eliminan columnas de BD en este ADR (ver Fase 6/7 en §7). Todo código existente que lee `line.IceAmount` sigue funcionando sin cambios; lo que cambia es que ya no se puede escribir directamente a esos campos por fuera de `_taxes`, y ningún handler nuevo debe leerlos para tomar una decisión.

### 3.4 Responsabilidad tributaria por empresa (ICE/IRBPNR en ventas)

**Regla de negocio confirmada:** que una compra traiga ICE/IRBPNR (el proveedor lo facturó) **no obliga automáticamente** a que la venta de ese mismo ítem aplique ICE/IRBPNR — es una realidad fiscal real del SRI: no toda empresa que compra un producto gravado con un impuesto especial es sujeto pasivo de ese impuesto en su propia venta (depende de si es fabricante/importador vs. un simple revendedor). El ERP debe soportar ambos escenarios mediante configuración, no asumir ninguno de los dos por default.

Se agrega **`CompanySpecialTaxResponsibility`** (entidad 1:N de `Company`, mismo shape que `ItemSpecialTaxConfiguration` — evita repetir el antipatrón "columna por impuesto" señalado en §2.8/§3.2 también a nivel de empresa):

```
CompanySpecialTaxResponsibility {
  Id, TenantId, CompanyId,
  SriTaxCategoryCode,   // "3" ICE, "5" IRBPNR — mismo catálogo que SriTaxCategoryCodes
  IsResponsibleOnSales, // true = la empresa debe aplicar este impuesto al vender
  IsActive
}
```

- Índice único `(CompanyId, SriTaxCategoryCode)`.
- Sin fila para un `SriTaxCategoryCode` dado, o `IsResponsibleOnSales = false` = la empresa **no** aplica ese impuesto en ventas (comportamiento por defecto — coherente con "línea sin ICE/IRBPNR no tiene ICE/IRBPNR falso por default", §8).
- Vive junto a la configuración fiscal existente de `Company` (`UpdateFiscalSettings`, pestaña "Fiscal" de Configuración → Empresa) como una sección nueva, no reemplaza `SpecialTaxpayerNo` ni ningún campo fiscal ya existente.

**Regla de cálculo en Ventas (reemplaza el paso 1 de §5):** la venta calcula ICE/IRBPNR **solo cuando se cumplen ambas condiciones**:
1. La empresa tiene `CompanySpecialTaxResponsibility.IsResponsibleOnSales = true` para ese `SriTaxCategoryCode`.
2. El ítem tiene una fila activa en `ItemSpecialTaxConfiguration` para ese mismo `SriTaxCategoryCode`.

Si falta cualquiera de las dos, la línea de venta no lleva ese impuesto — no es un error, es el comportamiento esperado. Si la compra trajo ICE/IRBPNR pero la empresa no es responsable de aplicarlo en ventas, el impuesto **se conserva únicamente como snapshot de compra** en `PurchaseInvoiceDetailTax` (§3.3) — nunca se traslada ni se infiere hacia el lado de Ventas.

### 3.5 Consumidores que cambian de fuente

- `SalesInvoiceElectronicDocumentDataProvider.BuildDetailLine` y `SalesReturnCreditNoteDataProvider`: leen de `SalesInvoiceDetailTax`/`SalesReturnDetailTax` en vez de armar el nodo manualmente desde escalares — agregan IRBPNR automáticamente cuando existe.
- Traductores de posteo contable (`PurchaseInvoiceConfirmedPostingTranslator`, `PurchaseCreditNoteAuthorizedPostingTranslator`, `PurchaseReturnAuthorizedPostingTranslator`, y sus equivalentes nuevos en Ventas si aplica): siguen recibiendo `PostingFact.TotalVat/TotalIce/TotalIrbpnr` sin cambio de contrato — solo cambia que esos totales, al agregarse desde las líneas, ahora sumarán consistentemente sobre `_taxes` en ambos módulos (hoy Compras ya lo hace vía los derivados; Ventas empieza a hacerlo).
- `PurchaseCreditNoteAuthorizedPostingTranslator` gana soporte de IRBPNR (hoy no lo maneja — gap detectado en §2.3/§2.7).

---

## 4. Flujo de Compras

1. XML trae impuestos de línea (ya soportado, ADR-028) → `PurchaseReceptionLineTax` → al crear la compra, se copian a `PurchaseInvoiceDetailTax` (mecanismo ya existente, sin cambios).
2. Si el producto no tiene el impuesto configurado (sin fila `ItemSpecialTaxConfiguration` para ese `SriTaxCategoryCode`) pero el XML trae ICE/IRBPNR para esa línea: se registra igual en `PurchaseInvoiceDetailTax` (el XML es evidencia fiscal, no se descarta — regla ya vigente para XML como fuente de verdad, ADR-028) y se marca la línea/documento con un estado de revisión o alerta (reutilizar el mecanismo de discrepancias ya usado en Recepción XML, sin inventar uno nuevo).
3. **No se crea/actualiza `ItemSpecialTaxConfiguration` automáticamente** por una discrepancia detectada en el XML — requiere confirmación explícita del usuario (edición manual del ítem). Esto evita que un dato de un solo proveedor/factura contamine la configuración maestra del producto.

## 5. Flujo de Ventas

1. Al armar la línea de venta, se resuelve ICE/IRBPNR consultando **ambas** configuraciones (§3.4): `CompanySpecialTaxResponsibility.IsResponsibleOnSales` de la empresa actual y `ItemSpecialTaxConfiguration` del producto, ambas por el mismo `SriTaxCategoryCode`. Solo si las dos son verdaderas se calcula el impuesto — falta una sola condición y la línea no lo lleva. **Nunca** se copia desde la última compra registrada del ítem — la venta no lee `PurchaseInvoiceDetailTax` ni ningún dato de la última factura de proveedor.
2. El cálculo (`RecalcTaxes()`) escribe el snapshot en `SalesInvoiceDetailTax` — no un puntero al catálogo ni al ítem.
3. Facturas ya confirmadas no cambian si después se actualiza la tarifa ICE/IRBPNR en el catálogo o en el ítem — el snapshot es inmutable una vez el documento pasa a estado no editable (mismo principio de inmutabilidad post-confirmación que ya rige para IVA).

### 5.1 Regla de coherencia compra-venta de impuestos especiales

Compras y Ventas comparten el mismo producto y el mismo `ItemSpecialTaxConfiguration`, pero son **procesos independientes** — ninguno lee el snapshot del otro. La coherencia entre ambos no se logra copiando datos entre módulos, sino porque los dos resuelven contra la misma configuración vigente del ítem:

1. **Compra electrónica:** cuando el XML del proveedor trae ICE/IRBPNR para una línea (§2.4), ese impuesto recibido se guarda tal cual como snapshot de compra en `PurchaseInvoiceDetailTax` — es evidencia fiscal, no se descarta ni se reinterpreta (regla ya vigente, ADR-028).
2. **Discrepancia:** si el ítem de esa línea no tiene configurado ese impuesto especial (sin fila `ItemSpecialTaxConfiguration` para ese `SriTaxCategoryCode`), el sistema genera una alerta/estado de revisión (§4.2) — el dato de compra queda registrado igual, pero señalizado como inconsistente con la configuración maestra del producto.
3. **Sin autoactualización:** el sistema **nunca** actualiza `ItemSpecialTaxConfiguration` automáticamente a partir de esa discrepancia (§4.3) — el ítem solo queda gravado con ICE/IRBPNR cuando alguien lo confirma explícitamente editando el producto.
4. **Venta:** no copia impuestos desde la última compra del ítem bajo ninguna circunstancia — siempre calcula ICE/IRBPNR desde `ItemSpecialTaxConfiguration` del producto **y** `CompanySpecialTaxResponsibility` de la empresa (§3.4), ambas vigentes al momento de la venta, y guarda su propio snapshot independiente en `SalesInvoiceDetailTax`.
5. **Compra con ICE/IRBPNR no obliga a la venta:** que la compra haya traído ICE/IRBPNR del proveedor (o que el ítem esté configurado con ese impuesto) **no implica** que la venta lo aplique — eso depende exclusivamente de si la empresa está configurada como responsable de aplicarlo en ventas (§3.4). Si la empresa no lo es, el impuesto queda únicamente como snapshot de compra en `PurchaseInvoiceDetailTax`, sin trasladarse a ninguna venta.
6. **Efecto esperado:** si un producto está configurado como gravado con ICE/IRBPNR (fila activa en `ItemSpecialTaxConfiguration`) **y** la empresa es responsable de aplicarlo en ventas (`CompanySpecialTaxResponsibility.IsResponsibleOnSales = true`), tanto Compras como Ventas aplican ese impuesto de forma consistente porque ambos consultan la misma configuración del producto — no porque uno le copie el dato al otro. Si falta la responsabilidad de la empresa, o si la compra detectó el impuesto vía XML pero el ítem no fue confirmado con esa configuración (punto 2), la venta de ese producto **no** aplicará el impuesto — es el comportamiento correcto en ambos casos, no un bug: evita que un dato no confirmado o una responsabilidad fiscal no aplicable a esta empresa determinen el comportamiento de las ventas.

---

## 6. Alternativas consideradas

- **Consolidar `SriVatRate`/`SriIceRate`/`SriIrbpnrRate` en un catálogo `TaxType` único** — descartado para este ADR: es una refactorización de infraestructura ya funcional y correcta, sin ninguna necesidad funcional del ticket que la motive; agregarla ahora sería alcance no solicitado sobre infraestructura que ya cumple "SSOT dinámico" (regla global del proyecto). Puede evaluarse en un ADR separado si surge una necesidad real (p.ej. un quinto impuesto SRI).
- **Agregar `IrbpnrCode` como columna fija a `ItemTaxConfig`** (propuesta del borrador inicial) — descartado tras revisión: reproduce en `Item` el mismo patrón "una columna nueva por impuesto especial" que ya genera deuda en los catálogos SRI (§2.8); no escala a un tercer impuesto especial futuro. Reemplazado por `ItemSpecialTaxConfiguration` (§3.2), colección 1:N key-valor por `SriTaxCategoryCode`.
- **Eliminar los campos escalares `Ice*` en esta fase** — descartado explícitamente por la regla del ticket ("no eliminar columnas legacy en la primera fase si hay riesgo") y por la regla global del proyecto de no romper funcionalidad existente sin migración de compatibilidad. Se difiere a un ticket posterior tras el período de convivencia (Fase 7 en §7).

---

## 7. Fases de implementación

> Orden vinculante confirmado en esta revisión — no reordenar sin volver a aprobar. La auditoría de consumidores (§2 de este ADR) se considera completada y no es una fase de implementación en sí misma.

1. **Tests de comportamiento actual (baseline)** — capturar el comportamiento vigente de `ApplyTaxes`/`RecalcTaxes`/totales en Compras y Ventas (incluyendo casos ICE Specific en Compras, ausencia de IRBPNR en Ventas) **antes de escribir ningún código de producto**. Estos tests son el criterio de "totales no cambian" de §8.
2. **Crear `SalesInvoiceDetailTax`** — entidad, tabla, migración EF, y extensión de `SalesInvoiceDetail.ApplyTaxes()`/`RecalcTaxes()` para aceptar `iceCalculationType`/monto fijo e IRBPNR (§3.3). En esta fase la tabla existe y se puede escribir, pero **todavía no es la única fuente** — convive con los escalares sin romperlos.
3. **Escritura SSOT en `*DetailTax`** — invertir la autoridad: `PurchaseInvoiceDetail`/`SalesInvoiceDetail` pasan a escribir siempre a través de `ReplaceTaxes()`, los escalares `Ice*` pasan a ser legacy compatibility mirror de solo lectura (§3.3). Se crean `ItemSpecialTaxConfiguration` (§3.2) y `CompanySpecialTaxResponsibility` (§3.4), y se migra la resolución de ICE/IRBPNR en Compras/Ventas a consultar ambas.
4. **Backfill idempotente** desde los campos legacy `Ice*`/`ExciseTaxCode` hacia `*DetailTax`/`ItemSpecialTaxConfiguration` para documentos e ítems históricos que aún no tengan filas en la colección nueva (Ventas no tiene ninguna hoy — backfill completo; Compras solo los casos donde `_taxes` esté vacío o desincronizado del escalar). Idempotente: correrlo dos veces no debe duplicar filas ni alterar totales.
5. **Migrar consumidores** — data providers de XML/RIDE de venta, devoluciones, notas de crédito (ambos módulos), traductores de posteo, a leer de `*DetailTax`/mirror en vez de escalares crudos (§3.4).
6. **Marcar campos legacy `Ice*`/`ExciseTaxCode` como no fuente de verdad** — comentario de dominio explícito confirmando su estado de legacy compatibility mirror (mismo patrón que ya usa el comentario de `PurchaseInvoiceDetailTax` L13-18), sin cambios de esquema.
7. **Ticket posterior (fuera de este ADR)**: decidir eliminación física de columnas legacy tras período de convivencia y confirmación de que ningún consumidor externo (reportes, integraciones) depende de ellas directamente. No se ejecuta en la primera fase bajo ninguna circunstancia.

---

## 8. Criterios de aceptación

- Línea sin ICE no tiene ICE falso por default (campo derivado nulo/vacío, no `0` disfrazado de "sin impuesto").
- Línea sin IRBPNR no tiene IRBPNR falso por default.
- ICE e IRBPNR existen solo cuando aplican — ninguna fila `*DetailTax` se crea especulativamente.
- Totales de documento (`TaxInclusiveTotal`, totales de factura) no cambian de valor para ningún documento existente tras el backfill (Fase 4 verificada contra Fase 2).
- XML/RIDE/reportes conservan los valores que producían antes del cambio para documentos ya emitidos.
- Recepción electrónica de Compras (ADR-028) sigue funcionando sin modificación de su contrato.
- Notas de crédito (Compras y Ventas) respetan y propagan los impuestos originales de la línea, IRBPNR incluido.
- Compra y Venta guardan snapshot histórico inmutable de impuestos por línea.
- El producto conserva su configuración tributaria base (`ItemTaxConfig` para IVA, `ItemSpecialTaxConfiguration` para ICE/IRBPNR), independiente del snapshot de cada documento.
- No hay doble fuente de verdad: toda escritura de impuestos de línea pasa por `*DetailTax`; los escalares `Ice*` (y `ExciseTaxCode` en `Item`) son siempre legacy compatibility mirror de solo lectura, nunca fuente de una decisión, cálculo o validación nueva.
- Ventas nunca copia impuestos especiales desde la última compra del ítem — siempre resuelve contra `ItemSpecialTaxConfiguration` vigente (§5.1).
- Una discrepancia XML-vs-configuración detectada en Compras (§4.2) no modifica `ItemSpecialTaxConfiguration` automáticamente ni afecta el cálculo de Ventas hasta que se confirme explícitamente (§5.1).
- Una compra con ICE/IRBPNR no obliga a que la venta del mismo ítem aplique ese impuesto — la venta lo calcula solo si la empresa tiene `CompanySpecialTaxResponsibility.IsResponsibleOnSales = true` **y** el ítem tiene `ItemSpecialTaxConfiguration` activa para ese impuesto (§3.4); si la empresa no es responsable, el impuesto de la compra queda solo en `PurchaseInvoiceDetailTax`.

## 9. Consecuencias

- Ventas gana capacidad de modelar IRBPNR e ICE "Specific" por primera vez — habilita facturación correcta para ítems gravados con esos impuestos que hoy no se pueden vender con el impuesto reflejado en el XML.
- Devoluciones y notas de crédito de ambos módulos ganan un mecanismo único de propagación de impuestos (copia de `*DetailTax`), cerrando el gap de IRBPNR no reversado.
- Compras invierte la autoridad de ICE (de escalar a derivado) sin cambio de contrato público — cualquier consumidor externo que lea `line.IceAmount` sigue funcionando igual.
- Contabilidad no cambia de contrato (`PostingFact`/`PostingAmountKind` ya soportan `TaxIrbpnr`); gana consistencia en el origen del dato agregado y cierra el gap de IRBPNR en NC de compra.
- **Pendiente fuera de este ADR:** `GetDailySalesReportQueryHandler` no desglosa ICE/IRBPNR — no se corrige aquí por no ser parte del alcance del ticket (no se auditó como "consumidor a migrar", es una funcionalidad de reporte inexistente, no un consumidor de un campo existente). Requiere ticket propio si se decide agregar ese desglose.
- **Pendiente fuera de este ADR:** consolidación de `SriVatRate`/`SriIceRate`/`SriIrbpnrRate` en un catálogo único (ver §6) — deliberadamente no se toca.
- **`ELECTRONIC-DOCUMENTS-IRBPNR-CATEGORY-01` — RESUELTO (2026-08-29)**: `SriTaxCategoryCodeResolver` (`ERP.Infrastructure/Services/ElectronicDocuments/`) ahora reconoce `IRBPNR→"5"` además de `VAT→"2"`/`ICE→"3"`. `InvoiceXmlBuilder` no requirió cambios (ya era agnóstico). Una factura de venta con IRBPNR ya genera su nodo `<impuesto>` correctamente en el XML electrónico. Ver `STATUS.md` § "ELECTRONIC-DOCUMENTS-IRBPNR-CATEGORY-01" para el detalle.

## 10. Fuera de alcance — no se toca en ningún ticket derivado de este ADR

Confirmado explícitamente en la revisión de aprobación. Ninguna fase de §7 puede tocar:

- **RIDE** (ADR-025, Design Frozen) — el builder de bajo nivel ya es agnóstico (§2.5); este ADR solo cambia quién le entrega los datos, nunca su diseño ni su render.
- **ElectronicDocuments v1.0** (ADR-023, Frozen) — contrato y builder XML (`InvoiceXmlBuilder`) sin cambios.
- **Recepción XML de Compras → Compra** (ADR-028, Frozen) — parseo (`PurchaseXmlDraftParser`) y `PurchaseReceptionLineTax` sin cambios; ya capturan lo necesario (§2.4).
- **SaaS / Platform** — fuera de la frontera ERP↔Platform (`ERP_CORE_FREEZE.md`); este ADR es exclusivamente ERP Core.
- **Permisos** — ningún endpoint, rol ni policy nuevo o modificado.
- **Menú** — ninguna entrada de navegación nueva o modificada.
- **Catálogos SRI globales** (`SriVatRate`/`SriIceRate`/`SriIrbpnrRate`, `SriTaxCategoryCodes`) — se reutilizan tal como están (§3.1, §6).

## Addendum 2026-08-29b — Corrección post-revisión de Subfase 5D-2 (PurchaseCreditNoteTaxSummary)

La primera implementación de 5D-2 agregó `IrbpnrCode`/`IrbpnrRate`/`IrbpnrName`/`IrbpnrAmount` como columnas fijas nuevas en `PurchaseCreditNoteTaxSummary` — exactamente el antipatrón "columna por impuesto" que este ADR ya señala en los catálogos SRI (§2.8) y descarta explícitamente para `ItemTaxConfig` (§3.2/§6). Detectado en revisión antes de continuar a 5D-3.

**Corrección aplicada:** `PurchaseCreditNoteTaxSummary` se rediseñó para seguir el mismo patrón que el resto del dominio — colección `Taxes` de `PurchaseCreditNoteTaxSummaryLine` (`TaxCode, TaxRateCode, TaxName, Rate, CalculationType, TaxAmount`, espejo de `PurchaseInvoiceDetailTax`/`PurchaseReturnDetailTax`) como única fuente de verdad. Se corrigió **IVA e ICE también**, no solo IRBPNR, para no dejar un estado híbrido (parte columnas fijas, parte filas) — la inconsistencia interna habría sido peor que el problema original.

Los antiguos `VatCode/VatRate/VatName/VatAmount/IceCode/IceRate/IceName/IceAmount/IrbpnrCode/IrbpnrRate/IrbpnrName/IrbpnrAmount` se preservan como **propiedades derivadas de solo lectura** (computadas desde `Taxes`, nunca columnas persistidas) — exclusivamente para no romper `CreditNoteMap.ToDto` y el resto de consumidores existentes, que siguen leyendo esos nombres sin cambios. Ningún consumidor nuevo debe agregar más propiedades derivadas por impuesto: el punto de acceso correcto para código nuevo es `Taxes`.

Como la tabla no tenía filas reales en ningún ambiente (`purchase_credit_note_tax_summaries` en 0 en desarrollo), se revirtió la migración `AddPurchaseCreditNoteIrbpnr` de la primera implementación y se generó una única migración limpia (`AddPurchaseCreditNoteTaxSummaryLines`) en vez de dejar un rastro de columnas agregadas-y-eliminadas en el historial de migraciones.

## Addendum 2026-08-29 — Resolución de conflicto con infraestructura FROZEN "Configuración Tributaria"

`CompanySpecialTaxResponsibility` (§3.4) choca textualmente con la prohibición "Crear... cualquier configuración tributaria a nivel empresa" de la infraestructura FROZEN "Configuración Tributaria" (`docs/architecture/frozen-infrastructure.md`, congelada 2026-07-01). Resuelto explícitamente: **este ADR es la ADR formal** que abre una excepción acotada a esa prohibición, documentada en `frozen-infrastructure.md` § "Excepción acotada — `CompanySpecialTaxResponsibility`". La regla FROZEN sigue vigente para todo lo demás (códigos, tarifas, catálogos a nivel empresa siguen prohibidos); la excepción cubre únicamente el booleano de responsabilidad de aplicación en ventas de un impuesto especial, nunca un dato tributario en sí (código/tarifa/catálogo), y nunca participa en Compras.

## Addendum 2026-08-29c — Fase 3 (ICE) nunca se completó; cerrada junto con Fase 6

Al ejecutar Fase 6 (§7.6, "marcar campos legacy... comentario de dominio explícito") se descubrió que **Fase 3 nunca se había completado para ICE** en las 4 entidades de línea (`PurchaseInvoiceDetail`/`SalesInvoiceDetail`/`PurchaseReturnDetail`/`SalesReturnDetail`) — solo IRBPNR y `PurchaseCreditNoteTaxSummary` (Addendum b) habían recibido el tratamiento de "propiedad computada desde `_taxes`, nunca escalar escrito directamente" que §3.3 exige. `Ice*` seguía siendo `private set`, escrito por `ApplyTaxes()`/`RecalcTaxes()`/`Create()`/`Freeze()`, y `TaxInclusiveTotal`/`LineGrandTotal` leían el escalar directamente — doble fuente de verdad real, no solo nominal.

**Decisión (confirmada explícitamente, alcance ajustado)**: no ejecutar Fase 6 como si Fase 3 ya estuviera cerrada. Se completó primero Fase 3 (ICE) — mismo patrón exacto que IRBPNR — y luego Fase 6 sobre el resultado real:

- Las 4 entidades convierten `IceCode/IceRate/IceAmount/IceCalculationType/SnapshotIceName`/`ReturnedIceAmount` a getters de solo lectura sobre `_taxes`/`Taxes`. `PurchaseReturnDetail.Freeze()` (`internal`) pierde 3 parámetros redundantes con `returnedTaxes`.
- EF: `Ice*` pasa a `builder.Ignore(...)` en las 4 configuraciones — **nunca `DROP COLUMN`** (EF lo scaffoldea por defecto al ignorar una propiedad antes mapeada; se corrigió a mano). Único DDL real: `ALTER COLUMN ... SET DEFAULT` en columnas `NOT NULL` que dependían de que EF las escribiera siempre — sin eso, un `INSERT` nuevo (que ya no las incluye) violaba la restricción. Las columnas físicas permanecen, huérfanas.
- `ExciseTaxCode` (`Item.TaxConfig`) migrado en los 6 consumidores restantes que aún lo leían directo (`PurchaseDraftUseCases` ×2, `GetSalesItemPricingQueryHandler`, `ItemMappingService`/`GetItemFullReportQuery`/`GetItemByIdQuery`, `InvoiceItemSearchRepository`) — todos ahora resuelven contra `ItemSpecialTaxConfiguration` (y, en Ventas, también `CompanySpecialTaxResponsibility` por §3.4/§5.1).
- Auditoría confirmó que los data providers de XML/RIDE de venta y `GetPurchaseItemContextQueryHandler` (hallazgos originales §2.3/§2.5/§2.9) **ya estaban migrados** por subfases anteriores (5C/5D-4/5A) — esos hallazgos del ADR quedan obsoletos, documentado aquí para no reabrirlos por error.
- Fase 7 (eliminación física) sigue sin iniciar — no se tocó en este addendum, requiere confirmación explícita futura.

Ver `STATUS.md` § "Fase 3 (ICE) completada + Fase 6" para el detalle de archivos, migraciones y validaciones.

## Referencias

- Auditoría técnica previa a esta decisión: sesión 2026-08-29 (agente de exploración sobre `backend/src`).
- ADR-021 (patrón SSOT de referencia), ADR-023/025/028 (infraestructura CLOSED que este ADR no reabre).
