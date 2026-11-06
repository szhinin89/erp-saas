# UIUX-GLOBAL-ARCHITECTURE-02 — aplicación global del Design System

Fecha: 2026-11-05. **Planificación aprobada por el propietario como base.** Bloque 1 implementado
y aprobado visualmente en UIUX-GLOBAL-HEADER-DEFAULT-01 (estado en STATUS). El propietario aprobó
`stacked` como nombre de la excepción; `"default"` se retiró al no tener consumidores productivos.
Decisión posterior del propietario: el kicker no se renderiza en ninguna cabecera del
componente oficial (la prop se conserva temporalmente, sin efecto) (reemplaza «kicker encima de la fila» del alcance del Bloque 1 descrito abajo;
contrato vigente en Frontend). Bloques 2–5 sin implementar ni autorizar.

Documento de planificación, no normativa ni segunda SSOT. El comportamiento vigente sigue en
[Frontend](../architecture/frontend.md); estado de entrega exclusivamente en
[STATUS](../../STATUS.md). Continúa [UIUX-GLOBAL-STANDARD-01](UIUX-GLOBAL-STANDARD-01.md),
que conserva su evidencia; su fila «C — defaults globales: no recomendados» queda reemplazada
por las decisiones del propietario registradas aquí.

## Decisiones del propietario

1. El encabezado compacto será el comportamiento predeterminado del componente oficial, después
   de resolver y validar las pantallas con kicker, subtítulos y acciones.
2. Las pestañas con exactamente tres opciones usarán automáticamente el patrón responsive cuando
   sus etiquetas sean compatibles. Exclusiones explícitas y justificadas, en especial si el número
   de pestañas cambia dinámicamente.
3. Los KPI simples tendrán disposición horizontal compacta por defecto, con excepciones de
   contenido justificadas.
4. Clientes adoptará el patrón de identidad y acciones de Proveedores, respetando sus diferencias
   de permisos y lógica.
5. El Resumen de Clientes no conserva permanentemente el diseño vertical: se adaptará a tres KPI
   compactos, sin acciones redundantes.
6. No crear otro sistema paralelo de componentes ni mantener CSS local duplicado.

UIUX-CUSTOMERS-02 (aprobado visualmente por el propietario) se preserva íntegro: su código,
pruebas, textos y evidencia son la referencia junto con Proveedores. Los pendientes de
Clasificación (BUG-CUSTOMERS-CLASSIFICATION-01) y CI (CI-1095-FIX, CI-1096-FIX) siguen su propio
registro en STATUS y no forman parte de este programa.

## Causa raíz

Cada mejora aprobada se incorporó como variante opt-in con el diseño anterior como default
(contratos UIUX-GLOBAL-HEADER-01 y UIUX-GLOBAL-TABS-01: «solo si se solicita explícitamente»).
Cada pantalla debe activarla manualmente. Se suman composiciones locales equivalentes:
`.md-supplier-summary` (grid de tres KPI), el override de tres columnas de
`StockReportPage.css` y cuatro barras de pestañas `.prd-tabs` escritas a mano.

## Inventario verificado del encabezado

Método: análisis estático de la etiqueta de apertura JSX de `PageShell`, `ErpPageTemplate` y
`NoAccessPage` en TSX productivo (excluye pruebas y las definiciones de `PageShell.tsx` y
`ErpPageTemplate.tsx`). `ZHScreenHeading` no tiene consumidores directos fuera de `PageShell`.
Corrige la estimación previa por `grep` (84 consumidores; 62/40/70), que mezclaba archivos y sitios.

| Medida | Sitios | Archivos |
|---|---|---|
| `PageShell` directo | 50 | 36 |
| `ErpPageTemplate` | 52 | 48 |
| Total con encabezado configurable | **102** | **84** |
| con `kicker` | 69 | 62 |
| con `action` | 39 | 39 |
| con `subtitle` | 77 | 69 |
| `kicker` + `action` | 32 | — |
| sin `kicker`, `action` ni `subtitle` | 4 | — |
| `headingVariant` explícito (Proveedores, Clientes) | 2 | 2 |
| `NoAccessPage` (título + «sin acceso», sin kicker ni acción) | 62 | 23 solo por esta vía, incl. `RouteAccessGuard` |

Total afectado por un cambio de default: 107 archivos (84 + 23). Varios sitios son estados
transitorios del mismo archivo (cargando, no encontrado, error) con subtítulo «Cargando…».

Fuera del alcance, con cabecera propia o sin shell: `ReportPage` (Compras, Ventas y Stock de
Reportes), autenticación/setup, páginas de Carga Inicial por entidad, `SalesPage` (POS) y
`CompanyManagementHubPage`.

CSS y pruebas acopladas: ningún CSS de módulo selecciona `.zh-screen-heading*` ni
`.page-shell-heading`. Pruebas: `components/PageShell.test.tsx` (afirma la clase default),
`MasterDataSuppliersPage.test.tsx` y `MasterDataCustomersPage.test.tsx` (sin kicker).

## Matriz por componente

### Encabezado (`ZHScreenHeading` / `PageShell` / `ErpPageTemplate`)

| Grupo | Decisión |
|---|---|
| Proveedores, Clientes (sin kicker, con subtítulo) | Default: retirar `headingVariant` explícito; resultado idéntico al aprobado |
| Kicker + subtítulo ± acción (listados, configuración, catálogos) | Default tras corregir el modificador compacto para kicker |
| Acción ancha o toolbar (`PermissionsAssignmentPage`, `ExpenseDocumentFormPage`, `SupplierPaymentDetailPage`, `PayableDetailPage`, `PurchaseCreditNoteListPage`) | Default con validación visual específica |
| Título/subtítulo dinámicos de detalle (asiento, saldo a favor, NC, devolución, sucursal, socio) | Default con validación visual específica |
| `DashboardPage` (saludo, fecha, `RuntimeModeBadge`) | Default con validación visual |
| `NoAccessPage` y estados transitorios | Default heredado |
| `ReportPage`, auth, Carga Inicial, POS, hub de empresas | Conservan su diseño: no consumen `ZHScreenHeading` |

### Pestañas (`ZHTabBar` y sistemas locales)

| Consumidor | Pestañas | Decisión |
|---|---|---|
| Proveedores, Clientes | 3 | Automático; retirar `variant` explícito |
| `ItemsPage` | 3 («Nuevo ítem») | Automático con `mobileLabel` «Nuevo» |
| `CashFundingRequestsPage` | 1–3 según permisos (fijo durante la sesión) | Automático cuando hay 3; sin `mobileLabel` (confirmado en Bloque 2) |
| `PriceListsPage` | 3 sin editar ↔ 6 al editar | Automático, sin exclusión (Bloque 2): el cambio conserva nodo, foco y selección; a 320 px la barra de 3 dejaba pestañas fuera de vista |
| `BranchDetailPage` | 3, markup local | Migrada a `ZHTabBar` (automático); `mobileLabel` «Establ.»/«Config.» (Bloque 2) |
| `UserConfigPage` | N, markup local con `aria-controls` | Conservar: mejor relación tab/panel que `ZHTabBar` |
| `CompanySettingsHubPage`, `OperationalPreferencesPage` | N, markup local | Migración opcional, sin cambio visual |
| `AccountingReportsPage` (5), Compras (2), `ItemFormTabs` (2, `fill`), `PaymentMethodsPage` (1–2), `SalesOperationalHeader` (2) | ≠ 3 | Sin cambio |
| `ConfigTabsLayout` (FROZEN), `md-detail-tabs`, `kdx-tabs` | — | Conservar |

### KPI (`ReportKpiCard`, `.pg-kpis`, `.prd-kpi-grid`)

| Caso | Decisión |
|---|---|
| KPI simples (sin `badge` ni `trend`) | Horizontal compacto por defecto (decisión 3) |
| KPI con `trend`/`badge` (`DashboardPage`) | Excepción de contenido: vertical |
| KPI de `ReportPage` (ícono, cifras monetarias) | Validar en horizontal; vertical solo con excepción justificada |
| 3 KPI en `.pg-kpis` (motivos de caja, cajas, catálogo, ítems, transportistas, stock) | Grid según cantidad en el DS; retirar override local de `StockReportPage.css` |
| `OperationalReadinessSection` (auto-fit propio) | Evaluar convergencia al grid del DS; conservar si la composición difiere |
| Resumen de Proveedores | Sustituir `.md-supplier-summary > .prd-kpi-grid` por el grid del DS cuando sea equivalente; los cortes difieren (1024/580 frente a 900/480) y requieren decisión visual |
| **Resumen de Clientes** | **Adaptar a tres KPI horizontales compactos, sin acciones redundantes**, igual composición que Proveedores (decisión 5). El diseño vertical actual es transitorio |

### Tablas, identidad y acciones

| Caso | Decisión |
|---|---|
| `ZHDataTable` (81 archivos) | Sin cambio de default; ocultación móvil de columnas por pantalla |
| Identidad principal/secundaria | Clientes adopta la de Proveedores (decisión 4) en `MasterDataPartnerListTab`, que ya comparte; el CSS pasa de `md-supplier-*` a una regla de módulo común a ambos roles, sin duplicado |
| Acciones de fila de Clientes | «Ver» + «Más acciones» con `ZHLineAction`/`ZHLineActionItem` en modo `portal`; conserva Editar, Configuración de ventas, Clasificación, Agregar como proveedor y Activar/Desactivar con sus permisos y callbacks |
| `ZHLineAction kind="menu"` | Candidato a `portal` por defecto (Proveedores ya lo usa; validar la línea de Compras) |
| `ZHRowDeleteAction` y botones simples | Sin cambio |
| Estados vacíos | Migración gradual a `.zh-inline-empty`/`EmptyState`; sin variantes nuevas |

## Bloque 1 — encabezado compacto predeterminado (alcance para revisión)

### Comportamiento actual del modificador

`.zh-screen-heading--compact` hace flex al `<header>` completo. Sin kicker no hay efecto
visible (un único hijo con `flex: 1`), pero con kicker este queda a la izquierda del título en
escritorio. Hasta 768 px el bloque se apila y el kicker queda arriba.

### Cambios propuestos

1. `zh-ui.css`, solo el modificador `.zh-screen-heading--compact`: retirar el flex del `<header>`
   para que el kicker quede siempre sobre la fila; conservar padding/margen compactos, la
   alineación título-descripción en escritorio y el apilado hasta 768 px. Proveedores y Clientes
   quedan sin diferencia visual, porque no usan kicker.
2. `ZHLayout.tsx`: el default de `variant` pasa a ser compacto. La opción de excluirse es el
   valor actual `"default"`; se propone renombrarlo `"stacked"` para que el nombre no contradiga
   el nuevo default (2 valores, sin variantes nuevas). Decisión del propietario.
3. `PageShell` / `ErpPageTemplate`: sin cambios de lógica; solo el tipo propagado.
4. Proveedores y Clientes: retirar `headingVariant="compact"` (redundante).
5. Excepciones: se declaran solo si una pantalla falla la validación visual, con
   `headingVariant="stacked"` y motivo en el código, listadas en Frontend.

No cambia: títulos, kicker, subtítulos, i18n, acciones, permisos, rutas, `document.title`,
`ReportPage`, tokens, breakpoints globales ni `.page-shell*`.

### Excepciones identificadas

- Reales, fuera del componente: `ReportPage`, auth/setup, Carga Inicial por entidad, POS
  (`SalesPage`) y `CompanyManagementHubPage`. No se tocan.
- Dentro de `PageShell`/`ErpPageTemplate` no hay una estructura funcionalmente distinta que
  justifique excluirse de antemano. Requieren validación focal (no exclusión): toolbars de
  acción anchas, detalles con título/subtítulo dinámicos y `DashboardPage`.

### Pruebas

Unitarias focales:
- `PageShell.test.tsx`: default compacto; opción de excluirse; kicker antes de la fila; acciones
  a la derecha; un solo H1; `NoAccessPage` hereda el compacto.
- `MasterDataSuppliersPage.test.tsx`, `MasterDataCustomersPage.test.tsx`: compacto sin prop
  explícita y sin kicker.
- Gates: `npx tsc -b`, ESLint focal, `architecture:check`,
  `check-architecture-guardrails.ps1 -SkipFrontendChunk`, control de staging y `git diff --check`.
  Sin suites generales ni SRI.

Visuales (antes/después, componentes reales con datos sintéticos; app real cuando haya
sesión del propietario), anchos 320, 360, 390, 480, 768, 769, 1024 y 1440 px:

| Perfil | Pantallas representativas |
|---|---|
| P1 sin kicker, con subtítulo (regresión) | Proveedores, Clientes: idénticas a las capturas aprobadas |
| P2 kicker + subtítulo + acción | `ChartOfAccountsPage`, `UsersPage`, `CarriersPage`, `InventoryAdjustmentReasonsPage` |
| P3 kicker + acción, sin subtítulo | `ItemsPage`, `WarehousesPage`, `BankAccountsPage`, `ItemTypesPage` |
| P4 solo kicker | `CajaPage`, `BrandsPage`, `BranchesPage` |
| P5 subtítulo largo | `CashFundingRequestsPage`, `AccountingReportsPage`, `PayablesPage`, `PurchaseCreditNoteListPage` |
| P6 acción ancha/toolbar | `PermissionsAssignmentPage`, `ExpenseDocumentFormPage`, `SupplierPaymentDetailPage`, `PayableDetailPage` |
| P7 detalle dinámico | `JournalEntryDetailPage`, `SupplierCreditDetailPage`, `PurchaseCreditNoteDetailPage`, `SalesReturnFormPage`, `MasterDataBusinessPartnerDetailPage`, `BranchDetailPage` |
| P8 saludo | `DashboardPage` |
| P9 heredados | `NoAccessPage`, estado «Cargando…» |

Criterios: orden kicker → título → descripción → acciones; un H1; sin overflow horizontal;
acciones visibles y sin solaparse; wrap legible con textos largos; es/en; zoom 200 %; foco de
teclado en acciones. Una pantalla que falle se corrige en el DS o se declara excepción
justificada; nunca con CSS local del encabezado.

Entrada: aprobación de este alcance y del nombre de la opción de excluirse. Salida: aprobación
visual del propietario; staging y commit los hace el propietario.

## Bloques siguientes (pendientes)

| Bloque | Alcance | Validación |
|---|---|---|
| 2 — Pestañas (UIUX-GLOBAL-TABS-DEFAULT-01: aprobado visual y técnicamente, pendiente de consolidación Git/CI) | `ZHTabBar`: patrón responsive automático con exactamente 3 pestañas; exclusión explícita `variant="standard"` (sin consumidores tras verificar `PriceListsPage`); retirar props de Proveedores/Clientes; `mobileLabel` en Ítems; migrar `BranchDetailPage` | `ZHTabBar.test` (2/3/4 pestañas, exclusión, nombre accesible); Ítems, `CashFundingRequestsPage`, `PriceListsPage`, `BranchDetailPage` a 320/390/480/481 |
| 3 — KPI (UIUX-GLOBAL-KPI-DEFAULT-01: aprobado visual y técnicamente, pendiente de consolidación Git/CI) | Horizontal por defecto para KPI simples con excepciones explícitas; grid de `.pg-kpis` según cantidad (1–3 llenan el ancho, 4 igual que hoy, 5+ envuelve como hoy); retirar override de Stock; converger el grid de Proveedores | `ReportKpiCard.test`; pantallas con 3, 4 y 6 KPI (Dashboard, Plan de cuentas) |
| 4 — Clientes | Resumen con tres KPI compactos sin acciones redundantes; identidad y «Ver + Más acciones»; CSS común de `masterData` sin duplicado `md-supplier-*`. Se inicia después de cerrar los pendientes de Clasificación y CI | Pruebas de Clientes/Proveedores y de acciones con permisos reducidos; 390/640/641/1440 |
| 5 — Acciones y vacíos | `portal` por defecto en `ZHLineAction kind="menu"` (validar Compras); migración gradual de vacíos de sección | `ZHLineAction.test`, línea de Compras |

## Reglas documentales aplicables

- La SSOT ([Frontend](../architecture/frontend.md)) describe solo lo implementado y probado. Hasta
  cerrar cada bloque conserva los contratos opt-in vigentes («Pestañas responsive opcionales», «Resumen de Proveedores», «Encabezado y pestañas de Clientes»).
- Al implementar cada bloque, en la misma entrega: reescribir el contrato afectado como default
  con su lista de excepciones; actualizar las secciones de Proveedores y Clientes que mencionan
  la activación explícita; y registrar el estado en STATUS. Sin ADR nuevo: cambia un default
  visual del DS, no la arquitectura ni contratos de API.
- Las excepciones se documentan en Frontend y en el consumidor. No se admiten CSS locales que
  repliquen una variante del DS.
