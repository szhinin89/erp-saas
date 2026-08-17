# DS Audit Report

- Fecha: 2026-08-17 13:49:51
- Scope: `modules` (moduleName: `inventory`)
- Generado por: `scripts/ds/ds-audit.ps1`
- Total hallazgos: **27**

> Auditoria por lineas (regex), no un parser real de CSS/TSX. ` NEEDS_DECISION ` siempre requiere revision humana - ver ` docs/design-system/DS_RULES.md `.

## Conteo por patron

| Patron | Hallazgos |
|---|---|
| `font-size` | 18 |
| `font-weight` | 7 |
| `letter-spacing` | 1 |
| `text-transform` | 1 |

## Conteo por clasificacion

| Clasificacion | Hallazgos |
|---|---|
| `OK_TOKEN` | 18 |
| `NEEDS_DECISION` | 7 |
| `OK_ICON` | 2 |

## Hallazgos

| Archivo | Linea | Patron | Contenido | Clasificacion | Accion sugerida |
|---|---|---|---|---|---|
| `frontend/src/modules/inventory/kardex/pages/KardexPage.css` | 103 | `font-weight` | `font-weight: 600;` | NEEDS_DECISION | Revisar: si es dato normal, mover a token var(--text-*-weight) o al componente global correspondiente. |
| `frontend/src/modules/inventory/kardex/pages/KardexPage.css` | 107 | `font-size` | `font-size: var(--text-body-sm-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/inventory/kardex/pages/KardexPage.css` | 120 | `font-weight` | `font-weight: 600;` | NEEDS_DECISION | Revisar: si es dato normal, mover a token var(--text-*-weight) o al componente global correspondiente. |
| `frontend/src/modules/inventory/kardex/pages/KardexPage.css` | 124 | `font-size` | `font-size: var(--text-body-sm-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/inventory/kardex/pages/KardexPage.css` | 132 | `font-size` | `font-size: var(--text-label-sm-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/inventory/kardex/pages/KardexPage.css` | 138 | `font-size` | `font-size: var(--text-headline-sm-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/inventory/kardex/pages/KardexPage.css` | 139 | `font-weight` | `font-weight: 700;` | NEEDS_DECISION | Revisar: si es dato normal, mover a token var(--text-*-weight) o al componente global correspondiente. |
| `frontend/src/modules/inventory/kardex/pages/KardexPage.css` | 178 | `font-size` | `font-size: var(--text-body-sm-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/inventory/kardex/pages/KardexPage.css` | 199 | `font-size` | `font-size: var(--text-body-sm-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/inventory/kardex/pages/KardexPage.css` | 203 | `font-size` | `font-size: var(--text-body-sm-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/inventory/warehouses/pages/BodegasPage.css` | 11 | `font-weight` | `font-weight: 600;` | NEEDS_DECISION | Revisar: si es dato normal, mover a token var(--text-*-weight) o al componente global correspondiente. |
| `frontend/src/modules/inventory/warehouses/pages/BodegasPage.css` | 15 | `font-size` | `font-size: var(--text-label-sm-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/inventory/warehouses/pages/BodegasPage.css` | 20 | `font-size` | `font-size: var(--text-label-sm-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/inventory/warehouses/pages/BodegasPage.css` | 23 | `font-size` | `font-size: var(--text-help-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/inventory/warehouses/pages/BodegasPage.css` | 48 | `font-size` | `font-size: 22px;` | OK_ICON | Sin accion - cumple regla DS. |
| `frontend/src/modules/inventory/warehouses/pages/BodegasPage.css` | 60 | `font-size` | `font-size: var(--text-body-lg-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/inventory/warehouses/pages/BodegasPage.css` | 61 | `font-weight` | `font-weight: 600;` | NEEDS_DECISION | Revisar: si es dato normal, mover a token var(--text-*-weight) o al componente global correspondiente. |
| `frontend/src/modules/inventory/warehouses/pages/BodegasPage.css` | 99 | `font-size` | `font-size: 16px;` | OK_ICON | Sin accion - cumple regla DS. |
| `frontend/src/modules/inventory/warehouses/pages/BodegasPage.css` | 103 | `font-size` | `font-size: var(--text-help-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/inventory/warehouses/pages/BodegasPage.css` | 104 | `font-weight` | `font-weight: 600;` | NEEDS_DECISION | Revisar: si es dato normal, mover a token var(--text-*-weight) o al componente global correspondiente. |
| `frontend/src/modules/inventory/warehouses/pages/BodegasPage.css` | 124 | `font-weight` | `font-weight: var(--text-label-md-weight);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/inventory/warehouses/pages/BodegasPage.css` | 125 | `font-size` | `font-size: var(--text-label-md-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/inventory/warehouses/pages/BodegasPage.css` | 130 | `font-size` | `font-size: var(--text-body-sm-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/inventory/warehouses/pages/BodegasPage.css` | 151 | `font-size` | `font-size: var(--text-label-sm-size, 11px);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/inventory/warehouses/pages/BodegasPage.css` | 153 | `text-transform` | `text-transform: uppercase;` | NEEDS_DECISION | Revisar si es titulo/label global (OK) o uppercase local sin justificacion. |
| `frontend/src/modules/inventory/warehouses/pages/BodegasPage.css` | 154 | `letter-spacing` | `letter-spacing: var(--text-label-sm-spacing);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/inventory/warehouses/pages/BodegasPage.css` | 157 | `font-size` | `font-size: var(--text-body-md-size);` | OK_TOKEN | Sin accion - cumple regla DS. |


