# DS Audit Report

- Fecha: 2026-08-17 15:14:40
- Scope: `modules` (moduleName: `security`)
- Generado por: `scripts/ds/ds-audit.ps1`
- Total hallazgos: **6**

> Auditoria por lineas (regex), no un parser real de CSS/TSX. ` NEEDS_DECISION ` siempre requiere revision humana - ver ` docs/design-system/DS_RULES.md `.

## Conteo por patron

| Patron | Hallazgos |
|---|---|
| `font-size` | 2 |
| `font-weight` | 2 |
| `letter-spacing` | 1 |
| `text-transform` | 1 |

## Conteo por clasificacion

| Clasificacion | Hallazgos |
|---|---|
| `OK_TOKEN` | 3 |
| `NEEDS_DECISION` | 3 |

## Hallazgos

| Archivo | Linea | Patron | Contenido | Clasificacion | Accion sugerida |
|---|---|---|---|---|---|
| `frontend/src/modules/security/pages/SecuritySettingsPage.css` | 18 | `font-weight` | `font-weight: 600;` | NEEDS_DECISION | Revisar: si es dato normal, mover a token var(--text-*-weight) o al componente global correspondiente. |
| `frontend/src/modules/security/pages/SecuritySettingsPage.css` | 19 | `font-size` | `font-size: var(--text-body-md-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/security/pages/SecuritySettingsPage.css` | 39 | `font-size` | `font-size: var(--text-badge-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/security/pages/SecuritySettingsPage.css` | 40 | `font-weight` | `font-weight: var(--text-badge-weight);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/security/pages/SecuritySettingsPage.css` | 41 | `text-transform` | `text-transform: uppercase;` | NEEDS_DECISION | Revisar si es titulo/label global (OK) o uppercase local sin justificacion. |
| `frontend/src/modules/security/pages/SecuritySettingsPage.css` | 42 | `letter-spacing` | `letter-spacing: 0.04em;` | NEEDS_DECISION | Revisar si es titulo/label global (OK) o tracking local sin justificacion. |


