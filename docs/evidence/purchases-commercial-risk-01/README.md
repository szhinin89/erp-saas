# PURCHASES-COMMERCIAL-RISK-01 (2026-10-23)

Capturas Playwright del `PurchaseLineCard` real y del `PurchaseConfirmChecklist` real, con fixtures
construidas a partir de la factura DINADEC 034-005-002241241 (datos leídos en modo solo lectura de
`erp_zh_dev`: 30 × $6.52167, descuento $16.24, subtotal $179.41, IVA $26.91, total $206.32, PVP $1.09).
No es una sesión autenticada. El arnés temporal se retiró después de capturar.

- `dinadec.png`: 30 SIXPACK X6 → 180 unidades, margen 8.56 %, sin incidencias.
- `negative.png` / `negative-panel.png`: PVP $0.90, margen −10.75 %, aviso «Margen negativo».
- `zero.png` / `zero-panel.png`: costo $1.00 = PVP $1.00, margen 0.00 %, aviso «Margen cero».
- `nopvp.png` / `nopvp-panel.png`: sin PVP, indicador «Sin PVP» (sin margen ficticio) y aviso existente.
- `suspicious.png` / `suspicious-panel.png`: presentación Unidad, margen −448.66 %, bloqueo existente sin aviso duplicado.
- `diverges.png` / `diverges-panel.png`: 29 SIXPACK, divergencia real que se sigue avisando.
- `confirm-modal.png`: una advertencia operativa por línea y una sola fila compacta «Rentabilidad por revisar».

LIMITACIÓN VISUAL: el entorno de captura no cargó Material Symbols; los íconos aparecen como el
texto de su ligadura.
