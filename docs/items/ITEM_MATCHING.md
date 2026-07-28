# Item Matching — Vinculación de Items desde Purchase Reception

**Estado**: ✅ Implementado (2026-07-27) — ampliado con "Create Item From Purchase Reception" (Fase 2.1, 2026-07-27)
**Nivel documental**: 3 (detalle técnico especializado)
**Precede a este documento**: [`docs/items/ITEM_MATCHING_AUDIT.md`](ITEM_MATCHING_AUDIT.md) — auditoría que identificó el vacío funcional cerrado aquí.

Cierra el ciclo de conciliación entre las líneas de un comprobante de compra recibido del SRI (`PurchaseReception`) y el catálogo de Items del ERP: persiste las líneas del XML, resuelve automáticamente los productos ya conocidos, sugiere candidatos por similitud para el resto, y permite confirmar la vinculación individual o masivamente — creando la relación proveedor↔ítem (`ItemSupplierCode`, Fase 8 de Items) como efecto de la confirmación.

---

## 1. Arquitectura

```
XML autorizado SRI (PurchaseReceptionDocument.XmlContent)
        │  IPurchaseXmlDraftParser.Parse()  (reutilizado — ya existía para "Crear compra")
        ▼
PurchaseReceptionLine[]  (persistidas al verificar el documento)
        │
        │  ItemMatchFinder  (ERP.Application/Modules/Inventory/ItemMatching)
        │    1. Código de proveedor exacto  → ItemSupplierCode
        │    2. Código auxiliar exacto      → ItemSupplierCode
        │    3. Descripción normalizada     → igualdad exacta sobre candidatos por similitud
        │    4. Similitud de texto (pg_trgm) → EF.Functions.TrigramsSimilarity
        ▼
MatchStatus: Pending | NeedsReview | AutoMatched | ManuallyMatched
        │
        │  Confirmación (individual o masiva) → ItemMatchConfirmationService
        ▼
PurchaseReceptionLine.ItemId resuelto + ItemSupplierCode creado (si no existía)
```

**Reutilización explícita — no se creó ningún componente paralelo:**

- El parseo del detalle XML (`codigoPrincipal`, `codigoAuxiliar`, `descripcion`, `cantidad`, `precioUnitario`, IVA informativo) es el mismo `PurchaseXmlDraftParser`/`ParsedPurchaseXmlLine` que ya usaba el flujo manual "Crear compra" — no existe un DTO ni un parser duplicado.
- La relación proveedor↔ítem sigue siendo exclusivamente `ItemSupplierCode` (Fase 8 de Items, FROZEN) — Item Matching solo la crea a través de `Item.AddSupplierCode(...)`, nunca reimplementa la relación.
- La búsqueda manual de un ítem alternativo reutiliza `ProductPicker.tsx` (ya usado en el formulario de compra manual) — no se creó un segundo selector.
- El endpoint de búsqueda de Items (`GET /api/v1/items?search=`) no cambió — Item Matching resuelve candidatos por su propio motor de similitud contra el repositorio, no reimplementa ese endpoint.

---

## 2. Flujo funcional

1. El usuario descarga el XML autorizado de un comprobante (`POST /reception/{id}/download-xml`, ya existente).
2. En el mismo momento, el handler parsea el detalle del XML y persiste una `PurchaseReceptionLine` por cada `<detalle>`:
   - Si el `codigoPrincipal` de la línea coincide con un `ItemSupplierCode` ya registrado para el proveedor del documento → la línea nace con `ItemId` resuelto y `MatchStatus = AutoMatched`, sin intervención del usuario.
   - Si no hay código exacto pero el motor de matching encuentra candidatos por descripción/similitud → `MatchStatus = NeedsReview`.
   - Si no hay ningún candidato → `MatchStatus = Pending`.
3. El usuario abre "Vincular productos" sobre el documento (solo visible en estado `VERIFIED`/`PROCESSED`) y ve, por cada línea sin resolver, hasta 5 sugerencias ordenadas por score.
4. Puede aceptar la sugerencia top, buscar manualmente con `ProductPicker`, y aplicar una línea a la vez o varias en lote.
5. Al confirmar, si el código de proveedor de la línea todavía no tenía un `ItemSupplierCode` registrado para ese proveedor, se crea uno nuevo — la próxima factura del mismo proveedor con el mismo código resolverá automáticamente en el paso 2.

Una línea inválida dentro de un lote (ítem inexistente, línea ya no encontrada) no aborta las demás — se reporta individualmente.

---

## 3. Decisiones (resumen — detalle completo en el plan de implementación)

- **Auto-resolución solo por código exacto de proveedor** — nunca por similitud, para no vincular un ítem incorrecto sin revisión humana.
- **`ItemSupplierCode` se crea únicamente al confirmar** (automático o manual) — nunca de forma especulativa por una simple sugerencia.
- **Umbral de similitud**: `0.35` (valor por defecto de `pg_trgm`) para que una línea pase de `Pending` a `NeedsReview`.
- **No se infieren impuestos desde el XML** — el motor de matching solo resuelve `ItemId`; los impuestos siguen viniendo exclusivamente de `Item.TaxConfig` (Infraestructura CLOSED — Configuración Tributaria).
- **Creación de Items nuevos**: soportada desde una línea sin match (ver sección 7) — pero solo individual, nunca masiva ni con datos inferidos más allá de lo que documenta esa sección.

---

## 4. Endpoints

Todos bajo `PurchaseReceptionController` (`api/v1/purchases/reception`), permiso `PurchasePermissions.View`:

| Método | Ruta | Uso |
|---|---|---|
| `GET` | `/{id}/lines` | Lista las líneas del documento con estado + sugerencias para las no resueltas |
| `POST` | `/lines/{id}/match-item` | Vinculación manual de una línea — body `{ itemId }` |
| `POST` | `/matching/bulk` | Vinculación masiva — body `[{ purchaseReceptionLineId, itemId }]` |
| `POST` | `/lines/{id}/create-item` | Crea un Item nuevo desde la línea y la vincula (sección 7) — body `{ sku, shortName, description, itemTypeId, categoryNodeId, brandId, defaultUomCode, barcodeType }` |

## 5. Estados (`ItemMatchStatus`)

| Estado | Significado |
|---|---|
| `Pending` | Sin ningún candidato por encima del umbral |
| `NeedsReview` | Hay sugerencias (descripción/similitud) pero ninguna por código exacto — requiere confirmación humana |
| `AutoMatched` | Resuelto automáticamente por código de proveedor exacto al persistir la línea |
| `ManuallyMatched` | Confirmado por el usuario — individual o en lote |

---

## 7. Create Item From Purchase Reception (Fase 2.1)

Cierra el caso en que la línea no corresponde a ningún Item existente: en vez de dejarla `Pending` indefinidamente, el usuario puede crear el Item directamente desde el panel de matching (`ZHItemMatchingPanel` → botón "Crear Item" por línea).

### Flujo

1. El usuario abre "Crear Item" sobre una línea `Pending`/`NeedsReview` sin `ItemId`.
2. El modal (`CreateItemFromLineModal.tsx`) prellena SKU/nombre corto/descripción desde la línea y pide los campos que el XML no trae: Tipo de ítem, Categoría, Marca, Unidad de medida y Tipo de código de barras (catálogos ya existentes, `GET /api/v1/catalog/brands|category-nodes|barcode-types|sri-uom` + `useItemTypeOptions()` — los mismos que usa el formulario completo de Items).
3. Al confirmar, `POST /api/v1/purchases/reception/lines/{id}/create-item` ejecuta `CreateItemFromReceptionLineCommandHandler`, que:
   - Construye un `CreateItemCommand` (módulo Items) con el código de barras derivado en el propio backend — `SupplierAuxCode ?? SupplierCode` de la línea — y lo envía vía `IMediator.Send(...)`, heredando **toda** la validación y los conflictos (`SKU_DUPLICATE`, `BARCODE_DUPLICATE`, tipo/categoría/marca inexistentes) del módulo Items sin reimplementarlos.
   - Si la creación del Item tiene éxito, llama a `ItemMatchConfirmationService.ConfirmAsync(...)` — el mismo servicio que usa la vinculación manual/masiva (Fase 2.0) — para crear `ItemSupplierCode` (si no existía ya para ese proveedor+código) y marcar la línea `ManuallyMatched` con `MatchedAt`/`MatchedBy`.

### Restricciones (verificadas contra las reglas ya cerradas del módulo Items — no relajadas)

- Solo permitido sobre líneas `Pending`/`NeedsReview` sin `ItemId` — una línea ya resuelta (`AutoMatched`/`ManuallyMatched`) responde `ITEM_ALREADY_MATCHED`.
- **Código de barras**: derivado en el backend como `SupplierAuxCode ?? SupplierCode` de la línea (el auxiliar del XML suele ser el código de barras real; si no viene, se usa el principal) — el usuario solo elige el *tipo*. Si la línea no trae ningún código, la operación falla explícitamente (no se inventa un valor).
- **Sin costo automático**: `Item` no tiene ningún campo de costo — `BaseSalePrice` (precio de **venta**, SSOT de Pricing Engine v2 CLOSED) nunca se completa desde `UnitPrice` de la línea. El costo se muestra en el modal como referencia informativa; el costo real del ítem se calculará más adelante vía `CurrentStock.AverageCost` cuando exista un movimiento de stock real (una compra).
- **Sin tax config automático**: `SaleVatCode`/`PurchaseVatCode`/`ExciseTaxCode` quedan `null` — no se infieren del XML (Infraestructura CLOSED — Configuración Tributaria). El usuario los completa después en la edición normal del Item.
- **Reutilización, no reimplementación**: la validación de creación (`CreateItemCommandValidator`), la generación de la relación proveedor (`ItemSupplierCode`, vía `ItemMatchConfirmationService`) y los catálogos de selección (Tipo/Categoría/Marca/UOM/Barcode type) son exactamente los mismos que usa el alta manual de Items — no existe un segundo camino de creación.

### Fuera de alcance — Fase 2.2 (Bulk Item Creation)

- Creación masiva de Items desde múltiples líneas a la vez.
- Detección avanzada de duplicados antes de crear (más allá del `SKU_DUPLICATE`/`BARCODE_DUPLICATE` ya heredado de Items).
- Sugerencia automática de categoría/UOM a partir del historial del proveedor o de ítems similares.
- Creación automática de precio de venta (`BaseSalePrice`) — permanece una decisión manual del usuario, ver Estándar de Precisión Numérica y Pricing Engine v2.

---

## 8. Evolución futura (fuera de alcance de esta fase)

- **Historial de precio de compra por proveedor**: `ItemSupplierCode` sigue sin campo de precio (ver auditoría original) — Item Matching no lo introduce.
- **Auditoría de dominio dedicada** (`PurchaseReceptionLineAudit`): evaluada y diferida — la infraestructura CLOSED de Entity Audit (`AI-RULES/AUDIT-INFRASTRUCTURE.md`) ya soporta agregarla sin tocar sus contratos FROZEN cuando el volumen de uso lo justifique.
- **Bulk Item Creation** (Fase 2.2): ver sección 7.
