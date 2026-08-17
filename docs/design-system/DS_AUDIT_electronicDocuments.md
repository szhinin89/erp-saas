# DS Audit Report

- Fecha: 2026-08-17 13:49:48
- Scope: `modules` (moduleName: `electronicDocuments`)
- Generado por: `scripts/ds/ds-audit.ps1`
- Total hallazgos: **14**

> Auditoria por lineas (regex), no un parser real de CSS/TSX. ` NEEDS_DECISION ` siempre requiere revision humana - ver ` docs/design-system/DS_RULES.md `.

## Conteo por patron

| Patron | Hallazgos |
|---|---|
| `font-size` | 11 |
| `font-weight` | 2 |
| `line-height` | 1 |

## Conteo por clasificacion

| Clasificacion | Hallazgos |
|---|---|
| `OK_TOKEN` | 10 |
| `NEEDS_DECISION` | 3 |
| `OK_LAYOUT` | 1 |

## Hallazgos

| Archivo | Linea | Patron | Contenido | Clasificacion | Accion sugerida |
|---|---|---|---|---|---|
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


