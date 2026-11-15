# PURCHASES-NEW-TAB-PHASE-A-FIX-01 — evidencia

Corrección de dos defectos de la barra del detalle de productos (`/purchases`, pestaña «Nueva compra») introducidos por la Fase A.

## Método y límites

- Página real (`PurchasesPage`, CSS real, `LayoutFrame` real) montada en un servidor Vite temporal, con **API simulada** y datos ficticios. No se usó la API real, sesión ni credenciales; no se guardó ni confirmó ninguna compra.
- Navegador: Microsoft Edge vía Playwright, viewport indicado en cada medida. Pantalla vacía (formulario nuevo), donde «Distribuir flete/gasto» está deshabilitado y por tanto «Ver motivo» está presente.
- Alturas en px (`getBoundingClientRect`). «Filas» = filas reales de controles de la barra (36 px de alto, gap 12 px); el alto esperado es `filas × 36 + (filas - 1) × 12`.

## Causas

1. **Barra de 84 px:** `.pdl-toolbar` (flex-wrap) es un ítem de la columna flex `.pdl-topbar--split`. Su alto se reserva midiéndolo antes de estirarlo al ancho final; con el buscador en `flex: 1 1 260px` (basis ≥ su max-content, ≈227 px) esa medición produce dos líneas (36 + 12 + 36 = 84). En el ancho final todo cabe en una línea y los controles quedan centrados en un alto ya reservado (48 px de aire). Confirmado: `width: 100%` en la barra, o `display: block/grid` en el contenedor, dan 36 px; `row-gap: 0` baja a 72; basis ≤ 200 px o `nowrap` también dan 36 px.
2. **«Ver motivo» a todo el ancho:** la raíz de `PurchaseActionReason` es `.zh-stack` (columna flex, `align-items: stretch` por defecto) y es hija de `.pdl-topbar--split` (`align-items: stretch`), por lo que el botón se estiraba al ancho del encabezado.

## Corrección (solo `frontend/src/modules/purchases/styles/purchases-invoice.css`)

- `.pdl-toolbar` recibe `width: 100%`.
- `.pdl-topbar--split > .zh-stack` recibe `align-items: flex-start`.

## Medidas antes → después (pantalla vacía)

| Ancho | Barra alto antes | Barra alto después | Filas antes → después | «Ver motivo» ancho antes | «Ver motivo» ancho después | Ancho del contenedor | Alto del encabezado del detalle | Overflow X antes / después |
|---|---|---|---|---|---|---|---|---|
| 1440 | 84 | 36 | 1 → 1 | 1368 | 104 | 1368 | 186 → 138 | 0 / 0 |
| 1280 | 84 | 36 | 1 → 1 | 1208 | 104 | 1208 | 186 → 138 | 0 / 0 |
| 1100 | 84 | 36 | 1 → 1 | 1028 | 104 | 1028 | 186 → 138 | 0 / 0 |
| 1024 | 84 | 36 | 1 → 1 | 952 | 104 | 952 | 186 → 138 | 0 / 0 |
| 900 | 84 | 36 | 1 → 1 | 828 | 104 | 828 | 186 → 138 | 0 / 0 |
| 768 | 84 | 36 | 1 → 1 | 696 | 104 | 696 | 186 → 138 | 0 / 0 |
| 640 | 132 | 132 | 3 → 3 | 568 | 104 | 568 | 234 → 234 | 0 / 0 |
| 500 | 132 | 132 | 3 → 3 | 428 | 104 | 428 | 234 → 234 | 0 / 0 |
| 390 | 132 | 132 | 3 → 3 | 318 | 104 | 318 | 234 → 234 | 0 / 0 |
| 360 | 132 | 132 | 3 → 3 | 288 | 104 | 288 | 234 → 234 | 0 / 0 |

Anchos intermedios (después): 700, 660 y 650 px → 1 fila / 36 px; 641 px → 2 filas / 84 px (ajuste legítimo; esperado 84). El ajuste a 3 filas en móvil (≤ 640 px) se conserva (132 px).

## Archivos

- `before-empty-1440.png`, `before-empty-390.png` — antes de la corrección.
- `after-empty-1440.png`, `after-empty-1024.png`, `after-empty-768.png`, `after-empty-390.png` — después.
- `after-draft-products-1440.png` (borrador con productos), `after-readonly-1440.png` (compra confirmada, solo lectura), `after-inactive-supplier-1440.png` (error de proveedor inactivo).
- `after-reason-open-1440.png`, `after-reason-open-390.png` — «Ver motivo» desplegado (`aria-expanded` verdadero tras Enter; Espacio lo cierra).
