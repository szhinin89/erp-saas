# DS Audit Report

- Fecha: 2026-08-17 14:06:28
- Scope: `modules` (moduleName: `access`)
- Generado por: `scripts/ds/ds-audit.ps1`
- Total hallazgos: **22**

> Auditoria por lineas (regex), no un parser real de CSS/TSX. ` NEEDS_DECISION ` siempre requiere revision humana - ver ` docs/design-system/DS_RULES.md `.

## Conteo por patron

| Patron | Hallazgos |
|---|---|
| `font-size` | 13 |
| `font-weight` | 5 |
| `line-height` | 2 |
| `text-transform` | 1 |
| `letter-spacing` | 1 |

## Conteo por clasificacion

| Clasificacion | Hallazgos |
|---|---|
| `OK_TOKEN` | 11 |
| `NEEDS_DECISION` | 5 |
| `OK_ICON` | 4 |
| `OK_LAYOUT` | 2 |

## Hallazgos

| Archivo | Linea | Patron | Contenido | Clasificacion | Accion sugerida |
|---|---|---|---|---|---|
| `frontend/src/modules/access/pages/ProfilesPage.css` | 44 | `font-size` | `font-size: 16px;` | OK_ICON | Sin accion - cumple regla DS. |
| `frontend/src/modules/access/pages/ProfilesPage.css` | 69 | `font-size` | `font-size: 20px;` | OK_ICON | Sin accion - cumple regla DS. |
| `frontend/src/modules/access/pages/ProfilesPage.css` | 75 | `font-size` | `font-size: var(--text-label-sm-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/access/pages/ProfilesPage.css` | 76 | `font-weight` | `font-weight: var(--text-label-sm-weight);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/access/pages/ProfilesPage.css` | 82 | `font-size` | `font-size: var(--text-label-sm-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/access/pages/ProfilesPage.css` | 83 | `line-height` | `line-height: 1.5;` | OK_LAYOUT | Sin accion - cumple regla DS. |
| `frontend/src/modules/access/pages/ProfilesPage.css` | 96 | `font-size` | `font-size: var(--text-label-sm-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/access/pages/ProfilesPage.css` | 97 | `font-weight` | `font-weight: var(--text-label-sm-weight);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/access/pages/ProfilesPage.css` | 101 | `font-size` | `font-size: var(--text-label-sm-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/access/pages/ProfilesPage.css` | 102 | `font-weight` | `font-weight: 700;` | NEEDS_DECISION | Revisar: si es dato normal, mover a token var(--text-*-weight) o al componente global correspondiente. |
| `frontend/src/modules/access/pages/ProfilesPage.css` | 145 | `font-size` | `font-size: 14px;` | OK_ICON | Sin accion - cumple regla DS. |
| `frontend/src/modules/access/pages/ProfilesPage.css` | 155 | `font-size` | `font-size: var(--text-label-sm-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/access/pages/ProfilesPage.css` | 178 | `font-size` | `font-size: 28px;` | OK_ICON | Sin accion - cumple regla DS. |
| `frontend/src/modules/access/pages/ProfilesPage.css` | 184 | `font-size` | `font-size: var(--text-label-sm-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/access/pages/ProfilesPage.css` | 185 | `font-weight` | `font-weight: 700;` | NEEDS_DECISION | Revisar: si es dato normal, mover a token var(--text-*-weight) o al componente global correspondiente. |
| `frontend/src/modules/access/pages/ProfilesPage.css` | 186 | `letter-spacing` | `letter-spacing: 0.08em;` | NEEDS_DECISION | Revisar si es titulo/label global (OK) o tracking local sin justificacion. |
| `frontend/src/modules/access/pages/ProfilesPage.css` | 187 | `text-transform` | `text-transform: uppercase;` | NEEDS_DECISION | Revisar si es titulo/label global (OK) o uppercase local sin justificacion. |
| `frontend/src/modules/access/pages/ProfilesPage.css` | 193 | `font-size` | `font-size: var(--text-help-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/access/pages/ProfilesPage.css` | 194 | `line-height` | `line-height: 1.4;` | OK_LAYOUT | Sin accion - cumple regla DS. |
| `frontend/src/modules/access/users/pages/UsersPage.css` | 35 | `font-weight` | `font-weight: 600;` | NEEDS_DECISION | Revisar: si es dato normal, mover a token var(--text-*-weight) o al componente global correspondiente. |
| `frontend/src/modules/access/users/pages/UsersPage.css` | 36 | `font-size` | `font-size: var(--text-body-md-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/access/users/pages/UsersPage.css` | 41 | `font-size` | `font-size: var(--text-body-sm-size);` | OK_TOKEN | Sin accion - cumple regla DS. |


