# Frontend Architecture Baseline — ERP SaaS ZH Technologies

**Versión:** Frontend Governance v1.0  
**Fecha:** 2026-05-21  
**Estado:** Baseline sellada — listo para QA formal  
**Release:** [`RELEASES/RELEASE-FRONTEND-GOVERNANCE-v1.0.md`](../RELEASES/RELEASE-FRONTEND-GOVERNANCE-v1.0.md)

---

## 1. Visión general

El frontend es una SPA **React 19 + Vite + TypeScript** organizada en **módulos de dominio** bajo `frontend/src/modules/`. La UI sigue el **ZH Form System** y una jerarquía visual fija:

**Shell → LayoutFrame → Plantilla → Contenido**

No existe un design system paralelo: tokens (`design-tokens.css`), componentes globales (`zh-ui.css`, `page-template.css`) y utilidades (`legacy-pages.css`) son la fuente de verdad visual.

| Bounded context | Ruta típica | Shell |
|-----------------|-------------|--------|
| **Tenant ERP** | `/dashboard`, `/ventas`, … | `AppLayout` |
| **Plataforma (control plane)** | `/platform/*` (+ redirect `/superadmin/*`) | `PlatformLayout` |
| **Auth / onboarding** | `/login`, `/select-subscriber` | `zh-auth-*` (excepción) |
| **Reportes** | `/reportes/*` | `AppLayout` + `ReportPageTemplate` |

**Regla de aislamiento:** estilos y plantillas **platform** (`smp-*`, `smb-*`) no se importan en módulos tenant.

---

## 2. Flujo de composición

```
Tenant:
  AppLayout
    └── LayoutFrame (variant="tenant")
          └── ErpPageTemplate | PageShell
                └── pg-page / TableCard / zh-form-tabs / contenido

Platform:
  PlatformLayout
    └── LayoutFrame (variant="platform")
          └── PlatformCrudTemplate
                └── pg-page / secciones / Menu Builder

Auth:
  (sin LayoutFrame)
    └── zh-auth-* layouts

Reportes:
  AppLayout → LayoutFrame → ReportPageTemplate → contenido
```

### Responsabilidades por capa

| Capa | Archivo | Responsabilidad |
|------|---------|-----------------|
| **Shell** | `components/AppLayout.tsx`, `layouts/PlatformLayout.tsx` | Sidebar/topbar, banner impersonación, `<Outlet />` |
| **Frame** | `components/layout/LayoutFrame.tsx` | Padding, max-width, scroll del contenido (`--shell-content-*`) |
| **Plantilla tenant** | `templates/ErpPageTemplate.tsx` | `PageShell` + título/acción + `.pg-page` opcional |
| **Plantilla platform** | `templates/PlatformCrudTemplate.tsx` | Cabecera oculta bajo topbar platform |
| **Plantilla reportes** | `components/ReportPageTemplate.tsx` | KPIs, filtros, gráfico (dominio reportes) |
| **Página** | `modules/**/pages/*.tsx` | Estado local, hooks, JSX de negocio (sin shell) |

**Prohibido en páginas:** importar `LayoutFrame`, duplicar sidebar/topbar, segundo `<h1>` con el mismo texto que el shell.

---

## 3. Layouts

### AppLayout (tenant)

- Menú lateral desde API de sesión (filtrado por permisos y plan SaaS).
- Banner impersonación platform en contexto tenant.
- Monta `LayoutFrame` con `variant="tenant"` alrededor del `<Outlet />`.

### PlatformLayout (control plane)

- Sidebar y topbar de plataforma (`sa-topbar-title` = H1 de ruta).
- `LayoutFrame` con `variant="platform"`.
- Rutas `/platform/*`; bookmarks `/superadmin/*` redirigen. Sin UUID tenant en URL (reglas SaaS).

### LayoutFrame

- Tokens: `--shell-content-padding`, `--shell-content-padding-compact` (≤980px), `--shell-content-max-width` (1440px), `--shell-content-gap`.
- Slots `banner` / `topUtilities` solo en shells.

---

## 4. Plantillas

### ErpPageTemplate (default tenant)

Usar en **pantallas nuevas de producto** y en la mayoría de módulos migrados (ventas, compras, inventario, contabilidad, config).

```tsx
<ErpPageTemplate title="…" action={…}>
  {/* pg-section, TableCard, tabs */}
</ErpPageTemplate>
```

### PageShell directo (válido)

Catálogos con `.zh-form-tabs` ya estabilizados: `CompaniesPage`, `CategoriesCatalogPage`, `CatalogSimplePage`, company-management, security, access.

Mismo nivel de conformidad (clase **A** en `PAGE-AUDIT.md`).

### PlatformCrudTemplate (default platform)

Todas las rutas `/platform/*` y hub menú/planes. `hideHeader` evita duplicar título con topbar.

### ReportPageTemplate

`SalesReportPage` — puede migrarse a `ErpPageTemplate` en el futuro; hoy es excepción de dominio documentada.

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
| `pg-*` | Transversal tenant + platform (utilidades) |
| `zh-*` | ZH Form / UI global |
| `acc-*` | Contabilidad |
| `vf-*` | Ventas facturas |
| `cn-*` | Notas de crédito |
| `prf-*` | Perfiles / RBAC modal |
| `sri-*` | Config SRI |
| `cat-*` | Estructura catálogo |
| `cls-*` | Clientes (paneles) |
| `br-*` | Sucursales |
| `bill-*` | Facturación |
| `cf-*` | Compras facturas |
| `gst-*` | Gastos |
| `crt-*` | Transportistas |
| `adj-*` | Ajustes inventario |
| `bod-*` | Bodegas |
| `dsh-*` | Dashboard |
| `sap-*` | Platform planes / menu builder |
| `rpt-*` | Reportes |
| `smp-*` | Menu Preview simulado (**platform only**) |
| `smb-*` | Menu Builder CRM (**platform only**) |

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
| 640px / 480px | Auth, SA panel, filtros reportes | Utilidades puntuales |

**Tablas anchas:** contenedor `pg-overflow-x`.  
**Modales:** `pg-modal--sm|md|lg|440|480`.

---

## 8. Governance rules

### Inline styles

| Ámbito | Regla ESLint |
|--------|----------------|
| `src/pages/**` | `error` — sin `style={{}}` estructural |
| `src/modules/**/pages/**` | `error` — idem |
| `src/modules/auth/pages/**` | `off` — clase C |
| `src/templates/**`, `components/layout/**` | `error` |

**Excepciones válidas:** props `style` en `Modal` / `ZHConfirmModal`; indent dinámico en `AccountTreeSelect`; `MenuPreview` indent por profundidad; hover runtime en simulador (no JSX estático).

### Inventario vivo

- Convenciones operativas: [`frontend-layout-conventions.md`](./frontend-layout-conventions.md)
- Páginas por clase A/B/C: [`../frontend/src/templates/PAGE-AUDIT.md`](../frontend/src/templates/PAGE-AUDIT.md)
- Mantenibilidad / max-lines: [`FRONTEND_MAINTAINABILITY.md`](./FRONTEND_MAINTAINABILITY.md)
- QA manual: [`FRONTEND_QA_CHECKLIST.md`](./FRONTEND_QA_CHECKLIST.md)

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
| `LayoutFrame` solo en shells | Un solo lugar para padding/max-width/scroll |
| `ErpPageTemplate` como default tenant | Título y `.pg-page` consistentes |
| `PageShell` directo permitido en catálogos tabs | Patrón ya probado; evitar reescritura masiva |
| `legacy-pages.css` global | Utilidades `pg-*` compartidas sin import por página |
| Prefijos por dominio | Evitar CSS monolítico y colisiones |
| Platform CSS aislado (`smp-*`, `smb-*`) | Menu builder no contamina tenant |
| ESLint `style` en pages | Prevenir regresión visual post-migración |
| Sin UUID tenant en URL | `sessionStorage` `erp.saas.*` (regla producto) |
| max-lines 400 en `modules/**/pages/**` | Warning — deuda acotada documentada, no bloqueante QA |

---

## 10. Reglas para nuevas pantallas

1. Elegir shell según contexto (tenant vs platform vs auth).
2. Tenant producto → `ErpPageTemplate` salvo que replique patrón `CompaniesPage` (tabs + `PageShell`).
3. Platform → `PlatformCrudTemplate`; no repetir título del topbar.
4. Crear `*-page.css` con prefijo de dominio si hay estilos no cubiertos por `pg-*` / `zh-ui`.
5. Tablas → `pg-overflow-x`; estados vacío/carga → `pg-pad-40`.
6. Añadir ruta a `PAGE-AUDIT.md` y claves i18n en `es` / `en` / `qu`.
7. Ejecutar `npm run lint` y `npm run build` antes del PR.

---

## 11. Checklist para contributors (PR UI)

- [ ] ¿Usa shell correcto sin `LayoutFrame` en la página?
- [ ] ¿Un solo H1 visible (shell o plantilla, no duplicado)?
- [ ] ¿Sin `style={{}}` estructural en `pages/`?
- [ ] ¿Tokens / `pg-*` en lugar de valores mágicos?
- [ ] ¿Tablas con overflow horizontal?
- [ ] ¿i18n en tres locales si hay texto visible?
- [ ] ¿`PAGE-AUDIT.md` actualizado si es pantalla nueva?
- [ ] ¿Prefijo CSS de dominio si aplica?
- [ ] ¿Sin GUID de tenant en query string?

---

## 12. Checklist para agentes IA

1. Leer este documento + `frontend-layout-conventions.md` + `PAGE-AUDIT.md`.
2. No proponer shells, routers ni stores nuevos sin petición explícita.
3. Migraciones visuales: solo CSS/clases; no cambiar handlers, APIs ni hooks de negocio.
4. No importar `smp-*` / `smb-*` fuera de platform.
5. Preferir `ErpPageTemplate` en pantallas tenant nuevas.
6. Buscar utilidad existente: `grep` en `legacy-pages.css` y `page-template.css` antes de CSS nuevo.
7. Tras cambios: `npm run build` + `npm run lint` (0 errores).
8. Actualizar inventario en `PAGE-AUDIT.md` si cambia conformidad visual.

---

## Referencias cruzadas

| Documento | Propósito |
|-----------|-----------|
| [`CLAUDE.md`](../CLAUDE.md) | Reglas implementación |
| [`FRONTEND_RULES.md`](../FRONTEND_RULES.md) | Entrada corta frontend |
| [`frontend-layout-conventions.md`](./frontend-layout-conventions.md) | Convenciones layout/CSS |
| [`FRONTEND_MAINTAINABILITY.md`](./FRONTEND_MAINTAINABILITY.md) | max-lines y splits post-QA |
| [`FRONTEND_QA_CHECKLIST.md`](./FRONTEND_QA_CHECKLIST.md) | Validación manual pre-release |
