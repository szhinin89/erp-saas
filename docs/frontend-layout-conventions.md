# Convenciones de layout frontend (ERP + Platform)

**Baseline arquitectónico (lectura obligatoria):** [`FRONTEND_ARCHITECTURE_BASELINE.md`](./FRONTEND_ARCHITECTURE_BASELINE.md)

**Fuente de verdad visual:** `frontend/src/styles/design-tokens.css`, `zh-ui.css`, `page-template.css`, `legacy-pages.css`.

**Inventario vivo:** `frontend/src/templates/PAGE-AUDIT.md` · **QA:** [`FRONTEND_QA_CHECKLIST.md`](./FRONTEND_QA_CHECKLIST.md)

---

## Arquitectura de capas (obligatoria)

Las páginas **no** montan navegación ni `LayoutFrame`. Solo renderizan el contenido del `<Outlet />`.

| Contexto | Shell | Frame | Plantilla | Contenido |
|----------|--------|-------|-----------|-----------|
| Tenant ERP | `AppLayout` | `LayoutFrame` (`variant="tenant"`) | `ErpPageTemplate` o `PageShell` | Módulo / tabs |
| Plataforma | `PlatformLayout` | `LayoutFrame` (`variant="platform"`) | `PlatformCrudTemplate` | Sección platform |
| Auth | — | — | `zh-auth-*` | Login / selectores |
| Reportes | `AppLayout` → Frame | — | `ReportPageTemplate` | KPIs + gráfico |

```
Tenant:    AppLayout → LayoutFrame → ErpPageTemplate | PageShell → contenido
Platform:  PlatformLayout → LayoutFrame → PlatformCrudTemplate → contenido
```

### LayoutFrame

- Archivo: `frontend/src/components/layout/LayoutFrame.tsx`
- Tokens: `--shell-content-padding`, `--shell-content-padding-compact` (≤980px), `--shell-content-max-width` (1440px), `--shell-content-gap`
- Slots (`banner`, `topUtilities`): solo en shells, no en páginas hijas

### ErpPageTemplate

- Archivo: `frontend/src/templates/ErpPageTemplate.tsx`
- Envuelve `PageShell` + opcional `.pg-page`
- **Usar por defecto** en pantallas tenant nuevas o migradas (ventas, compras, inventario, config, etc.)

### PlatformCrudTemplate

- Archivo: `frontend/src/templates/PlatformCrudTemplate.tsx`
- Envuelve `PlatformPageTemplate` con `hideHeader` en rutas bajo topbar
- **Usar por defecto** en `/platform/*`

---

## Criterio: ErpPageTemplate vs PageShell directo

| Usar `ErpPageTemplate` | Usar `PageShell` directo (válido) |
|------------------------|-----------------------------------|
| Pantallas CRUD estándar, documentos, listados con acciones en shell | Catálogos jerárquicos con `.zh-form-tabs` ya estabilizados (`CompaniesPage`, `CategoriesCatalogPage`, `CatalogSimplePage`) |
| Alta/edición con `pg-page` y secciones | Company-management, security, access (patrón ficha + tabs legacy) |
| Cualquier pantalla nueva de producto tenant | Solo si replica el patrón documentado de referencia |

Ambos son **clase A** en `PAGE-AUDIT.md`. No duplicar título con `pg-header-row` cuando `PageShell` ya lo provee.

---

## Reglas tenant (ERP)

| Permitido | Prohibido |
|-----------|-----------|
| `ErpPageTemplate` / `PageShell`, `pg-page`, `pg-section`, `TableCard` | `LayoutFrame` o sidebar/topbar en la página |
| Tokens `--space-*`, `--shell-content-*` | `padding` / `margin` / `maxWidth` inline estructural |
| Utilidades `pg-*` (`legacy-pages.css`) | `pg-header-row` duplicando título del shell |
| CSS por dominio (`vf-*`, `acc-*`, …) | Shell `layout-*` ad-hoc, colores hardcodeados |

---

## SuperAdmin (plataforma)

### Política de títulos

| Ubicación | Responsabilidad |
|-----------|-----------------|
| `sa-topbar-title` | Título de ruta (único H1 de shell) |
| `PlatformCrudTemplate` | Cabecera de página oculta (`hideHeader` / `shellLayout`) |
| `ZHScreenHeading` en página | Subtítulo / KPI — **no** repetir texto del topbar |

### Aislamiento menu builder (platform)

| Prefijo | Archivo | Uso |
|---------|---------|-----|
| `smp-*` | `menu-builder/menu-preview-sim.css` | Simulación navegador en `MenuPreview` |
| `smb-*` | `superadmin/menu-plan-composer.css` | CRM workspace, audit, modales, toggles |

**No** importar `smp-*` / `smb-*` en módulos tenant. Inline permitido en `MenuPreview` solo para indentación dinámica por profundidad del árbol (`paddingLeft` calculado).

---

## Prefijos CSS oficiales

### Transversal (`legacy-pages.css` + `page-template.css`)

| Prefijo | Uso |
|---------|-----|
| `pg-*` | Layout, tablas, modales, estados, utilidades compartidas |
| `zh-*` | Componentes ZH Form / UI global |

### Por dominio (`modules/**/**/*-page.css`)

| Prefijo | Módulo / pantalla |
|---------|-------------------|
| `acc-*` | Contabilidad (`AccountingPage.css`) |
| `vf-*` | Ventas facturas listado |
| `cn-*` | Notas de crédito (crear + columnas) |
| `prf-*` | Perfiles / modal RBAC |
| `sri-*` | Configuración SRI |
| `cat-*` | Estructura catálogo (cascade) |
| `cls-*` | Clientes (paneles) |
| `br-*` | Sucursales |
| `bill-*` | Facturación (logo, footer) |
| `cf-*` | Compras / facturas |
| `gst-*` | Gastos |
| `crt-*` | Transportistas |
| `adj-*` | Ajustes inventario |
| `bod-*` | Bodegas |
| `dsh-*` | Dashboard |
| `sap-*` | SuperAdmin planes |
| `rpt-*` | Reportes (`ReportPageTemplate.css`) |
| `smp-*` | Menu Builder — preview simulado (platform) |
| `smb-*` | Menu Builder — CRM workspace (platform) |

**Regla:** estilos únicos de pantalla → prefijo de dominio; patrones repetidos → subir a `pg-*`. Prefijos `smp-*` / `smb-*` solo en platform.

---

## Responsive — breakpoints

### Oficial recomendado: **980px**

Alineado con `LayoutFrame` (`--shell-content-padding-compact`) y la mayoría de módulos migrados (`acc`, `cat`, `br`, `create-invoice`, `prf`).

```css
@media (max-width: 980px) {
  /* grids multi-columna → 1 columna; revisar modales y tablas */
}
```

### Excepciones documentadas

| Breakpoint | Dónde | Motivo |
|------------|-------|--------|
| **1024px** | `DashboardPage.css`, `ventas-facturas-page.css` | KPIs / soporte en layouts previos |
| **760px** | `suppliers-page.css` | Formulario proveedores más estrecho |
| **640px / 480px** | Auth, SuperAdmin panel, filtros reportes | Shells o utilidades puntuales |

**No refactorizar** todos los breakpoints en un solo valor sin ticket dedicado. Pantallas nuevas: preferir **980px** + `pg-overflow-x` en tablas anchas.

### Checklist responsive

- Tablas: contenedor `pg-overflow-x`
- Modales: `pg-modal--sm|md|lg|440|480`
- Grids 2–3 columnas: media query a 1 columna en ≤980px
- Formularios largos: scroll interno del shell (no `overflow` inline en página)

---

## Política de inline styles

### Prohibido en pantallas de producto

En `src/pages/**` y `src/modules/**/pages/**` (salvo excepciones):

- `style={{ margin, padding, maxWidth, overflow, textAlign, … }}` estructural
- Wrappers arbitrarios solo para layout

### Permitido (excepciones)

| Ámbito | Motivo |
|--------|--------|
| `src/modules/auth/pages/**` | Clase C — layouts `zh-auth-*` |
| `Modal`, `ZHConfirmModal`, `ZHPromptModal` | Prop `style` / `maxWidth` de API del componente |
| `AccountTreeSelect` | Indent dinámico por nivel (`width` en árbol) |
| `ZHAppSubscriberHeader` | Posición fixed del menú (coordenadas calculadas) |
| `MenuPreview` + SuperAdmin menu builder | Herramienta plataforma aislada |
| Gráficos SVG demo | Preferir clases `rpt-*` cuando sea posible |

### Enforcement ESLint

- `error`: `JSXAttribute[name.name="style"]` en `src/pages/**`, `src/modules/**/pages/**`, `src/templates/**`, `src/components/layout/**`
- `off`: `src/modules/auth/pages/**`

---

## Prevención de drift

1. Pantalla tenant nueva → `ErpPageTemplate` + utilidades `pg-*`.
2. Pantalla SuperAdmin nueva → `PlatformCrudTemplate`.
3. Antes de PR UI: `rg 'style=\{\{'` en el módulo tocado; actualizar `PAGE-AUDIT.md` si cambia deuda.
4. No introducir `pg-header-row` en páginas con `PageShell`/`ErpPageTemplate`.
5. Revisar coherencia con `.cursor/rules/erp-unified-rules.mdc` (ZH Form, tabs, Copy UX).

---

## Estado de migración (2026-05-21)

**Tenant ERP producto:** migración visual consolidada (ventas, compras, inventario, contabilidad, catálogo, clientes, sucursales, SRI, billing, RBAC modal, layouts).

**Deuda residual tenant en `modules/**/pages`:** cerrada en micro-lote gobernanza (empresa, NC listado, crear gasto, transportistas, crear ajuste).

**Pendiente fuera de tenant estándar:** i18n de subtítulos hardcodeados; warnings `max-lines` en páginas ventas grandes (refactor opcional).

---

## Referencias

- `frontend/src/templates/PAGE-AUDIT.md` — inventario por pantalla
- `frontend/src/templates/README.md` — plantillas
- `.cursor/rules/erp-unified-rules.mdc` — ZH Form, tabs, Copy UX
- `.cursor/rules/saas-navigation-no-sensitive-url.mdc` — IDs fuera de URL
