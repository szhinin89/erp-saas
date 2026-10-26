# Kardex — selector de productos y alineación

Continuación sobre el diff local de UX/UI. Implementado para revisión; **pendiente del gate del propietario**, no CLOSED. Sin staging, commit, push ni deploy.

## Causa comprobada y alcance

**BUG REAL:** las clases `pf-picker-dropdown`, `pf-picker-item` y sus descendientes usadas en el selector no tienen definición en ningún CSS vigente. El dropdown participaba del flujo normal, carecía de superficie/borde/scroll y SKU/nombre se concatenaban visualmente. La reproducción antes del fix falló a ambos anchos: a 390 px Bodega/Desde/Hasta bajaban 340 px; a 1440 px la fila de filtros crecía a 401 px. [Log previo](../selector-before.log).

**KEEP:** hook de búsqueda, debounce, resultados durante carga, selección del objeto original, botones nativos y su activación Tab + Enter/Espacio, filtros, documentos y resto de los cambios locales previos. No se añade navegación con flechas ni indicador nuevo de carga/vacío. No se modifica inventario, valoración, precisión, API ni backend.

**Reutilización:** revisados `erp-form-core.css`, `zh-ui.css`, `ItemLookupPicker`, `ZHPickerResultItem`, `ZHField` y `LayoutFrame`. El DS ya dispone del panel y las filas necesarios. No se modifican componentes globales ni se crea otro selector.

## Archivos de esta continuación

- `frontend/src/modules/inventory/kardex/pages/KardexPage.tsx`: selector existente con `zh-picker__dropdown` y `ZHPickerResultItem`; SKU técnico y nombre comercial en líneas diferenciadas. Mismo callback y condición de visibilidad.
- `frontend/src/modules/inventory/kardex/pages/KardexPage.css`: solo dos líneas añadidas (comentario + ajuste de palabras largas en los resultados de producto).
- `frontend/src/modules/inventory/kardex/pages/KardexPage.test.tsx`: cinco regresiones focales de búsqueda, selección, cero resultados y carga.
- `frontend/src/modules/inventory/kardex/pages/KardexPage.visual.test.tsx`: amplía la prueba previa con diez resultados, cero resultados, carga y shell real a 390/1440 px.
- Esta evidencia y sus logs/capturas. Los cambios anteriores del expediente se conservan sin nuevas ediciones.

## Validación

| Control | Resultado |
|---|---|
| Pruebas focales de página/selector/expediente | PASS 32/32: 20 Chromium + 5 página + 7 expediente ([log](../selector-components.log)) |
| TypeScript `tsc --noEmit` y `tsc -b` | PASS |
| ESLint focal de los tres TSX de página/pruebas | PASS |
| Build | PASS (resumen abajo) |
| Arquitectura | PASS, cero infracciones nuevas (resumen abajo) |
| `git diff --check` y revisión del diff | PASS |

Registros de esta continuación retirados de la evidencia (resumen fiel de su contenido):

- `selector-after.log` (vitest v4.1.6, inicio 20:13:00): 1 archivo, 6 PASS y 12 omitidas (18). Ejecución parcial superada por el log 32/32.
- `selector-build.log` (`tsc -b && run-platform-guard && vite build`): platform guard PASS en sus cinco controles (0 violaciones; 136 endpoints escaneados); `✓ built in 1.33s`.
- `selector-architecture.log`: TAP 140 tests, 140 pass, 0 fail; todas las comprobaciones `[PASS]`; `Architecture gate PASS: 0 new violations.`

Chromium verifica posiciones idénticas de inputs/select antes y después de abrir sugerencias, altura compacta de filtros, panel alineado con el input, fondo/borde, scroll, ausencia de overflow horizontal y de ancestros que recorten el panel. Se comprueba también que el panel reciba el hit-test sobre controles subyacentes y que Tab + Enter/Espacio activen los botones. El callback React se prueba por separado en jsdom.

## Capturas

| Estado | Antes | Después |
|---|---|---|
| Diez resultados, 390 px | [captura](before/picker-results-390.png) | [captura](after/picker-results-390.png) |
| Diez resultados, 1440 px | [captura](before/picker-results-1440.png) | [captura](after/picker-results-1440.png) |
| Cero resultados, 390 px | sin cambio visual (idéntica a después) | [captura](after/picker-empty-390.png) |
| Cero resultados, 1440 px | sin cambio visual (idéntica a después) | [captura](after/picker-empty-1440.png) |
| Shell real | — | [390 px](after/picker-shell-390.png), [1440 px](after/picker-shell-1440.png) |

Se retiraron por ser binariamente idénticas (mismo blob Git) a capturas conservadas: `before/picker-empty-*`, `before/picker-loading-*` y `after/picker-loading-*` (iguales a `after/picker-empty-*`), y los catorce estados del expediente/página en `after/` (iguales a [`../after/`](../after/)).

En móvil el panel se superpone a los filtros inferiores mientras está abierto, sin desplazarlos; conserva el ancho del input y el scroll interno del DS.

**Límites:** Chromium con markup React y CSS reales, datos sintéticos y sin sesión autenticada ni API. `LayoutFrame` real incluido en dos casos; no incluye navegación lateral ni cabecera de AppLayout. Las fuentes externas de Google siguen sin estar disponibles, por lo que las capturas muestran ligaduras de iconos y tipografía alternativa. El SSR del fixture no hidrata handlers React: activación nativa en Chromium y callbacks en jsdom se comprueban por separado. Queda pendiente el smoke autenticado del propietario.

## Gate del propietario (desde frontend, PowerShell)

```powershell
npx.cmd vitest run src/modules/inventory/kardex/pages/KardexPage.test.tsx src/modules/inventory/kardex/pages/KardexPage.visual.test.tsx src/modules/inventory/kardex/components/KardexMovementDetailModal.test.tsx
npx.cmd tsc --noEmit
npx.cmd eslint src/modules/inventory/kardex/pages/KardexPage.tsx src/modules/inventory/kardex/pages/KardexPage.test.tsx src/modules/inventory/kardex/pages/KardexPage.visual.test.tsx
npm.cmd run architecture:check
git diff --check
```

Para regenerar las capturas, definir `KARDEX_CAPTURE_STAGE=after` y `KARDEX_CAPTURE_DIR=../docs/evidence/kardex-ux-01/selector` antes de la prueba visual; la regeneración vuelve a producir los duplicados retirados, que no deben versionarse. No se ejecutaron suites funcionales completas ni operaciones sobre bases.
