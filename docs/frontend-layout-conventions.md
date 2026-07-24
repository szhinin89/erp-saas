# Convenciones de layout frontend (ERP)

**Baseline arquitectónico (lectura obligatoria):** [`FRONTEND_ARCHITECTURE_BASELINE.md`](./FRONTEND_ARCHITECTURE_BASELINE.md)

**Fuente de verdad visual:** `frontend/src/styles/design-tokens.css`, `zh-ui.css`, `page-template.css`, `legacy-pages.css`.

---

## Arquitectura de capas (obligatoria)

Las páginas de módulo **no** montan navegación ni `LayoutFrame`. Solo renderizan el contenido del `<Outlet />`.

```
AppLayout
    │
    LayoutFrame
        │
        ├── ZHAppSubscriberHeader  (+ MainMenuBar en bottomLeft)
        │
        ├── <Outlet />
        │
        └── ERP Modules (modules/{dominio}/pages/**)
```

| Capa | Archivo | Responsabilidad |
|------|---------|-----------------|
| **Shell** | `components/AppLayout.tsx` | Resuelve sesión/menú (`useAppLayoutNavigation`), logout, monta `LayoutFrame` |
| **Frame** | `components/layout/LayoutFrame.tsx` | Padding, max-width, scroll del contenido (`--shell-content-*`) |
| **Header + nav** | `components/zh/ZHAppSubscriberHeader.tsx`, `components/MainMenuBar.tsx` | Identidad suscriptor, selector empresa/idioma, navegación principal |
| **Plantilla** | `templates/ErpPageTemplate.tsx` o `PageShell` directo | `PageShell` + título/acción + `.pg-page` opcional |
| **Plantilla reportes** | `components/ReportPageTemplate.tsx` | KPIs, filtros, gráfico (dominio reportes) |
| **Página** | `modules/**/pages/*.tsx` | Estado local, hooks, JSX de negocio (sin shell) |

| Contexto | Shell | Frame | Plantilla | Contenido |
|----------|--------|-------|-----------|-----------|
| ERP | `AppLayout` | `LayoutFrame` (`variant="subscriber"`) | `ErpPageTemplate` o `PageShell` | Módulo / tabs |
| Auth | — | — | `zh-auth-*` | Login / selectores |
| Reportes | `AppLayout` → `LayoutFrame` | — | `ReportPageTemplate` | KPIs + gráfico |

**Prohibido en páginas:** importar `LayoutFrame`, duplicar header/navegación, segundo `<h1>` con el mismo texto que el shell.

### LayoutFrame

- Archivo: `frontend/src/components/layout/LayoutFrame.tsx`
- Tokens: `--shell-content-padding`, `--shell-content-padding-compact` (≤980px), `--shell-content-max-width` (1440px), `--shell-content-gap`
- Slots (`banner`, `topUtilities`): solo en `AppLayout`, no en páginas hijas

### ErpPageTemplate

- Archivo: `frontend/src/templates/ErpPageTemplate.tsx`
- Envuelve `PageShell` + opcional `.pg-page`
- **Usar por defecto** en pantallas nuevas o migradas (ventas, compras, inventario, configuración, etc.)

---

## Criterio: ErpPageTemplate vs PageShell directo

| Usar `ErpPageTemplate` | Usar `PageShell` directo (válido) |
|------------------------|-----------------------------------|
| Pantallas CRUD estándar, documentos, listados con acciones en shell | Catálogos jerárquicos con `.zh-form-tabs` ya estabilizados (`CompaniesPage`, catálogos de estructura) |
| Alta/edición con `pg-page` y secciones | Company-management, security, access (patrón ficha + tabs) |
| Cualquier pantalla nueva de producto | Solo si replica el patrón documentado de referencia |

Ambos son válidos. No duplicar título con `pg-header-row` cuando `PageShell` ya lo provee.

---

## Reglas de pantalla (ERP)

| Permitido | Prohibido |
|-----------|-----------|
| `ErpPageTemplate` / `PageShell`, `pg-page`, `pg-section`, `TableCard` | `LayoutFrame` o header/navegación en la página |
| Tokens `--space-*`, `--shell-content-*` | `padding` / `margin` / `maxWidth` inline estructural |
| Utilidades `pg-*` (`legacy-pages.css`) | `pg-header-row` duplicando título del shell |
| CSS por dominio (`vf-*`, `bod-*`, …) | Shell `layout-*` ad-hoc, colores hardcodeados |

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
| `vf-*` | Ventas facturas listado |
| `prf-*` | Perfiles / modal RBAC |
| `sri-*` | Configuración SRI |
| `br-*` | Sucursales |
| `bill-*` | Facturación (logo, footer) |
| `crt-*` | Transportistas |
| `bod-*` | Bodegas |
| `dsh-*` | Dashboard |
| `rpt-*` | Reportes (`ReportPageTemplate.css`) |

**Regla:** estilos únicos de pantalla → prefijo de dominio; patrones repetidos → subir a `pg-*`.

---

## Responsive — breakpoints

### Oficial recomendado: **980px**

Alineado con `LayoutFrame` (`--shell-content-padding-compact`) y la mayoría de módulos migrados (`br`, `create-invoice`, `prf`).

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
| **640px / 480px** | Auth, filtros reportes | Shells o utilidades puntuales |

**No refactorizar** todos los breakpoints en un solo valor sin ticket dedicado. Pantallas nuevas: preferir **980px** + `pg-overflow-x` en tablas anchas.

### Checklist responsive

- Tablas: contenedor `pg-overflow-x`
- Modales: `pg-modal--sm|md|lg|440|480`
- Grids 2–3 columnas: media query a 1 columna en ≤980px
- Formularios largos: scroll interno del shell (no `overflow` inline en página)

---

## Política de inline styles

### Prohibido en pantallas de producto

En `src/modules/**/pages/**` (salvo excepciones):

- `style={{ margin, padding, maxWidth, overflow, textAlign, … }}` estructural
- Wrappers arbitrarios solo para layout

### Permitido (excepciones)

| Ámbito | Motivo |
|--------|--------|
| `src/modules/auth/pages/**` | Layouts `zh-auth-*` |
| `Modal`, `ZHConfirmModal`, `ZHPromptModal` | Prop `style` / `maxWidth` de API del componente |
| `AccountTreeSelect` | Indent dinámico por nivel (`width` en árbol) |
| `ZHAppSubscriberHeader` | Posición fixed del menú de usuario (coordenadas calculadas) |
| Gráficos SVG demo | Preferir clases `rpt-*` cuando sea posible |

### Enforcement ESLint

- `error`: `JSXAttribute[name.name="style"]` en `src/modules/**/pages/**`, `src/templates/**`, `src/components/layout/**`
- `off`: `src/modules/auth/pages/**`

---

## Prevención de drift

1. Pantalla nueva → `ErpPageTemplate` + utilidades `pg-*`.
2. Antes de PR UI: `rg 'style=\{\{'` en el módulo tocado.
3. No introducir `pg-header-row` en páginas con `PageShell`/`ErpPageTemplate`.
4. Revisar coherencia con `.cursor/rules/erp-unified-rules.mdc` (ZH Form, tabs, Copy UX).

---

## Referencias

- `.cursor/rules/erp-unified-rules.mdc` — ZH Form, tabs, Copy UX
- `.cursor/rules/saas-navigation-no-sensitive-url.mdc` — IDs fuera de URL
