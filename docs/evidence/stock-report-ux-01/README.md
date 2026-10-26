# Reporte de Stock — pulido UX/UI (2026-10-25)

Implementado para revisión del propietario; **no CLOSED**, pendiente de su gate. Ruta `/reportes/stock`.

## Causa comprobada

Clasificación **DEBT de distribución**. `StockReportPage` utiliza `.pg-kpis`, cuyo contrato compartido declara cuatro columnas, pero solo presenta tres tarjetas: a 1440 px quedaban 356 px sin ocupar al final. Los dos `.rpt-filter-field` no crecen dentro de `.rpt-filters-fields`; en móvil ese contenedor comparte fila con las acciones, estrechando ambos controles. El estado vacío acumula 40 px exteriores de `.pg-pad-40` y 48 px verticales interiores de `.empty-state`; el contenedor alcanzaba 222 px en móvil.

Reproducción previa con Chromium y CSS real: **5/8 FAIL**, por hueco KPI en los cuatro estados de escritorio y altura vacía en móvil; 3/8 PASS. Capturas previas preservadas en `before/`. La captura original del propietario no fue adjuntada a esta conversación: se confirma la causa mediante código y reproducción, sin afirmar comparación directa con esa imagen.

**KEEP:** cálculos, acumulados, formatos/precisiones, parámetros API, filtros, permisos, columnas, mensajes y comportamiento de Buscar/Limpiar. No se altera carga ni lógica de error. No se introduce funcionalidad nueva.

## Cambios nuevos, separados de Kardex

- `frontend/src/modules/reportes/pages/StockReportPage.tsx`: import CSS, wrapper de scope local y clase para el contenedor de resultados; cinco líneas de diff de producción.
- `frontend/src/modules/reportes/pages/StockReportPage.css`: tres columnas KPI en escritorio y una en móvil; campos flexibles, acciones en su propia fila móvil; reducción de padding exclusivamente cuando hay estado vacío. Todos los valores visuales usan tokens del DS.
- `frontend/src/modules/reportes/pages/StockReportPage.test.tsx`: añade una regresión de parámetros Buscar y restablecimiento Limpiar, conservando las ocho pruebas previas.
- `frontend/src/modules/reportes/pages/StockReportPage.visual.test.tsx`: ocho casos Chromium, con React y LayoutFrame reales, para vacío/carga/resultados/error a 390 y 1440 px.
- Esta carpeta de evidencias.

Revisados `ReportPageTemplate.tsx/.css`, `page-template.css`, `zh-ui.css`, `legacy-pages.css`, `LayoutFrame`, `ZHDataTable` y reglas frontend. Se reutilizan **ReportKpiCard, ReportFiltersBar, ReportFilterField, ZHDataTable, ZhSelect y ZhTextInput**. No se modifican componentes/estilos compartidos ni otros reportes.

**Kardex y sus evidencias preexistentes permanecen idénticos**, comprobado comparando SHA-256 de todos sus archivos antes/después. El archivo backend no versionado previo también permanece fuera del trabajo.

## Validación del agente

| Control | Resultado |
|---|---|
| Pruebas focales | PASS 17/17: nueve de página y ocho Chromium ([log](components.log)) |
| TypeScript `tsc --noEmit`, compilación `tsc -b` del build | PASS |
| ESLint focal de los tres TSX del reporte | PASS |
| `npm run build` | PASS (resumen abajo) |
| Arquitectura | 140/140 pruebas de guardrails PASS; gate final PASS, cero infracciones nuevas ([log final](architecture-final.log)) |
| `git diff --check` y revisión del diff | PASS |
| Preservación de Kardex | PASS, hashes idénticos |

No suites funcionales completas, backend, bases, migraciones, staging, commit, push ni deploy. El build puede regenerar timestamps en informes Platform: se restituye únicamente esa salida generada, tras comprobar que no hay otros cambios.

El primer gate detectó el selector local `.empty-state` como prefijo no permitido. Se corrigió con el modificador local `rpt-stock-results--empty`, que solo activa el layout compacto en vacío; sin cambiar el contrato global. Las 140 pruebas de scripts ya habían pasado y, tras el ajuste de UI, se repitieron las pruebas focales y el runner completo `node ../tools/architecture/run-all.mjs` con ratchet.

Registros retirados de la evidencia (resumen fiel de su contenido):

- `architecture.log` (gate inicial): TAP 140 tests, 140 pass, 0 fail; `[FAIL] css-prefixes (0 historical, 1 new)` en `frontend/src/modules/reportes/pages/StockReportPage.css` — `F-css-prefix: class ".empty-state" must use prefix "rpt-" or global zh-ui/pg-* token`; `Architecture gate FAILED: 1 new violation(s)` (baseline histórico 0). Superado por el [log final](architecture-final.log).
- `build.log` (`tsc -b && run-platform-guard && vite build`): platform guard PASS en sus cinco controles (0 violaciones; 136 endpoints escaneados); `✓ built in 1.53s`.

## Evidencia visual

| Estado | Antes 390 / 1440 | Después 390 / 1440 |
|---|---|---|
| Vacío | [390](before/empty-390.png), [1440](before/empty-1440.png) | [390](after/empty-390.png), [1440](after/empty-1440.png) |
| Carga | — | [390](after/loading-390.png), [1440](after/loading-1440.png) |
| Resultados | [390](before/results-390.png), [1440](before/results-1440.png) | [390](after/results-390.png), [1440](after/results-1440.png) |
| Error | — | [390](after/error-390.png), [1440](after/error-1440.png) |

Las capturas previas de carga y error se retiraron por redundar con las previas de vacío y resultados, que ya muestran el hueco KPI y la distribución de filtros.

Chromium comprueba aprovechamiento del ancho KPI y filtros, alineación de controles/acciones, ausencia de overflow de página, altura vacía reducida y mensaje centrado. La tabla conserva su región de scroll horizontal.

**Límites:** datos/API sustituidos por fixtures, sin sesión autenticada; se captura el DOM realmente renderizado por React en jsdom y se mide en Chromium, sin hidratación de eventos allí. Las interacciones se prueban en jsdom. LayoutFrame está incluido, cabecera/lateral de AppLayout no. Google Fonts no está disponible: iconos aparecen como nombres de ligaduras y tipografía alternativa; no valida tipografía final. Pendiente el smoke autenticado del propietario.

## Gate focal del propietario (PowerShell desde frontend)

```powershell
npx.cmd vitest run src/modules/reportes/pages/StockReportPage.test.tsx src/modules/reportes/pages/StockReportPage.visual.test.tsx
npx.cmd tsc --noEmit
npx.cmd eslint src/modules/reportes/pages/StockReportPage.tsx src/modules/reportes/pages/StockReportPage.test.tsx src/modules/reportes/pages/StockReportPage.visual.test.tsx
npm.cmd run architecture:check
git diff --check
```

Para regenerar las capturas finales, definir `$env:STOCK_CAPTURE_STAGE='after'` antes de la prueba visual. El propietario revisa el diff nuevo junto al diff preexistente de Kardex y realiza staging/commit cuando corresponda.
