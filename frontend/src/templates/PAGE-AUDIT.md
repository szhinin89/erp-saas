# Inventario visual de páginas (snapshot)

Actualizar al migrar pantallas. Convenciones: [`docs/frontend-layout-conventions.md`](../../docs/frontend-layout-conventions.md).

**Última actualización:** 2026-05-21 — baseline sellado (Frontend Governance v1.0, QA-ready).

## Leyenda

| Clase | Significado |
|-------|-------------|
| **A** | `PageShell` / `ErpPageTemplate` / `PlatformCrudTemplate` / `ReportPage` |
| **B** | `pg-page` + cabecera manual (`pg-header-row` o `ZHScreenHeading`) |
| **C** | Shell custom (`dsh-page`, `zh-auth-*`, `sap-*`) |

## A — Conformes (tenant + SuperAdmin)

### Shell y plantillas
- `AppLayout` + `LayoutFrame` + rutas tenant
- `PlatformLayout` + `LayoutFrame` + `PlatformCrudTemplate`
- `templates/ErpPageTemplate.tsx`, `templates/PlatformCrudTemplate.tsx`

### Ya conformes (referencia previa)
- `modules/companies/pages/CompaniesPage.tsx`
- `modules/access/pages/SubscriberAccessPage.tsx`
- `modules/security/pages/SecuritySettingsPage.tsx`
- `modules/company-management/pages/CompanyManagementListPage.tsx`
- `modules/company-management/pages/CompanyManagementFormPage.tsx`
- `modules/shared/pages/FeaturePlaceholderPage.tsx`, `ModulePlaceholderPage.tsx`
- `modules/reportes/pages/SalesReportPage.tsx` (`ReportPageTemplate`)
- `modules/catalog/pages/CatalogSimplePage.tsx`
- `modules/catalog/pages/CategoriesCatalogPage.tsx`, `SubcategoriesCatalogPage.tsx`, `LinesCatalogPage.tsx` (`PageShell`)

### Lote alta (2026-05-21)
- `modules/dashboard/pages/DashboardPage.tsx`
- `modules/branches/pages/BranchesPage.tsx`
- `modules/configuracion/empresa/pages/CompanyConfigPage.tsx`
- `modules/products/pages/ProductPage.tsx`
- `modules/customers/pages/CustomersPage.tsx`
- `modules/ventas/pages/VentasFacturasPage.tsx`
- `modules/accounting/pages/AccountingPage.tsx`
- `modules/compras/suppliers/pages/SuppliersPage.tsx`
- `modules/access/pages/ProfilesPage.tsx` *(ErpPageTemplate + modal RBAC en `ProfilesPage.css`)*

### Lote B/C → ErpPageTemplate (2026-05-21)

**Configuración**
- `modules/configuracion/sri/pages/SriConfigPage.tsx`
- `modules/configuracion/facturacion/pages/BillingSettingsPage.tsx`

**Catálogo**
- `modules/catalog/pages/CatalogStructurePage.tsx`
- `modules/catalog/pages/BrandsPage.tsx`
- `modules/catalog/pages/UnitsPage.tsx`
- `modules/catalog/pages/ProductTypesPage.tsx`

**Compras**
- `modules/compras/facturas/pages/ComprasListPage.tsx`
- `modules/compras/facturas/pages/CrearCompraPage.tsx`
- `modules/compras/ordenes/pages/OrdenesCompraListPage.tsx`
- `modules/compras/ordenes/pages/CrearOrdenCompraPage.tsx`
- `modules/compras/ordenes/pages/OrdenCompraDetailPage.tsx`

**Gastos**
- `modules/gastos/pages/GastosListPage.tsx`
- `modules/gastos/pages/CrearGastoPage.tsx`

**Inventario**
- `modules/inventario/warehouses/pages/BodegasPage.tsx`
- `modules/inventario/ajustes/pages/AjustesListPage.tsx`
- `modules/inventario/ajustes/pages/CrearAjustePage.tsx`
- `modules/inventario/ajustes/pages/AjusteDetailPage.tsx`
- `modules/inventario/transferencias/pages/TransferenciasListPage.tsx`
- `modules/inventario/transferencias/pages/CrearTransferenciaPage.tsx`
- `modules/inventario/transferencias/pages/TransferenciaDetailPage.tsx`

**Ventas (facturación avanzada)**
- `modules/ventas/pages/CreateInvoicePage.tsx` *(CSS `create-invoice-page.css`, sin inline estructural)*
- `modules/ventas/pages/CreditNotesPage.tsx`
- `modules/ventas/pages/CreateCreditNotePage.tsx` *(CSS `credit-notes-page.css`, 0 inline estructural)*

**Logística**
- `modules/logistica/transportistas/pages/CarriersPage.tsx`

**SaaS (tenant)**
- `pages/saas/SaasOverviewPage.tsx`
- `pages/saas/SaasBillingPage.tsx`

### SuperAdmin
- `modules/platform/pages/PlatformPanelPage.tsx`
- `pages/Platform/PlatformOverviewPage.tsx`
- `pages/Platform/SuperAdminPlansPage.tsx`
- `pages/Platform/PlatformMenuPlansHubPage.tsx`
- `pages/Platform/PlatformCompaniesShellPage.tsx`

## B — Pendiente (sin `pg-header-row` en páginas de producto)

Ninguna pantalla de módulo tenant listada arriba conserva `pg-header-row` en el archivo de página (grep 2026-05-21).

**Deuda en `modules/**/pages` (tenant):** — *(cerrada 2026-05-21: CompanyConfig, CreditNotes listado, CrearGasto, Carriers, CrearAjuste; SalesReport → `rpt-chart-tooltip-anchor`)*

**Excepciones fuera de páginas tenant:**
- `MenuPreview` — indentación dinámica por profundidad (`style={paddingLeft}` en ítems sidebar; 3 usos)
- Componentes base modales (`Modal`, `ZHConfirmModal`) — prop `style` de API
- Componentes base: `Modal`, `ZHConfirmModal`, `ZHPromptModal` — prop `style` de API
- `AccountTreeSelect` — indent dinámico por nivel
- Auth: `SubscriberSelectPage` — clase C (`eslint` off en `modules/auth/pages`)

## C — Excepciones válidas

- **Auth:** `LoginPage`, `ForgotPasswordPage`, `ResetPasswordPage`, `PasswordResetPage`, `SubscriberSelectPage`, `CompanySelectPage`
- **SuperAdmin:** `SuperAdminPlansSection` — subcabecera `sap-dash-head` bajo topbar de ruta
- **Reportes:** `components/ReportPageTemplate.tsx` — plantilla de dominio con `pg-header-row` interno (consumida por `SalesReportPage`)

## Utilidades compartidas

### `legacy-pages.css` (global vía `App.tsx`)
- Layout: `pg-pad-40`, `pg-overflow-x`, `pg-section--mb-4`, `pg-mt-4`, `pg-td-center`, `pg-form-span-full`, `pg-radio-group`, `pg-summary-box`
- Modales: `pg-modal--sm`, `pg-modal--md`, `pg-modal--lg`, `pg-modal--440`, `pg-modal--480`, `pg-reject-body`, `pg-reject-hint`, `pg-modal-hint`
- Tablas: `pg-th-right`, `pg-td-right`, `pg-row-inactive`, `pg-row-clickable`, `pg-cell-muted`, `pg-cell-strong`, `pg-cell-warn`, `pg-cell-success`
- Acciones: `pg-actions-inline`, `pg-actions-inline-10`, `pg-btn-error-ghost`, `pg-table-actions-row`
- Ficha: `pg-info-item-label`, `pg-info-item-value`, `pg-doc-hero-mono`, `pg-doc-notes-label`
- Estados: `pg-state-pad-24`, `pg-state-pad-24-center`, iconos `pg-icon-*`

### CSS por dominio (nuevos/ampliados 2026-05-21)

**Lotes previos**
- `catalog-list-page.css` — Brands, Units, ProductTypes
- `orden-compra-page.css` — `OrdenCompraDetailPage`, `CrearOrdenCompraPage`
- `BodegasPage.css` — modal + listado (`bod-*`)
- `credit-notes-page.css` — `CreateCreditNotePage` (`cn-th-*`, `cn-items-scroll`)
- `AccountingPage.css` — tabs contables (`acc-tab-*`, `acc-journal-*`, `acc-cell-balance-*`)

**Hardening final (2026-05-21)**
- `sri-config-page.css` — `SriConfigPageDataTab` (`sri-*`: ambiente, WSDL, toggles, hints)
- `CatalogStructurePage.css` — cascade + modal (`cat-*`: grid 3 col, items, scroll, `@media ≤980px` → 1 col)
- `customers-page.css` — paneles listado/categorías/contactos/auditoría (`cls-*` + `pg-overflow-x`)
- `branches-page.css` — `BranchesListSection`, `BranchFormModal` (`br-*` + `pg-modal--lg`)
- `compras-facturas-page.css` — ampliado (`cf-th-*`) para `CrearCompraPage`
- `billing-settings-page.css` — `BillingSettingsPage` (`bill-*`: logo, footer textarea)
- `gastos-page.css` — ampliado (`gst-totals-*`) para `CrearGastoPage`
- `carriers-page.css` — `crt-col-name`; `ajustes-pages.css` — `adj-stock-hint*`

Reutilización transversal: `pg-pad-40`, `pg-overflow-x`, `pg-th/td-right`, `pg-modal--*`, `pg-summary-box`, `pg-btn-error-ghost`.

## Deuda pendiente (real, post-hardening)

| Área | Qué queda | Prioridad |
|------|-----------|-----------|
| **Micro-deuda tenant pages** | — *(CompanyConfig, CreditNotes, CrearGasto, Carriers, CrearAjuste — 2026-05-21)* |
| **SRI / cascade / clientes / sucursales / CrearCompra / Billing** | — *(completados)* |
| **Contabilidad tabs** | — *(completado)* |
| **SuperAdmin CRM menu** | — *(2026-05-21: `smp-*` + `smb-*`, 0 inline estructural en CRM)* |
| **Componentes base** | `Modal`, `ZHConfirmModal`, `ZHPromptModal` — prop `style` | Excepción API |
| **Árbol contable** | `AccountTreeSelect` indent dinámico | Excepción válida |
| **Auth** | `SubscriberSelectPage` (~2) | Clase C |
| **i18n** | Subtítulos hardcodeados en listados | Fuera alcance visual |

## Riesgos / notas

- **Cascade catálogo:** hover/selected migrado a CSS (`:hover`, `.cat-cascade-item--selected`); sin `useState` de hover.
- **SRI ambiente:** `.sri-env-option--selected` usa fallback `var(--color-primary-subtle, #f0f4ff)` alineado al patrón previo.
- **Responsive:** `.cat-cascade-grid` colapsa a 1 columna en `≤980px`; tablas usan `pg-overflow-x`.
- **Regresión evitada:** no se alteraron handlers, stores, APIs ni rutas.

## Platform — Menu Builder (2026-05-21)

| CSS | Ámbito |
|-----|--------|
| `menu-preview-sim.css` (`smp-*`) | `MenuPreview` simulación navegador |
| `menu-plan-composer.css` (`smb-*` + `menu-plan-composer__*`) | CRM workspace, audit, modales, preview section |

Aislamiento: no importar `smp-*`/`smb-*` en módulos tenant.

## Gobernanza (2026-05-21)

- `docs/frontend-layout-conventions.md` — sincronizado con arquitectura real, prefijos, breakpoints, inline policy
- ESLint `error` en `style={{}}` para `src/modules/**/pages/**` (excepción: `modules/auth/pages`)
- Breakpoint oficial documentado: **980px** (excepciones 1024 / 760 / 640)

## Validación

- `npm run build` — OK (2026-05-21, baseline sellado)
- `npm run lint` — OK, 0 errores (warnings: max-lines ×5, react-refresh, hooks deps)
- **QA manual:** [`docs/FRONTEND_QA_CHECKLIST.md`](../../docs/FRONTEND_QA_CHECKLIST.md)
- **Baseline:** [`docs/FRONTEND_ARCHITECTURE_BASELINE.md`](../../docs/FRONTEND_ARCHITECTURE_BASELINE.md)
