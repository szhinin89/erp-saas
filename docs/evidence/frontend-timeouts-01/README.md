# Frontend — 2 timeouts recurrentes bajo la suite completa

Fecha: 2026-10-24. Clasificación: **DEBT de pruebas** (sin bug de producción). Solo se modificaron
pruebas y la configuración de Vitest; sin cambios de producción, reglas, datos, migraciones,
commit ni push.

## Causa raíz (medida, no supuesta)

| Prueba | Medición | Causa |
|---|---|---|
| `ResolvePendingProductsModal.flows.test.tsx` › 23-product batch | 11,3 s aislada (límite 30 s, 45 s bajo carga) | 115 `fireEvent.change` fila a fila sobre un formulario de 23 filas, cada uno con un re-render. Además, consultas `getAllByLabelText`/`getByRole` sobre todo el documento (~0,4–0,6 s cada una). |
| `AccountingPostingsPage.test.tsx` › caps selection at 100 | 6,1 s aislada (límite 15 s, 24 s bajo carga) | 100 clics reales = 100 commits de React (Profiler: `commits=100`, ~33 ms de render por commit en jsdom). React confirma cada evento discreto aunque se agrupen en `act`, así que la prueba no puede evitar el coste. La contención con ~15 workers en paralelo lo multiplica por ~4. |

## Corrección

- **Compras:** el lote usa el flujo real de asignación masiva (Tipo, Marca, Categoría, Unidad base
  → «Aplicar a seleccionados») y el margen («Sugerir PVP final»). Las consultas se acotan a la barra
  `.prl-bulk` y los valores de las filas se leen con un solo selector. Sigue cubriendo las 23 líneas,
  los códigos XML `0`/` 0 ` (barcode vacío y `barcode/barcodeType = null` en el payload) y el envío de
  los 23 productos. Añade aserciones de PVP 3.75 y marca en las 23 filas y en el request. El llenado
  fila a fila ya lo cubren las pruebas de 1–2 filas.
- **Contabilidad:** mantiene los 100 clics reales. Añade aserciones de 100 marcados, el 101 sin
  marcar y deshabilitado, y el contador `(100/100)`. El diálogo se consulta con `within(dialog)`. El
  archivo corre en el proyecto Vitest `heavy-dom` (`vite.config.ts`, `sequence.groupOrder: 1`),
  después del resto y sin contención. El límite de 15 s es el original; no se aumentó.

## Resultados

| Verificación | Antes | Después |
|---|---|---|
| Compras, prueba de 23 productos (aislada) | 11,3 s | ~1,0 s (5 ejecuciones) |
| Contabilidad, tope de 100 (aislada) | 6,1 s | ~5,0 s, en grupo `heavy-dom` sin contención |
| Mutación: tope 100 → 101 | — | la prueba FALLA (detecta) |
| Mutación: precargar código XML `0` como barcode | — | la prueba FALLA (detecta) |
| Suite completa (1 ejecución) | 3250/3252, 2 timeouts | 3252/3252 pruebas PASS; 1 archivo falló por el `afterAll` (`browser.close`, 10 s) de `ZHOverlayGeometry.test.tsx`, que lanza un Chromium real y no está modificado. Aislado: 20/20 PASS. |
| `npx tsc -b`, ESLint, build, `architecture:check` | — | PASS (0 errores; 140/140, 0 violaciones nuevas) |

Nota: la ejecución completa no debió realizarse. Por regla del proyecto, la suite completa la
ejecuta el usuario en su gate de PowerShell.
