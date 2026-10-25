# Ítems — SSOT funcional

Única fuente funcional del módulo Ítems. Describe solo el comportamiento implementado y cubierto por tests vigentes. Precio de venta: Motor de Pricing (CLOSED, ADR-021). Stock y costo: [inventory.md](./inventory.md). Rationale: [ADR-040](../decisions/ADR-040-item-company-scope-inventory-nature.md).

Código: `ERP.Domain/Modules/Items`, `ERP.Application/Modules/Items`, `ERP.Infrastructure/Persistence/{Configurations,Repositories}/Items`. API: `/api/v1/items`, `/api/v1/catalog`, `/api/v1/catalog/classifications`, `/api/v1/item-types`.

## 1. Alcance y aislamiento

- `Item` pertenece a Tenant + Company, sin BranchId; las sucursales comparten el catálogo de su empresa. La empresa sale del contexto autenticado, es inmutable y sin contexto válido las lecturas devuelven vacío.
- Hijos (variantes, barcodes, imágenes, conversiones, empaques, códigos de proveedor, impuestos especiales, sustitutos) heredan el alcance de su Item mediante el filtro global.
- Catálogos de clasificación (Brand, ItemCategoryNode, AttributeGroup, AttributeDefinition, ItemTypeDefinition) son Tenant-wide. BarcodeTypeDefinition e ItemMarginStatusDefinition son catálogos globales.
- Pricing y sustitutos solo aceptan Items de la misma empresa.

## 2. Identidad y unicidad

- SKU: obligatorio, ≤ 50 caracteres, `[A-Za-z0-9-_.]`; se normaliza a mayúsculas; editable (el Id es la identidad relacional). Único por empresa: el handler responde `SKU_DUPLICATE` y lo garantiza el índice `uq_items_tenant_company_sku`. El SKU de variante también es único por empresa (`(TenantId, CompanyId, SKU)` en variantes).
- Nombre corto ≤ 50, descripción ≤ 254, observaciones ≤ 500.
- Barcode: ≥ 1 al crear, exactamente uno principal, sin repetidos en la solicitud, tipo existente y activo en el catálogo. Único por empresa entre variantes y empaques (trigger `a1_item_barcode_unique`).
- Desde Resolver productos pendientes de Compras, sin barcode comercial de unidad se crea un `Internal` principal `INT-` + GUID; nunca deriva del proveedor ni modifica SKU. El EAN real se añade con `AddBarcodeCommand` al mismo Item/variante, conservando relaciones e historial. Generación y reintentos: [Compras](./purchases.md#ausencia-de-barcode-en-productos-pendientes).
- Código de proveedor: único por Tenant + Company + Supplier + Code; ≤ 100 caracteres; a lo sumo uno principal; puede asociarse a un nivel de empaque activo del mismo Item.
- El mismo SKU, barcode, variante o código de proveedor puede repetirse en otra empresa.
- Diagnóstico de conflictos (solo lectura): `POST /api/v1/items/code-conflicts` (`items.view`, Tenant + Company autenticados; el proveedor del body solo filtra dentro de la empresa) devuelve, por SKU/SKU de variante/barcode/código de proveedor, el Item propietario (Id + nombre) o el duplicado entre líneas del lote sin guardar. `ExcludeId` evita el falso duplicado del propio registro al editar. Usa las mismas claves de unicidad; no las reemplaza: la persistencia sigue siendo la autoridad (incluida la concurrencia). Frontend: `ItemCodeConflicts` (adaptador) sobre `ZHCodeConflicts`, con «Ver ítem» → `/products/items?view={id}` en otra pestaña. Integrado en alta/edición de Ítems, editor compartido, barcodes, variantes, presentaciones, Resolver productos pendientes e importador inicial.

## 3. Naturaleza operativa (`ItemNature`)

- Valores: `Product` y `Service`. Se fija al crear; `UpdateItem` no la modifica.
- `Product` participa en Inventory (`ParticipatesInInventory`). `Service` no usa bodega ni genera stock, Kardex ni costo; su `StockControlEnabled` se fuerza a OFF.
- `ItemTypeDefinition` es solo clasificación (catálogo tenant-editable, existente y activo al crear); no determina comportamiento.

## 4. Configuración

- Obligatorios al crear: tipo de ítem, unidad base (≤ 10, mayúsculas), categoría, marca, IVA de venta e IVA de compra. El IVA se valida contra el catálogo SRI vigente al día de la empresa. ICE opcional (≤ 10).
- Categoría: debe existir, estar activa, ser hoja (sin hijos activos) y no pertenecer a una rama deshabilitada. La profundidad máxima del árbol se resuelve por empresa (`ResolveMaxCategoryDepthAsync`).
- Marca: debe existir y estar activa.
- Venta (`ItemSaleConfig`): vendible, descuento máximo 0–100, disponibilidad Web/POS/Mobile, e-commerce, favorito.
- Stock (`ItemStockConfig`): `StockControlEnabled`, decimales en cantidad/venta, stock mínimo/máximo ≥ 0 con mínimo ≤ máximo.
- `BaseSalePrice` ≥ 0 o null; es el precio base SSOT que consume Pricing. Cambiarlo genera `ItemPriceChangedEvent` solo si el valor cambia. La confirmación de una compra puede actualizarlo ([purchases.md § 4](./purchases.md#4-confirmación)).
- Impuestos especiales (ICE/IRBPNR): a lo sumo una configuración por categoría tributaria SRI.

## 5. Variantes, empaques y conversiones

- Al crear un Item se crea su variante por defecto con el SKU del Item. Variantes adicionales: SKU (override ≤ 80 o derivado de los atributos de eje) y combinación de atributos únicos entre activas.
- Empaques: un Product exige exactamente una presentación base; un Service admite como máximo una. La presentación base tiene cantidad base 1. No se repite UOM + cantidad base.
- No se puede quitar un empaque usado por códigos de proveedor ni por documentos confirmados, ni cambiar la cantidad base de uno usado en documentos confirmados (se crea uno nuevo).
- Conversiones de UOM: origen, destino y factor > 0. Sustitutos: un Item no es sustituto de sí mismo.

## 6. Ciclo de vida

- Deshabilitar o habilitar (`IsActive`), nunca borrar físico. Variantes, barcodes y códigos de proveedor también se desactivan. Desvincular un matching incorrecto desactiva el código aprendido no principal ([purchases.md § 3](./purchases.md#3-recepción-electrónica-y-conciliación-de-ítems)).
- La auditoría de Item (`ItemAudit`) se registra desde los eventos de dominio de creación, actualización, habilitación y precio.

## 7. Pendiente (no operativo)

- `TracksLot` / `TracksSeries` se configuran, pero ningún flujo registra lotes ni series; la carga inicial los rechaza. Ver [inventory.md § 8](./inventory.md#8-pendiente-no-operativo).
- Naturaleza `Digital`: no existe en el código.

## 8. Evidencia

Terminología de formularios: `SKU`, `Nombre comercial` (`shortName`), `Descripción completa`
(`description`), `Tipo` (`itemTypeId`, clasificación), `Naturaleza operativa` (`nature`),
`Unidad base de inventario` (`defaultUomCode`), `Marca`, `Categoría`, `Tarifa IVA venta`,
`Tarifa IVA compra`, `Código de barras`, `Tipo de código de barras`, `Código proveedor`,
`Observaciones`. Las etiquetas compartidas consumen las claves `items.*` de i18n; el marcador
de obligatoriedad pertenece a `ZHField`, no al texto de la etiqueta. Unidad de presentación y
equivalencia en unidad base describen el empaque, no la unidad base del Item. Los encabezados
Excel de carga inicial se conservan como contrato de lectura; su interfaz explica la equivalencia
con estos nombres. Nombre de importación alimenta nombre comercial y descripción completa.

La precarga de selectores asíncronos recupera el valor persistido (incluido el tipo inmutable)
sin sobrescribir campos ya editados. Evidencia y límites de validación:
[alineación de formularios](../evidence/items-forms-alignment-01/README.md).

Domain/Application (filtro Items/Inventory/Purchases, 2026-10-19): 354/354 y 551/551 PASS. PostgreSQL: `ItemCompanyScopeA1Tests` 16/16 PASS. Cobertura adicional: `ItemNatureA1Tests`, `CreateItemCommandValidatorTests`, `UpdateItemCommandHandlerTests`, `ReplaceItemPackagingLevelsTests`, `ItemVariantBarcodeTests`, `ItemSupplierCodeTests`, `RequiredItemVatTests`, `ItemTaxConfigTests`.
