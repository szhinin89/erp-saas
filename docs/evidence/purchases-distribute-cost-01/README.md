# PURCHASES-DISTRIBUTE-COST-UX-01 (2026-10-23)

Capturas Playwright del `DistributeCostModal` real, con el mismo arnés temporal y los mismos datos
antes y después: 6 productos (uno con nombre de ~100 caracteres), cantidades e importes con
decimales y un descuento, flete $37.85 con ARROZ excluido. No es una sesión autenticada. El arnés
se retiró después de capturar.

| Medición (1366 × 900) | Antes | Después |
|---|---|---|
| Alto máximo de fila | 787 px | 61 px |
| Ancho del contenido / ancho visible | 1110 / 900 px (columnas cortadas, sin scroll) | 1038 / 1038 px |
| Última columna visible | No | Sí |
| Móvil 390 px: contenido / visible | 1102 / 366 px | 334 / 334 px, sin scroll de página |

- `before-desktop-initial.png`, `before-desktop-calculated.png`, `before-desktop-one-line.png`,
  `before-mobile-calculated.png`: descripción partida carácter por carácter y columnas fuera de vista.
- `after-desktop-initial.png`, `after-desktop-calculated.png`, `after-desktop-one-line.png`:
  descripción en máx. 2 líneas, cifras alineadas y resumen en dos grupos.
- `after-desktop-narrow-scrolled.png` (760 px): scroll horizontal solo dentro de la tabla
  (928 / 670 px) con la columna Ítem fija.
- `after-mobile-calculated.png`, `after-mobile-summary.png`: filas en dos columnas etiqueta/valor
  y resumen compacto.

LIMITACIÓN VISUAL: el entorno de captura no cargó Material Symbols; el botón de cierre muestra el
texto de su ligadura («close»).
