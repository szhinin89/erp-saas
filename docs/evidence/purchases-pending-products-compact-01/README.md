# PURCHASES-PENDING-PRODUCTS-COMPACT-01 (2026-10-23)

Capturas Playwright del `ResolvePendingProductsModal` real con el mismo arnés temporal y los mismos
datos antes y después: 23 líneas XML de un proveedor (una con sugerencia del motor de matching, ICE en
la última, descuentos, IVA 0 % y 15 %). Catálogos, servicio de recepción y consulta de presentaciones
sustituidos por fixtures; sin API, sesión ni ERP real. El arnés se retiró después de capturar.

| Medición (1366 × 900, 23 productos) | Antes | Después |
|---|---|---|
| Alto de fila | 200 px | 82 px (detalle bajo demanda) |
| Filas visibles en la primera pantalla | 3 | 7 |
| Alto total del contenido | 6536 px | 2483 px |
| Controles visibles | 319 | 54 |
| Pie | Cancelar · Resolver | 23 seleccionadas · 1 listas · 22 pendientes · Cancelar · Resolver |
| Móvil 390 px: lista / visible | 1054 / 326 px (scroll horizontal) | 326 / 326 px, sin scroll horizontal |

- `before-desktop-23.png`, `before-desktop-23-errors.png`, `before-mobile-23.png`: tabla de 7
  columnas con todos los campos de cada producto abiertos; en móvil la barra masiva ocupa la primera
  pantalla y la tabla se desplaza en horizontal.
- `after-desktop-23.png`: filas compactas (línea XML, acción, producto ERP, «Detalles»); la etiqueta
  de incidencias de Compras sobre el borde de cada fila con datos por completar.
- `after-desktop-expanded.png`: detalle de una fila en cinco secciones (línea XML, producto ERP,
  clasificación, presentación, impuestos y precio) con los mismos campos que antes.
- `after-desktop-23-errors.png`: al enviar con datos incompletos se expanden las filas con error y se
  muestran los mensajes existentes junto a cada campo.
- `after-desktop-status-popover.png`: la etiqueta lista lo que falta y «Completar datos» abre la fila.
- `after-mobile-23.png`, `after-mobile-expanded.png`: fila apilada y detalle en una columna.

Teclado (navegador real): Enter abre «Detalles», Tab entra al primer campo de la fila, Espacio cierra;
los datos escritos se conservan al contraer y volver a expandir.

LIMITACIÓN VISUAL: el entorno de captura no cargó Material Symbols; los iconos muestran el texto de su
ligadura («expand_more», «close», «warning»).
