# Items — Fase 1: Información Base del Item (Identidad)

**Estado**: ✅ FASE 1 COMPLETADA
**Fecha de cierre**: 2026-07-02
**Nivel documental**: 3 (detalle técnico especializado, referenciado desde [`STATUS.md`](../../STATUS.md))

Este documento es la referencia oficial de las decisiones funcionales y técnicas de la Fase 1 del módulo Items. Las fases siguientes (Identificación, Tributación, Comercial, Inventario, Variantes, Pricing, Compras) se construyen sobre esta base sin reabrir lo aquí definido.

---

## 1. Resumen de la fase

**Objetivo**: definir de forma completa y cerrada la identidad del ítem — los atributos que lo identifican de forma única y estable dentro del catálogo, independientemente de cualquier otro dominio (tributación, precio, stock, variantes).

**Alcance**: SKU, Descripción corta y larga, Tipo de Item (`ItemType`), Marca, Categoría y el árbol dinámico de categorías, junto con las restricciones de edición y unicidad de estos atributos.

**Fuera de alcance de esta fase** (pertenecen a fases posteriores, no fueron tocados): códigos de barra, códigos de proveedor, impuestos, precio inicial, configuración de stock/venta, variantes, listas de precio, integración de compras.

---

## 2. Decisiones funcionales aprobadas

| # | Decisión | Estado |
|---|----------|--------|
| 1 | El SKU es **editable** después de creado el ítem (no inmutable). Justificación: el identificador relacional real del agregado `Item` es su `Id` (Guid) — todas las relaciones del sistema (variantes, precios, barcodes, líneas de documentos) enlazan por `ItemId`, nunca por SKU. El SKU es un atributo de negocio único pero mutable. | ✅ Aprobada |
| 2 | El SKU es **único por tenant**, garantizado con índice único a nivel de base de datos (no solo verificación de aplicación). La unicidad se valida también en edición, excluyendo el propio ítem. | ✅ Aprobada |
| 3 | El SKU es **case-insensitive** (normalizado a mayúsculas) — sin cambios respecto al comportamiento previo. | ✅ Aprobada (sin cambio) |
| 4 | Editar el SKU del ítem padre **no afecta** el SKU ya asignado a sus variantes existentes — el SKU de variante es independiente, no una referencia (FK) al SKU del ítem. | ✅ Aprobada |
| 5 | El `ItemType` permanece **inmutable** después de creado. | ✅ Aprobada (sin cambio) |
| 6 | `CategoryNodeId` y `BrandId` son **obligatorios** en la creación del ítem, para **todos** los valores de `ItemType`, sin excepciones condicionales. | ✅ Aprobada |
| 7 | La Marca debe **existir y estar activa** — validado en el handler de creación contra el repositorio de marcas (antes solo se exigía no-nulo). | ✅ Aprobada |
| 8 | La Categoría debe ser un **nodo hoja** (sin hijos activos), existir, estar activa y no pertenecer a una rama con ancestro deshabilitado. | ✅ Aprobada (sin cambio, regla ya vigente) |
| 9 | El selector de categoría muestra la **ruta completa (breadcrumb)** — "Línea > Categoría > Subcategoría" — en vez de solo el nombre del nodo hoja, para eliminar ambigüedad entre nodos con nombres iguales en ramas distintas. | ✅ Aprobada |
| 10 | La profundidad máxima del árbol de categorías es un **parámetro configurable por empresa** (default 3 niveles), implementado sobre la infraestructura ya CLOSED de Org Config Jerárquica (`OrgSettings`). | ✅ Aprobada |
| 11 | Crear un nodo de categoría que excedería la profundidad máxima configurada se **rechaza** (validación estricta, ningún nodo se crea). Adicionalmente, un nodo que ya está en el nivel máximo **no se ofrece como padre disponible** para crear un hijo nuevo (tanto en el backend como en la UI de administración del catálogo). | ✅ Aprobada |
| 12 | La descripción del ítem se mantiene como `Description` (254 caracteres) + `Observations` (libre) — **no se agrega** un campo de descripción extendida en esta fase. Las "características" del ítem a futuro deben usar los campos JSONB ya existentes en el dominio (`Specifications`/`MarketingAttributes`), no un campo de texto libre nuevo. | ✅ Aprobada |
| 13 | `CategoryNodeId` y `BrandId` tienen ahora **integridad referencial real** (FK en base de datos, `OnDelete(Restrict)`) hacia `item_category_nodes` y `brands` respectivamente — antes eran columnas UUID sin ninguna restricción declarada. | ✅ Aprobada |

---

## 3. Reglas de dominio (invariantes)

1. Un `Item` debe tener un `SKU` único por `tenant_id` (garantizado por índice único de base de datos).
2. El `SKU` puede editarse en cualquier momento posterior a la creación, siempre que el nuevo valor siga siendo único por tenant.
3. Editar el `SKU` de un `Item` no modifica el `SKU` de sus `ItemVariant` existentes.
4. El `ItemType` de un `Item` no puede modificarse después de creado.
5. Un `Item` debe pertenecer a exactamente un `CategoryNodeId`, y ese nodo debe ser una **hoja** (sin hijos activos) del árbol de categorías.
6. La categoría asignada a un `Item` debe existir, estar activa, y no pertenecer a una rama con algún ancestro deshabilitado.
7. Un `Item` debe pertenecer a exactamente una `BrandId`, que debe existir y estar activa.
8. `CategoryNodeId` y `BrandId` son obligatorios al crear un `Item`, para cualquier `ItemType`.
9. El árbol de categorías (`ItemCategoryNode`) es dinámico y N-ario — no tiene niveles fijos predefinidos en el dominio (`CategoryNodeLevel` es descriptivo, no estructural).
10. La profundidad máxima del árbol de categorías es configurable por empresa (`OrgSettings`, clave `catalog.max_category_depth`, default 3 niveles). Ningún nodo puede crearse si excede esa profundidad.
11. Una categoría no puede deshabilitarse mientras tenga ítems activos asignados (regla preexistente, confirmada sin cambios en esta fase).

---

## 4. Impacto arquitectónico

**Módulos NO afectados por esta fase**: Ventas, Compras, Inventario, Pricing, Facturación Electrónica, Variantes.

**Por qué no hay impacto**: todas las relaciones de estos módulos hacia `Item` usan `ItemId` (Guid, clave relacional real del agregado) — nunca `SKU`. Hacer el SKU editable no rompe ninguna referencia existente porque ningún módulo lo usa como clave foránea. Las reglas de Categoría/Marca (obligatoriedad, FK, profundidad) son exclusivas del proceso de creación/clasificación del ítem y del subsistema de catálogo de categorías; no alteran el comportamiento de líneas de compra/venta, cálculo de stock, ni motor de precios.

---

## 5. Cambios técnicos realizados

**Backend**: nuevo método de dominio `Item.UpdateSku()`; `UpdateItemCommand`/`Handler`/`Validator` ampliados con `SKU` y validación de unicidad excluyendo el propio ítem; `CreateItemCommandHandler` ampliado con validación de existencia/estado de marca (`ValidateBrandAsync`); `CreateCategoryNodeCommandHandler` ampliado con validación de profundidad máxima contra `OrgSettings`; nueva clave `OrgSettingKeys.Catalog.MaxCategoryDepth`; `CategoryNodeDto`/`CategoryTreeDto` ampliados con `Depth`/`MaxDepth`.

**Frontend**: SKU visible y editable también en modo edición del formulario; selector de categoría con breadcrumb completo (usa el campo `path` ya expuesto por el backend); administrador de catálogo de categorías (`TreeEditor`) oculta la opción de agregar hijo en nodos que alcanzaron la profundidad máxima.

**Base de datos**: índice único `(tenant_id, sku)` sobre `items`; FK `items.category_node_id → item_category_nodes.id` (`Restrict`); FK `items.brand_id → brands.id` (`Restrict`).

**Migraciones**: `Fase1ItemIdentityHardening` — verificada previamente la ausencia de SKUs duplicados y referencias huérfanas de categoría/marca en la base de datos antes de aplicarse.

**Validaciones**: unicidad de SKU (aplicación + BD, en creación y edición); existencia/estado activo de marca (creación); profundidad máxima de categoría (creación de nodo).

**Tests**: 62 tests de dominio + 23 de aplicación en verde (suite completa, incluyendo los agregados en el rediseño previo del flujo de creación). Verificación adicional de arranque de API confirmando resolución correcta de las nuevas dependencias inyectadas.

---

## 6. Riesgos conocidos (a revisar en fases posteriores, no resueltos aquí)

- El campo `CategoryNodeLevel` (`Family/Category/Subcategory/Custom`) sigue siendo puramente descriptivo — nada impide estructuralmente que un nodo de nivel "Subcategory" sea padre de un nodo "Family". No se corrigió en esta fase por no haber sido solicitado; queda documentado como hallazgo de auditoría previa.
- El método `ItemCategoryNode.Reparent()` existe en el dominio pero no tiene ningún punto de invocación (código muerto) y no cuenta con detección de ciclos. Si se habilita en el futuro, requiere revisión de recomputo de `Path` para descendientes.
- Si un ítem creado antes de esta fase tiene `CategoryNodeId`/`BrandId` en `null` (dato heredado, previo a que fueran obligatorios), permanece editable sin forzar backfill — comportamiento intencional, pero a tener en cuenta si una fase futura decide requerir backfill retroactivo.

---

## 7. Pendientes — pertenecen a otras fases (no tratados aquí)

- **Fase 2 — Identificación**: códigos de barra, código principal, códigos de proveedor, unicidad de barcode.
- **Fase 3 — Tributación**: IVA venta/compra, ICE, catálogos SRI.
- **Fase 4 — Comercial**: precio inicial, lista de precios, descuento máximo.
- **Fase 5 — Inventario y Venta**: TracksStock, lotes, series, decimales, stock mínimo/máximo, disponibilidad POS/Web/Mobile.
- **Fase 6 — Variantes**: atributos, SKU de variante, barcode de variante, imágenes.
- **Fase 7 — Pricing**: `ItemPrice`, `PriceList`, historial, simulación.
- **Fase 8 — Compras**: relación con proveedor, integración de compras.

---

## 8. Estado de la fase

**Estado: ✅ FASE 1 COMPLETADA**

**Resultado**: la identidad del Item quedó completamente definida y documentada. Las siguientes fases podrán construirse sobre esta base sin modificar las decisiones tomadas en esta fase.
