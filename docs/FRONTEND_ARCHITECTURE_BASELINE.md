# Frontend Architecture Baseline — ERP SaaS ZH Technologies

**Estado:** Vigente — refleja el Application Shell actual del ERP.

---

## 1. Visión general

El frontend es una SPA **React 19 + Vite + TypeScript** organizada en **módulos de dominio** bajo `frontend/src/modules/`. La UI sigue el **ZH Form System** y una jerarquía visual fija:

**AppLayout → LayoutFrame → Plantilla → Contenido**

No existe un design system paralelo: tokens (`design-tokens.css`), componentes globales (`zh-ui.css`, `page-template.css`) y utilidades (`legacy-pages.css`) son la fuente de verdad visual.

| Bounded context | Ruta típica | Shell |
|-----------------|-------------|--------|
| **ERP** | `/dashboard`, `/sales`, `/inventory`, … | `AppLayout` |
| **Auth / onboarding** | `/login`, `/select-subscriber` | `zh-auth-*` (excepción, sin shell) |
| **Reportes** | `/reports/*` | `AppLayout` + `ReportPageTemplate` |

---

## 2. Application Shell

```
AppLayout
    │
    LayoutFrame
        │
        ├── ZHAppSubscriberHeader
        │       ├── CompanySwitcher (leftExtra)
        │       ├── LanguageSwitcher (rightExtra)
        │       └── MainMenuBar (bottomLeft)
        │
        ├── React Router <Outlet />
        │
        └── ERP Modules (modules/{dominio}/pages/**)
```

### Flujo de composición

```
AppLayout
  └── LayoutFrame (variant="subscriber")
        └── ErpPageTemplate | PageShell
              └── pg-page / TableCard / zh-form-tabs / contenido

Auth:
  (sin LayoutFrame)
    └── zh-auth-* layouts

Reportes:
  AppLayout → LayoutFrame → ReportPageTemplate → contenido
```

### Responsabilidades por capa

| Capa | Archivo | Responsabilidad |
|------|---------|-----------------|
| **Shell** | `components/AppLayout.tsx` | Resuelve sesión/menú vía `useAppLayoutNavigation`, logout, monta `LayoutFrame` |
| **Frame** | `components/layout/LayoutFrame.tsx` | Padding, max-width, scroll del contenido (`--shell-content-*`) |
| **Header** | `components/zh/ZHAppSubscriberHeader.tsx` | Identidad suscriptor, selector empresa/idioma, menú de usuario |
| **Navegación principal** | `components/MainMenuBar.tsx`, `components/AppLayoutMainMenu.tsx`, `components/useAppLayoutNavigation.ts`, `nav/navConfig.ts` | Construye y renderiza el menú principal desde `GET /api/me/menu` |
| **Plantilla** | `templates/ErpPageTemplate.tsx` | `PageShell` + título/acción + `.pg-page` opcional |
| **Plantilla reportes** | `components/ReportPageTemplate.tsx` | KPIs, filtros, gráfico (dominio reportes) |
| **Página** | `modules/**/pages/*.tsx` | Estado local, hooks, JSX de negocio (sin shell) |

**Prohibido en páginas:** importar `LayoutFrame`, duplicar header/navegación, segundo `<h1>` con el mismo texto que el shell.

---

## 3. Layouts

### AppLayout

- Resuelve el menú de sesión desde `GET /api/me/menu` (filtrado por permisos y plan SaaS).
- Renderiza `ZHAppSubscriberHeader` con `CompanySwitcher`, `LanguageSwitcher` y `MainMenuBar`.
- Monta `LayoutFrame` con `variant="subscriber"` alrededor del `<Outlet />`.

### LayoutFrame

- Tokens: `--shell-content-padding`, `--shell-content-padding-compact` (≤980px), `--shell-content-max-width` (1440px), `--shell-content-gap`.
- Slots `banner` / `topUtilities` solo en `AppLayout`.

---

## 4. Plantillas

### ErpPageTemplate (default)

Usar en **pantallas nuevas de producto** y en la mayoría de módulos migrados (ventas, compras, inventario, contabilidad, configuración).

```tsx
<ErpPageTemplate title="…" action={…}>
  {/* pg-section, TableCard, tabs */}
</ErpPageTemplate>
```

### PageShell directo (válido)

Catálogos con `.zh-form-tabs` ya estabilizados: `CompaniesPage`, catálogos de estructura, company-management, security, access.

### ReportPageTemplate

`SalesReportPage` y reportes — excepción de dominio documentada.

### Criterio: ErpPageTemplate vs PageShell directo

| Usar `ErpPageTemplate` | Usar `PageShell` directo (válido) |
|------------------------|-----------------------------------|
| Pantallas CRUD estándar, documentos, listados con acciones en shell | Catálogos jerárquicos con `.zh-form-tabs` ya estabilizados (`CompaniesPage`, catálogos de estructura) |
| Alta/edición con `pg-page` y secciones | Company-management, security, access (patrón ficha + tabs) |
| Cualquier pantalla nueva de producto | Solo si replica el patrón documentado de referencia |

Ambos son válidos. No duplicar título con `pg-header-row` cuando `PageShell` ya lo provee.

---

## 5. Estrategia CSS

### Jerarquía (no romper)

1. `styles/design-tokens.css` — variables
2. `styles/zh-ui.css` — componentes globales (`.table`, `.badge`, `.zh-btn`, …)
3. `styles/page-template.css` — layout página (`.pg-page`, `.pg-kpi`, …)
4. `pages/legacy-pages.css` — utilidades transversales `pg-*` (import global en `App.tsx`)
5. `modules/**/**/*-page.css` — estilos **únicos** del dominio (prefijo de módulo)

### Prefijos oficiales

| Prefijo | Ámbito |
|---------|--------|
| `pg-*` | Transversal — utilidades |
| `zh-*` | ZH Form / UI global |
| `vf-*` | Ventas facturas |
| `prf-*` | Perfiles / RBAC modal |
| `sri-*` | Config SRI |
| `br-*` | Sucursales |
| `bill-*` | Facturación |
| `crt-*` | Transportistas |
| `bod-*` | Bodegas |
| `dsh-*` | Dashboard |
| `rpt-*` | Reportes |

**Regla:** si un patrón se repite en 3+ pantallas → subir a `pg-*`. No duplicar colores fuera de tokens.

---

## 6. Tokens clave

Definidos en `frontend/src/styles/design-tokens.css`:

- Color: `--color-primary`, `--color-surface-*`, `--color-text-*`, `--color-border`, `--color-error`, …
- Espacio: `--space-1` … `--space-6`
- Tipografía: `--text-body-md-size`, `--text-label-sm-size`, …
- Shell: `--shell-content-padding`, `--shell-content-max-width`, …

---

## 7. Responsive

| Breakpoint | Uso oficial | Excepciones documentadas |
|------------|-------------|---------------------------|
| **980px** | **Recomendado** — grids multi-columna → 1 col; shell padding compact | — |
| 1024px | Dashboard KPIs, ventas facturas soporte | Legacy previo |
| 760px | Proveedores formulario | Caso estrecho |
| 640px / 480px | Auth, filtros reportes | Utilidades puntuales |

**Tablas anchas:** contenedor `pg-overflow-x`.
**Modales:** `pg-modal--sm|md|lg|440|480`.

---

## 8. Governance rules

### Inline styles

| Ámbito | Regla ESLint |
|--------|----------------|
| `src/modules/**/pages/**` | `error` — sin `style={{}}` estructural |
| `src/modules/auth/pages/**` | `off` — excepción |
| `src/templates/**`, `components/layout/**` | `error` |

**Excepciones válidas:** props `style` en `Modal` / `ZHConfirmModal`; indent dinámico en `AccountTreeSelect`; hover runtime en `ZHAppSubscriberHeader` (coordenadas calculadas).

### CI frontend

```bash
cd frontend
npm run lint   # 0 errores requerido
npm run build
```

Playwright smoke: `.github/workflows/frontend-ci.yml`.

---

## 9. Decisiones arquitectónicas importantes

| Decisión | Motivo |
|----------|--------|
| `LayoutFrame` solo en `AppLayout` | Un solo lugar para padding/max-width/scroll |
| `ErpPageTemplate` como default | Título y `.pg-page` consistentes |
| `PageShell` directo permitido en catálogos tabs | Patrón ya probado; evitar reescritura masiva |
| `legacy-pages.css` global | Utilidades `pg-*` compartidas sin import por página |
| Prefijos por dominio | Evitar CSS monolítico y colisiones |
| ESLint `style` en `modules/**/pages/**` | Prevenir regresión visual |
| Sin UUID tenant en URL | `sessionStorage` `erp.saas.*` (regla producto) |

---

## 10. Reglas para nuevas pantallas

1. Toda pantalla nueva vive bajo `AppLayout` → `LayoutFrame` → `<Outlet />`.
2. Producto nuevo → `ErpPageTemplate` salvo que replique patrón de catálogo con tabs (`PageShell`).
3. Crear `*-page.css` con prefijo de dominio si hay estilos no cubiertos por `pg-*` / `zh-ui`.
4. Tablas → `pg-overflow-x`; estados vacío/carga → `pg-pad-40`.
5. Añadir claves i18n en `es` / `en` / `qu`.
6. Ejecutar `npm run lint` y `npm run build` antes del PR.

---

## 11. Checklist para contributors (PR UI)

- [ ] ¿Pantalla dentro de `AppLayout` → `LayoutFrame`, sin montar `LayoutFrame` en la página?
- [ ] ¿Un solo H1 visible (shell o plantilla, no duplicado)?
- [ ] ¿Sin `style={{}}` estructural en `modules/**/pages/**`?
- [ ] ¿Tokens / `pg-*` en lugar de valores mágicos?
- [ ] ¿Tablas con overflow horizontal?
- [ ] ¿i18n en tres locales si hay texto visible?
- [ ] ¿Prefijo CSS de dominio si aplica?
- [ ] ¿Sin GUID de tenant en query string?

---

## 12. Checklist para agentes IA

1. Leer este documento (cuerpo normativo único de layout/CSS frontend — `frontend-layout-conventions.md` fue fusionado aquí, ver nota al final).
2. No proponer shells, routers ni stores nuevos sin petición explícita.
3. Migraciones visuales: solo CSS/clases; no cambiar handlers, APIs ni hooks de negocio.
4. Preferir `ErpPageTemplate` en pantallas nuevas.
5. Buscar utilidad existente: `grep` en `legacy-pages.css` y `page-template.css` antes de CSS nuevo.
6. Tras cambios: `npm run build` + `npm run lint` (0 errores).
7. Antes de un PR de UI: `rg 'style=\{\{'` en el módulo tocado — no introducir `pg-header-row` en páginas con `PageShell`/`ErpPageTemplate`. Coherencia adicional: `.cursor/rules/erp-unified-rules.mdc` (ZH Form, tabs, Copy UX).

---

## Referencias cruzadas

| Documento | Propósito |
|-----------|-----------|
| [`/CLAUDE.md`](../CLAUDE.md) | Reglas globales |
| [`/frontend/CLAUDE.md`](../frontend/CLAUDE.md) | Reglas frontend (adaptador) |
| [`docs/architecture/frontend.md`](architecture/frontend.md) | Cuerpo normativo Design System / ZH Form |
| `.cursor/rules/erp-unified-rules.mdc` | ZH Form, tabs, Copy UX (Cursor) |
| `.cursor/rules/saas-navigation-no-sensitive-url.mdc` | IDs fuera de URL (Cursor) |

> **Nota (Bloque 16B, 2026-08-07):** este documento fusiona el contenido antes duplicado en `docs/frontend-layout-conventions.md` (ahora un stub de redirección, ver ese archivo). Cuerpo normativo único de layout/CSS frontend: este archivo.
