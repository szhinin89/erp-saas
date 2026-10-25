# Compras — avisos compactos de códigos duplicados

Fecha: 2026-10-24. Clasificación: **BUG REAL** de presentación.

## Cambio y alcance

Una etiqueta `PurchaseLineIssuesTag` por fila agrupa conflictos y demás incidencias sin mensajes
repetidos. Su popup reutiliza `ZHCodeConflicts compact`: código, descripción del propietario y
«Ver ítem» en otra pestaña. SKU y barcode se marcan en sus campos; el código del proveedor en la
línea XML. Los rechazos conservan valores y refrescan el diagnóstico. Solo quedan avisos globales
para motivos sin fila visible o indisponibilidad del diagnóstico.

La consulta existente se extrajo sin cambiar su comportamiento a `useItemCodeConflicts`;
`ItemCodeConflicts` mantiene su presentación habitual. El DS incorpora un slot opcional para
el detalle compartido y considera enlaces al enfocar la primera acción por teclado.
Sin validadores nuevos, cambios de backend, contratos, datos, impuestos o códigos provisionales.
Los cambios previos del workspace se conservaron. Sin commit, push ni deploy.

SSOT: [Compras](../../architecture/purchases.md#productos-pendientes-compactos),
[patrón de incidencias](../../architecture/frontend.md#incidencias-flotantes-de-linea-zh-ds-line-issues-01).

## Pruebas

| Verificación | Resultado final |
|---|---|
| Vitest: Compras, Ítems, importador, componentes de Ítems y notices (`--maxWorkers=2`) | **PASS 763/763**, 71 archivos |
| Focal: modal, compacto, flujos, ProductPicker y diagnóstico compartido (`--maxWorkers=1`) | **PASS 37/37** |
| TypeScript (`tsc -b`, `tsc --noEmit`) y build | **PASS** |
| ESLint | **PASS**, 0 errores; advertencias existentes |
| `architecture:check` | **PASS**, 0 infracciones nuevas |
| Diff review y `git diff --check` | **PASS** |
| Desktop 1440 × 1000 y móvil 390 × 844 | **PASS** |
| Popup dentro del viewport móvil, apertura Enter y foco en enlace | **PASS** |
| Scroll interno: el popup sigue al ancla (desplazamiento de 60 px) | **PASS** |
| «Ver ítem»: nueva pestaña en `/products/items?view=owner-1` | **PASS** |
| Guardado rechazado: valores conservados y sin alerta global repetida | **PASS** |

Primera ejecución ampliada: **FAIL 3/763** (expectativa antigua de mensaje inline, carrera de
teclado en ProductPicker y timeout del lote de 23 bajo concurrencia). Se actualizó la expectativa
al diseño solicitado; repetición aislada y suite completa con concurrencia limitada: PASS.
La primera prueba de popup en jsdom necesitó simular un clic de puntero (`detail: 1`); el teclado
se verificó además en Chromium real. Un build intermedio detectó un separador con codificación
incorrecta, corregido antes del build final PASS.

PostgreSQL no ejecutado: esta entrega solo modifica presentación y reutiliza el diagnóstico
existente; no cambia transacciones, scope, locks ni invariantes de persistencia.

## Capturas

Arnés temporal con `ResolvePendingProductsModal`, i18n español y CSS reales del DS y Compras,
12 filas de prueba y respuestas fixture de catálogos, diagnóstico y rechazo. El arnés fue retirado.
Sin sesión, API de negocio ni datos reales. La navegación comprueba pestaña y URL; no consulta la
ficha del propietario en una base real. La fuente externa Material Symbols no estuvo disponible
en el arnés: algunos iconos aparecen como texto; no se cambió su implementación.

- [Desktop: etiquetas compactas](desktop.png)
- [Desktop: popup con propietarios y enlaces](desktop-popup.png)
- [Desktop: popup tras desplazar el modal](desktop-scroll.png)
- [Desktop: rechazo, campos marcados y datos conservados](desktop-failed-save.png)
- [Móvil: etiqueta compacta](mobile.png)
- [Móvil: popup y foco de teclado](mobile-popup.png)
- [Mediciones y resultados del navegador](visual-results.json)

Referencia visual previa: [avisos en bloques superiores](../items-code-conflicts-01/purchases-resolve-conflicts.png).
