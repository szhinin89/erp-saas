# Item Matching — Vinculación de Items desde Purchase Reception

**Estado**: ✅ Implementado (2026-07-27)
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
- **No se crean Items nuevos en esta fase** — una línea sin candidato queda `Pending` indefinidamente hasta que el catálogo tenga el producto o el usuario lo busque manualmente.
- **No se infieren impuestos desde el XML** — el motor de matching solo resuelve `ItemId`; los impuestos siguen viniendo exclusivamente de `Item.TaxConfig` (Infraestructura CLOSED — Configuración Tributaria).

---

## 4. Endpoints

Todos bajo `PurchaseReceptionController` (`api/v1/purchases/reception`), permiso `PurchasePermissions.View`:

| Método | Ruta | Uso |
|---|---|---|
| `GET` | `/{id}/lines` | Lista las líneas del documento con estado + sugerencias para las no resueltas |
| `POST` | `/lines/{id}/match-item` | Vinculación manual de una línea — body `{ itemId }` |
| `POST` | `/matching/bulk` | Vinculación masiva — body `[{ purchaseReceptionLineId, itemId }]` |

## 5. Estados (`ItemMatchStatus`)

| Estado | Significado |
|---|---|
| `Pending` | Sin ningún candidato por encima del umbral |
| `NeedsReview` | Hay sugerencias (descripción/similitud) pero ninguna por código exacto — requiere confirmación humana |
| `AutoMatched` | Resuelto automáticamente por código de proveedor exacto al persistir la línea |
| `ManuallyMatched` | Confirmado por el usuario — individual o en lote |

---

## 6. Evolución futura (fuera de alcance de esta fase)

- **Creación rápida de Items desde una línea sin match**: hoy una línea `Pending` sin candidato queda así indefinidamente; una fase futura podría ofrecer un atajo para crear el Item directamente desde el panel de matching, precargando SKU/descripción/proveedor — requiere su propio diseño (validaciones, impuestos obligatorios, categoría) y no se implementa en esta fase por decisión explícita del alcance.
- **Historial de precio de compra por proveedor**: `ItemSupplierCode` sigue sin campo de precio (ver auditoría original) — Item Matching no lo introduce.
- **Auditoría de dominio dedicada** (`PurchaseReceptionLineAudit`): evaluada y diferida — la infraestructura CLOSED de Entity Audit (`AI-RULES/AUDIT-INFRASTRUCTURE.md`) ya soporta agregarla sin tocar sus contratos FROZEN cuando el volumen de uso lo justifique.
