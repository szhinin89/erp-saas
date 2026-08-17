# DS Audit Report

- Fecha: 2026-08-17 13:49:49
- Scope: `modules` (moduleName: `masterData`)
- Generado por: `scripts/ds/ds-audit.ps1`
- Total hallazgos: **48**

> Auditoria por lineas (regex), no un parser real de CSS/TSX. ` NEEDS_DECISION ` siempre requiere revision humana - ver ` docs/design-system/DS_RULES.md `.

## Conteo por patron

| Patron | Hallazgos |
|---|---|
| `font-size` | 29 |
| `font-weight` | 13 |
| `text-transform` | 2 |
| `letter-spacing` | 2 |
| `line-height` | 1 |
| `font-style` | 1 |

## Conteo por clasificacion

| Clasificacion | Hallazgos |
|---|---|
| `OK_TOKEN` | 34 |
| `NEEDS_DECISION` | 12 |
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
| `frontend/src/modules/masterData/pages/masterdata-pages.css` | 147 | `font-weight` | `font-weight: 700;` | NEEDS_DECISION | Revisar: si es dato normal, mover a token var(--text-*-weight) o al componente global correspondiente. |
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
| `frontend/src/modules/masterData/pages/masterdata-pages.css` | 249 | `font-weight` | `font-weight: 600;` | NEEDS_DECISION | Revisar: si es dato normal, mover a token var(--text-*-weight) o al componente global correspondiente. |
| `frontend/src/modules/masterData/pages/masterdata-pages.css` | 253 | `font-size` | `font-size: var(--text-body-md-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/masterData/pages/masterdata-pages.css` | 258 | `font-size` | `font-size: var(--text-help-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/masterData/pages/masterdata-pages.css` | 269 | `font-size` | `font-size: var(--text-help-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/masterData/pages/masterdata-pages.css` | 305 | `font-weight` | `font-weight: 600;` | NEEDS_DECISION | Revisar: si es dato normal, mover a token var(--text-*-weight) o al componente global correspondiente. |
| `frontend/src/modules/masterData/pages/masterdata-pages.css` | 309 | `font-size` | `font-size: var(--text-help-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/masterData/pages/masterdata-pages.css` | 314 | `font-size` | `font-size: var(--text-help-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/masterData/pages/masterdata-pages.css` | 339 | `font-size` | `font-size: var(--text-body-md-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/masterData/pages/masterdata-pages.css` | 343 | `font-size` | `font-size: 18px;` | OK_ICON | Sin accion - cumple regla DS. |
| `frontend/src/modules/masterData/pages/masterdata-pages.css` | 395 | `font-size` | `font-size: var(--text-help-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/masterData/pages/masterdata-pages.css` | 396 | `font-weight` | `font-weight: 600;` | NEEDS_DECISION | Revisar: si es dato normal, mover a token var(--text-*-weight) o al componente global correspondiente. |
| `frontend/src/modules/masterData/pages/masterdata-pages.css` | 407 | `font-size` | `font-size: var(--text-help-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/masterData/pages/masterdata-pages.css` | 447 | `font-size` | `font-size: var(--text-body-md-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/masterData/pages/masterdata-pages.css` | 448 | `font-weight` | `font-weight: 500;` | NEEDS_DECISION | Revisar: si es dato normal, mover a token var(--text-*-weight) o al componente global correspondiente. |
| `frontend/src/modules/masterData/pages/masterdata-pages.css` | 455 | `font-size` | `font-size: var(--text-help-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/masterData/pages/masterdata-pages.css` | 471 | `font-size` | `font-size: var(--text-body-sm-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/masterData/pages/masterdata-pages.css` | 488 | `font-size` | `font-size: 18px;` | NEEDS_DECISION | Revisar: si es dato normal, mover a token var(--text-*) o a ZHFieldLabel/ZHDataValue/ZHMoneyValue global. |
| `frontend/src/modules/masterData/pages/masterdata-pages.css` | 489 | `font-weight` | `font-weight: 700;` | NEEDS_DECISION | Revisar: si es dato normal, mover a token var(--text-*-weight) o al componente global correspondiente. |
| `frontend/src/modules/masterData/pages/masterdata-pages.css` | 494 | `font-size` | `font-size: 18px;` | NEEDS_DECISION | Revisar: si es dato normal, mover a token var(--text-*) o a ZHFieldLabel/ZHDataValue/ZHMoneyValue global. |
| `frontend/src/modules/masterData/pages/masterdata-pages.css` | 495 | `font-weight` | `font-weight: 700;` | NEEDS_DECISION | Revisar: si es dato normal, mover a token var(--text-*-weight) o al componente global correspondiente. |
| `frontend/src/modules/masterData/pages/masterdata-pages.css` | 507 | `font-size` | `font-size: 0.85rem;` | NEEDS_DECISION | Revisar: si es dato normal, mover a token var(--text-*) o a ZHFieldLabel/ZHDataValue/ZHMoneyValue global. |


