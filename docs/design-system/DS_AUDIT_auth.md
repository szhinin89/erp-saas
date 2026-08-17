# DS Audit Report

- Fecha: 2026-08-17 14:05:47
- Scope: `modules` (moduleName: `auth`)
- Generado por: `scripts/ds/ds-audit.ps1`
- Total hallazgos: **39**

> Auditoria por lineas (regex), no un parser real de CSS/TSX. ` NEEDS_DECISION ` siempre requiere revision humana - ver ` docs/design-system/DS_RULES.md `.

## Conteo por patron

| Patron | Hallazgos |
|---|---|
| `font-size` | 17 |
| `letter-spacing` | 8 |
| `font-weight` | 8 |
| `text-transform` | 4 |
| `line-height` | 2 |

## Conteo por clasificacion

| Clasificacion | Hallazgos |
|---|---|
| `OK_TOKEN` | 24 |
| `NEEDS_DECISION` | 9 |
| `OK_ICON` | 4 |
| `OK_LAYOUT` | 2 |

## Hallazgos

| Archivo | Linea | Patron | Contenido | Clasificacion | Accion sugerida |
|---|---|---|---|---|---|
| `frontend/src/modules/auth/pages/CompletePasswordResetPage.css` | 5 | `font-size` | `font-size: var(--text-headline-md-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/auth/pages/CompletePasswordResetPage.css` | 6 | `font-weight` | `font-weight: var(--text-headline-md-weight);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/auth/pages/CompletePasswordResetPage.css` | 7 | `letter-spacing` | `letter-spacing: var(--text-headline-md-spacing);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/auth/pages/CompletePasswordResetPage.css` | 13 | `font-size` | `font-size: var(--text-label-sm-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/auth/pages/CompletePasswordResetPage.css` | 14 | `font-weight` | `font-weight: var(--text-label-sm-weight);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/auth/pages/CompletePasswordResetPage.css` | 16 | `text-transform` | `text-transform: uppercase;` | NEEDS_DECISION | Revisar si es titulo/label global (OK) o uppercase local sin justificacion. |
| `frontend/src/modules/auth/pages/CompletePasswordResetPage.css` | 17 | `letter-spacing` | `letter-spacing: 0.12em;` | NEEDS_DECISION | Revisar si es titulo/label global (OK) o tracking local sin justificacion. |
| `frontend/src/modules/auth/pages/ForgotPasswordPage.css` | 5 | `font-size` | `font-size: var(--text-headline-md-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/auth/pages/ForgotPasswordPage.css` | 6 | `font-weight` | `font-weight: var(--text-headline-md-weight);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/auth/pages/ForgotPasswordPage.css` | 7 | `letter-spacing` | `letter-spacing: var(--text-headline-md-spacing);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/auth/pages/ForgotPasswordPage.css` | 13 | `font-size` | `font-size: var(--text-label-sm-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/auth/pages/ForgotPasswordPage.css` | 14 | `font-weight` | `font-weight: var(--text-label-sm-weight);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/auth/pages/ForgotPasswordPage.css` | 16 | `text-transform` | `text-transform: uppercase;` | NEEDS_DECISION | Revisar si es titulo/label global (OK) o uppercase local sin justificacion. |
| `frontend/src/modules/auth/pages/ForgotPasswordPage.css` | 17 | `letter-spacing` | `letter-spacing: 0.12em;` | NEEDS_DECISION | Revisar si es titulo/label global (OK) o tracking local sin justificacion. |
| `frontend/src/modules/auth/pages/LoginPage.css` | 33 | `font-size` | `font-size: 22px;` | OK_ICON | Sin accion - cumple regla DS. |
| `frontend/src/modules/auth/pages/LoginPage.css` | 37 | `font-size` | `font-size: var(--text-headline-lg-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/auth/pages/LoginPage.css` | 38 | `font-weight` | `font-weight: var(--text-headline-lg-weight);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/auth/pages/LoginPage.css` | 39 | `letter-spacing` | `letter-spacing: var(--text-headline-lg-spacing);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/auth/pages/LoginPage.css` | 41 | `line-height` | `line-height: 1;` | OK_LAYOUT | Sin accion - cumple regla DS. |
| `frontend/src/modules/auth/pages/LoginPage.css` | 46 | `font-size` | `font-size: var(--text-body-md-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/auth/pages/LoginPage.css` | 70 | `font-size` | `font-size: var(--text-body-md-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/auth/pages/LoginPage.css` | 73 | `font-size` | `font-size: 18px;` | OK_ICON | Sin accion - cumple regla DS. |
| `frontend/src/modules/auth/pages/LoginPage.css` | 94 | `line-height` | `line-height: 1;` | OK_LAYOUT | Sin accion - cumple regla DS. |
| `frontend/src/modules/auth/pages/LoginPage.css` | 101 | `font-size` | `font-size: 18px;` | OK_ICON | Sin accion - cumple regla DS. |
| `frontend/src/modules/auth/pages/LoginPage.css` | 118 | `font-size` | `font-size: var(--text-label-sm-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/auth/pages/LoginPage.css` | 137 | `font-size` | `font-size: var(--text-body-md-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/auth/pages/LoginPage.css` | 174 | `font-size` | `font-size: 14px;` | OK_ICON | Sin accion - cumple regla DS. |
| `frontend/src/modules/auth/pages/LoginPage.css` | 178 | `font-size` | `font-size: var(--text-label-sm-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/auth/pages/LoginPage.css` | 179 | `font-weight` | `font-weight: var(--text-label-sm-weight);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/auth/pages/LoginPage.css` | 180 | `letter-spacing` | `letter-spacing: 0.1em;` | NEEDS_DECISION | Revisar si es titulo/label global (OK) o tracking local sin justificacion. |
| `frontend/src/modules/auth/pages/LoginPage.css` | 181 | `text-transform` | `text-transform: uppercase;` | NEEDS_DECISION | Revisar si es titulo/label global (OK) o uppercase local sin justificacion. |
| `frontend/src/modules/auth/pages/LoginPage.css` | 201 | `font-size` | `font-size: 22px;` | NEEDS_DECISION | Revisar: si es dato normal, mover a token var(--text-*) o a ZHFieldLabel/ZHDataValue/ZHMoneyValue global. |
| `frontend/src/modules/auth/pages/ResetPasswordPage.css` | 5 | `font-size` | `font-size: var(--text-headline-md-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/auth/pages/ResetPasswordPage.css` | 6 | `font-weight` | `font-weight: var(--text-headline-md-weight);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/auth/pages/ResetPasswordPage.css` | 7 | `letter-spacing` | `letter-spacing: var(--text-headline-md-spacing);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/auth/pages/ResetPasswordPage.css` | 13 | `font-size` | `font-size: var(--text-label-sm-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/auth/pages/ResetPasswordPage.css` | 14 | `font-weight` | `font-weight: var(--text-label-sm-weight);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/auth/pages/ResetPasswordPage.css` | 16 | `text-transform` | `text-transform: uppercase;` | NEEDS_DECISION | Revisar si es titulo/label global (OK) o uppercase local sin justificacion. |
| `frontend/src/modules/auth/pages/ResetPasswordPage.css` | 17 | `letter-spacing` | `letter-spacing: 0.12em;` | NEEDS_DECISION | Revisar si es titulo/label global (OK) o tracking local sin justificacion. |


