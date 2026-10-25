# Ítems: conflictos de códigos unificados

Fecha: 2026-10-24. Clasificación: EXTEND. Alcance: todos los puntos de alta/edición de Ítems.

Regla vigente: [Ítems § Identidad y unicidad](../../architecture/items.md#2-identidad-y-unicidad).
Componente DS: [frontend.md](../../architecture/frontend.md) (`ZHCodeConflicts`).

## Capturas

Playwright sobre un arnés temporal (retirado) con los componentes reales `ItemFormTabs` y
`ResolvePendingProductsModal`, i18n español y CSS del DS. Solo la respuesta de
`POST /api/v1/items/code-conflicts` es fixture; sin sesión, base ni datos reales.

- `items-create-conflicts.png`: alta de Ítem; SKU y barcode duplicados con propietario y «Ver ítem».
- `purchases-resolve-conflicts.png`: Resolver productos pendientes; barcode y código de proveedor
  existentes con propietario, y barcode repetido entre las líneas 2 y 3 del lote (sin enlace).
- `purchases-resolve-conflicts-mobile.png`: misma modal a 390 × 844.

Comprobado en la captura: «Ver ítem» abre una pestaña nueva en `/products/items?view={id}`;
el formulario de origen conserva el SKU tecleado y la modal de Compras sigue abierta.

LIMITACIÓN VISUAL: el arnés no cargó Material Symbols ni el CSS de página de Compras (iconos
como texto «close», barra masiva apilada). No afecta a los avisos, que usan CSS real del DS.

## Validación

| Comprobación | Resultado |
|---|---|
| Application focal (Items, ItemMatching, InitialLoad, handler nuevo) | PASS 423/423 |
| PostgreSQL: `ItemRepositorySupplierCodeMatchTests` + `ResolvePurchaseReceptionLinesIntegrationTests` | PASS 22/22 |
| Frontend focal (items, purchases, initialLoad, components/items) | PASS 750/750 + 5 nuevas |
| Architecture.Tests backend | PASS 143/143 |
| TypeScript `tsc -b`, build | PASS |
| ESLint | PASS; 0 errores, 35 advertencias existentes |
| `architecture:check` | PASS; 0 nuevas infracciones |

PostgreSQL cubre propietario por SKU, SKU de variante, barcode de variante y de empaque, código
de proveedor por proveedor (otro proveedor e inactivo no bloquean), exclusión del propio registro,
otra empresa y otro tenant. La clase ya estaba registrada en `tools/ci/backend-test-groups.json`.
