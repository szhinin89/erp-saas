# DS Audit Report

- Fecha: 2026-08-17 15:14:40
- Scope: `modules` (moduleName: `items`)
- Generado por: `scripts/ds/ds-audit.ps1`
- Total hallazgos: **7**

> Auditoria por lineas (regex), no un parser real de CSS/TSX. ` NEEDS_DECISION ` siempre requiere revision humana - ver ` docs/design-system/DS_RULES.md `.

## Conteo por patron

| Patron | Hallazgos |
|---|---|
| `font-size` | 6 |
| `font-weight` | 1 |

## Conteo por clasificacion

| Clasificacion | Hallazgos |
|---|---|
| `OK_TOKEN` | 5 |
| `OK_ICON` | 2 |

## Hallazgos

| Archivo | Linea | Patron | Contenido | Clasificacion | Accion sugerida |
|---|---|---|---|---|---|
| `frontend/src/modules/items/catalog/wizard/catalog-wizard.css` | 14 | `font-size` | `font-size: var(--text-body-md-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/items/catalog/wizard/catalog-wizard.css` | 51 | `font-size` | `font-size: 20px;` | OK_ICON | Sin accion - cumple regla DS. |
| `frontend/src/modules/items/catalog/wizard/catalog-wizard.css` | 59 | `font-size` | `font-size: 20px;` | OK_ICON | Sin accion - cumple regla DS. |
| `frontend/src/modules/items/catalog/wizard/catalog-wizard.css` | 64 | `font-size` | `font-size: var(--text-body-md-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/items/catalog/wizard/catalog-wizard.css` | 80 | `font-size` | `font-size: var(--text-help-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/items/catalog/wizard/catalog-wizard.css` | 84 | `font-size` | `font-size: var(--text-label-sm-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/items/catalog/wizard/catalog-wizard.css` | 85 | `font-weight` | `font-weight: var(--text-label-sm-weight);` | OK_TOKEN | Sin accion - cumple regla DS. |


