# DS Audit Report

- Fecha: 2026-08-17 13:49:57
- Scope: `modules`
- Generado por: `scripts/ds/ds-audit.ps1`
- Total hallazgos: **496**

> Auditoria por lineas (regex), no un parser real de CSS/TSX. ` NEEDS_DECISION ` siempre requiere revision humana - ver ` docs/design-system/DS_RULES.md `.

## Conteo por patron

| Patron | Hallazgos |
|---|---|
| `font-size` | 278 |
| `font-weight` | 137 |
| `letter-spacing` | 39 |
| `text-transform` | 25 |
| `line-height` | 15 |
| `font-style` | 2 |

## Conteo por clasificacion

| Clasificacion | Hallazgos |
|---|---|
| `OK_TOKEN` | 264 |
| `NEEDS_DECISION` | 126 |
| `OK_GLOBAL` | 56 |
| `OK_ICON` | 35 |
| `OK_LAYOUT` | 15 |

## Hallazgos

| Archivo | Linea | Patron | Contenido | Clasificacion | Accion sugerida |
|---|---|---|---|---|---|
| `frontend/src/modules/access/pages/ProfilesPage.css` | 44 | `font-size` | `font-size: 16px;` | OK_ICON | Sin accion - cumple regla DS. |
| `frontend/src/modules/access/pages/ProfilesPage.css` | 69 | `font-size` | `font-size: 20px;` | OK_ICON | Sin accion - cumple regla DS. |
| `frontend/src/modules/access/pages/ProfilesPage.css` | 75 | `font-size` | `font-size: var(--text-label-sm-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/access/pages/ProfilesPage.css` | 76 | `font-weight` | `font-weight: 600;` | NEEDS_DECISION | Revisar: si es dato normal, mover a token var(--text-*-weight) o al componente global correspondiente. |
| `frontend/src/modules/access/pages/ProfilesPage.css` | 82 | `font-size` | `font-size: var(--text-label-sm-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/access/pages/ProfilesPage.css` | 83 | `line-height` | `line-height: 1.5;` | OK_LAYOUT | Sin accion - cumple regla DS. |
| `frontend/src/modules/access/pages/ProfilesPage.css` | 96 | `font-size` | `font-size: var(--text-label-sm-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/access/pages/ProfilesPage.css` | 97 | `font-weight` | `font-weight: 600;` | NEEDS_DECISION | Revisar: si es dato normal, mover a token var(--text-*-weight) o al componente global correspondiente. |
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
| `frontend/src/modules/auth/pages/LoginPage.css` | 118 | `font-size` | `font-size: 12px;` | NEEDS_DECISION | Revisar: si es dato normal, mover a token var(--text-*) o a ZHFieldLabel/ZHDataValue/ZHMoneyValue global. |
| `frontend/src/modules/auth/pages/LoginPage.css` | 137 | `font-size` | `font-size: var(--text-body-md-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/auth/pages/LoginPage.css` | 174 | `font-size` | `font-size: 14px;` | OK_ICON | Sin accion - cumple regla DS. |
| `frontend/src/modules/auth/pages/LoginPage.css` | 178 | `font-size` | `font-size: 10px;` | NEEDS_DECISION | Revisar: si es dato normal, mover a token var(--text-*) o a ZHFieldLabel/ZHDataValue/ZHMoneyValue global. |
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
| `frontend/src/modules/caja/pages/CajaPage.css` | 51 | `font-size` | `font-size: var(--text-label-sm-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/caja/pages/CajaPage.css` | 56 | `font-size` | `font-size: 18px;` | NEEDS_DECISION | Revisar: si es dato normal, mover a token var(--text-*) o a ZHFieldLabel/ZHDataValue/ZHMoneyValue global. |
| `frontend/src/modules/caja/pages/CajaPage.css` | 57 | `font-weight` | `font-weight: 700;` | NEEDS_DECISION | Revisar: si es dato normal, mover a token var(--text-*-weight) o al componente global correspondiente. |
| `frontend/src/modules/caja/pages/CajaPage.css` | 105 | `font-weight` | `font-weight: 700;` | NEEDS_DECISION | Revisar: si es dato normal, mover a token var(--text-*-weight) o al componente global correspondiente. |
| `frontend/src/modules/company-management/pages/company-management.css` | 2 | `font-weight` | `font-weight: 600;` | NEEDS_DECISION | Revisar: si es dato normal, mover a token var(--text-*-weight) o al componente global correspondiente. |
| `frontend/src/modules/configuracion/facturacionElectronica/sections/sri-config-page.css` | 49 | `font-weight` | `font-weight: var(--text-label-md-weight);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/configuracion/facturacionElectronica/sections/sri-config-page.css` | 50 | `font-size` | `font-size: var(--text-label-md-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/configuracion/facturacionElectronica/sections/sri-config-page.css` | 54 | `font-size` | `font-size: var(--text-help-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/configuracion/facturacionElectronica/sections/sri-config-page.css` | 61 | `font-size` | `font-size: var(--text-help-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/configuracion/facturacionElectronica/sections/sri-config-page.css` | 71 | `font-size` | `font-size: var(--text-help-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/configuracion/facturacionElectronica/sections/sri-config-page.css` | 97 | `line-height` | `line-height: 0;` | OK_LAYOUT | Sin accion - cumple regla DS. |
| `frontend/src/modules/configuracion/facturacionElectronica/sections/sri-config-page.css` | 109 | `font-size` | `font-size: var(--text-help-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/configuracion/facturacionElectronica/sections/sri-config-page.css` | 126 | `font-size` | `font-size: var(--text-label-md-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/configuracion/facturacionElectronica/sections/sri-config-page.css` | 143 | `font-size` | `font-size: var(--text-help-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
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
| `frontend/src/modules/electronicDocuments/monitor/components/electronic-documents-monitor.css` | 19 | `font-weight` | `font-weight: 400;` | NEEDS_DECISION | Revisar: si es dato normal, mover a token var(--text-*-weight) o al componente global correspondiente. |
| `frontend/src/modules/electronicDocuments/monitor/components/electronic-documents-monitor.css` | 29 | `font-size` | `font-size: var(--text-help-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/electronicDocuments/monitor/components/electronic-documents-monitor.css` | 34 | `font-size` | `font-size: var(--text-help-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/electronicDocuments/monitor/components/electronic-documents-monitor.css` | 50 | `line-height` | `line-height: 0;` | OK_LAYOUT | Sin accion - cumple regla DS. |
| `frontend/src/modules/electronicDocuments/monitor/components/electronic-documents-monitor.css` | 61 | `font-size` | `font-size: var(--text-help-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/electronicDocuments/monitor/components/electronic-documents-monitor.css` | 67 | `font-size` | `font-size: var(--text-label-md-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/electronicDocuments/monitor/components/electronic-documents-monitor.css` | 100 | `font-size` | `font-size: var(--text-help-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/electronicDocuments/monitor/components/electronic-documents-monitor.css` | 111 | `font-size` | `font-size: 12px;` | NEEDS_DECISION | Revisar: si es dato normal, mover a token var(--text-*) o a ZHFieldLabel/ZHDataValue/ZHMoneyValue global. |
| `frontend/src/modules/electronicDocuments/monitor/components/electronic-documents-monitor.css` | 118 | `font-size` | `font-size: var(--text-help-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/electronicDocuments/monitor/components/electronic-documents-monitor.css` | 158 | `font-size` | `font-size: var(--text-help-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/electronicDocuments/monitor/components/electronic-documents-monitor.css` | 163 | `font-size` | `font-size: var(--text-label-md-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/electronicDocuments/monitor/components/electronic-documents-monitor.css` | 170 | `font-size` | `font-size: var(--text-help-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/electronicDocuments/monitor/components/electronic-documents-monitor.css` | 178 | `font-size` | `font-size: var(--text-label-md-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/electronicDocuments/monitor/components/electronic-documents-monitor.css` | 179 | `font-weight` | `font-weight: 600;` | NEEDS_DECISION | Revisar: si es dato normal, mover a token var(--text-*-weight) o al componente global correspondiente. |
| `frontend/src/modules/emissionPoints/pages/emission-points-page.css` | 29 | `font-size` | `font-size: var(--text-body-sm-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
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
| `frontend/src/modules/items/catalog/wizard/catalog-wizard.css` | 14 | `font-size` | `font-size: var(--text-body-md-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/items/catalog/wizard/catalog-wizard.css` | 51 | `font-size` | `font-size: 20px;` | OK_ICON | Sin accion - cumple regla DS. |
| `frontend/src/modules/items/catalog/wizard/catalog-wizard.css` | 59 | `font-size` | `font-size: 20px;` | OK_ICON | Sin accion - cumple regla DS. |
| `frontend/src/modules/items/catalog/wizard/catalog-wizard.css` | 64 | `font-size` | `font-size: var(--text-body-md-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/items/catalog/wizard/catalog-wizard.css` | 80 | `font-size` | `font-size: var(--text-help-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/items/catalog/wizard/catalog-wizard.css` | 84 | `font-size` | `font-size: var(--text-label-sm-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/items/catalog/wizard/catalog-wizard.css` | 85 | `font-weight` | `font-weight: 600;` | NEEDS_DECISION | Revisar: si es dato normal, mover a token var(--text-*-weight) o al componente global correspondiente. |
| `frontend/src/modules/logistica/transportistas/pages/carriers-page.css` | 19 | `font-weight` | `font-weight: 600;` | NEEDS_DECISION | Revisar: si es dato normal, mover a token var(--text-*-weight) o al componente global correspondiente. |
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
| `frontend/src/modules/purchases/styles/purchase-credit-note.css` | 14 | `font-size` | `font-size: var(--text-label-sm-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/purchases/styles/purchase-credit-note.css` | 15 | `text-transform` | `text-transform: uppercase;` | NEEDS_DECISION | Revisar si es titulo/label global (OK) o uppercase local sin justificacion. |
| `frontend/src/modules/purchases/styles/purchase-credit-note.css` | 16 | `letter-spacing` | `letter-spacing: var(--text-label-sm-spacing);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/purchases/styles/purchase-credit-note.css` | 22 | `font-size` | `font-size: var(--text-body-md-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/purchases/styles/purchase-credit-note.css` | 23 | `font-weight` | `font-weight: 600;` | NEEDS_DECISION | Revisar: si es dato normal, mover a token var(--text-*-weight) o al componente global correspondiente. |
| `frontend/src/modules/purchases/styles/purchase-credit-note.css` | 30 | `font-size` | `font-size: inherit;` | OK_GLOBAL | Sin accion - cumple regla DS. |
| `frontend/src/modules/purchases/styles/purchase-credit-note.css` | 31 | `font-weight` | `font-weight: inherit;` | OK_GLOBAL | Sin accion - cumple regla DS. |
| `frontend/src/modules/purchases/styles/purchase-credit-note.css` | 35 | `font-size` | `font-size: inherit;` | OK_GLOBAL | Sin accion - cumple regla DS. |
| `frontend/src/modules/purchases/styles/purchase-credit-note.css` | 36 | `font-weight` | `font-weight: inherit;` | OK_GLOBAL | Sin accion - cumple regla DS. |
| `frontend/src/modules/purchases/styles/purchase-credit-note.css` | 57 | `font-size` | `font-size: var(--text-help-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/purchases/styles/purchase-credit-note.css` | 62 | `font-size` | `font-size: var(--text-label-md-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/purchases/styles/purchase-credit-note.css` | 63 | `font-weight` | `font-weight: var(--text-label-md-weight);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/purchases/styles/purchase-credit-note.css` | 76 | `font-weight` | `font-weight: 600;` | NEEDS_DECISION | Revisar: si es dato normal, mover a token var(--text-*-weight) o al componente global correspondiente. |
| `frontend/src/modules/purchases/styles/purchase-credit-note.css` | 77 | `font-size` | `font-size: var(--text-body-md-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/purchases/styles/purchase-credit-note.css` | 81 | `font-size` | `font-size: var(--text-help-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/purchases/styles/purchase-credit-note.css` | 86 | `font-size` | `font-size: var(--text-help-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/purchases/styles/purchase-credit-note.css` | 93 | `font-size` | `font-size: var(--text-body-md-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/purchases/styles/purchase-reception.css` | 49 | `font-weight` | `font-weight: var(--text-label-md-weight);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/purchases/styles/purchase-reception.css` | 54 | `font-size` | `font-size: var(--text-help-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/purchases/styles/purchase-reception.css` | 69 | `font-size` | `font-size: var(--text-label-md-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/purchases/styles/purchase-reception.css` | 70 | `font-weight` | `font-weight: var(--text-label-md-weight);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/purchases/styles/purchase-reception.css` | 75 | `font-size` | `font-size: var(--text-help-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/purchases/styles/purchase-reception.css` | 85 | `font-size` | `font-size: var(--text-help-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/purchases/styles/purchase-reception.css` | 91 | `font-size` | `font-size: var(--text-help-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/purchases/styles/purchase-reception.css` | 112 | `font-size` | `font-size: var(--text-help-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/purchases/styles/purchase-reception.css` | 122 | `font-size` | `font-size: var(--text-label-md-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/purchases/styles/purchase-reception.css` | 123 | `font-weight` | `font-weight: 700;` | NEEDS_DECISION | Revisar: si es dato normal, mover a token var(--text-*-weight) o al componente global correspondiente. |
| `frontend/src/modules/purchases/styles/purchase-reception.css` | 131 | `font-size` | `font-size: inherit;` | OK_GLOBAL | Sin accion - cumple regla DS. |
| `frontend/src/modules/purchases/styles/purchase-reception.css` | 132 | `font-weight` | `font-weight: inherit;` | OK_GLOBAL | Sin accion - cumple regla DS. |
| `frontend/src/modules/purchases/styles/purchase-reception.css` | 149 | `font-size` | `font-size: var(--text-help-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/purchases/styles/purchase-reception.css` | 160 | `font-size` | `font-size: var(--text-help-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/purchases/styles/purchase-reception.css` | 179 | `font-size` | `font-size: var(--text-help-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/purchases/styles/purchase-reception.css` | 192 | `font-size` | `font-size: var(--text-help-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/purchases/styles/purchase-reception.css` | 203 | `font-size` | `font-size: var(--text-help-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/purchases/styles/purchase-reception.css` | 220 | `font-weight` | `font-weight: 700;` | NEEDS_DECISION | Revisar: si es dato normal, mover a token var(--text-*-weight) o al componente global correspondiente. |
| `frontend/src/modules/purchases/styles/purchase-reception.css` | 226 | `font-size` | `font-size: var(--text-help-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/purchases/styles/purchase-reception.css` | 227 | `font-weight` | `font-weight: 600;` | NEEDS_DECISION | Revisar: si es dato normal, mover a token var(--text-*-weight) o al componente global correspondiente. |
| `frontend/src/modules/purchases/styles/purchase-reception.css` | 243 | `font-size` | `font-size: var(--text-help-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/purchases/styles/purchase-reception.css` | 244 | `font-weight` | `font-weight: 700;` | NEEDS_DECISION | Revisar: si es dato normal, mover a token var(--text-*-weight) o al componente global correspondiente. |
| `frontend/src/modules/purchases/styles/purchase-reception.css` | 250 | `font-size` | `font-size: var(--text-help-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/purchases/styles/purchase-reception.css` | 256 | `font-size` | `font-size: var(--text-label-md-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/purchases/styles/purchase-reception.css` | 257 | `font-weight` | `font-weight: 600;` | NEEDS_DECISION | Revisar: si es dato normal, mover a token var(--text-*-weight) o al componente global correspondiente. |
| `frontend/src/modules/purchases/styles/purchase-reception.css` | 266 | `font-size` | `font-size: var(--text-help-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/purchases/styles/purchases-invoice.css` | 19 | `font-size` | `.pf-icon--22 { font-size: 22px; }` | OK_ICON | Sin accion - cumple regla DS. |
| `frontend/src/modules/purchases/styles/purchases-invoice.css` | 27 | `font-size` | `font-size: 22px;` | OK_ICON | Sin accion - cumple regla DS. |
| `frontend/src/modules/purchases/styles/purchases-invoice.css` | 31 | `font-size` | `.pdl-block__icon { font-size: 16px; }` | OK_ICON | Sin accion - cumple regla DS. |
| `frontend/src/modules/purchases/styles/purchases-invoice.css` | 32 | `font-size` | `.pdl-ctx-col__loading-icon { font-size: 13px; }` | OK_ICON | Sin accion - cumple regla DS. |
| `frontend/src/modules/purchases/styles/purchases-invoice.css` | 33 | `font-size` | `.pdl-cost-alert__icon { font-size: 13px; }` | OK_ICON | Sin accion - cumple regla DS. |
| `frontend/src/modules/purchases/styles/purchases-invoice.css` | 178 | `font-size` | `font-size: var(--text-headline-md-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/purchases/styles/purchases-invoice.css` | 179 | `font-weight` | `font-weight: 700;` | NEEDS_DECISION | Revisar: si es dato normal, mover a token var(--text-*-weight) o al componente global correspondiente. |
| `frontend/src/modules/purchases/styles/purchases-invoice.css` | 207 | `font-size` | `font-size: 18px;` | OK_ICON | Sin accion - cumple regla DS. |
| `frontend/src/modules/purchases/styles/purchases-invoice.css` | 235 | `font-weight` | `font-weight: 600;` | NEEDS_DECISION | Revisar: si es dato normal, mover a token var(--text-*-weight) o al componente global correspondiente. |
| `frontend/src/modules/purchases/styles/purchases-invoice.css` | 390 | `font-size` | `font-size: var(--text-label-sm-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/purchases/styles/purchases-invoice.css` | 391 | `font-weight` | `font-weight: var(--text-label-sm-weight);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/purchases/styles/purchases-invoice.css` | 400 | `font-size` | `font-size: 18px;` | OK_ICON | Sin accion - cumple regla DS. |
| `frontend/src/modules/purchases/styles/purchases-invoice.css` | 410 | `font-size` | `font-size: 18px;` | NEEDS_DECISION | Revisar: si es dato normal, mover a token var(--text-*) o a ZHFieldLabel/ZHDataValue/ZHMoneyValue global. |
| `frontend/src/modules/purchases/styles/purchases-invoice.css` | 523 | `font-size` | `.pf-schedule-action-icon { font-size: 14px; }` | OK_ICON | Sin accion - cumple regla DS. |
| `frontend/src/modules/purchases/styles/purchases-invoice.css` | 534 | `font-size` | `font-size: var(--text-label-sm-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/purchases/styles/purchases-invoice.css` | 541 | `font-weight` | `.pf-schedule-installment-number { font-weight: 600; }` | NEEDS_DECISION | Revisar: si es dato normal, mover a token var(--text-*-weight) o al componente global correspondiente. |
| `frontend/src/modules/purchases/styles/purchases-invoice.css` | 544 | `font-size` | `font-size: var(--text-help-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/purchases/styles/purchases-invoice.css` | 558 | `font-size` | `font-size: var(--text-label-sm-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/purchases/styles/purchases-invoice.css` | 563 | `font-size` | `font-size: inherit;` | OK_GLOBAL | Sin accion - cumple regla DS. |
| `frontend/src/modules/purchases/styles/purchases-invoice.css` | 564 | `font-weight` | `font-weight: inherit;` | OK_GLOBAL | Sin accion - cumple regla DS. |
| `frontend/src/modules/purchases/styles/purchases-invoice.css` | 574 | `font-size` | `font-size: var(--text-label-sm-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/purchases/styles/purchases-invoice.css` | 577 | `font-size` | `font-size: 32px;` | OK_ICON | Sin accion - cumple regla DS. |
| `frontend/src/modules/purchases/styles/purchases-invoice.css` | 589 | `font-size` | `.pf-retention-action-icon { font-size: 16px; }` | OK_ICON | Sin accion - cumple regla DS. |
| `frontend/src/modules/purchases/styles/purchases-invoice.css` | 590 | `font-weight` | `.pf-retention-amount { font-weight: 700; }` | NEEDS_DECISION | Revisar: si es dato normal, mover a token var(--text-*-weight) o al componente global correspondiente. |
| `frontend/src/modules/purchases/styles/purchases-invoice.css` | 592 | `font-size` | `font-size: inherit;` | OK_GLOBAL | Sin accion - cumple regla DS. |
| `frontend/src/modules/purchases/styles/purchases-invoice.css` | 593 | `font-weight` | `font-weight: inherit;` | OK_GLOBAL | Sin accion - cumple regla DS. |
| `frontend/src/modules/purchases/styles/purchases-invoice.css` | 597 | `font-size` | `font-size: 20px;` | OK_ICON | Sin accion - cumple regla DS. |
| `frontend/src/modules/purchases/styles/purchases-invoice.css` | 601 | `font-weight` | `.pf-totals__label--strong { font-weight: 600; }` | NEEDS_DECISION | Revisar: si es dato normal, mover a token var(--text-*-weight) o al componente global correspondiente. |
| `frontend/src/modules/purchases/styles/purchases-invoice.css` | 603 | `font-size` | `font-size: var(--text-label-sm-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/purchases/styles/purchases-invoice.css` | 605 | `font-weight` | `font-weight: var(--text-label-sm-weight);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/purchases/styles/purchases-invoice.css` | 613 | `font-weight` | `font-weight: 600;` | NEEDS_DECISION | Revisar: si es dato normal, mover a token var(--text-*-weight) o al componente global correspondiente. |
| `frontend/src/modules/purchases/styles/purchases-invoice.css` | 621 | `font-size` | `font-size: var(--text-label-sm-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/purchases/styles/purchases-invoice.css` | 639 | `font-size` | `font-size: var(--text-label-sm-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/purchases/styles/purchases-invoice.css` | 655 | `font-size` | `font-size: var(--text-body-md-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/purchases/styles/purchases-invoice.css` | 659 | `font-size` | `font-size: 18px;` | OK_ICON | Sin accion - cumple regla DS. |
| `frontend/src/modules/purchases/styles/purchases-invoice.css` | 660 | `line-height` | `line-height: 1.2;` | OK_LAYOUT | Sin accion - cumple regla DS. |
| `frontend/src/modules/purchases/styles/purchases-invoice.css` | 713 | `font-size` | `font-size: var(--text-label-md-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/purchases/styles/purchases-invoice.css` | 714 | `font-weight` | `font-weight: var(--text-label-md-weight);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/purchases/styles/purchases-invoice.css` | 739 | `font-size` | `font-size: var(--text-label-sm-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/purchases/styles/purchases-invoice.css` | 743 | `font-size` | `font-size: var(--text-body-md-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/purchases/styles/purchases-invoice.css` | 744 | `font-weight` | `font-weight: 600;` | NEEDS_DECISION | Revisar: si es dato normal, mover a token var(--text-*-weight) o al componente global correspondiente. |
| `frontend/src/modules/purchases/styles/purchases-invoice.css` | 751 | `font-size` | `font-size: inherit;` | OK_GLOBAL | Sin accion - cumple regla DS. |
| `frontend/src/modules/purchases/styles/purchases-invoice.css` | 752 | `font-weight` | `font-weight: inherit;` | OK_GLOBAL | Sin accion - cumple regla DS. |
| `frontend/src/modules/purchases/styles/purchases-invoice.css` | 769 | `font-size` | `font-size: var(--text-label-md-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/purchases/styles/purchases-invoice.css` | 770 | `font-weight` | `font-weight: 700;` | NEEDS_DECISION | Revisar: si es dato normal, mover a token var(--text-*-weight) o al componente global correspondiente. |
| `frontend/src/modules/purchases/styles/purchases-invoice.css` | 777 | `font-size` | `font-size: var(--text-headline-md-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/purchases/styles/purchases-invoice.css` | 778 | `font-weight` | `font-weight: 800;` | NEEDS_DECISION | Revisar: si es dato normal, mover a token var(--text-*-weight) o al componente global correspondiente. |
| `frontend/src/modules/purchases/styles/purchases-invoice.css` | 780 | `letter-spacing` | `letter-spacing: -0.5px;` | NEEDS_DECISION | Revisar si es titulo/label global (OK) o tracking local sin justificacion. |
| `frontend/src/modules/purchases/styles/purchases-invoice.css` | 783 | `font-size` | `font-size: inherit;` | OK_GLOBAL | Sin accion - cumple regla DS. |
| `frontend/src/modules/purchases/styles/purchases-invoice.css` | 784 | `font-weight` | `font-weight: inherit;` | OK_GLOBAL | Sin accion - cumple regla DS. |
| `frontend/src/modules/purchases/styles/purchases-invoice.css` | 786 | `letter-spacing` | `letter-spacing: inherit;` | OK_GLOBAL | Sin accion - cumple regla DS. |
| `frontend/src/modules/purchases/styles/purchases-invoice.css` | 810 | `font-size` | `font-size: var(--text-label-sm-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/purchases/styles/purchases-invoice.css` | 818 | `font-size` | `font-size: var(--text-title-sm-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/purchases/styles/purchases-invoice.css` | 819 | `font-weight` | `font-weight: 800;` | NEEDS_DECISION | Revisar: si es dato normal, mover a token var(--text-*-weight) o al componente global correspondiente. |
| `frontend/src/modules/purchases/styles/purchases-invoice.css` | 829 | `font-size` | `font-size: var(--text-help-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/purchases/styles/purchases-invoice.css` | 856 | `font-size` | `font-size: var(--text-headline-sm-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/purchases/styles/purchases-invoice.css` | 857 | `font-weight` | `font-weight: 700;` | NEEDS_DECISION | Revisar: si es dato normal, mover a token var(--text-*-weight) o al componente global correspondiente. |
| `frontend/src/modules/purchases/styles/purchases-invoice.css` | 910 | `font-size` | `font-size: var(--text-help-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/purchases/styles/purchases-invoice.css` | 911 | `font-weight` | `font-weight: 600;` | NEEDS_DECISION | Revisar: si es dato normal, mover a token var(--text-*-weight) o al componente global correspondiente. |
| `frontend/src/modules/purchases/styles/purchases-invoice.css` | 985 | `font-size` | `font-size: var(--text-label-md-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/purchases/styles/purchases-invoice.css` | 986 | `font-weight` | `font-weight: 700;` | NEEDS_DECISION | Revisar: si es dato normal, mover a token var(--text-*-weight) o al componente global correspondiente. |
| `frontend/src/modules/purchases/styles/purchases-invoice.css` | 991 | `font-size` | `font-size: var(--text-help-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/purchases/styles/purchases-invoice.css` | 1033 | `font-weight` | `font-weight: 800;` | NEEDS_DECISION | Revisar: si es dato normal, mover a token var(--text-*-weight) o al componente global correspondiente. |
| `frontend/src/modules/purchases/styles/purchases-invoice.css` | 1034 | `letter-spacing` | `letter-spacing: -0.5px;` | NEEDS_DECISION | Revisar si es titulo/label global (OK) o tracking local sin justificacion. |
| `frontend/src/modules/purchases/styles/purchases-invoice.css` | 1037 | `font-size` | `font-size: inherit;` | OK_GLOBAL | Sin accion - cumple regla DS. |
| `frontend/src/modules/purchases/styles/purchases-invoice.css` | 1038 | `font-weight` | `font-weight: inherit;` | OK_GLOBAL | Sin accion - cumple regla DS. |
| `frontend/src/modules/purchases/styles/purchases-invoice.css` | 1040 | `letter-spacing` | `letter-spacing: inherit;` | OK_GLOBAL | Sin accion - cumple regla DS. |
| `frontend/src/modules/purchases/styles/purchases-invoice.css` | 1044 | `font-size` | `font-size: var(--text-title-sm-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/purchases/styles/purchases-invoice.css` | 1048 | `font-size` | `font-size: var(--text-headline-md-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/purchases/styles/purchases-invoice.css` | 1103 | `font-size` | `font-size: var(--text-body-sm-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/purchases/styles/purchases-invoice.css` | 1113 | `font-size` | `font-size: inherit;` | OK_GLOBAL | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/pages/SalesPage.css` | 21 | `font-weight` | `font-weight: var(--text-label-md-weight);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/pages/SalesPage.css` | 28 | `font-size` | `font-size: inherit;` | OK_GLOBAL | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/pages/SalesPage.css` | 29 | `font-weight` | `font-weight: inherit;` | OK_GLOBAL | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/pages/SalesPage.css` | 37 | `font-size` | `font-size: var(--text-help-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/pages/SalesPage.css` | 38 | `line-height` | `line-height: var(--text-help-height);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/pages/SalesPage.css` | 80 | `font-size` | `font-size: var(--text-label-sm-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/pages/SalesPage.css` | 81 | `font-weight` | `font-weight: var(--text-label-md-weight);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/pages/SalesPage.css` | 92 | `font-size` | `font-size: inherit;` | OK_GLOBAL | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/pages/SalesPage.css` | 123 | `font-size` | `font-size: var(--text-label-sm-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/pages/SalesPage.css` | 128 | `font-size` | `font-size: var(--text-body-md-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/pages/SalesPage.css` | 129 | `font-weight` | `font-weight: var(--text-label-md-weight);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/pages/SalesPage.css` | 138 | `font-size` | `font-size: var(--text-label-sm-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/pages/SalesPage.css` | 139 | `font-weight` | `font-weight: var(--text-label-md-weight);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/pages/SalesPage.css` | 143 | `font-size` | `font-size: inherit;` | OK_GLOBAL | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/pages/SalesPage.css` | 144 | `font-weight` | `font-weight: inherit;` | OK_GLOBAL | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/pages/SalesPage.css` | 148 | `font-size` | `font-size: 9px;` | NEEDS_DECISION | Revisar: si es dato normal, mover a token var(--text-*) o a ZHFieldLabel/ZHDataValue/ZHMoneyValue global. |
| `frontend/src/modules/sales/pages/SalesPage.css` | 149 | `font-weight` | `font-weight: 400;` | NEEDS_DECISION | Revisar: si es dato normal, mover a token var(--text-*-weight) o al componente global correspondiente. |
| `frontend/src/modules/sales/pages/SalesPage.css` | 154 | `font-size` | `font-size: var(--text-body-md-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/pages/SalesPage.css` | 155 | `font-weight` | `font-weight: var(--text-label-md-weight);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/pages/SalesPage.css` | 160 | `font-size` | `font-size: inherit;` | OK_GLOBAL | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/pages/SalesPage.css` | 161 | `font-weight` | `font-weight: inherit;` | OK_GLOBAL | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/pages/SalesPage.css` | 172 | `font-size` | `font-size: var(--text-label-sm-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/pages/SalesPage.css` | 193 | `font-size` | `font-size: var(--text-label-sm-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/pages/SalesPage.css` | 198 | `font-size` | `font-size: var(--text-body-md-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/pages/SalesPage.css` | 199 | `font-weight` | `font-weight: var(--text-label-md-weight);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/pages/SalesPage.css` | 205 | `font-weight` | `font-weight: 700;` | NEEDS_DECISION | Revisar: si es dato normal, mover a token var(--text-*-weight) o al componente global correspondiente. |
| `frontend/src/modules/sales/pages/SalesPage.css` | 221 | `font-size` | `font-size: var(--text-label-sm-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/pages/SalesPage.css` | 238 | `font-weight` | `font-weight: 700;` | NEEDS_DECISION | Revisar: si es dato normal, mover a token var(--text-*-weight) o al componente global correspondiente. |
| `frontend/src/modules/sales/pages/SalesPage.css` | 288 | `font-size` | `font-size: var(--text-help-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-invoice.css` | 59 | `font-size` | `font-size: 18px;` | OK_ICON | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-invoice.css` | 66 | `font-size` | `font-size: 18px;` | NEEDS_DECISION | Revisar: si es dato normal, mover a token var(--text-*) o a ZHFieldLabel/ZHDataValue/ZHMoneyValue global. |
| `frontend/src/modules/sales/styles/sales-invoice.css` | 105 | `font-size` | `font-size: 18px;` | OK_ICON | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-invoice.css` | 110 | `font-size` | `font-size: var(--text-label-sm-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-invoice.css` | 125 | `text-transform` | `text-transform: uppercase;` | OK_GLOBAL | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-invoice.css` | 135 | `font-weight` | `font-weight: 600;` | NEEDS_DECISION | Revisar: si es dato normal, mover a token var(--text-*-weight) o al componente global correspondiente. |
| `frontend/src/modules/sales/styles/sales-invoice.css` | 148 | `font-size` | `font-size: var(--text-help-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-invoice.css` | 150 | `font-weight` | `font-weight: 600;` | NEEDS_DECISION | Revisar: si es dato normal, mover a token var(--text-*-weight) o al componente global correspondiente. |
| `frontend/src/modules/sales/styles/sales-invoice.css` | 156 | `font-size` | `font-size: inherit;` | OK_GLOBAL | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-invoice.css` | 157 | `font-weight` | `font-weight: inherit;` | OK_GLOBAL | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-invoice.css` | 187 | `font-size` | `font-size: var(--text-headline-lg-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-invoice.css` | 188 | `font-weight` | `font-weight: 800;` | NEEDS_DECISION | Revisar: si es dato normal, mover a token var(--text-*-weight) o al componente global correspondiente. |
| `frontend/src/modules/sales/styles/sales-invoice.css` | 190 | `letter-spacing` | `letter-spacing: -0.02em;` | NEEDS_DECISION | Revisar si es titulo/label global (OK) o tracking local sin justificacion. |
| `frontend/src/modules/sales/styles/sales-invoice.css` | 194 | `font-size` | `font-size: inherit;` | OK_GLOBAL | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-invoice.css` | 195 | `font-weight` | `font-weight: inherit;` | OK_GLOBAL | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-invoice.css` | 197 | `letter-spacing` | `letter-spacing: inherit;` | OK_GLOBAL | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-invoice.css` | 204 | `font-size` | `font-size: var(--text-label-sm-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-invoice.css` | 213 | `font-weight` | `font-weight: var(--text-label-sm-weight);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-invoice.css` | 214 | `text-transform` | `text-transform: uppercase;` | NEEDS_DECISION | Revisar si es titulo/label global (OK) o uppercase local sin justificacion. |
| `frontend/src/modules/sales/styles/sales-invoice.css` | 215 | `font-size` | `font-size: var(--text-label-sm-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-invoice.css` | 216 | `letter-spacing` | `letter-spacing: var(--text-label-sm-spacing);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-invoice.css` | 225 | `font-size` | `font-size: var(--text-label-sm-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-invoice.css` | 230 | `font-weight` | `font-weight: 600;` | NEEDS_DECISION | Revisar: si es dato normal, mover a token var(--text-*-weight) o al componente global correspondiente. |
| `frontend/src/modules/sales/styles/sales-invoice.css` | 233 | `font-size` | `font-size: inherit;` | OK_GLOBAL | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-invoice.css` | 234 | `font-weight` | `font-weight: inherit;` | OK_GLOBAL | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-invoice.css` | 267 | `font-size` | `font-size: 22px;` | OK_ICON | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-invoice.css` | 274 | `font-size` | `font-size: var(--text-body-md-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-invoice.css` | 315 | `font-size` | `font-size: var(--text-label-sm-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-invoice.css` | 316 | `font-weight` | `font-weight: var(--text-label-sm-weight);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-invoice.css` | 317 | `text-transform` | `text-transform: uppercase;` | NEEDS_DECISION | Revisar si es titulo/label global (OK) o uppercase local sin justificacion. |
| `frontend/src/modules/sales/styles/sales-invoice.css` | 319 | `letter-spacing` | `letter-spacing: 0.06em;` | NEEDS_DECISION | Revisar si es titulo/label global (OK) o tracking local sin justificacion. |
| `frontend/src/modules/sales/styles/sales-invoice.css` | 322 | `font-size` | `font-size: 10px;` | NEEDS_DECISION | Revisar: si es dato normal, mover a token var(--text-*) o a ZHFieldLabel/ZHDataValue/ZHMoneyValue global. |
| `frontend/src/modules/sales/styles/sales-invoice.css` | 407 | `font-size` | `font-size: 16px;` | OK_ICON | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-invoice.css` | 447 | `letter-spacing` | `letter-spacing: normal;` | OK_GLOBAL | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-invoice.css` | 466 | `font-size` | `font-size: var(--text-body-md-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-invoice.css` | 490 | `font-size` | `font-size: var(--text-label-sm-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-invoice.css` | 491 | `line-height` | `line-height: 1.4;` | OK_LAYOUT | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-invoice.css` | 495 | `font-size` | `font-size: 14px;` | OK_ICON | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-invoice.css` | 512 | `font-weight` | `font-weight: 500;` | NEEDS_DECISION | Revisar: si es dato normal, mover a token var(--text-*-weight) o al componente global correspondiente. |
| `frontend/src/modules/sales/styles/sales-invoice.css` | 516 | `font-weight` | `font-weight: 500;` | NEEDS_DECISION | Revisar: si es dato normal, mover a token var(--text-*-weight) o al componente global correspondiente. |
| `frontend/src/modules/sales/styles/sales-invoice.css` | 527 | `font-size` | `font-size: var(--text-help-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-invoice.css` | 528 | `font-weight` | `font-weight: 500;` | NEEDS_DECISION | Revisar: si es dato normal, mover a token var(--text-*-weight) o al componente global correspondiente. |
| `frontend/src/modules/sales/styles/sales-invoice.css` | 532 | `font-size` | `font-size: 16px;` | OK_ICON | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-invoice.css` | 542 | `font-size` | `font-size: var(--text-body-md-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-invoice.css` | 567 | `font-size` | `font-size: var(--text-body-md-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-invoice.css` | 572 | `font-weight` | `font-weight: 600;` | NEEDS_DECISION | Revisar: si es dato normal, mover a token var(--text-*-weight) o al componente global correspondiente. |
| `frontend/src/modules/sales/styles/sales-invoice.css` | 584 | `font-size` | `font-size: var(--text-label-sm-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-invoice.css` | 585 | `font-weight` | `font-weight: 700;` | NEEDS_DECISION | Revisar: si es dato normal, mover a token var(--text-*-weight) o al componente global correspondiente. |
| `frontend/src/modules/sales/styles/sales-invoice.css` | 604 | `font-size` | `font-size: var(--text-headline-md-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-invoice.css` | 605 | `font-weight` | `font-weight: 700;` | NEEDS_DECISION | Revisar: si es dato normal, mover a token var(--text-*-weight) o al componente global correspondiente. |
| `frontend/src/modules/sales/styles/sales-invoice.css` | 611 | `font-size` | `font-size: var(--text-help-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-invoice.css` | 616 | `font-size` | `font-size: var(--text-label-sm-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-invoice.css` | 638 | `font-style` | `font-style: normal;` | OK_LAYOUT | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-invoice.css` | 639 | `font-weight` | `font-weight: inherit;` | OK_GLOBAL | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-invoice.css` | 649 | `font-size` | `font-size: var(--text-label-sm-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-invoice.css` | 719 | `font-size` | `font-size: var(--text-badge-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-invoice.css` | 720 | `font-weight` | `font-weight: 700;` | NEEDS_DECISION | Revisar: si es dato normal, mover a token var(--text-*-weight) o al componente global correspondiente. |
| `frontend/src/modules/sales/styles/sales-invoice.css` | 721 | `letter-spacing` | `letter-spacing: 0.04em;` | NEEDS_DECISION | Revisar si es titulo/label global (OK) o tracking local sin justificacion. |
| `frontend/src/modules/sales/styles/sales-invoice.css` | 727 | `line-height` | `line-height: 1.6;` | OK_LAYOUT | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-invoice.css` | 734 | `font-size` | `font-size: var(--text-body-lg-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-invoice.css` | 735 | `font-weight` | `font-weight: 600;` | NEEDS_DECISION | Revisar: si es dato normal, mover a token var(--text-*-weight) o al componente global correspondiente. |
| `frontend/src/modules/sales/styles/sales-invoice.css` | 737 | `line-height` | `line-height: 1.3;` | OK_LAYOUT | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-invoice.css` | 743 | `font-size` | `font-size: var(--text-label-sm-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-invoice.css` | 751 | `font-weight` | `font-weight: 700;` | NEEDS_DECISION | Revisar: si es dato normal, mover a token var(--text-*-weight) o al componente global correspondiente. |
| `frontend/src/modules/sales/styles/sales-invoice.css` | 778 | `font-size` | `font-size: var(--text-label-sm-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-invoice.css` | 779 | `font-weight` | `font-weight: var(--text-label-sm-weight);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-invoice.css` | 780 | `text-transform` | `text-transform: uppercase;` | NEEDS_DECISION | Revisar si es titulo/label global (OK) o uppercase local sin justificacion. |
| `frontend/src/modules/sales/styles/sales-invoice.css` | 781 | `letter-spacing` | `letter-spacing: var(--text-label-sm-spacing);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-invoice.css` | 785 | `font-size` | `font-size: var(--text-help-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-invoice.css` | 786 | `font-weight` | `font-weight: 600;` | NEEDS_DECISION | Revisar: si es dato normal, mover a token var(--text-*-weight) o al componente global correspondiente. |
| `frontend/src/modules/sales/styles/sales-invoice.css` | 790 | `font-weight` | `font-weight: 800;` | NEEDS_DECISION | Revisar: si es dato normal, mover a token var(--text-*-weight) o al componente global correspondiente. |
| `frontend/src/modules/sales/styles/sales-invoice.css` | 791 | `font-size` | `font-size: var(--text-body-md-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-invoice.css` | 801 | `font-size` | `font-size: var(--text-label-sm-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-invoice.css` | 802 | `font-weight` | `font-weight: 800;` | NEEDS_DECISION | Revisar: si es dato normal, mover a token var(--text-*-weight) o al componente global correspondiente. |
| `frontend/src/modules/sales/styles/sales-invoice.css` | 803 | `text-transform` | `text-transform: uppercase;` | NEEDS_DECISION | Revisar si es titulo/label global (OK) o uppercase local sin justificacion. |
| `frontend/src/modules/sales/styles/sales-invoice.css` | 804 | `letter-spacing` | `letter-spacing: var(--text-label-sm-spacing);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-invoice.css` | 808 | `font-size` | `font-size: var(--text-label-sm-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-product-card.css` | 56 | `font-size` | `font-size: var(--text-label-md-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-product-card.css` | 57 | `font-weight` | `font-weight: 800;` | NEEDS_DECISION | Revisar: si es dato normal, mover a token var(--text-*-weight) o al componente global correspondiente. |
| `frontend/src/modules/sales/styles/sales-product-card.css` | 65 | `font-size` | `font-size: 22px;` | OK_ICON | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-product-card.css` | 116 | `font-size` | `font-size: var(--text-badge-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-product-card.css` | 117 | `font-weight` | `font-weight: 800;` | NEEDS_DECISION | Revisar: si es dato normal, mover a token var(--text-*-weight) o al componente global correspondiente. |
| `frontend/src/modules/sales/styles/sales-product-card.css` | 119 | `letter-spacing` | `letter-spacing: 0.02em;` | NEEDS_DECISION | Revisar si es titulo/label global (OK) o tracking local sin justificacion. |
| `frontend/src/modules/sales/styles/sales-product-card.css` | 122 | `font-size` | `font-size: var(--text-title-sm-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-product-card.css` | 126 | `line-height` | `line-height: 1.3;` | OK_LAYOUT | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-product-card.css` | 150 | `text-transform` | `text-transform: uppercase;` | OK_GLOBAL | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-product-card.css` | 151 | `letter-spacing` | `letter-spacing: 0.04em;` | OK_GLOBAL | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-product-card.css` | 154 | `font-size` | `font-size: var(--text-title-sm-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-product-card.css` | 155 | `font-weight` | `font-weight: 700;` | NEEDS_DECISION | Revisar: si es dato normal, mover a token var(--text-*-weight) o al componente global correspondiente. |
| `frontend/src/modules/sales/styles/sales-product-card.css` | 160 | `font-weight` | `font-weight: 800;` | NEEDS_DECISION | Revisar: si es dato normal, mover a token var(--text-*-weight) o al componente global correspondiente. |
| `frontend/src/modules/sales/styles/sales-product-card.css` | 161 | `font-size` | `font-size: 17px;` | NEEDS_DECISION | Revisar: si es dato normal, mover a token var(--text-*) o a ZHFieldLabel/ZHDataValue/ZHMoneyValue global. |
| `frontend/src/modules/sales/styles/sales-product-card.css` | 184 | `text-transform` | `text-transform: uppercase;` | OK_GLOBAL | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-product-card.css` | 185 | `letter-spacing` | `letter-spacing: 0.04em;` | OK_GLOBAL | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-product-card.css` | 195 | `font-weight` | `font-weight: 700;` | NEEDS_DECISION | Revisar: si es dato normal, mover a token var(--text-*-weight) o al componente global correspondiente. |
| `frontend/src/modules/sales/styles/sales-product-card.css` | 218 | `text-transform` | `text-transform: uppercase;` | OK_GLOBAL | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-product-card.css` | 219 | `letter-spacing` | `letter-spacing: 0.04em;` | OK_GLOBAL | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-product-card.css` | 228 | `font-weight` | `font-weight: 700;` | NEEDS_DECISION | Revisar: si es dato normal, mover a token var(--text-*-weight) o al componente global correspondiente. |
| `frontend/src/modules/sales/styles/sales-product-card.css` | 239 | `font-size` | `font-size: 20px;` | OK_ICON | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-product-card.css` | 247 | `line-height` | `line-height: 1.2;` | OK_LAYOUT | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-product-card.css` | 255 | `text-transform` | `text-transform: uppercase;` | OK_GLOBAL | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-product-card.css` | 256 | `letter-spacing` | `letter-spacing: 0.04em;` | OK_GLOBAL | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-product-card.css` | 264 | `font-size` | `font-size: var(--text-title-sm-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-product-card.css` | 265 | `font-weight` | `font-weight: 800;` | NEEDS_DECISION | Revisar: si es dato normal, mover a token var(--text-*-weight) o al componente global correspondiente. |
| `frontend/src/modules/sales/styles/sales-product-card.css` | 275 | `font-size` | `font-size: var(--text-label-sm-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-product-card.css` | 276 | `text-transform` | `text-transform: uppercase;` | NEEDS_DECISION | Revisar si es titulo/label global (OK) o uppercase local sin justificacion. |
| `frontend/src/modules/sales/styles/sales-product-card.css` | 278 | `letter-spacing` | `letter-spacing: var(--text-label-sm-spacing);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-product-card.css` | 281 | `font-size` | `font-size: var(--text-label-sm-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-product-card.css` | 285 | `font-size` | `font-size: var(--text-label-sm-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-product-card.css` | 286 | `font-weight` | `font-weight: 700;` | NEEDS_DECISION | Revisar: si es dato normal, mover a token var(--text-*-weight) o al componente global correspondiente. |
| `frontend/src/modules/sales/styles/sales-product-card.css` | 292 | `font-size` | `font-size: var(--text-label-sm-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-product-card.css` | 308 | `text-transform` | `text-transform: uppercase;` | OK_GLOBAL | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-product-card.css` | 309 | `letter-spacing` | `letter-spacing: 0.04em;` | OK_GLOBAL | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-product-card.css` | 314 | `font-size` | `font-size: 21px;` | NEEDS_DECISION | Revisar: si es dato normal, mover a token var(--text-*) o a ZHFieldLabel/ZHDataValue/ZHMoneyValue global. |
| `frontend/src/modules/sales/styles/sales-product-card.css` | 315 | `font-weight` | `font-weight: 800;` | NEEDS_DECISION | Revisar: si es dato normal, mover a token var(--text-*-weight) o al componente global correspondiente. |
| `frontend/src/modules/sales/styles/sales-product-card.css` | 345 | `font-size` | `font-size: var(--text-label-sm-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-product-card.css` | 351 | `text-transform` | `text-transform: uppercase;` | OK_GLOBAL | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-product-card.css` | 352 | `letter-spacing` | `letter-spacing: 0.03em;` | OK_GLOBAL | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-product-card.css` | 366 | `font-weight` | `font-weight: 800;` | NEEDS_DECISION | Revisar: si es dato normal, mover a token var(--text-*-weight) o al componente global correspondiente. |
| `frontend/src/modules/sales/styles/sales-product-card.css` | 368 | `text-transform` | `text-transform: uppercase;` | OK_GLOBAL | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-product-card.css` | 369 | `letter-spacing` | `letter-spacing: 0.05em;` | OK_GLOBAL | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-product-card.css` | 376 | `font-size` | `font-size: var(--text-headline-lg-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-product-card.css` | 377 | `font-weight` | `font-weight: 800;` | NEEDS_DECISION | Revisar: si es dato normal, mover a token var(--text-*-weight) o al componente global correspondiente. |
| `frontend/src/modules/sales/styles/sales-product-card.css` | 379 | `letter-spacing` | `letter-spacing: -0.5px;` | NEEDS_DECISION | Revisar si es titulo/label global (OK) o tracking local sin justificacion. |
| `frontend/src/modules/sales/styles/sales-product-card.css` | 380 | `line-height` | `line-height: 1.2;` | OK_LAYOUT | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-product-card.css` | 387 | `font-size` | `font-size: var(--text-label-sm-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-product-card.css` | 388 | `font-weight` | `font-weight: var(--text-label-sm-weight);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-product-card.css` | 390 | `text-transform` | `text-transform: uppercase;` | NEEDS_DECISION | Revisar si es titulo/label global (OK) o uppercase local sin justificacion. |
| `frontend/src/modules/sales/styles/sales-product-card.css` | 391 | `letter-spacing` | `letter-spacing: var(--text-label-sm-spacing);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-product-card.css` | 403 | `font-size` | `font-size: var(--text-body-md-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-product-card.css` | 407 | `font-size` | `font-size: 48px;` | OK_ICON | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-return.css` | 32 | `font-size` | `font-size: var(--text-help-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-return.css` | 37 | `font-weight` | `font-weight: 600;` | NEEDS_DECISION | Revisar: si es dato normal, mover a token var(--text-*-weight) o al componente global correspondiente. |
| `frontend/src/modules/sales/styles/sales-return.css` | 38 | `font-size` | `font-size: var(--text-body-md-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-return.css` | 42 | `font-size` | `font-size: var(--text-label-sm-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-return.css` | 47 | `font-size` | `font-size: var(--text-help-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-return.css` | 54 | `font-size` | `font-size: var(--text-body-md-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-return.css` | 71 | `font-size` | `font-size: var(--text-label-sm-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-return.css` | 72 | `text-transform` | `text-transform: uppercase;` | NEEDS_DECISION | Revisar si es titulo/label global (OK) o uppercase local sin justificacion. |
| `frontend/src/modules/sales/styles/sales-return.css` | 73 | `letter-spacing` | `letter-spacing: var(--text-label-sm-spacing);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-return.css` | 79 | `font-size` | `font-size: var(--text-body-md-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-return.css` | 80 | `font-weight` | `font-weight: 600;` | NEEDS_DECISION | Revisar: si es dato normal, mover a token var(--text-*-weight) o al componente global correspondiente. |
| `frontend/src/modules/sales/styles/sales-return.css` | 84 | `font-size` | `font-size: var(--text-headline-sm-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-return.css` | 94 | `font-size` | `font-size: inherit;` | OK_GLOBAL | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-return.css` | 95 | `font-weight` | `font-weight: inherit;` | OK_GLOBAL | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-return.css` | 99 | `font-size` | `font-size: inherit;` | OK_GLOBAL | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-return.css` | 100 | `font-weight` | `font-weight: inherit;` | OK_GLOBAL | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-return.css` | 105 | `font-size` | `font-size: var(--text-body-md-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-return.css` | 134 | `font-size` | `font-size: var(--text-body-md-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-return.css` | 135 | `font-weight` | `font-weight: 600;` | NEEDS_DECISION | Revisar: si es dato normal, mover a token var(--text-*-weight) o al componente global correspondiente. |
| `frontend/src/modules/sales/styles/sales-return.css` | 145 | `font-size` | `font-size: inherit;` | OK_GLOBAL | Sin accion - cumple regla DS. |
| `frontend/src/modules/sales/styles/sales-return.css` | 146 | `font-weight` | `font-weight: inherit;` | OK_GLOBAL | Sin accion - cumple regla DS. |
| `frontend/src/modules/security/pages/SecuritySettingsPage.css` | 18 | `font-weight` | `font-weight: 600;` | NEEDS_DECISION | Revisar: si es dato normal, mover a token var(--text-*-weight) o al componente global correspondiente. |
| `frontend/src/modules/security/pages/SecuritySettingsPage.css` | 19 | `font-size` | `font-size: var(--text-body-md-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/security/pages/SecuritySettingsPage.css` | 39 | `font-size` | `font-size: var(--text-badge-size);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/security/pages/SecuritySettingsPage.css` | 40 | `font-weight` | `font-weight: var(--text-badge-weight);` | OK_TOKEN | Sin accion - cumple regla DS. |
| `frontend/src/modules/security/pages/SecuritySettingsPage.css` | 41 | `text-transform` | `text-transform: uppercase;` | NEEDS_DECISION | Revisar si es titulo/label global (OK) o uppercase local sin justificacion. |
| `frontend/src/modules/security/pages/SecuritySettingsPage.css` | 42 | `letter-spacing` | `letter-spacing: 0.04em;` | NEEDS_DECISION | Revisar si es titulo/label global (OK) o tracking local sin justificacion. |


