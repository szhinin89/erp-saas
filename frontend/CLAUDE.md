# Frontend — reglas de implementación

Este archivo complementa [`../CLAUDE.md`](../CLAUDE.md). Si hay conflicto, prevalece `../CLAUDE.md`.

Cuerpo normativo completo: [`../docs/architecture/frontend.md`](../docs/architecture/frontend.md) · [`../docs/architecture/modal-standard.md`](../docs/architecture/modal-standard.md) · [`../docs/architecture/visual-messages.md`](../docs/architecture/visual-messages.md) · [`../docs/architecture/pr-rules-catalog.md`](../docs/architecture/pr-rules-catalog.md).

No repetir aquí el `CLAUDE.md` raíz. No incluir estado de avance ni lista histórica de bloques de migración (14A/14B/15A/…) — eso vive en `STATUS.md`.

---

## Design System SSOT

Usar componentes oficiales:

- `ZhTextInput`, `ZhNumberInput`, `ZhDecimalInput`, `ZhDateInput`, `ZhPhoneInput`, `ZhSelect`, `ZhTextarea`
- `ZHBtn`, `ZHIconButton`, `Badge`, `ReportKpiCard`
- `ZHField` / `ZHForm` cuando aplique
- `ZHDataTable` (`components/zh/ZHDataTable.tsx`) es el componente estándar del Design System para listados tabulares administrativos (ver [ChartOfAccountsPage](../frontend/src/modules/accounting/pages/ChartOfAccountsPage.tsx), [DocumentFlowPoliciesPage](../frontend/src/modules/configuracion/documentFlows/pages/DocumentFlowPoliciesPage.tsx)). Las tablas HTML manuales (`<table className="table">`) solo se permiten como excepción justificada cuando `ZHDataTable` no cubre el caso (documentar el motivo en el código).
- `ZHDataTable` soporta `showRowNumber` para mostrar una columna auxiliar "N°". Debe usarse en listados administrativos donde ayude a ubicar registros. No representa el Id del registro y debe mantenerse visualmente secundario.
- Las tablas manuales existentes deben migrarse gradualmente a `ZHDataTable`. En migraciones nuevas, usar `showRowNumber` para listados administrativos principales salvo excepción justificada.

Tabla completa de estándares únicos (incluye modal, tabs, tabla, grid, toggle, íconos) y sus equivalentes deprecados: [`../docs/architecture/frontend.md § Design System`](../docs/architecture/frontend.md#design-system--estándares-únicos-obligatorios).

## Prohibiciones

- No crear otro input/select/textarea custom si existe componente ZH.
- No crear otro badge/botón/KPI si existe equivalente.
- No usar inline styles estáticos (`style={{...}}`) — excepción única: valores dinámicos (dimensiones calculadas, transforms, colores de datos).
- No crear CSS local para estilos ya resueltos por `zh-ui.css`.
- No duplicar tokens de `design-tokens.css`.
- No borrar CSS por prefijo sin verificar consumidores TSX reales.
- No modificar flujos auth/session sin auditoría (`authRefreshManager`, `fullLogout()` son las únicas puertas de entrada/salida de sesión).

## CSS

- `design-tokens.css` contiene tokens (colores, spacing, tipografía, sombras, radios).
- `zh-ui.css` contiene componentes visuales reutilizables (`zh-*`).
- `page-template.css` contiene patrones de página (`pg-*`).
- CSS local (`{pagina}-page.css`) solo para layout/dominio específico de esa pantalla — prefijo propio del módulo.

## Formularios

- Todo nuevo formulario debe iniciar con componentes ZH (React Hook Form + Zod + `ZHField`/`ZhDecimalInput`/etc.).
- HTML crudo permitido solo para:
  - `email`, `password`, `checkbox` (fuera de `ZHToggle`), `radio`, `file`, `color`
  - scanner/picker/autocomplete especializado (`ZhWarehouseSelector`, `CustomerPicker`, `SupplierPicker`)
  - tablas editables
  - SRI crítico
  - IAM/permisos
  - stock/logística crítica (cuando el control tiene semántica de negocio que un input genérico ZH no captura — documentar el motivo en el código, nunca asumir por nombre de módulo)

## Launcher

- Mantener apertura en nueva pestaña si el flujo existente lo requiere.
- No cambiar comportamiento `target="_blank"` sin aprobación.

## Auditoría de reutilización obligatoria

Antes de escribir UI nueva, declarar explícitamente: (1) qué plantillas oficiales se revisaron, (2) qué componentes existentes se reutilizan, (3) qué se extiende y cómo, (4) justificación técnica de cualquier componente nuevo, (5) confirmación de que no existe un equivalente. Detalle: [`../docs/architecture/frontend.md § Reutilización obligatoria`](../docs/architecture/frontend.md#reutilización-obligatoria--auditoría-previa-a-crear-ui).
