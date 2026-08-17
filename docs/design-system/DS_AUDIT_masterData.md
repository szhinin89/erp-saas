# DS Audit Report

- Fecha: 2026-08-17 14:00:42
- Scope: `modules` (moduleName: `masterData`)
- Generado por: `scripts/ds/ds-audit.ps1`
- Total hallazgos: **45**

> Auditoria por lineas (regex), no un parser real de CSS/TSX. ` NEEDS_DECISION ` siempre requiere revision humana - ver ` docs/design-system/DS_RULES.md `.

## Conteo por patron

| Patron | Hallazgos |
|---|---|
| `font-size` | 28 |
| `font-weight` | 11 |
| `text-transform` | 2 |
| `letter-spacing` | 2 |
| `line-height` | 1 |
| `font-style` | 1 |

## Conteo por clasificacion

| Clasificacion | Hallazgos |
|---|---|
| `OK_TOKEN` | 38 |
| `NEEDS_DECISION` | 5 |
| `OK_ICON` | 1 |
| `OK_LAYOUT` | 1 |

## Hallazgos

| Archivo | Linea | Patron | Contenido | Clasificacion | Accion sugerida |
|---|---|---|---|---|---|
| `frontend/src/modules/masterData/pages/masterdata-pages.css` | 24 | `font-size` | `font-size: var(--text-label-sm-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/masterData/pages/masterdata-pages.css` | 25 | `font-weight` | `font-weight: var(--text-label-sm-weight);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/masterData/pages/masterdata-pages.css` | 37 | `font-size` | `font-size: var(--text-help-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/masterData/pages/masterdata-pages.css` | 43 | `font-size` | `font-size: var(--text-help-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/masterData/pages/masterdata-pages.css` | 62 | `font-size` | `font-size: var(--text-body-md-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/masterData/pages/masterdata-pages.css` | 72 | `font-size` | `font-size: var(--text-label-sm-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/masterData/pages/masterdata-pages.css` | 73 | `font-weight` | `font-weight: var(--text-label-sm-weight);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/masterData/pages/masterdata-pages.css` | 74 | `letter-spacing` | `letter-spacing: var(--text-label-sm-spacing);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/masterData/pages/masterdata-pages.css` | 75 | `text-transform` | `text-transform: uppercase;` | NEEDS_DECISION | Revisar si es titulo/label global (OK) o uppercase local sin justificacion. |
| `frontend/src/modules/masterData/pages/masterdata-pages.css` | 91 | `font-size` | `font-size: var(--text-body-md-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/masterData/pages/masterdata-pages.css` | 97 | `font-size` | `font-size: var(--text-body-md-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/masterData/pages/masterdata-pages.css` | 102 | `font-style` | `font-style: normal;` | OK_LAYOUT | Sin accion - cumple regla DS. |
| `frontend/src/modules/masterData/pages/masterdata-pages.css` | 123 | `font-size` | `font-size: var(--text-label-md-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/masterData/pages/masterdata-pages.css` | 124 | `font-weight` | `font-weight: var(--text-label-md-weight);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/masterData/pages/masterdata-pages.css` | 141 | `font-weight` | `font-weight: var(--text-label-md-weight);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/masterData/pages/masterdata-pages.css` | 146 | `font-size` | `font-size: var(--text-badge-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/masterData/pages/masterdata-pages.css` | 147 | `font-weight` | `font-weight: var(--text-badge-weight);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/masterData/pages/masterdata-pages.css` | 163 | `font-size` | `font-size: var(--text-body-md-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/masterData/pages/masterdata-pages.css` | 167 | `font-size` | `font-size: var(--text-label-sm-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/masterData/pages/masterdata-pages.css` | 168 | `font-weight` | `font-weight: var(--text-label-sm-weight);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/masterData/pages/masterdata-pages.css` | 169 | `text-transform` | `text-transform: uppercase;` | NEEDS_DECISION | Revisar si es titulo/label global (OK) o uppercase local sin justificacion. |
| `frontend/src/modules/masterData/pages/masterdata-pages.css` | 170 | `letter-spacing` | `letter-spacing: var(--text-label-sm-spacing);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/masterData/pages/masterdata-pages.css` | 183 | `font-size` | `font-size: var(--text-title-sm-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/masterData/pages/masterdata-pages.css` | 184 | `font-weight` | `font-weight: var(--text-title-sm-weight);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/masterData/pages/masterdata-pages.css` | 185 | `line-height` | `line-height: var(--text-title-sm-height);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/masterData/pages/masterdata-pages.css` | 210 | `font-size` | `font-size: var(--text-help-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/masterData/pages/masterdata-pages.css` | 218 | `font-size` | `font-size: var(--text-help-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/masterData/pages/masterdata-pages.css` | 252 | `font-size` | `font-size: var(--text-body-md-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/masterData/pages/masterdata-pages.css` | 257 | `font-size` | `font-size: var(--text-help-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/masterData/pages/masterdata-pages.css` | 268 | `font-size` | `font-size: var(--text-help-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/masterData/pages/masterdata-pages.css` | 307 | `font-size` | `font-size: var(--text-help-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/masterData/pages/masterdata-pages.css` | 312 | `font-size` | `font-size: var(--text-help-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/masterData/pages/masterdata-pages.css` | 337 | `font-size` | `font-size: var(--text-body-md-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/masterData/pages/masterdata-pages.css` | 341 | `font-size` | `font-size: 18px;` | OK_ICON | Sin accion - cumple regla DS. |
| `frontend/src/modules/masterData/pages/masterdata-pages.css` | 393 | `font-size` | `font-size: var(--text-help-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/masterData/pages/masterdata-pages.css` | 394 | `font-weight` | `font-weight: var(--text-label-sm-weight);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/masterData/pages/masterdata-pages.css` | 405 | `font-size` | `font-size: var(--text-help-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/masterData/pages/masterdata-pages.css` | 445 | `font-size` | `font-size: var(--text-body-md-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/masterData/pages/masterdata-pages.css` | 446 | `font-weight` | `font-weight: 600;` | NEEDS_DECISION | Revisar: si es dato normal, mover a token var(--text-*-weight) o al componente global correspondiente. |
| `frontend/src/modules/masterData/pages/masterdata-pages.css` | 453 | `font-size` | `font-size: var(--text-help-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/masterData/pages/masterdata-pages.css` | 469 | `font-size` | `font-size: var(--text-body-sm-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/masterData/pages/masterdata-pages.css` | 486 | `font-size` | `font-size: var(--text-headline-sm-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/masterData/pages/masterdata-pages.css` | 487 | `font-weight` | `font-weight: 700;` | NEEDS_DECISION | Revisar: si es dato normal, mover a token var(--text-*-weight) o al componente global correspondiente. |
| `frontend/src/modules/masterData/pages/masterdata-pages.css` | 492 | `font-size` | `font-size: var(--text-headline-sm-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/masterData/pages/masterdata-pages.css` | 493 | `font-weight` | `font-weight: 700;` | NEEDS_DECISION | Revisar: si es dato normal, mover a token var(--text-*-weight) o al componente global correspondiente. |


