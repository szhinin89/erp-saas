# Kardex — corrección UX/UI focal (2026-10-25)

Publicado en `50a1af3d9` sobre la base histórica `8a2c3692d`. **No CLOSED**: gate del propietario y smoke autenticado pendientes de acreditar. Publicación y CI #999 SUCCESS: [STATUS](../../../STATUS.md#inventario-v1--evidencia-ci-de-referencia).

## Base y causa comprobada

- Base histórica de la intervención (HEAD/main al iniciar): `8a2c3692d8fb5c870e2284e06832b60dc014cac5`; publicación UX/UI: `50a1af3d9`.
- [CI #997, intento 2: success](https://github.com/szhinin89/erp-saas/actions/runs/40248632996).
- BUG REAL: `KardexPage.css` no tenía ningún import en el código. Chromium confirmó filtros con display block y apilados incluso a 1440 px. La reproducción inicial produjo 12/12 FAIL al comprobar la distribución flex ausente, antes de modificar producción.
- DEBT: reglas CSS repetidas, anchos mínimos rígidos y cinco tarjetas sobre el grid compartido de tres columnas (dos filas con hueco). Expediente con márgenes de panel dentro de cards y etiquetas pequeñas en mayúsculas.
- KEEP: columnas, búsquedas por producto/documento, selección, filtros, URL, permisos, navegación, DTO, formato/precisión y valoración B4. No se modificó el hook ni el backend.
- FALTA: ninguna funcionalidad nueva requerida.

Lecturas: CLAUDE raíz/frontend, STATUS, FEATURES, documentos operativos del agente, enforcement por fase, frontend/Design System, estándar de modales, SSOT Inventory y ADR-040/041. Reutilización: ErpPageTemplate, ReportKpiCard horizontal, ZHField, ZHBtn, ZHDataTable, ZHModal y zh-description-grid. Sin componentes ni tokens nuevos, ni cambios globales.

## Archivos y resultado visual

- `frontend/src/modules/inventory/kardex/pages/KardexPage.tsx`: import local, retiro de pestaña única sin navegación ni cambio de estado (callback vacío), ReportKpiCard en lugar del KPI local.
- `frontend/src/modules/inventory/kardex/pages/KardexPage.css`: deduplicación y layout con tokens; filtros flexibles, cinco tarjetas compactas, wrap móvil y espaciado del expediente.
- `frontend/src/modules/inventory/kardex/components/KardexMovementDetailModal.tsx`: listas descriptivas semánticas del DS, valores legibles, separación entre secciones y relaciones con wrap.
- `frontend/src/modules/inventory/kardex/components/KardexMovementDetailModal.test.tsx`: regresión de documento origen, anterior/siguiente y cierre; conserva las seis pruebas existentes de precisión.
- `frontend/src/modules/inventory/kardex/pages/KardexPage.visual.test.tsx`: Chromium con markup React real y CSS de producción; datos sintéticos, sin API/BD.

## Validación del agente

| Control | Resultado |
|---|---|
| Componentes focales | PASS 19/19: 12 Chromium + 7 modal |
| TypeScript `tsc --noEmit` y compilación `tsc -b` | PASS |
| ESLint de los cuatro archivos TSX afectados | PASS, sin errores ni warnings |
| `npm run build` | PASS (resumen abajo) |
| `npm run architecture:check` | PASS: 140 pruebas de guardrails, cero infracciones nuevas (resumen abajo); conserva advertencias y score general preexistentes |
| `git diff --check` y revisión del diff | PASS |

Registros de esta fase retirados de la evidencia (resumen fiel de su contenido):

- `components.log` (vitest v4.1.6, inicio 20:07:32): 2 archivos, **19/19 PASS**. Superado por el [log 32/32 del selector](selector-components.log).
- `build.log` (`tsc -b && run-platform-guard && vite build`): platform guard PASS en sus cinco controles (0 violaciones; 136 endpoints escaneados); `✓ built in 2.02s`.
- `architecture.log`: TAP 140 tests, 140 pass, 0 fail; todas las comprobaciones `[PASS]`; `Architecture gate PASS: 0 new violations.`

Durante la validación del agente no se ejecutaron suites funcionales completas ni se accedió a bases; tampoco se realizó staging, commit, push ni deploy en esa fase. La publicación posterior se identifica al inicio. El build regeneró únicamente timestamps de dos informes ajenos; se restituyó su contenido original sin modificar Git.

## Evidencia visual y límites

Chromium headless, 390×900 y 1440×900: vacío, cargando, resultados, búsqueda documental, expediente y expediente cargando. Se inspeccionaron resultados de escritorio y expediente móvil, incluidas relaciones/auditoría/contabilidad al final del scroll. La tabla conserva su scroll horizontal propio.

| Vista | Antes | Después |
|---|---|---|
| Resultados escritorio | [captura](before/results-1440.png) | [captura](after/results-1440.png) |
| Resultados móvil | [captura](before/results-390.png) | [captura](after/results-390.png) |
| Expediente escritorio | [captura](before/detail-1440.png) | [captura](after/detail-1440.png) |
| Expediente móvil | [captura](before/detail-390.png) | [captura](after/detail-390.png), [parte inferior](after/detail-bottom-390.png) |

`after/` contiene además los estados vacío, cargando, búsqueda documental, expediente cargando y la parte inferior del expediente a 1440 px. En `before/` se conservan solo resultados y expediente, que ya muestran los defectos de distribución; las capturas previas de los demás estados se retiraron por su escaso valor adicional. Limitaciones: render aislado sin shell lateral ni sesión autenticada; hook de datos sustituido por fixture; no valida integración API ni interacción hidratada en navegador. Las interacciones del expediente se verifican en jsdom. No se pudieron descargar Inter/Material Symbols de Google: las capturas muestran fuentes alternativas y nombres de ligaduras; no constituyen aprobación final de tipografía/iconografía. Las capturas iniciales del modal incluyen su transición; las finales desactivan animaciones para inspeccionar contenido opaco.

## Gate focal del propietario (PowerShell, desde frontend)

```powershell
npx.cmd vitest run src/modules/inventory/kardex/pages/KardexPage.visual.test.tsx src/modules/inventory/kardex/components/KardexMovementDetailModal.test.tsx
npx.cmd tsc --noEmit
npx.cmd eslint src/modules/inventory/kardex/pages/KardexPage.tsx src/modules/inventory/kardex/pages/KardexPage.visual.test.tsx src/modules/inventory/kardex/components/KardexMovementDetailModal.tsx src/modules/inventory/kardex/components/KardexMovementDetailModal.test.tsx
npm.cmd run architecture:check
git diff --check
```

Revisión autenticada pendiente con las fuentes disponibles: filtros/búsquedas y dropdowns, volver desde documentos, estados y expediente en escritorio/móvil. El código ya está publicado; no hay staging ni commit de UX/UI pendientes. El cierre sigue pendiente de acreditar ese gate y el smoke autenticado.
