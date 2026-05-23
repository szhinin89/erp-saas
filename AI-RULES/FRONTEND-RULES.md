# Frontend — reglas de implementación

Canónico React/TypeScript. Baseline UI sellada: `docs/FRONTEND_ARCHITECTURE_BASELINE.md`. Catálogo PR F-xx: [PR-RULES-CATALOG.md](./PR-RULES-CATALOG.md).

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

```typescript
import { isJwtPlatformOperatorRole } from '../constants/platformAuth';

const isAdmin = role === 'Admin' || isJwtPlatformOperatorRole(role);
const canView   = isAdmin || hasPerm('modulo.recurso.view');
const canCreate = isAdmin || hasPerm('modulo.recurso.create');
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

### Banner operador platform (impersonación)

Operador platform con `tenantId` real: banner plegable compacto; detalle empresa y "Volver al panel global" solo expandido.

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

**Alcance:** `frontend/src/pages/**`, `navConfig.ts`, `AppLayout`, `app.nav.*`, `*.tabList`.

### Checklist `.zh-form-tabs` + `PageShell` + `TableCard`

1. Listado: `{módulo}.tabList` en es/en/qu; no `app.nav.*` en esa pestaña.
2. `PageShell` **`action`**: solo en pestaña datos (`tab === 'data'`, etc.).
3. `ZHDirtyBar`: misma condición; en altas `saveLabel` "Crear".
4. Tras cambio `location.pathname`, cerrar drawer en `AppLayout`.
5. Listado: `ZHSearchBar`; botón nueva fila alineado a productos/sucursales.
6. No dejar solo "Cancelar" sin primario visible en pie.

### Menú lateral

Nombre módulo **sin** prefijo "Ver"/"View".

### Botones y cabecera

Verbos de dominio en altas; **«Guardar cambios»** en edición (`common.saveChanges`).

---

## Menú estático

**Alcance:** `frontend/src/nav/**` — `navConfig.ts`.

- Cada `to` **máximo una vez** entre grupos estáticos (excepción: Favoritos en `localStorage`).
- Rutas `/platform/*` y redirect legacy `/superadmin/*` (solo en `platformRoutes.tsx`); extras de nav en `getPlatformPanelNavExtras` / `buildGlobalPlatformNavGroups`; **no** en BD `ui_nav_items`.
- Al añadir ruta: alias en `MENU_ROUTE_ALIASES` si hay variante legacy.

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
