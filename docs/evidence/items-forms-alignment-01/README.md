# Ítems — auditoría y alineación de formularios

Fecha: 2026-10-24. Clasificación: **BUG REAL**. SSOT: [Ítems](../../architecture/items.md),
[Compras](../../architecture/purchases.md), [Frontend/Design System](../../architecture/frontend.md).
ADRs 040/041 preservados. Sin cambios de datos, migraciones, contratos API, reglas fiscales,
inventario o contabilidad; sin commit, push ni deploy.

## Superficies inspeccionadas

| Superficie | Implementación y datos | Operaciones |
|---|---|---|
| Productos → Ítems | `ItemsPage` → `ItemFormTabs`; `itemService.getById` → `toFormValues`; catálogos únicos `useItemCreationCatalogs` | Alta, edición y consulta (`disabled`, también `?view=id` desde «Ver ítem») |
| Compras manuales | `PurchasesPage` → `ItemEditorModal` → `ItemEditorForm`; mismo `getById`, `buildDefaults`, create/update oficiales | Alta desde línea sin ítem y edición del ítem vinculado |
| Compras recibidas/XML | Mismo editor individual; `ResolvePendingProductsModal` → `PendingProductRow`, `PendingProductsBulkBar`, `PendingProductFacts` | Crear, vincular, otra presentación de un nuevo producto, asignación masiva |
| Barcodes y proveedores | `BarcodeListEditor`, `SupplierCodesSection`, `PrincipalCodesSummary`, `VariantsSection` | Alta inicial, consulta, agregar/desactivar barcode, proveedor y presentación asociada |
| Variantes y presentaciones | `VariantsSection`, `CollectionSection/PackagingLevelsSection` | Atributos, SKU de variante, barcodes, presentación base y empaques adicionales |
| Importación inicial | `InitialLoadProductCatalogPage` → `ImportWizardPage`; `ItemImportColumns`, `ItemImportProcessor`, `ClosedXmlItemImportSheetReader` | Plantilla, preview, errores, creación mediante comando oficial |
| Otros módulos | Búsqueda de consumidores de `ItemEditorModal`/`ItemFormTabs` | Las invocaciones actuales están en Compras y Productos; otros módulos usan selectores, no otro formulario maestro |

## Matriz antes/después y comportamiento

P = Productos alta/edición/consulta; C = editor individual de Compras; R = resolver pendientes;
I = importador. Un campo no expuesto por un flujo no se añade ni se inventa. «Persistido» es el
valor del DTO del ítem; en alta se usan los defaults oficiales, y en R/I los datos propuestos del
XML/archivo se validan antes de crear. La consulta de Productos deshabilita edición y guardado.

| Campo / significado | Antes (P / C / R / I) | Después oficial | Origen / valor cargado | Validación y edición | Clasificación |
|---|---|---|---|---|---|
| `sku`, identificador comercial interno | SKU con `*` embebido / SKU / SKU / SKU | SKU | DTO o entrada; R propone código permitido | Obligatorio, 50, formato existente, unicidad por empresa; editable; duplicados + «Ver ítem» conservados | BUG REAL |
| `shortName`, nombre corto comercial | Nombre comercial / Nombre y Nombre corto / Nombre ERP / Nombre | Nombre comercial | DTO; alta C propone nombre hasta 50; R editable; I columna Nombre | Obligatorio, máximo 50; editable | BUG REAL |
| `description`, texto completo | Descripción completa / Descripción / Descripción ERP / misma columna Nombre | Descripción completa | DTO; propuestas separadas del nombre del ítem; I usa Nombre para ambos | Obligatoria, máximo 254; no fusionada con nombre comercial | BUG REAL |
| `itemTypeId`, clasificación | Tipo / Tipo de ítem vacío / Tipo de producto / Tipo de Ítem | Tipo | ID de catálogo, nombre dinámico; no enum | Obligatorio al crear; inmutable al editar; ID reaplicado al llegar catálogo | BUG REAL |
| `nature`, participación operativa | Naturaleza operativa / mismo texto sin marcador / Product fijo en R e I | Naturaleza operativa | DTO Product/Service; C alta permite ambos; R/I crean Product | Inmutable tras alta; Product y Service siguen distintos; ayuda de inmutabilidad compartida | BUG REAL |
| `defaultUomCode` | Unidad base de inventario / Unidad de medida / Unidad base / Unidad Base | Unidad base de inventario | Código catálogo SRI y DTO; I carga código, no nombre | Obligatoria; máximo 10; ayuda oficial de stock/costo/Kardex reutilizada | BUG REAL |
| `brandId` | Marca en todas; C exigía también al editar | Marca | DTO + catálogo; R decisión de fila; I resuelve nombre | Alta obligatoria; actualización admite ausencia según contrato vigente; edición no se sobrescribe por catálogos tardíos | BUG REAL |
| `categoryNodeId` | Categoría en todas; C exigía también al editar | Categoría | DTO + catálogo de hojas/rutas; I resuelve nombre | Alta obligatoria; hoja/rama activa validada por owner; opcional en update oficial | BUG REAL |
| `saleVatCode` | Tarifa IVA venta con `*` / IVA de venta / IVA venta / IVA Venta | Tarifa IVA venta | DTO `taxConfig` y catálogo SRI; selección independiente de compra | Obligatoria, código vigente; sin cambios de tarifa, cálculo ni defaults | BUG REAL |
| `purchaseVatCode` | Tarifa IVA compra con `*` / IVA de compra / IVA compra / IVA Compra | Tarifa IVA compra | DTO `taxConfig`; en R procede del XML, solo lectura | Obligatoria; independiente de venta; no se transforma el impuesto XML en decisión editable | BUG REAL |
| `exciseTaxCode` | Código ICE con `*` pese a opcional | Código ICE | DTO/catálogo; R usa XML; C update conserva DTO | Opcional; reglas fiscales intactas | BUG REAL |
| `observations` | Observaciones | Observaciones | DTO o entrada; I columna homónima | Opcional, máximo 500; no se mezcla con descripción | KEEP |
| Barcode de unidad/variante | Código / Código de barras / Código de barras de la unidad (opcional) | Código de barras; R conserva calificador de unidad/opcional | Variantes del DTO, entrada o EAN válido del XML | Alta P/C exige al menos uno; R puede generar Internal provisional por backend; unicidad global existente y principal único | BUG REAL |
| Tipo de barcode | Tipo / Tipo de código de barras | Tipo de código de barras | Catálogo activo de tipos de barcode; distinto del tipo de ítem | Obligatorio cuando se declara barcode de unidad; no se infiere otro tipo | BUG REAL |
| Proveedor y código | Proveedor / Código proveedor | Proveedor / Código proveedor | Relaciones supplierCodes, contexto de proveedor o columna del archivo | Unicidad por empresa/proveedor/código, máximo 100; se conserva vínculo a presentación | KEEP |
| Principal (barcode/proveedor) | Principal | Principal | Flags del hijo del ítem | Exactamente un barcode principal; a lo sumo un código proveedor principal; validadores existentes | KEEP |
| SKU de variante | SKU / SKU (override) | SKU / SKU (override) en alta de variante | DTO o combinación de atributos/override | Máximo 80 para override; unicidad de variante y combinación; concepto diferente del SKU del padre | KEEP |
| Atributos de variante / orden | Atributos / Orden | Mismos | DTO y definiciones de atributos | Requeridos según definición; se conservan validación y estado activo | KEEP |
| Nombre de presentación | Nombre / Nombre presentación | Nombre, dentro de sección Presentación | `packagingLevels.name` o entrada R | Requerido al declarar presentación; no es nombre comercial del ítem | BUG REAL |
| Unidad de presentación | Unidad de presentación / Unidad presentación | Unidad de presentación | `uomCode` del empaque | Catálogo SRI; distinta de `defaultUomCode` | BUG REAL |
| Factor del empaque | Equivalencia en unidad base / Unidades por presentación | Equivalencia en unidad base | `baseQuantity` / `presentationFactor` | Mayor que cero; base=1; unicidad UOM+cantidad y restricciones de uso conservadas | BUG REAL |
| Barcode de presentación | Código de barras / Código de barras de esta presentación (opcional) | Se conserva calificador de presentación | `packagingLevels.barcode` | Opcional y único; no se traslada al barcode de unidad | KEEP |
| Peso, presentación base, defaults compra/venta | Peso / Es unidad base / Compra / Venta | Mismos | DTO de presentación | Reglas del editor oficial; no expuestos por C; no se añaden | KEEP |
| Precio base y precio ingresado | Precio ingresado / Precio sugerido / PVP final (unidad base) / PVP | Se conservan según significado | BaseSalePrice sin IVA; C sugiere neto; R recibe bruto y deriva neto; I PVP neto | P permite precio ausente al crear; C/R requieren >0 para su flujo; I POS=SI requiere >0. Diferencias vigentes de flujo, no se fusionan precios | KEEP |
| Rentabilidad y costos | Costo base actual, último costo, promedio / costo XML, simulación | Mismos | Profitability DTO frente a documento XML; datos distintos | Solo lectura; mismas políticas de precisión; no se tocan cálculos | KEEP |
| `saleConfig` | Disponible para venta, descuento máximo, Web/POS/Mobile, eCommerce | Mismos | DTO; C update reenvía config existente | Descuento 0–100; C no añade controles de configuración avanzada; permisos intactos | KEEP |
| `stockConfig` | Control de stock, lote, serie, decimales, mínimos/máximos | Mismos | DTO; C update conserva flags y cantidades | Naturaleza Service no participa; rangos y validación existentes; lotes/series siguen no operativos | KEEP |
| Estado | Activo / Inactivo | Mismos | `isActive` del DTO | Lectura en formulario; activación por acciones oficiales, sin borrado físico | KEEP |
| Imágenes / conversiones / sustitutos | Secciones oficiales de Productos | Mismos | Colecciones del DTO | Edición por owner; conversiones y sustitutos no se confunden con presentaciones | KEEP |
| Acciones del editor | Crear Item / Actualizar Item | Crear ítem / Actualizar ítem | Claves existentes `items.form.*` | Mismos callbacks y comandos; sin nuevas rutas ni permisos | BUG REAL |
| Obligatoriedad/errores/ayudas | Asteriscos en traducciones, errores «nombre corto», ayudas distintas | Marcadores ZHField; errores nombre comercial/descripción completa/unidad base/IVA coherentes | i18n + schemas existentes; mensajes backend de create/update alineados | Reglas de owner conservadas; C actualización adopta opcionalidad de marca/categoría ya admitida | BUG REAL |
| Encabezados Excel | Nombre, Tipo de Ítem, Unidad Base, IVA Venta/Compra, Código Barra N | Contrato físico conservado; preview y ayuda usan nombres oficiales y explican correspondencia | `ItemImportColumns` única fuente del reader y processor | Cambiar encabezados invalidaría archivos existentes; no se añade contrato paralelo | KEEP |

## Causa raíz demostrada

`buildDefaults` copia correctamente `existingItem.itemTypeId`. El `reset()` puede ejecutarse
antes de que `useItemTypeOptions` haya añadido su `<option>`. El selector nativo no controlado
queda en el placeholder vacío aunque el DTO traiga un ID válido. El efecto de recuperación de
`ItemEditorForm` esperaba cinco catálogos, pero solo reaplicaba categoría, marca, UOM y ambos
IVA: faltaba `itemTypeId`. Estar deshabilitado preservaba la inmutabilidad, pero no recuperaba
su selección. Se incluye el tipo en esa recuperación, sin enviarlo en UpdateItem.

No hay conversión entre un enum de naturaleza y el GUID de tipo. El mapper y el contrato DTO
conservan ambos por separado. No hay evidencia de pérdida de persistencia: en la etapa anterior
se consultaron los seis ítems de `erp_zh_dev` en una transacción `BEGIN READ ONLY` (servidor local
`postgreszh`, puerto publicado 5435): seis tipos existentes y activos, cero identidades vacías,
cero marca/categoría/IVA ausentes. Esta etapa no volvió a modificar ni consultar PostgreSQL.

Hallazgo adicional: ambos efectos de recuperación reaplicaban valores del DTO sobre una marca
ya editada mientras otro catálogo seguía pendiente. Ahora comprueban `getFieldState().isDirty`.
Las pruebas esperan la precarga real del DTO antes de editar y liberan explícitamente la promesa
pendiente del catálogo; no dependen de demoras de red arbitrarias.

## Pruebas y evidencia

- [Regresión previa](regression-before.txt): cuatro FAIL deliberados al omitir la sincronización
  del tipo y quitar la protección de edición. El código corregido se restauró en `finally`.
- `ItemEditorModal.test.tsx`: 32/32 PASS; incluye seis nuevas pruebas (paridad Productos/Compras,
  naturaleza Service inmutable y reapertura, tipo tardío y edición, marca editada en dos flujos,
  actualización con clasificación opcional). Se ejecutan los componentes reales con servicios
  simulados, React Testing Library y jsdom; no son una sesión autenticada del ERP.
- Cierre (retomado el 2026-10-24): el primer reporte JSON tenía 1 FAIL en
  `ResolvePendingProductsModal.compact.test.tsx`. La prueba esperaba el texto anterior «La unidad de
  medida es obligatoria.»; el esquema ya emite el mensaje oficial `items.validation.uomRequired`
  («La unidad base de inventario es obligatoria.»), igual que el editor compartido y la prueba
  `flows`. Se corrigió solo la aserción; no se cambió el comportamiento.
- Focal frontend (Compras/componentes, `components/items`, `modules/items`, `initialLoad`):
  42 archivos, 313/313 PASS.
- Suite frontend completa ([resumen](frontend-tests-summary.txt)): 322 archivos, 3244/3248 PASS, 4 FAIL ajenos:
  `caja/cajaI18n.test.ts` (1) y `accounting/pages/JournalEntryDetailPage.test.tsx` (3). Ningún archivo
  de esas superficies está modificado, y los 4 fallan igual en un worktree limpio de `HEAD`
  (5622f63a1), que se creó en una ruta temporal y luego se eliminó. Son fallos previos, no regresiones de
  esta tarea.
- Application focal (`Items|InitialLoad|ItemMatching`): 446/446 PASS ([resumen](items-forms-application-summary.txt)).
  Este filtro es más amplio que el de la etapa anterior (189/189). Son pruebas unitarias, sin PostgreSQL.
- `npx tsc -b`: PASS. `npm run lint`: 0 errores (35 warnings `max-lines` previos). `npm run build`: PASS.
  `npm run architecture:check`: 140/140 tests y gate PASS (0 violaciones nuevas).

Los casos existentes de duplicados, «Ver ítem», creación/vinculación por lote, barcodes,
variantes y presentaciones permanecen en la regresión. No se sustituyeron formularios ni
validadores y no se modificaron los contratos públicos de estos flujos.

## Archivos de esta tarea

No se incluyen aquí los numerosos cambios previos que ya tenía el workspace.

- `frontend/src/components/items/ItemEditorModal/{ItemEditorForm.tsx,ItemEditorModal.tsx,itemEditorSchema.ts,ItemEditorModal.test.tsx}`
- `frontend/src/modules/items/components/ItemForm/{GeneralTab.tsx,TaxConfigTab.tsx,ItemFormTabs.tsx}`
- `frontend/src/modules/purchases/components/{PendingProductRow.tsx,PendingProductsBulkBar.tsx,PendingProductFacts.tsx,ResolvePendingProductsModal.flows.test.tsx,ResolvePendingProductsModal.compact.test.tsx}` (en `compact` solo se alineó el texto esperado de unidad base)
- `frontend/src/modules/purchases/schemas/pendingProductsSchema.ts`
- `frontend/src/modules/initialLoad/pages/InitialLoadProductCatalogPage.tsx`
- `frontend/src/i18n/locales/{es,en}.json`
- `backend/src/ERP.Application/Modules/Items/UseCases/{CreateItem/CreateItemCommandValidator.cs,UpdateItem/UpdateItemCommandValidator.cs}`: solo textos.
- `docs/architecture/items.md`, `STATUS.md` y esta carpeta de evidencia.

## Validación real y pendientes

**Pendiente:** capturas nuevas de Productos y Compras, navegación visual y ciclo de guardar y
reabrir en el ERP real. Computer Use se detuvo en la etapa anterior por no poder identificar la
URL de Chrome con confianza. No se insistió ni se usó otra vía para eludir esa política.

La evidencia PostgreSQL anterior es real y de solo lectura; la paridad de formularios y los
ciclos de creación/edición de esta etapa son automatizados con servicios simulados. No se
afirma validación visual ni escritura real. Los encabezados Excel siguen siendo los del contrato
vigente, con equivalencias visibles en la ayuda; no se cambió el formato de importación.
