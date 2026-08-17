# DS Audit Report

- Fecha: 2026-08-17 15:14:42
- Scope: `modules` (moduleName: `logistica`)
- Generado por: `scripts/ds/ds-audit.ps1`
- Total hallazgos: **1**

> Auditoria por lineas (regex), no un parser real de CSS/TSX. ` NEEDS_DECISION ` siempre requiere revision humana - ver ` docs/design-system/DS_RULES.md `.

## Conteo por patron

| Patron | Hallazgos |
|---|---|
| `font-weight` | 1 |

## Conteo por clasificacion

| Clasificacion | Hallazgos |
|---|---|
| `NEEDS_DECISION` | 1 |

## Hallazgos

| Archivo | Linea | Patron | Contenido | Clasificacion | Accion sugerida |
|---|---|---|---|---|---|
| `frontend/src/modules/logistica/transportistas/pages/carriers-page.css` | 19 | `font-weight` | `font-weight: 600;` | NEEDS_DECISION | Revisar: si es dato normal, mover a token var(--text-*-weight) o al componente global correspondiente. |


