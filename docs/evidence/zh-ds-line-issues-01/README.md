# ZH-DS-LINE-ISSUES-01 - EXTEND (2026-10-23)

Capturas Playwright de `PurchaseLineCard` real con `ZHLineIssuesTag` global y fixtures
existentes de Compras (`xmlIceEditedLine`, `missingWarehouseReadiness`). No corresponde
a una factura persistida ni a una sesion autenticada. Arnes temporal retirado tras capturar.

- `purchases-expanded.png`: etiqueta unica sobre el borde, linea expandida.
- `purchases-collapsed.png`: etiqueta conservada con el detalle colapsado.
- `purchases-panel.png`: panel abierto por Enter; foco verificado en Recalcular desde XML.
- `purchases-mobile.png`: viewport 390 x 844; panel x=12, ancho=320, dentro del viewport.

LIMITACION VISUAL: el entorno de captura no cargo Material Symbols desde Google Fonts;
los iconos aparecen como sus nombres de ligadura. No se sustituyeron iconos ni fuentes del producto.
La etiqueta, colores, contador, lista, panel y posicion corresponden al CSS real del DS.

PASS: 24/24 Vitest focales (ZHLineIssuesTag, ZHNoticePopover, PurchaseLineCard.compact),
TypeScript (`tsc -b`), build de produccion (Platform guard y Vite), ESLint focal (0 errores; advertencia previa max-lines de PurchasesPage),
architecture:check (140 pruebas del guardrail; 0 infracciones nuevas), diff --check.
Cobertura: ninguna/una/varias, prioridad del consumidor, contador sin success, expansion/colapso,
accion y cierre, foco por teclado/Escape, scroll interno/documento, arriba/abajo y limites moviles.

Archivos de implementacion: components/zh/notices/{ZHLineIssuesTag.tsx,ZHLineIssuesTag.test.tsx,
ZHNoticeBadge.tsx,ZHNoticePopover.test.tsx,index.ts,zh-notice.css}; modules/purchases/{pages/PurchasesPage.tsx,
pages/PurchaseLineCard.compact.test.tsx,styles/purchases-invoice.css} (bajo frontend/src).
SSOT actualizados: docs/architecture/frontend.md y purchases.md. Avance: STATUS.md.
Los cambios previos del workspace en otros archivos se conservaron.
