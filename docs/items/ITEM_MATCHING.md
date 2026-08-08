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

La creación de Items desde una línea (sección 7) no tiene endpoint propio — compone `POST /api/v1/items` (Items, genérico) + `POST /lines/{id}/match-item` (arriba).

## 5. Estados (`ItemMatchStatus`)

| Estado | Significado |
|---|---|
| `Pending` | Sin ningún candidato por encima del umbral |
| `NeedsReview` | Hay sugerencias (descripción/similitud) pero ninguna por código exacto — requiere confirmación humana |
| `AutoMatched` | Resuelto automáticamente por código de proveedor exacto al persistir la línea |
| `ManuallyMatched` | Confirmado por el usuario — individual o en lote |

---

## 7. Create Item From Purchase Reception (Fase 2.1 / 2.1.1)

Cierra el caso en que la línea no corresponde a ningún Item existente: en vez de dejarla `Pending` indefinidamente, el usuario puede crear el Item directamente — desde el panel de matching de Recepción SRI (`ZHItemMatchingPanel`) **o** desde una línea de compra manual sin producto (`PurchasesPage.tsx`) — con el mismo componente reutilizable.

### Arquitectura (revisada en Fase 2.1.1 — sin backend propio)

La Fase 2.1 original introdujo un endpoint compuesto (`CreateItemFromReceptionLineCommand`) que creaba el Item y lo vinculaba a la línea en una sola llamada. La Fase 2.1.1 lo **eliminó** porque el requisito de reutilización ("`CreateItemModal` no debe conocer Compras") solo se cumple si el componente de creación depende exclusivamente del contrato genérico de Items — y ese endpoint compuesto ya asumía una `PurchaseReceptionLineId`. Se comprobó que no hacía falta: los dos primitivos ya existentes alcanzan.

```
frontend/src/components/items/CreateItemModal/   (genérico — no importa nada de Compras)
        │  POST /api/v1/items  (CreateItemCommand, ya existente — acepta supplierCodes en el mismo alta)
        ▼
Item creado (+ ItemSupplierCode si initialData trajo supplierId+supplierCode)
        │
        │  (solo en el wrapper de Purchase Reception)
        ▼
POST /api/v1/purchases/reception/lines/{id}/match-item   (MatchItemCommand, Fase 2.0, ya existente)
        │
        ▼
Línea ManuallyMatched (MatchedAt/MatchedBy) — ItemMatchConfirmationService no duplica el
ItemSupplierCode porque SupplierCodeExistsAsync ya lo encuentra creado en el paso anterior.
```

- **Componente genérico** — `frontend/src/components/items/CreateItemModal/` (`CreateItemModal.tsx`, `CreateItemForm.tsx`, `createItemSchema.ts`, `types.ts`): recibe `CreateItemInitialData` opcional (nombre, código de barras, código/nombre/id de proveedor, UOM), llama únicamente `itemService.create(...)` (`POST /api/v1/items`), y devuelve el Item creado vía `onCreated`. No importa nada de `modules/purchases`.
- **Wrapper de Recepción SRI** — `frontend/src/modules/purchases/components/CreateItemFromReceptionLineModal.tsx`: arma el `initialData` desde la línea (`barcode: supplierAuxCode ?? supplierCode`), y tras `onCreated` llama `purchaseReceptionService.matchItem(lineId, item.id)` para vincular — reutiliza el endpoint de vinculación manual de la Fase 2.0, no reimplementa nada.
- **Integración en Compra manual** — `PurchasesPage.tsx` (`PurchaseLineCard`): usa el `CreateItemModal` **directamente**, sin wrapper — una línea de compra manual no tiene `PurchaseReceptionLineId`, así que no hay nada que vincular en el backend de Compras; el ítem creado simplemente se selecciona en la línea (mismo patrón que `ProductPicker.onSelect`).

### Restricciones (verificadas contra las reglas ya cerradas del módulo Items — no relajadas)

- Solo permitido sobre líneas sin `ItemId` — el botón "Crear Item" no aparece si la línea ya está `AutoMatched`/`ManuallyMatched`.
- **Código de barras**: el *valor* lo decide quien invoca el modal (`initialData.barcode`, típicamente `supplierAuxCode ?? supplierCode` de la línea) — el usuario solo elige el *tipo* (catálogo tenant-editable, sin default seguro posible). Es un campo editable del formulario, no una inferencia oculta.
- **Sin costo automático**: `Item` no tiene ningún campo de costo — `BaseSalePrice` (precio de **venta**, SSOT de Pricing Engine v2 CLOSED) nunca se completa desde el costo de compra. El costo real del ítem se calculará más adelante vía `CurrentStock.AverageCost` cuando exista un movimiento de stock real.
- **Sin tax config automático**: `SaleVatCode`/`PurchaseVatCode`/`ExciseTaxCode` quedan `null` — no se infieren del XML (Infraestructura CLOSED — Configuración Tributaria). El usuario los completa después en la edición normal del Item.
- **`ItemSupplierCode` sin duplicar**: se crea en el mismo alta del Item cuando `initialData` trae `supplierId`+`supplierCode`; si el wrapper de Recepción SRI además llama `matchItem`, `ItemMatchConfirmationService` detecta que ya existe (`SupplierCodeExistsAsync`) y no lo repite.
- **Reutilización, no reimplementación**: validación de creación (`CreateItemCommandValidator`), catálogos de selección (Tipo/Categoría/Marca/UOM/Barcode type) y la relación proveedor son exactamente los mismos que usa el alta manual completa de Items.
- **No atómico entre creación y vinculación** (solo aplica al wrapper de Recepción SRI): son dos llamadas HTTP separadas. Si la segunda (`matchItem`) falla tras crear el Item, se informa explícitamente ("Item creado, pero no se pudo vincular automáticamente") en vez de perder el Item o fingir una falla total — el usuario puede vincularlo manualmente después con "Vincular".

### Fuera de alcance — Fase 2.2 (Bulk Item Creation)

- Creación masiva de Items desde múltiples líneas a la vez.
- Detección avanzada de duplicados antes de crear (más allá del `SKU_DUPLICATE`/`BARCODE_DUPLICATE` ya heredado de Items).
- Sugerencia automática de categoría/UOM a partir del historial del proveedor o de ítems similares.
- Creación automática de precio de venta (`BaseSalePrice`) — permanece una decisión manual del usuario, ver Estándar de Precisión Numérica y Pricing Engine v2.

---

## 8. Evolución futura (fuera de alcance de esta fase)

- **Historial de precio de compra por proveedor**: `ItemSupplierCode` sigue sin campo de precio (ver auditoría original) — Item Matching no lo introduce.
- **Auditoría de dominio dedicada** (`PurchaseReceptionLineAudit`): evaluada y diferida — la infraestructura CLOSED de Entity Audit (`docs/architecture/audit-infrastructure.md`) ya soporta agregarla sin tocar sus contratos FROZEN cuando el volumen de uso lo justifique.
- **Bulk Item Creation** (Fase 2.2): ver sección 7.
