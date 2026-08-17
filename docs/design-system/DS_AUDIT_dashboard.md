# DS Audit Report

- Fecha: 2026-08-17 13:49:50
- Scope: `modules` (moduleName: `dashboard`)
- Generado por: `scripts/ds/ds-audit.ps1`
- Total hallazgos: **23**

> Auditoria por lineas (regex), no un parser real de CSS/TSX. ` NEEDS_DECISION ` siempre requiere revision humana - ver ` docs/design-system/DS_RULES.md `.

## Conteo por patron

| Patron | Hallazgos |
|---|---|
| `font-size` | 16 |
| `font-weight` | 6 |
| `letter-spacing` | 1 |

## Conteo por clasificacion

| Clasificacion | Hallazgos |
|---|---|
| `OK_TOKEN` | 20 |
| `NEEDS_DECISION` | 2 |
| `OK_ICON` | 1 |

## Hallazgos

| Archivo | Linea | Patron | Contenido | Clasificacion | Accion sugerida |
|---|---|---|---|---|---|
| `frontend/src/modules/dashboard/pages/DashboardPage.css` | 38 | `font-size` | `font-size: var(--text-label-sm-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/dashboard/pages/DashboardPage.css` | 73 | `font-size` | `font-size: var(--text-label-sm-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/dashboard/pages/DashboardPage.css` | 91 | `font-size` | `font-size: var(--text-label-md-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/dashboard/pages/DashboardPage.css` | 95 | `font-size` | `font-size: var(--text-label-md-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/dashboard/pages/DashboardPage.css` | 96 | `font-weight` | `font-weight: 600;` | NEEDS_DECISION | Revisar: si es dato normal, mover a token var(--text-*-weight) o al componente global correspondiente. |
| `frontend/src/modules/dashboard/pages/DashboardPage.css` | 115 | `font-size` | `font-size: var(--text-headline-md-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/dashboard/pages/DashboardPage.css` | 116 | `font-weight` | `font-weight: var(--text-headline-md-weight);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/dashboard/pages/DashboardPage.css` | 117 | `letter-spacing` | `letter-spacing: var(--text-headline-md-spacing);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/dashboard/pages/DashboardPage.css` | 123 | `font-size` | `font-size: var(--text-label-md-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/dashboard/pages/DashboardPage.css` | 124 | `font-weight` | `font-weight: var(--text-label-md-weight);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/dashboard/pages/DashboardPage.css` | 143 | `font-size` | `font-size: var(--text-label-sm-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/dashboard/pages/DashboardPage.css` | 191 | `font-size` | `font-size: 20px;` | OK_ICON | Sin accion - cumple regla DS. |
| `frontend/src/modules/dashboard/pages/DashboardPage.css` | 203 | `font-size` | `font-size: var(--text-label-md-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/dashboard/pages/DashboardPage.css` | 204 | `font-weight` | `font-weight: var(--text-label-md-weight);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/dashboard/pages/DashboardPage.css` | 210 | `font-size` | `font-size: var(--text-help-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/dashboard/pages/DashboardPage.css` | 244 | `font-size` | `font-size: var(--text-label-md-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/dashboard/pages/DashboardPage.css` | 245 | `font-weight` | `font-weight: var(--text-label-md-weight);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/dashboard/pages/DashboardPage.css` | 250 | `font-size` | `font-size: var(--text-help-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/dashboard/pages/DashboardPage.css` | 255 | `font-size` | `font-size: var(--text-help-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/dashboard/pages/DashboardPage.css` | 279 | `font-size` | `font-size: var(--text-label-md-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/dashboard/pages/DashboardPage.css` | 280 | `font-weight` | `font-weight: 700;` | NEEDS_DECISION | Revisar: si es dato normal, mover a token var(--text-*-weight) o al componente global correspondiente. |
| `frontend/src/modules/dashboard/pages/DashboardPage.css` | 285 | `font-size` | `font-size: var(--text-help-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/dashboard/pages/DashboardPage.css` | 295 | `font-size` | `font-size: var(--text-help-size);` | OK_TOKEN | Sin accion - cumple regla DS. |


