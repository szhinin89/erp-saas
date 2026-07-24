# Frontend — reglas de implementación

Canónico React 19 + TypeScript + Vite. Baseline arquitectónica: [`docs/FRONTEND_ARCHITECTURE_BASELINE.md`](../docs/FRONTEND_ARCHITECTURE_BASELINE.md). Convenciones de layout/CSS: [`docs/frontend-layout-conventions.md`](../docs/frontend-layout-conventions.md). Catálogo PR F-xx: [PR-RULES-CATALOG.md](./PR-RULES-CATALOG.md).

---

## Application Shell (obligatorio)

Toda pantalla ERP vive dentro del shell único:

```
AppLayout
    │
    LayoutFrame
        │
        ├── ZHAppTenantHeader
        │       ├── ZHHeaderCompanyIdentity  (identidad suscriptor + CompanySwitcher)
        │       ├── ZHAppLauncher            (navegación principal: panel Módulos/Favoritos)
        │       ├── ZHHeaderActionButton×2   (search/notifications, "próximamente")
        │       ├── LanguageSwitcher
        │       └── ZHHeaderUserMenu
        │
        ├── React Router <Outlet />
        │
        └── ERP Modules              (modules/{dominio}/pages/**)
```

- **`AppLayout`** (`components/AppLayout.tsx`): obtiene el menú de sesión (`useAppLayoutNavigation`), resuelve logout y monta `LayoutFrame`.
- **`LayoutFrame`** (`components/layout/LayoutFrame.tsx`): frame de contenido compartido — padding, max-width y scroll (`--shell-content-*`). No contiene navegación.
- **`ZHAppTenantHeader`** (`components/zh/ZHAppTenantHeader.tsx`): cabecera fija, orquesta los subcomponentes de `components/zh/header/`: identidad de empresa, App Launcher, selector de idioma (`LanguageSwitcher`), acciones globales y menú de usuario.
- **`ZHAppLauncher`** (`components/zh/header/ZHAppLauncher.tsx`): reemplaza la barra horizontal de módulos; botón "apps" que abre un panel con tabs **Módulos** (todos los grupos de `useAppLayoutNavigation` + `nav/navConfig.ts`) y **Favoritos** (`localStorage` `zh-favorites`), reutilizando `MainMenuList`/`navSubtreeMatchesPath`/`NavIcon` de `components/AppLayoutMainMenu.tsx`.
- **Páginas de módulo** (`modules/{dominio}/pages/**`): **no** montan `LayoutFrame` ni navegación; solo renderizan contenido dentro del `<Outlet />`.

---

## Estructura de módulo (obligatoria)

```
frontend/src/modules/{dominio}/
├── api/          ← service.ts (HTTP)
├── schemas/      ← Zod
├── hooks/        ← useAsync + estado
└── pages/        ← página + CSS único (prefijo propio)
```

- Organizar por módulos; `shared/` solo para reutilizables con ownership claro.
- Lógica de negocio en hooks/servicios, no en JSX complejo.
- API desde capa `api/` del módulo, no inline en componentes.

---

## Permisos en UI (conveniencia — autorización real en backend)

Navegación 100% server-driven (`GET /api/v1/me/menu`). Único gate de página por rol permitido: `isAdminRole()` de `access/permissionUi.ts`. **Prohibido** `role === 'Admin'` ad-hoc o nuevas comparaciones de string. Contrato completo: [SECURITY.md#security--access-contract-v1--locked](./SECURITY.md#security--access-contract-v1--locked).

```typescript
import { isAdminRole } from '../../../access/permissionUi';

const isAdminUser = isAdminRole(user?.role);
if (!isAdminUser) return <NoAccessPage title={t('security.title')} />;
```

---

## ZH Form System

**Fuente visual:** `frontend/src/styles/design-tokens.css`, `zh-ui.css`, `page-template.css`. Componentes: `frontend/src/components/zh/ZHForm.tsx`.

### Reglas

- **No** formularios CSS ad-hoc (`.companies-form`, `.primary-btn`).
- **No** hardcodear colores; usar tokens (`--color-primary`, `--space-*`, etc.). **No** variables legacy `--zh-blue*`.
- **Sí** `PageShell`, `TableCard`, `Modal` como contenedores.

### Componentes mínimos

`ZHFormHeader`, `ZHMultiTenantHeader`, `ZHFormBody`, `ZHFormSection`, `ZHGrid`, `ZHField`, `ZHFormAlert`, `ZHFormActions`, `ZHToggle`, `ZHBtn`.

### Patrones

- Formulario completo (modal/wizard): `ZHFormHeader` → `ZHFormBody` → `ZHFormActions`.
- Catálogo (`PageShell` + `TableCard` + `.zh-form-tabs`): **no** duplicar `ZHFormHeader` si `PageShell` basta.
- Errores: `ZHFormAlert` / `hintType`.
- Patrón visual nuevo: extender `ZHForm.tsx` + `ZHForm.css` una vez.

### Formularios obligatorios

```tsx
import { ZHBtn, ZHField } from '../../../components/zh/ZHForm';

<ZHField label="RUC" required error={errors.ruc?.message}>
  <input className="zh-input" {...register('ruc')} />
</ZHField>
<ZHBtn variant="primary" size="md" type="submit">Guardar</ZHBtn>
```

---

## Design System — estándares únicos obligatorios

Para evitar fragmentación de UI, los siguientes patrones son **los únicos
permitidos** (componentes en `frontend/src/components/zh/ZHForm.tsx` salvo
indicación contraria). Cualquier alternativa equivalente queda **deprecada**
y debe migrarse al tocar el archivo.

| Necesidad | Estándar único | Deprecado |
|-----------|-----------------|-----------|
| Botón | `ZHBtn` (`variant` opcional, default `'secondary'`; `size?`) | `<button className="zh-btn ...">` crudo — excepción única: `<Link>` de navegación estilizado como botón (`ZHBtn` solo renderiza `<button>`) |
| Campo de formulario | `ZHField` (`label?`, `required?`, `fieldError`/`error`, `hint`/`hintType`, `density?: 'default'\|'compact'`) | `.pf-field`/`.pf-field__*`/`.pf-label` (eliminado, ADR Fase 3B) — usar `density="compact"` para grillas densas de líneas, nunca un segundo sistema |
| Badge / etiqueta de estado corta | `Badge` (`components/PageShell.tsx`; `label`, `variant: 'green'\|'gray'\|'red'\|'blue'\|'orange'`, `size?: 'md'`, `upper?`, resto de atributos de `<span>`) | `<span className="badge badge--...">` crudo |
| Grid de campos en formularios | `<ZHGrid cols={1\|2\|3}>` | `.pg-form-grid`, `.pg-form-grid--N` |
| Checkbox / switch (incluye tablas y matrices) | `ZHToggle` (`label`, `description`, `value`, `onChange`, `disabled?`) | `.zh-inline-check`, `.companies-checkbox-label`, `.toggle`/`.toggle-ui`, `.md-page-check` |
| Tamaño de íconos `material-symbols-outlined` | `.zh-icon-sm` (14px) / `.zh-icon-md` (16px) / `.zh-icon-lg` (18px) / `.zh-icon-xl` (32px) | `style={{ fontSize: N }}`, `.prd-icon-sm` |
| Header/footer de modal (`.zh-modal-overlay > .zh-modal`) | `ZHModalHeader` + `ZHFormActions` | headers/footers ad-hoc por archivo, `.md-modal*` |
| Tabs de formulario/catálogo | `.prd-tabs` / `.prd-tab-btn` / `.prd-tab-btn--active` (namespace compartido en `items-catalog.css`) | `.zh-form-tabs` |
| Tabla genérica | `.table` + `.prd-table-wrap` | `.md-table`, `.md-table-wrap` |
| Bloque de "actividad reciente" | `.prd-activity__*` (`items-catalog.css`) | `.bod-activity__*` y equivalentes duplicados por módulo |

Nota: `pf-badge`, `prd-status-badge`, `pg-kpi-badge`, `md-badge` **no** están deprecados — son variantes con semántica propia (status dot de 2 estados, tendencia de KPI) no consolidadas todavía en `Badge`. No copiar su patrón para casos nuevos que sí encajen en `Badge` (etiqueta simple con color semántico).

### Reutilización obligatoria — auditoría previa a crear UI

Antes de escribir código de UI para cualquier pantalla, formulario, modal, wizard,
dashboard o componente nuevo, es **obligatorio** seguir este orden y dejar constancia
explícita de haberlo hecho (ver "Regla para IA" abajo):

1. **Revisar la infraestructura existente** — ¿ya existe un componente oficial que
   resuelve el caso? (tabla de arriba + `ZHModal`, `ZHDrawer`, `ZHCard`, `PageShell`,
   `ErpPageTemplate`, `ReportPageTemplate`, `EmptyState`, `LoadingState`, `ErrorState`,
   `TableCard`). Si existe, se reutiliza — no se reimplementa.
2. **Revisar pantallas similares** de otros módulos (Ventas, Compras, Inventario,
   Master Data, Contabilidad, Caja, Logística) — un problema equivalente debe resolverse
   con la misma estructura visual, no una nueva.
3. **Extender antes que duplicar**: si el componente existente cubre ~90-95% del caso,
   se le agrega una prop/variante (ej. `ZHField` → `density="compact"`). **Prohibido**
   crear un componente paralelo (`CompactField`) para evitar tocar el oficial.
4. **Crear un componente nuevo** solo si: (a) representa un patrón realmente nuevo,
   (b) es reutilizable en varios módulos, y (c) no puede resolverse extendiendo uno
   existente. Requiere justificación explícita de las tres condiciones.
5. **Patrón de un solo módulo** (ej. `PurchasesInvoiceTotals`, `InventoryMovementTimeline`,
   `AccountingVoucherSummary`) permanece dentro de `modules/{dominio}/` — no entra al
   Design System aunque esté bien construido.

**Orden de prioridad obligatorio** (nunca al revés): Design System → Templates
oficiales (`ErpPageTemplate`/`ReportPageTemplate`) → Componentes compartidos →
Extender un componente existente → Crear componente nuevo (justificado) → CSS
específico del módulo.

**Prohibido**: botones/campos/modales/badges alternativos a los de la tabla; copiar
JSX entre pantallas en vez de reutilizar el componente; duplicar layouts existentes;
introducir estilos nuevos cuando ya existe un patrón equivalente.

**Regla para IA (obligatoria)**: antes de escribir código de UI, indicar explícitamente
en la respuesta — (1) qué plantillas oficiales se revisaron, (2) qué componentes
existentes se reutilizarán, (3) qué se extenderá y cómo (si aplica), (4) justificación
técnica de cualquier componente nuevo, (5) confirmación de que no existe un componente
equivalente. Sin esta auditoría, la implementación se considera incompleta.

### Enforcement (F-04)

`npm run architecture:design-system` (`tools/architecture/check-design-system.mjs`,
incluido en `npm run architecture:check` / CI) detecta los patrones deprecados
de la tabla anterior en `frontend/src/modules/**`, `frontend/src/pages/**`,
`frontend/src/templates/**` y `frontend/src/components/**` (incluye `F-04-btn`,
`F-04-badge` y `F-04-pf-field` para los estándares de botón/badge/campo agregados
en la Fase 3B), además de `F-04-color` (hex fuera de `design-tokens.css`) y
`F-04-token` (`var(--x)` no definido en `design-tokens.css`) sobre todo
`frontend/src/**/*.css`. Código legacy permitido temporalmente vía
`tools/architecture/architecture-grandfather.json#designSystemGrandfathered`
(`{file, rules}`); archivos/reglas no listados allí bloquean el PR. Detalle de
reglas: [`AI-RULES/PR-RULES-CATALOG.md#f-04--design-system-único-ui`](PR-RULES-CATALOG.md#f-04--design-system-único-ui).

### Alineación de datos numéricos (F-05 / NUM-001)

Todo dato numérico (montos, cantidades, porcentajes, stock, impuestos, totales, secuenciales, valores calculados) se alinea a la **derecha** en inputs, tablas, cards, KPIs, labels, dashboards, reportes y cualquier componente reutilizable — nunca centrado ni a la izquierda, salvo excepción documentada y aprobada por arquitectura. Si un componente base no lo soporta, se corrige el componente, nunca una excepción local. Detalle completo, ejemplos y excepciones: [`AI-RULES/PR-RULES-CATALOG.md#f-05--alineación-de-datos-numéricos-num-001`](PR-RULES-CATALOG.md#f-05--alineación-de-datos-numéricos-num-001).

### Excepción: barra de guardado de página completa

`.pg-actions-bar` (con `.pg-actions-info` + `.pg-actions-buttons`) se
**mantiene** para barras de guardado a nivel de página completa que incluyen
texto informativo (ej. `BillingSettingsPage`, `CompanyProfileSettingsSection`,
`SriConfigurationSection`). `ZHFormActions` no tiene slot para ese texto y es
exclusivo de footers de modal/formulario contenido.

---

## Formularios de entidad (`zh-form-tabs`)

**Alcance:** catálogos con ficha + listado en `.zh-form-tabs`.

### Tab por defecto

Estado inicial: `'data'` o `'general'` (productos) — **no** `'list'` salvo excepción documentada.

### Orden fijo (dos pestañas)

1. **Datos** — `common.formTab.data`; formulario visible al entrar; dentro del mismo `TableCard`; sin `ZHFormHeader` si `PageShell` basta.
2. **Listado** — i18n **`{módulo}.tabList`** (no `app.nav.*`).

Orden tablist/DOM: Datos → extras → listado.

### PageShell y listado

Barra crear/guardar **solo** en pestaña datos; listado sin acciones globales de alta.

### Más de dos pestañas

Confirmar orden con usuario. Referencia: `ProductsPage` (Datos → Imágenes → `products.tabList`).

---

## Copy UX

**Alcance:** `frontend/src/modules/**/pages/**`, `navConfig.ts`, `AppLayout`, `app.nav.*`, `*.tabList`.

### Checklist `.zh-form-tabs` + `PageShell` + `TableCard`

1. Listado: `{módulo}.tabList` en es/en/qu; no `app.nav.*` en esa pestaña.
2. `PageShell` **`action`**: solo en pestaña datos (`tab === 'data'`, etc.).
3. `ZHDirtyBar`: misma condición; en altas `saveLabel` "Crear".
4. Tras cambio `location.pathname`, cerrar el panel abierto del `ZHAppLauncher`.
5. Listado: `ZHSearchBar`; botón nueva fila alineado a productos/sucursales.
6. No dejar solo "Cancelar" sin primario visible en pie.

### Menú lateral

Nombre módulo **sin** prefijo "Ver"/"View".

### Botones y cabecera

Verbos de dominio en altas; **«Guardar cambios»** en edición (`common.saveChanges`).

---

## Menú principal (ZHAppLauncher)

**Alcance:** `frontend/src/nav/navConfig.ts`, `components/useAppLayoutNavigation.ts`, `components/zh/header/ZHAppLauncher.tsx`, `components/AppLayoutMainMenu.tsx`.

- El menú se construye en backend (`GetSessionMenuQuery` → `GET /api/me/menu`) y se mapea a `NavGroup[]` con `mapSessionMenuToNavGroups`.
- Cada `to` **máximo una vez** entre grupos (excepción: Favoritos en `localStorage`, clave `zh-favorites`).
- Alias de ruta legacy → ruta canónica: `MENU_ROUTE_ALIASES` en `navConfig.ts` (ej. `/logistica/bodegas` → `/inventory/warehouses`).

---

## CSS — jerarquía de 3 niveles

```
design-tokens.css    → variables
zh-ui.css            → componentes globales (.table, .badge, .zh-btn…)
page-template.css    → layout (.pg-page, .pg-kpi…)
{pagina}-page.css    → SOLO clases únicas de esa pantalla
```

Antes de CSS local: verificar si existe en `zh-ui.css` o `page-template.css`.

Prefijos por página: ver [NAMING.md](./NAMING.md#prefijos-css-por-página).

Clases frecuentes (no recrear): `.pg-page`, `.table`, `.badge`, `.zh-status`, `.zh-btn`, `.zh-form-tabs`, `.zh-input`, `.pg-kpi-icon--*`.

---

## Diálogos

**Prohibido** `window.prompt`, `window.confirm`, `window.alert` nativos. Usar modales ZH estándar.

---

## i18n — Kichwa de Cañar

- Locale **`qu`**, archivo **`qu.json`**.
- Claves nuevas: **siempre** `es.json`, `en.json`, `qu.json`.
- Contenido `qu`: **Kichwa de Cañar, Ecuador** — no quechua genérico.
- Prohibido texto duro visible al usuario.

---

## Auth refresh (frontend)

Solo `authRefreshManager` (Web Locks + BroadcastChannel). Detalle tokens: [SECURITY.md](./SECURITY.md).

---

## Validación formularios

Zod + `zodResolver` + react-hook-form. Schema en `schemas/{modulo}/`. Ver [ENFORCEMENT.md](./ENFORCEMENT.md).

---

## CI frontend

`npm run lint`, `npx tsc --noEmit`, `npm run build`, Playwright smoke (`.github/workflows/frontend-ci.yml`).
