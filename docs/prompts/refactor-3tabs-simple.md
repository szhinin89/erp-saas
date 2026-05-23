# Prompt: Convertir pantalla ERP a flujo 3-tabs

## Uso

Pegar este prompt y escribir al final únicamente la ruta de la pantalla:

```
Aplica este prompt a: inventory/brands
```

---

## Lo que hace Claude antes de escribir código

1. Leer el archivo de la página indicada
2. Extraer automáticamente del código existente:
   - Tipo DTO/entidad (`BrandItem`, `WarehouseDto`, etc.)
   - Prefijo CSS del módulo (desde `css-prefixes.json` según el directorio)
   - Permisos `canShow()` ya usados
   - Keys `t()` ya existentes (no duplicar)
   - Campos del formulario (del `useForm` o `register`)
   - Funciones de API (`catalogService.brands`, `bodegaService.list`, etc.)
   - Si tiene hook separado (`useBodegasPage`) o todo inline

Solo después escribe código.

---

## Resultado esperado

La pantalla pasa de su estructura actual a **3 tabs** con el mismo estilo visual
que `inventory/products` y `inventory/warehouses`:

| Tab | Contenido |
|-----|-----------|
| **📊 Resumen** (default) | KPIs en grid 3 col + 4 quick-stats + feed actividad reciente |
| **📋 Listado** | Buscador prominente + tabla + highlight + modal confirm toggle |
| **➕ Nuevo / Editar** | Formulario inline (reemplaza el modal actual) |

---

## REGLAS — Claude debe seguirlas sin excepción

### 1 — i18n primero, código después

Añadir **todas** las claves nuevas a `es.json`, `en.json` y `qu.json`
**antes** de escribir cualquier componente.

Nunca dejar texto visible hardcodeado. Todo usa `t('clave', 'fallback')`.
Los mensajes de error de Zod que ya son texto plano español se muestran tal cual
(no necesitan `t()`). Los que son keys i18n deben resolverse con `t(error)`.

Namespace obligatorio de claves nuevas:
```
{modulo}.tabs.*       {modulo}.kpi.*        {modulo}.qs.*
{modulo}.activity.*   {modulo}.search.*     {modulo}.table.*
{modulo}.toggle.*     {modulo}.form.*
{modulo}.created.success    {modulo}.updated.success
```

### 2 — CSS prefix estricto

El prefijo del módulo viene de
`tools/architecture/config/css-prefixes.json`.

**Regla:** en el `.css` del módulo solo se usan clases con ese prefijo.
Las clases `prd-*` de `frontend/src/pages/ProductsPage.css` son reutilizables;
se importan en la página principal, no se reescriben:

```tsx
// En la página principal (ej: BrandsPage.tsx)
import '../../../../pages/ProductsPage.css'; // prd-* shared styles
import './CatalogsPage.css';                 // prefijo propio del módulo
```

Clases `prd-*` ya disponibles (no crear en el módulo):
- Tabs: `prd-tabs`, `prd-tab-btn`, `prd-tab-btn--active`, `prd-tab-icon`,
  `prd-tab-edit-badge`, `prd-tab-content`, `prd-fadein`
- Dashboard: `prd-quick-stats`, `prd-qs` + variantes, `prd-activity` + subclases
- Listado: `prd-search-box`, `prd-search-input`, `prd-search-icon`,
  `prd-search-clear`, `prd-search-meta`, `prd-search-shortcut`,
  `prd-highlight`, `prd-empty-search` + subclases,
  `prd-table-wrap`, `prd-row--inactive`, `prd-status-dot` + variantes,
  `prd-actions-cell`, `prd-btn-mute`, `prd-btn-activate`
- Modal: `prd-modal-backdrop`, `prd-modal`, `prd-modal__*`, `prd-btn--danger`
- Toast: `prd-toast`, `prd-toast--success/error/info`, `prd-toast__*`
- Spinner/success: `prd-spinner`, `prd-btn--success`

### 3 — No tocar contratos de datos

Estos archivos **no se modifican**:
- `*/api/*Service.ts`
- `*/schemas/*Schema.ts`
- `*/hooks/use*.ts` o `*/pages/use*Page.ts` (si existe)

### 4 — Permisos

Siempre `canShow('permiso.key')` de `usePermissionsUi`.
Si `!canView` → `<NoAccessPage title={t(...)} />`.

### 5 — Sin `window.confirm/alert/prompt`

Usar el modal de confirmación con clases `prd-modal-*`. Ver patrón en
`frontend/src/modules/inventario/warehouses/components/WarehouseConfirmModal.tsx`.

### 6 — Detección de guardado exitoso

Si el hook usa `handleSubmit` internamente y no devuelve `boolean`,
detectar éxito con `useRef` (sin modificar el hook):

```tsx
const prevSavingRef = useRef(false);
useEffect(() => {
  const wasSaving = prevSavingRef.current;
  prevSavingRef.current = page.saving;
  if (wasSaving && !page.saving && !page.saveError && activeTab === 'nuevo') {
    addActivity(nombre, editando ? 'updated' : 'created');
    showToast(t(editando ? 'x.updated.success' : 'x.created.success'), 'success');
    cancelEdit();
  }
});
```

Si la lógica es inline en el componente (sin hook separado), mover el `try/catch`
del submit para capturar éxito directamente y llamar al store.

### 7 — Ctrl+K enfoca el buscador

```tsx
useEffect(() => {
  const h = (e: KeyboardEvent) => {
    if ((e.ctrlKey || e.metaKey) && e.key === 'k') {
      e.preventDefault();
      setActiveTab('listado');
      setTimeout(() => searchRef.current?.focus(), 80);
    }
  };
  window.addEventListener('keydown', h);
  return () => window.removeEventListener('keydown', h);
}, [setActiveTab]);
```

---

## Archivos a producir (en orden)

```
1. es.json + en.json + qu.json   ← PRIMERO siempre
2. {modulo}UiStore.ts            ← Zustand: tab, editingItem, toast, activity
3. {Modulo}ToastManager.tsx      ← auto-dismiss 3s, clases prd-toast-*
4. {Modulo}ConfirmModal.tsx      ← clases prd-modal-* (copia del patrón)
5. {Modulo}ResumenTab.tsx        ← KPIs + quick-stats + actividad
6. {Modulo}ListadoTab.tsx        ← búsqueda + tabla + highlight + confirm
7. {Modulo}FormTab.tsx           ← formulario inline con ZHField/ZHBtn
8. {Modulo}Page.tsx              ← reescribir: 3 tabs + imports CSS
9. {Modulo}Page.css              ← solo clases {prefix}-* propias del módulo
```

---

## Store mínimo (estructura fija)

```typescript
import { create } from 'zustand';

export type TabId = 'resumen' | 'listado' | 'nuevo';

export const use{Modulo}UiStore = create<{
  activeTab: TabId;
  editingItem: {Dto} | null;
  toast: { id: string; message: string; type: 'success'|'error'|'info' } | null;
  recentActivity: { id: string; itemName: string; action: string; timestamp: Date }[];
  setActiveTab: (t: TabId) => void;
  startEdit: (item: {Dto}) => void;
  cancelEdit: () => void;
  showToast: (msg: string, type: 'success'|'error'|'info') => void;
  dismissToast: () => void;
  addActivity: (name: string, action: 'created'|'updated'|'disabled'|'enabled') => void;
}>((set) => ({
  activeTab: 'resumen', editingItem: null, toast: null, recentActivity: [],
  setActiveTab: (tab) => set({ activeTab: tab }),
  startEdit:    (item) => set({ editingItem: item, activeTab: 'nuevo' }),
  cancelEdit:   () => set({ editingItem: null, activeTab: 'listado' }),
  showToast:    (message, type) => set({ toast: { id: `${Date.now()}`, message, type } }),
  dismissToast: () => set({ toast: null }),
  addActivity:  (itemName, action) => set((s) => ({
    recentActivity: [
      { id: `${Date.now()}`, itemName, action, timestamp: new Date() },
      ...s.recentActivity.slice(0, 9),
    ],
  })),
}));
```

---

## Checklist antes de terminar

```bash
# TypeScript: 0 errores
cd frontend && node_modules/.bin/tsc --noEmit --project tsconfig.app.json

# Arquitectura: 11/11 PASS
node tools/architecture/run-all.mjs

# i18n: nuevas keys en los 3 locales
node tools/check-i18n-keys.cjs

# Sin texto hardcodeado fuera de t()
grep -rn '"[A-ZÁÉÍÓÚ][a-záéíóú ]\{4,\}"' {dir}/components/ {dir}/pages/{Modulo}Page.tsx \
  | grep -v 't(\|//\|aria-\|placeholder\|title='
```

---

## Anti-patrones prohibidos

| ❌ Prohibido | ✅ Corrección |
|-------------|-------------|
| Escribir componentes antes de las keys i18n | Keys en es/en/qu primero |
| `<span>Texto visible</span>` directo | `<span>{t('key','fallback')}</span>` |
| Clases `prd-*` en el `.css` del módulo | Solo importar `ProductsPage.css` |
| Clases del módulo sin prefijo propio | `{prefix}-nombre-clase` siempre |
| `window.confirm` / `window.alert` | `{Modulo}ConfirmModal` con clases `prd-modal-*` |
| Colores `#hex` o `rgb()` hardcodeados | Solo `var(--color-*)` del design system |
| Modificar `*Service.ts` o `*Schema.ts` | Esos archivos son intocables |
| Omitir `import ProductsPage.css` | La UI aparece rota (clases `prd-*` sin efecto) |
| Importar service de otro módulo | Cada módulo usa solo su propio service |

---

Aplica este prompt a: **`{RUTA}`**
