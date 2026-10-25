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

### Desglose documental XML

- Una línea fiscal puede originar dos o más `PurchaseInvoiceDetail` operativos. Todos conservan `PurchaseReceptionLineId`; `XmlAllocatedSubtotal` identifica explícitamente los componentes. La promoción permanece exclusivamente como origen fiscal en recepción: no se agrega como otra línea operativa ni genera efectos duplicados. La recepción, su XML y sus impuestos no se modifican.
- Los comandos existentes de alta/actualización aceptan `XmlAllocation` por componente: `Subtotal` bruto antes del descuento, `Discount` exacto y `Taxes` con `PurchaseReceptionLineTaxId`, `TaxableBase` y `TaxAmount`. Cantidad, presentación y precio unitario son operativos; el subtotal documental asignado es explícito, no se infiere de cantidad × precio. Se devuelve como `XmlAllocatedSubtotal`, junto con los identificadores de impuestos de origen (`PurchaseReceptionLineTaxId`, también en la evidencia XML del borrador), al consultar la compra; la consulta por id incluye la evidencia XML en cualquier estado.
- Cada impuesto asignado tiene `Source=XmlAllocation`: el servidor obtiene código, tarifa y clasificación del origen/catálogo; el cliente solo asigna bases e importes. No calcula bases físicas, azúcar, volumen ni tributos desde cantidades comerciales. No se aprende una equivalencia proveedor→producto por el desglose, ni se interpreta la promoción como una presentación de sus componentes.
- Guardar y confirmar exigen igualdad exacta del grupo en subtotal bruto, descuento, base neta y total; además, por cada impuesto documental y código tributario, suma de bases e importes asignados igual al original, conservando tarifa y procedencia. Se rechazan grupos incompletos, impuestos ajenos/duplicados, mezcla de promoción y componentes, y cruces de tenant/empresa/sucursal/proveedor/comprobante. Las cantidades operativas no se comparan con la cantidad comercial de la promoción.
- Los componentes se guardan con la misma operación atómica del borrador y se reabren con sus asignaciones. Recalcular, distribuir costos o confirmar no recalcula sus impuestos; modificar el descuento global de un componente se bloquea y requiere reasignar explícitamente el grupo. Las líneas sin desglose conservan sus reglas actuales.
- La confirmación reutiliza los motores existentes: entrada por detalle Product, Kardex/promedio ponderado, una CxP y el hecho contable de la compra. Costo inventariable = base neta asignada + flete + otros costos; IVA, ICE e IRBPNR permanecen separados. Las devoluciones continúan referenciando el detalle operativo y prorrateando su snapshot asignado; no hay conciliación automática de NC promocionales.
- Precisión de las columnas nuevas: `xml_allocated_subtotal` es monto fiscal, `FiscalPrecision.TaxAmount`, `numeric(18,2)`; representa el subtotal documental asignado sin derivarlo de un precio redondeado. El descuento y las bases/importes tributarios reutilizan las columnas existentes y su precisión fiscal. Los identificadores de origen tributario tienen FK restrictiva e índice único por detalle + impuesto original. El modelo y la instalación se consolidan en la única baseline V1; no se aplican cambios a bases existentes en esta entrega.
- Interfaz (mismo formulario de Compras, sin pantallas ni tablas paralelas): la acción «Desglosar» aparece solo en líneas XML cuyos impuestos traen su identificador de origen. El modal se titula «Desglosar productos · código XML» con un subtítulo breve, resume en una línea la descripción original, subtotal bruto, descuento, impuestos y total, y ubica el buscador de productos (resultados flotantes sobre el contenido, con SKU, nombre completo y presentación) sobre una tabla compacta: una fila por producto con presentación (o unidad base), cantidad, costo asignado (subtotal bruto) y acceso a sus impuestos. Descuento, bases e importes de cada impuesto original (IVA, ICE, IRBPNR) y flete/otros costos se asignan explícitamente en el detalle de la fila, abierto bajo demanda. Una única barra inferior muestra distribuido y pendiente/excedente, la conciliación por concepto (bajo demanda) y Cancelar/Aplicar; el motivo de bloqueo de «Aplicar» se consulta con el disclosure textual «Ver motivo», accesible por teclado y toque, sin depender de `title`. «Aplicar» exige al menos dos componentes, diferencia cero por concepto y por impuesto, y conservar flete y otros costos asignados del grupo.
- Al aplicar, los componentes reemplazan la línea promocional en su misma posición como filas normales; sus totales e impuestos salen de la asignación, nunca de cantidad × precio. Producto, cantidad, precio, presentación, descuento, duplicar y eliminar de cada fila se bloquean; se editan solo con «Editar desglose» sobre el grupo completo. La bodega es operativa y sigue editable por fila. El formulario rechaza guardar un grupo descuadrado; borrador, recálculo, reapertura y confirmación usan el mismo payload.
- Historial: el modal puede consultar, página a página, compras confirmadas del mismo proveedor con componentes del mismo código XML. Solo propone productos, presentación y cantidades; todos los importes fiscales se vuelven a asignar y no se crea ninguna equivalencia ni aprendizaje.

## Incidencias de linea

Compras utiliza [ZH-DS-LINE-ISSUES-01](./frontend.md#incidencias-flotantes-de-linea-zh-ds-line-issues-01)
mediante `ZHLineIssuesTag`. `PurchaseLineIssuesTag` es solo el adaptador de traducciones;
la construccion de incidencias, prioridad y callbacks correctivos permanece en Compras.
El DS es la fuente unica de presentacion, apertura, accesibilidad, posicionamiento y cierre.

### Rentabilidad de línea

- Fórmula única `lib/margin.ts`: margen sobre PRECIO `(PVP − costo) / PVP` (misma que
  `ItemProfitabilityUseCases.CalcMargin` y el guard de confirmación); el recargo sobre costo
  (`calcMarkupPercent`) no se usa en Compras.
- Bases homogéneas: costo = costo unitario a registrar en unidad base (neto de descuento, con
  flete/gastos asignados, sin IVA ni ICE, como `LandedUnitCost`); PVP = `Item.BaseSalePrice` vía
  `PricingResolver`, sin IVA y por unidad base. Nunca el precio por presentación.
- `marginState` del VM (`purchaseLinePresentation.ts`): `positive`/`zero`/`negative` a la escala
  visible del porcentaje; `noSalePrice` y `unavailable` muestran «Sin PVP»/«—», nunca un 0 % ficticio.
- Avisos (slot comercial `readiness.warning`, mutuamente excluyentes, no bloqueantes, solo productos
  inventariables): sin precio de venta, margen negativo (−50 % ≤ m < 0) y margen cero. Bajo −50 % rige
  el bloqueo existente de presentación sospechosa, sin aviso adicional. No existe aviso de margen bajo:
  no hay umbral empresarial configurable.
- «Cantidad o costo distintos al documento del proveedor» compara en unidad base contra la
  presentación de origen y contra `xmlLineSubtotal` (`precioTotalSinImpuesto`, ya neto de descuento);
  una conversión válida (30 SIXPACK X6 = 180 unidades) no lo dispara.

### Validaciones y errores UX P0

- Los errores existentes de Zod/RHF y API `lines[n].*` se presentan una vez junto al campo o dato
  afectado. Cantidad, costo, bodega y presentación se asocian mediante `aria-invalid` y
  `aria-describedby`; impuestos y producto mantienen su contexto. Los demás errores, incluido
  descuento, permanecen identificados en el detalle. La etiqueta global resume los campos por
  corregir aun con el detalle colapsado, sin copiar sus mensajes; «Ir al campo» abre el detalle
  y dirige el foco. Corregir un campo revalida ese campo, sin borrar errores API de otros campos.
- Los motivos antes exclusivos de `title` en Confirmar, Distribuir y Aplicar desglose se consultan
  mediante `ZHBtn` disclosure «Ver motivo». No se habilita la acción bloqueada ni se añade una franja permanente.
- El buscador general y `ProductPicker` distinguen buscando, resultados vacíos y error de consulta.
  El error ofrece reintento de la misma consulta/filtro y no se representa como ausencia de productos.
  Las respuestas obsoletas se descartan y el foco de resultados se reinicia al cambiar de consulta.
- Desglose XML conserva sus validadores y cuadre actuales. Cantidades e importes inválidos se asocian
  a sus controles; los fallos de carga/historial/sugerencias y aplicación permanecen dentro del modal.
  La conciliación sigue disponible bajo demanda; el motivo de Aplicar es consultable sin hover.

Evidencia focal P0 (2026-10-23): 91/91 pruebas PASS de `PurchaseLineCard.compact`, `ProductPicker`,
`PurchaseLineBreakdownModal`, `PurchaseActionReason`, `usePurchaseProductSearch` y regresiones del
hook de Compras (pilotFix/draftRecovery/retention); TypeScript, ESLint focal y arquitectura.
Capturas con componentes reales y fixtures, sin sesión ni datos persistidos:
[campos](../evidence/purchases-ux-p0/line-fields.png),
[línea colapsada](../evidence/purchases-ux-p0/line-collapsed.png),
[búsqueda fallida móvil](../evidence/purchases-ux-p0/search-error-mobile.png),
[desglose](../evidence/purchases-ux-p0/breakdown.png).
Limitación de captura: Material Symbols externo no cargó; los iconos muestran sus nombres de ligadura.
Sin cambios de reglas, cálculos, contratos, backend, datos u otros módulos.

### Mensajes compactos P1

- Una sola fila presenta el estado documental y distingue «No permite guardar» de
  «Puede guardar · no confirmar» usando los bloqueos existentes: Guardar con sus mismos predicados
  y Confirmar con `documentState` (mas la intencion de retencion). No modifica validaciones ni
  los predicados de Guardar/Confirmar. Los cambios locales sin guardar se marcan en la misma fila. El disclosure «Incidencias» permite localizar cada linea
  afectada, proveedor, documento, cuotas, retencion o Guardar; desplaza y dirige el foco al destino.
- `buildPurchaseLineIssues` es el modelo de presentacion unico de Compras para la etiqueta global
  y la navegacion del resumen. El detalle individual permanece en `ZHLineIssuesTag`; el resumen
  no vuelve a enumerar sus mensajes. El aviso inferior de productos pendientes y el banner
  superior de borrador guardable/no confirmable se retiran, conservando acceso a todas las lineas.
- El total XML se integra en la cabecera existente. Se retiran los contadores duplicados de
  resueltas/pendientes y solo se muestra el contador de advertencias cuando es positivo.
  Estado, solo lectura, fecha y motivo se presentan juntos; los documentos historicos no ofrecen guardar.
- Los estados normales de retencion y la equivalencia de guardado en unidad base son informacion:
  texto en su seccion, sin contarlos como incidencias ni crear banners. Se conserva el motivo de
  omision de retencion; numero automatico y emision conjunta se explican en el control de intencion.
- Un rechazo general aparece una vez, con detalles sin repeticiones; no genera tambien un toast.
  Los errores de campo/linea/cuotas/retencion permanecen en su contexto; Distribuir conserva sus
  rechazos dentro del modal. La duplicidad SRI mantiene su aviso unico y «Ver compra existente».
- Una distribucion local informa que falta guardar; los mensajes de operaciones persistidas
  conservan su significado. Confirmar explica sus efectos y las restricciones reales de anulacion,
  sin afirmar que es irreversible. Registrar una solicitud SRI es informacion, no compra anulada:
  el estado persistido y `RetentionAnnulmentPanel` siguen siendo la fuente de su resultado.

Validacion focal P1 (2026-10-23): 516/516 pruebas PASS en `src/modules/purchases`, notices ZH y
disclosure de `ZHBtn` (mensajes, navegacion/foco por prioridad, bloqueos de borrador, duplicidad SRI,
campos, retencion, confirmacion/anulacion y regresiones P0). `tsc -b`, ESLint (0 errores; advertencias
previas), build y `architecture:check` (0 infracciones nuevas) PASS.
Capturas con el formulario real y fixtures (API simulada), sin sesion ni datos persistidos:
[borrador antes](../evidence/purchases-ux-p1/before-draft.png) y
[despues](../evidence/purchases-ux-p1/after-draft.png),
[incidencias desplegadas](../evidence/purchases-ux-p1/after-draft-issues.png),
[estado historico antes](../evidence/purchases-ux-p1/before-readonly.png) y
[despues](../evidence/purchases-ux-p1/after-readonly.png),
[confirmacion](../evidence/purchases-ux-p1/after-confirm.png).
En las capturas «antes» Material Symbols no cargo y los iconos se ven como ligaduras de texto.
Sin suites completas, cambios de calculos/contratos/backend/datos, commit, push ni deploy.

### Mensajes de modales P2

- Confirmar: `buildConfirmChecklist` conserva todas las comprobaciones y bloqueos; presentaciones
  vinculadas/faltantes forman un único control. Bloqueos y advertencias aparecen primero, una línea
  cada uno; los controles correctos se resumen en una sola línea «Correcto». El aviso de bloqueo solo
  aparece si existe un bloqueo. La compra manual conserva su nota neutral.
  Las advertencias no bloqueantes vigentes de cada línea (`buildConfirmLineWarnings`, que reutiliza
  `buildPurchaseLineIssues`: cantidad/costo distintos al XML, costo fuera de rango) se agrupan en
  «Advertencias (N)», una fila «Línea N · producto — motivo», entre los bloqueos y «Correcto». No
  bloquean ni alteran el aviso de bloqueo; los bloqueos de línea no se repiten ahí. Las advertencias
  comerciales (ver [Rentabilidad de línea](#rentabilidad-de-línea)) no se enumeran por producto:
  `buildConfirmCommercialRisk` las resume en una sola fila «Rentabilidad por revisar (N línea(s))»
  con el conteo por tipo; el detalle vive en el `ZHLineIssuesTag` de cada línea.
- Desglose XML: un importe pendiente total de cero no se presenta como cuadrado. El pie muestra
  «Pendiente»/«Excedente» si el total difiere, «Conceptos sin cuadrar» si difiere algún concepto o
  impuesto, y «Cuadrado» solo sin diferencias. El conteo vive en «Conciliación (n)»; el motivo de
  Aplicar indica una sola vez revisar la conciliación. Validez y cuadre siguen en `breakdownBalance`.
- Distribuir: se mantiene la advertencia de transporte. El monto vacío es una indicación en su campo,
  no un aviso; la selección inválida de ítems se advierte una vez. Los totales repetidos se unifican
  (factura después/CxP esperada; redondeo/por cuadrar) y el rechazo del servidor sigue en el modal.
  Presentación (solo UI; cálculo intacto en `simulateCostDistribution`): modal `2xl`, tabla
  `.table` compacta dentro de `.table-scroll` (scroll horizontal solo en la tabla, columna Ítem fija
  con `table--sticky-column`), checkbox y descripción en la misma celda (máx. 2 líneas, texto completo
  en `title`). En móvil, `responsive-table` apila cada producto en dos columnas etiqueta/valor
  (`data-label`). Los ocho totales se agrupan en «Distribución» y «Factura» (`zh-summary-grid`), cada
  grupo con su resultado primero. Es un preview calculado en vivo: excepción documentada de
  `ZHDataTable`.
- Productos pendientes: los motivos atribuibles a una fila se agrupan en su `PurchaseLineIssuesTag`,
  sin resumen superior ni mensajes inline repetidos. Solo los motivos sin fila visible permanecen globales.
- Anular: `buildPurchaseCancelMessage` informa efectos locales, que la anulación local no anula ante el
  SRI la factura del proveedor y, si existe, la anulación terminal de la retención. Con retención
  autorizada por el SRI solo indica ese requisito, sin anunciar efectos que no ocurrirán. El modal de
  solicitud SRI no cambia.

Validación focal P2 (2026-10-23): 534/534 pruebas PASS en `src/modules/purchases`, notices ZH y
disclosure de `ZHBtn` (checklist con cero, una y varias incidencias; desglose cuadrado y descuadrado
por concepto con total cero; distribución; rechazo parcial; anulación con retención y SRI).
`tsc -b`, ESLint (0 errores; advertencias max-lines previas), build, `architecture:check` e i18n PASS.
Capturas con componentes reales y fixtures, sin API, sesión ni ERP real; el resumen de identificación
de Confirmar no se incluye porque no cambió. Material Symbols no cargó: los iconos aparecen como nombres.
[confirmar sin incidencias](../evidence/purchases-ux-p2/confirm-zero.png),
[una](../evidence/purchases-ux-p2/confirm-one.png),
[varias](../evidence/purchases-ux-p2/confirm-many.png) y
[móvil](../evidence/purchases-ux-p2/confirm-many-mobile.png);
[desglose descuadrado por concepto](../evidence/purchases-ux-p2/breakdown-concept-unbalanced.png) y
[cuadrado](../evidence/purchases-ux-p2/breakdown-balanced.png);
[distribuir](../evidence/purchases-ux-p2/distribute-initial.png),
[con advertencia](../evidence/purchases-ux-p2/distribute-lines-warning.png) y
[móvil](../evidence/purchases-ux-p2/distribute-mobile.png);
[rechazo parcial](../evidence/purchases-ux-p2/pending-partial-rejection.png);
[anular sin retención](../evidence/purchases-ux-p2/cancel-no-retention.png),
[con retención](../evidence/purchases-ux-p2/cancel-retention.png) y
[retención autorizada](../evidence/purchases-ux-p2/cancel-retention-sri-authorized.png).

### Productos pendientes compactos

- `ResolvePendingProductsModal` integra el formulario único (RHF + `pendingProductsFormSchema`), la
  carga de presentaciones, el envío todo o nada y el pie. `PendingProductsBulkBar` (edición masiva),
  `PendingProductRow` (fila + detalle) y `PendingProductFacts` (datos XML y desglose del PVP) solo
  presentan. `PurchasesPage` no cambia de rol: abre el modal y aplica el resultado.
- Cada línea XML es una fila compacta (selección, línea del proveedor, acción, producto ERP) con
  detalle expandible que conserva todos los campos. El detalle colapsado permanece montado: expandir
  o colapsar no pierde datos. Con una sola línea abre expandido; cambiar la acción abre la fila.
- Datos por completar: `pendingRowIssues` deriva de `pendingProductsFormSchema` y de la comprobación
  de precio sin IVA del envío (`lacksNetSalePrice`), sin reglas alternativas. Se muestran con
  `PurchaseLineIssuesTag` (adaptador único de Compras de `ZHLineIssuesTag`, compartido con las líneas
  de la compra) y su acción abre la fila. El pie cuenta seleccionadas, listas y pendientes
  (seleccionadas con incidencias o rechazadas por el servidor).
- Al enviar con errores se expanden las filas afectadas antes de enfocar el primer campo; un rechazo
  del servidor expande sus filas. Ningún campo con error queda oculto.
- Los conflictos de SKU, barcode y código de proveedor reutilizan el diagnóstico global de Ítems
  (`useItemCodeConflicts`) y `ZHCodeConflicts` en modo compacto dentro del popup de la fila.
  El detalle muestra código, propietario y «Ver ítem» en otra pestaña; los duplicados del lote
  muestran las filas implicadas sin enlace. SKU/barcode se marcan en sus campos y el código del
  proveedor en la línea XML. El rechazo conserva los datos, refresca el diagnóstico y evita repetir
  el mismo conflicto. No cambia validación, envío ni persistencia.
- El diagnóstico asíncrono vigente también alimenta el pie y la habilitación de Resolver.
  Una fila seleccionada con códigos sin verificar o con conflictos no se cuenta como lista.
  Durante debounce/consulta se muestra «Verificando códigos» en su etiqueta compacta y se impide
  resolver el lote. Cambiar códigos invalida inmediatamente el diagnóstico anterior; solo una
  respuesta correspondiente a los candidatos actuales puede confirmar la verificación. Un fallo
  de consulta no confirma unicidad. Corregir un código recalcula etiqueta y contadores sin guardar.

Validación (2026-10-23): caracterización previa y posterior con los mismos escenarios (crear,
vincular, sugerencia, agrupar presentación, edición masiva, margen, edición individual, errores,
selección parcial, 23 productos), más diseño compacto (expandir/colapsar, contadores, etiqueta,
foco). Vitest de Compras, notices ZH y disclosure 589/589, `tsc -b`, ESLint (0 errores), build,
`architecture:check` y guardrail PR-7 PASS; retirada la excepción ADR-047 del modal (≤ 500 líneas).
[Evidencia antes/después](../evidence/purchases-pending-products-compact-01/README.md).

## 3. Recepción electrónica y conciliación de ítems

- Importar el TXT del SRI crea un `PurchaseReceptionDocument` por comprobante en la sucursal activa. Descargar el XML autorizado solo opera sobre documentos Imported; si falla, el documento conserva su estado. Al descargarlo se persisten sus líneas con la conciliación automática.
- La NC se interpreta con su parser propio, sin Item Matching de factura. Si el XML autorizado no tiene líneas utilizables, se conserva como `Verified` con procesamiento `Failed`, conteos y diagnóstico; `Processed`/`ProcessedWithWarnings` requieren al menos una línea procesada. Una descarga repetida sobre `Verified` se rechaza sin volver a consultar SRI ni reemplazar el snapshot. La NC continúa por la entrada única de §6.
- En el detalle de NC, `descuento` es opcional según los XSD SRI 1.0.0/1.1.0 existentes: su ausencia representa cero descuento y no descarta la línea. Si el elemento está presente, su valor debe ser válido. Se conservan los importes e impuestos del XML, incluido IVA 0 %.
- "Actualizar estados" recalcula el estado de compra, gasto y documento sin reimportar, guardar ni consultar al SRI. La recepción refleja la compra por `PurchaseState` (None/Draft/Confirmed/Cancelled/Discarded) + `PurchaseId`.
- "Crear compra" desde la recepción precarga el formulario a partir del XML guardado y la conciliación persistida. La compra se crea solo al guardar el borrador. Una recepción puede generar compra o gasto, nunca ambos.
- Conciliación (`ItemMatchStatus`: Pending, NeedsReview, AutoMatched, ManuallyMatched):
  - Código principal o auxiliar de proveedor exacto → vínculo por una relación `ItemSupplierCode` activa del mismo proveedor y empresa. No se interpreta como SKU general; sin relación queda pendiente de revisión manual.
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

Desglose XML backend (2026-10-23): `PurchaseXmlAllocationTests`, `PurchaseXmlBreakdownTests`, confirmación por componentes en `ConfirmPurchaseHandlerTests` y `PurchaseXmlBreakdownPostgreSqlTests`. Validación focal: Domain 59/59, Application 91/91 y PostgreSQL desechable 12/12 (incluyen regresiones XML/ICE y devoluciones). Cubren asignación exacta, descuento, procedencia, rechazo de impuestos/grupos duplicados, alta, actualización, reapertura, rollback posterior al reemplazo, aislamiento por empresa/tenant y confirmación sin duplicar entradas/CxP. Caso `029-001-001341832`, código `13563`: fixture reconstruido con los importes suministrados, no verificación del XML autorizado original; bases y tarifas de prueba explícitas, nunca inferidas por el motor. Modelo EF sin cambios pendientes, guardrail de arquitectura sin infracciones nuevas y diff revisado. Sin cambios de reglas contables ni ejecución sobre bases existentes.

Desglose XML frontend (2026-10-23): `purchaseXmlBreakdown.test.ts` (9: elegibilidad, cuadre por concepto/impuesto, diferencia de un centavo, reemplazo en posición, re-desglose, flete, grupo alterado, totales desde la asignación, reapertura) y `PurchaseLineBreakdownModal.test.tsx` (5: cabecera de una línea y resumen sin códigos SRI, filas compactas con impuestos bajo demanda, reapertura cuadrada desde la barra única, bloqueo por impuesto descuadrado con conciliación bajo demanda, búsqueda visible y alta con presentación de compra). Compras frontend 462/462, TypeScript, ESLint, build y gate de arquitectura PASS; backend focal re-ejecutado: Domain 5/5, Application 51/51, PostgreSQL desechable 7/7.

ICE en Compras (2026-10-21): `PurchaseIceSnapshotTests`, `PurchaseIceSpecificResolutionTests`, `PurchaseInvoiceDetailXmlTaxSnapshotTests`, `PurchaseReturnTests` (prorrateo ICE XML) y `PurchaseIceXmlSnapshotPostgreSqlTests` (PostgreSQL real 5/5: persistencia, reapertura, recálculo y descuento bloqueado); frontend `purchaseLineReadiness.test.ts`. Sin migración ni ejecución sobre datos existentes.

Unificación de NC (2026-10-21): `PurchaseReturnEndToEndTests.UnifiedCreditNotes.cs` y `.Reprocessing.cs`, PostgreSQL real 21/21 con SRI simulado; contratos API 24/24; Application focal 79/79; contabilización PostgreSQL de devolución/descuento 6/6; frontend focal 32/32. Cubren salida única, descuento sin inventario, idempotencia, concurrencia, vínculo histórico sin efectos, duplicados, cancelación interna, reversas incompletas y SRI anulado/pendiente/no verificable. Build/lint/TypeScript y gate de arquitectura PASS; límite externo del guardrail general en [entrega](../agent/PURCHASE_CREDIT_NOTE_UNIFICATION_DELIVERY.md). Sin migración ni ejecución sobre datos de negocio.

Domain/Application (filtro Items/Inventory/Purchases, 2026-10-19): 354/354 y 551/551 PASS. PostgreSQL 2026-10-19: `PurchaseCancelledStockMovementIntegrationTests`, `ResolvePurchaseReceptionLinesIntegrationTests` y `PurchaseInvoiceConfirmedPostingIntegrationTests` (dentro de 39/39 PASS). Cobertura adicional: `ConfirmPurchaseHandlerTests`, `CancelPurchaseHandlerTests`, `DiscardPurchaseDraftTests`, `PurchaseDraftRecoveryTests`, `PurchaseXmlConfirmationGuardTests`, `PurchaseInvoiceCostProrationConsistencyTests`, `ItemMatchFinderTests`, `ItemMatchConfirmationServiceTests`, `AuthorizePurchaseReturnHandlerTests`, `CancelPurchaseReturnUseCasesTests`, `PurchaseCreditNoteDiscountIntegrationTests`, `SupplierCreditMovementTests`, `PurchaseRetentionConfirmIntegrationTests`.

### Ausencia de barcode en productos pendientes

- Regla única (frontend `isRealBarcode`, backend `IsValidGtin`): un código del XML (auxiliar o
  principal) solo es barcode si es GTIN-8/12/13/14 con dígito verificador válido. Cualquier otro
  valor es código del proveedor. No existen listas de valores excluidos. El frontend precarga el
  GTIN válido y sugiere el tipo solo para EAN-8/EAN-13. Cuando el cliente omite el barcode, el
  backend recupera el GTIN válido del XML si su tipo es inferible (EAN-8/EAN-13). Lo conserva en su
  presentación (unidad o caja/pack), previa comprobación de unicidad.
- Un barcode explícito de la fila o la solicitud nunca se descarta. Solo el vacío o los espacios
  equivalen a ausencia. Un valor explícito conserva sus validaciones existentes: la unidad exige
  tipo de catálogo, de modo que un Interno ingresado como `5077` es válido y uno sin tipo se rechaza.
  Sin barcode de unidad, el backend genera `INT-` + GUID de 32 dígitos hexadecimales, tipo de
  catálogo `Internal`, principal de la variante base. Nunca inventa EAN ni cambia SKU.
- La generación comprueba disponibilidad por Tenant + Company y dentro del lote (hasta 10
  candidatos). El trigger PostgreSQL existente garantiza unicidad entre variantes y empaques,
  incluso en concurrencia. Una colisión del Interno al crear provoca rollback del lote completo,
  recarga y regeneración (hasta tres reintentos); el agotamiento devuelve error por producto.
- Un barcode comercial ya ocupado rechaza la creación y solicita Vincular; nunca crea otro Item
  ni reemplaza un código comercial. Se mantienen creación/vinculación atómicas, presentaciones,
  edición masiva y precios mediante los mismos comandos y componentes.
- `ItemSupplierCode` conserva por separado el código válido del proveedor (por ejemplo, un código
  corto o numérico que no es GTIN), con la normalización existente y su presentación. Vacío y `0` no generan equivalencias;
  el snapshot XML se conserva. El SKU sigue siendo el ingresado por el usuario.
- El EAN real se añade después mediante el alta de barcode existente de Ítems, sobre el mismo
  Item/variante. Se conservan Interno, Id, relaciones, presentaciones e historial.
