# Frontend — reglas de implementación

Canónico React 19 + TypeScript + Vite. Baseline arquitectónica: [`docs/FRONTEND_ARCHITECTURE_BASELINE.md`](../FRONTEND_ARCHITECTURE_BASELINE.md). Convenciones de layout/CSS: [`docs/frontend-layout-conventions.md`](../frontend-layout-conventions.md). Catálogo PR F-xx: [PR-RULES-CATALOG.md](./pr-rules-catalog.md).

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

Navegación 100% server-driven (`GET /api/v1/me/menu`). Único gate de página por rol permitido: `isAdminRole()` de `access/permissionUi.ts`. **Prohibido** `role === 'Admin'` ad-hoc o nuevas comparaciones de string. Contrato completo: [SECURITY.md#security--access-contract-v1--locked](./security.md#security--access-contract-v1--locked).

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
| Header/footer de modal (`.zh-modal-overlay > .zh-modal`) | `ZHModal` (props `title`/`subtitle` para header, `footer` para acciones — ver [modal-standard.md](./modal-standard.md)) | headers/footers ad-hoc por archivo, `.md-modal*`, `ZHModalHeader` (**eliminado** — prohibido importarlo, ver modal-standard.md) |
| Tabs de formulario/catálogo | `.prd-tabs` / `.prd-tab-btn` / `.prd-tab-btn--active` (namespace compartido en `items-catalog.css`) | `.zh-form-tabs` |
| Listado tabular de registros (administrativo, transaccional, de detalle o dentro de un formulario) | `ZHDataTable` (`components/zh/ZHDataTable.tsx`; columnas tipadas, `rowKey`, `loading`, `emptyMessage`, paginación opcional, `showRowNumber`/`rowNumberOffset` para columna "N°" auxiliar (ZH-DATATABLE-ROW-NUMBER-01), `rowClassName` por fila, `tableClassName`/`cellClassName` para variantes ya documentadas de `zh-ui.css` (`table--compact`, `table--neutral`, `table--align-top`, `table--matrix`, `table--sticky-column`, `zh-table-cell--num`) — usa internamente `.table`/`.table-scroll`) — ZH-LISTING-STANDARD-01, ampliado a todo el frontend por ZH-LISTING-GLOBAL-STANDARD-06 | `<table className="table">` armada a mano por página; `.md-table`, `.md-table-wrap`, `.prd-table-wrap` (eliminada 15A). Excepciones documentadas: fila de alta inline sin entidad real, tabla editable compleja por celda/fila (edición inline con RHF, `useFieldArray`), preview técnico calculado en vivo dentro de un formulario activo, matriz dinámica con columnas data-driven, reporte composable con columnas libres por consumidor (`ReportPageTemplate`), o necesidad de `<tfoot>`/comportamiento que `ZHDataTable` no expone (ej. `JournalEntryDetailPage`) — documentar el motivo puntual en el código; no ampliar `ZHDataTable` para cubrirlos sin evaluar impacto en sus consumidores existentes |
| Bloque de "actividad reciente" | `.prd-activity__*` (`items-catalog.css`) | `.bod-activity__*` y equivalentes duplicados por módulo |
| Input de texto simple | `ZhTextInput` (`components/zh/inputs/`) | `<input type="text">` crudo |
| Input numérico entero | `ZhNumberInput` (`components/zh/inputs/`) | `<input type="number">` crudo (además prohibido por el Estándar de Precisión Numérica) |
| Input decimal (montos/cantidades/precios) | `ZhDecimalInput` (`components/zh/inputs/`) | `<input type="number">`/`type="text"` con parseo manual |
| Input de fecha | `ZhDateInput` (`components/zh/inputs/`) | `<input type="date">` crudo |
| Input de teléfono | `ZhPhoneInput` (`components/zh/inputs/`) | `<input type="tel">` crudo |
| Select simple | `ZhSelect` (`components/zh/inputs/`) | `<select>` crudo |
| Textarea de notas/descripciones | `ZhTextarea` (`components/zh/inputs/`) | `<textarea>` crudo |
| Búsqueda/selección en catálogos grandes (single o multiple con chips) | `ZhSearchSelect<T>` (`components/zh/inputs/`; datasource local `options` o remoto `loadOptions` con debounce + `AbortSignal`, `maxResults` default 30, teclado, clear) — sin dominio; wrappers por dominio encima, p. ej. `SupplierSearchSelect` (`modules/masterData/components/`, buscador ÚNICO de proveedores registrados: BP con rol Proveedor por nombre/nombre comercial/RUC, `value` por id o fila, `activeOnly`; su única lógica remota vive en `masterData/utils/supplierSearch.ts`) — ZH-SUPPLIER-SEARCH-REUSABLE-01 / SINGLE-SOURCE-02 | `<select>` / `ZhSelect` con cientos o miles de `<option>`; buscadores por módulo que dupliquen debounce/teclado/chips; `SupplierPicker` y selectores de proveedor propios por módulo (**eliminados**) |
| Botón ícono | `ZHIconButton` (`components/zh/`) | `<button className="...">` con solo un ícono, implementaciones ad-hoc por módulo |
| KPI card | `ReportKpiCard` (`components/ReportPageTemplate.tsx`) | tarjetas de KPI custom por módulo |

Nota: `pf-badge`, `prd-status-badge`, `pg-kpi-badge`, `md-badge` **no** están deprecados — son variantes con semántica propia (status dot de 2 estados, tendencia de KPI) no consolidadas todavía en `Badge`. No copiar su patrón para casos nuevos que sí encajen en `Badge` (etiqueta simple con color semántico).

### Excepciones permitidas al uso de componentes ZH de input

Los siguientes casos **no** requieren envolver el control en un componente ZH — son
HTML nativo o infraestructura especializada ya cubierta por otras reglas:

- `type="email"`, `type="password"`, `type="checkbox"` (fuera de `ZHToggle`), `type="radio"`, `type="file"`, `type="color"`
- Scanner / autocomplete / picker especializado (ej. `ZhWarehouseSelector`, `CustomerPicker`, `SupplierSearchSelect`)
- Tablas editables (celdas con edición inline tienen su propio patrón, no el de formulario)
- Módulos SRI crítico, IAM/permisos, stock/logística crítica — cuando el control tiene semántica de negocio que un input ZH genérico no captura (documentar el motivo puntual en el propio código, no asumir la excepción por el nombre del módulo)

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
introducir estilos nuevos cuando ya existe un patrón equivalente. En concreto, queda
prohibido crear: otro input de texto/número/decimal/fecha/teléfono custom, otro select
custom, otro textarea custom, otro botón ícono, otro badge, otro KPI card, o CSS local
que reimplemente un estilo ya definido en `zh-ui.css`. CSS local solo se permite para
layout específico del módulo, composición de pantalla, o reglas de dominio visual que
no existan en el Design System. Si falta una capacidad visual común, no se crea dentro
del módulo — se propone primero como extensión del Design System (`zh-ui.css` o
`components/zh/` según corresponda).

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
reglas: [`pr-rules-catalog.md#f-04`](./pr-rules-catalog.md#f-04--design-system-único-ui).

### Alineación de datos numéricos (F-05 / NUM-001)

Todo dato numérico (montos, cantidades, porcentajes, stock, impuestos, totales, secuenciales, valores calculados) se alinea a la **derecha** en inputs, tablas, cards, KPIs, labels, dashboards, reportes y cualquier componente reutilizable — nunca centrado ni a la izquierda, salvo excepción documentada y aprobada por arquitectura. Si un componente base no lo soporta, se corrige el componente, nunca una excepción local. Detalle completo, ejemplos y excepciones: [`pr-rules-catalog.md#f-05`](./pr-rules-catalog.md#f-05--alineación-de-datos-numéricos-num-001).

### Presentación numérica read-only con precisión semántica (ZH-DESIGN-SYSTEM-PRECISION-02)

La pantalla declara **qué** representa el dato; el Design System decide **cómo** mostrarlo; dominio/aplicación deciden **cómo** calcularlo. Una sola cadena, sin caminos paralelos:

| Capa | Pieza única | Responsabilidad |
|------|-------------|-----------------|
| Origen | `EffectivePrecisionPolicyDto` (backend) | ÚNICA respuesta de precisión: 7 configurables por empresa, 4 fijos del sistema (`FiscalPrecision`) y contractuales fijos de dominio (`WarehousePrecision`, `CreditTermsPrecision`, `ItemPrecision`), los mismos que usa la columna física |
| Semántica | `PrecisionKind` (`lib/config/precisionPolicy.config.ts`) | `money`, `tax`, `accounting`, `salesUnitPrice`, `purchaseUnitPrice`, `unitCost`, `averageCost`, `quantity`, `percentage`, `fiscalPercentage`, `conversionFactor`, `warehouseCapacity`, `creditInstallmentPercentage`, `packagingWeight` |
| Precisión | `resolvePrecisionDecimals` + PrecisionPolicy de la empresa (React: `usePrecisionDecimals`) | semántica → decimales; único mapa `PRECISION_FIELD_BY_KIND` |
| Formato | `formatDecimalDisplay` (`lib/sanitizers.ts`) | redondeo Decimal.js `ROUND_HALF_UP`, punto decimal; `locale` explícito solo cambia separadores/agrupación |
| Presentación | `ZHMoneyValue` (con símbolo) / `ZHNumberValue` (sin símbolo; `prefix`/`suffix` opcionales, p. ej. `suffix="%"`) | prop `precision`; `null`/`undefined` → `—`; cero con escala; signo visible; `align` end y `tabular-nums` por defecto |
| Layout | `ZHDataTable`, `ReportKpiCard`, `ZHInfoRow` | alojan el nodo de presentación; **nunca** conocen ni resuelven precisión |

Reglas:

- **`percentage` ≠ `fiscalPercentage`.** `percentage` = porcentajes operativos configurables por empresa (descuentos, márgenes, reglas). `fiscalPercentage` = porcentajes fiscales de escala FIJA del sistema (p. ej. % de retención SRI), leídos de `FiscalPrecision.Percentage` vía `fiscalPercentageDecimals`; el backend rechaza en validación valores con más escala en vez de redondearlos en silencio.
- **Semántica ≠ símbolo.** `PrecisionKind` define la escala; el componente define la apariencia. El mismo `precision="averageCost"` se usa en `ZHMoneyValue` (con `$`) o en `ZHNumberValue` (columna sin símbolo) y resuelve los mismos decimales. Prohibido crear un kind por apariencia.
- **Tablas:** columna numérica = `align: "right"` (header + celda) + `render` que devuelve `ZHNumberValue`/`ZHMoneyValue` con `precision`. Sin celda numérica propia, sin `text-align` por módulo.
- **KPI:** `ReportKpiCard value={<ZHNumberValue … precision="…" />}`; el valor hereda la tipografía/tono de la tarjeta. Un `string` sigue siendo válido.
- **Label + valor:** `ZHInfoRow label={…} value={<ZHMoneyValue … precision="…" />}`; el label nunca conoce la precisión.
- **Inputs:** `ZhDecimalInput`/`ZhCurrencyInput` exigen `precision` (única API; sin `decimals` público ni default). El input normaliza coma/paste a punto canónico y redondea con el mismo motor; entrar y salir sin editar no reescribe el valor ni emite `onChange`.
- **Commit de negocio (`ZhDecimalInput.onValueCommit`):** `onBlur` es foco/touched/validación y se llama siempre; el dato se confirma con `onValueCommit(value: string)`, que el input emite una sola vez y SOLO tras edición real (teclado, borrado, paste, coma), con el texto canónico final. No se emite por foco/blur sin editar, por un `value` controlado nuevo ni por un cambio de policy — tampoco cuando el dato guardado tiene más escala que la visible (p. ej. 12.34567 mostrado 12.3457). Consumidores nuevos: no usar `onBlur` como commit indiscriminado ni inferir edición comparando el valor redondeado con el persistido.
- **Reactividad a la policy:** los valores read-only (`ZHMoneyValue`/`ZHNumberValue`, o `usePrecisionDecimals` + `formatDecimalDisplay` en textos compuestos) se re-renderizan al instante. Un input montado **no reescribe su texto** sin edición; la nueva escala gobierna el teclado y el blur de la siguiente edición. El cambio de empresa remonta el árbol mediante el gating de `SessionBootstrap`.
- **Prohibido:** `decimals` (no existe en la API pública), lecturas directas de la policy para presentación, literales `2/4/6` como escala, `toFixed`/`Intl.NumberFormat`/formatters por módulo, tabla o KPI. `usePrecisionDecimals` + `formatDecimalDisplay`/`formatMoney(x, decimals)` solo cuando el destino exige un `string` (texto compuesto, atributos, mensajes), nunca para evitar un componente de presentación disponible. Una utilidad pura recibe la escala (o la policy vía `usePrecisionPolicy`, resuelta con `resolvePrecisionDecimals`) desde su caller React.
- **Contrato fijo (ZH-DESIGN-SYSTEM-PRECISION-06):** un dato cuya escala no es configurable por empresa (p. ej. capacidad de bodega `numeric(18,4)`, % de cuota de crédito `numeric(5,2)`, peso de empaque `numeric(10,3)`) NO se resuelve en frontend: su escala es una constante de dominio backend (usada también por la columna EF), se expone en `EffectivePrecisionPolicyDto` y se declara como `PrecisionKind` (`warehouseCapacity`, `creditInstallmentPercentage`, `packagingWeight`). Nunca una constante frontend ni un kind por apariencia/unidad.

### Precision guard (F-PREC — ZH-DESIGN-SYSTEM-PRECISION-05/06)

Ruta ÚNICA de precisión (06): Backend SSOT → `EffectivePrecisionPolicyDto` → `PrecisionPolicy` → `PrecisionKind` → `resolvePrecisionDecimals` → Design System → consumidor. TypeScript la impone en la API (`precision` obligatorio en `ZHMoneyValue`/`ZHNumberValue`/`ZhDecimalInput`/`ZhCurrencyInput`/`ZHPromptModal type="decimal"`; formatters con escala obligatoria; sin `decimals` público ni default 2). `architecture:check` incluye `frontend-precision` (`tools/architecture/check-frontend-precision.mjs`), que bloquea en código productivo de `frontend/src`:

- componente numérico sin `precision` (F-PREC-implicit-value) y cualquier `decimals` en JSX (F-PREC-decimals);
- `formatMoney(x)`/`formatMoneyWithSymbol(x)` sin escala (F-PREC-implicit-format);
- `toFixed`/`Intl.NumberFormat` locales (F-PREC-toFixed, F-PREC-intl);
- `getPrecisionPolicy()`/`getPrecisionPolicySnapshot()` directos (F-PREC-policy-read): presentación con `precision`/`usePrecisionDecimals`; utilidades puras con la escala o la policy (`usePrecisionPolicy`) entregada por su caller;
- una segunda tabla semántica → campo `…Decimals` (F-PREC-alt-mapping): el único mapeo es `PRECISION_FIELD_BY_KIND`.

Sin deuda legacy tolerada (no hay grandfather de precisión). **Excepción legítima** — solo si NO decide presentación de negocio (cálculo, métrica técnica, payload externo): `tools/architecture/config/frontend-precision.json` → `exceptions` con archivo, regla, conteo exacto y `reason`; nunca wildcard ni carpeta completa. Fuera de alcance: tests, `src/test/**`, internos del Design System (`components/zh/**`) y la infraestructura de precisión (`lib/sanitizers.ts`, `precisionPolicy.config.ts`, `usePrecisionPolicy.ts`).

Un contrato de escala nuevo se agrega en este orden: constante de dominio backend (usada por la columna) → campo de `EffectivePrecisionPolicyDto` → campo de `PrecisionPolicy` + `PrecisionKind` + fila de `PRECISION_FIELD_BY_KIND` → consumidores con `precision`.

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

1. Listado: `{módulo}.tabList` en es/en; no `app.nav.*` en esa pestaña.
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

Prefijos por página: ver [NAMING.md](./naming.md#prefijos-css-por-página).

Clases frecuentes (no recrear): `.pg-page`, `.table`, `.badge`, `.zh-status`, `.zh-btn`, `.zh-form-tabs`, `.zh-input`, `.pg-kpi-icon--*`.

---

## Diálogos

**Prohibido** `window.prompt`, `window.confirm`, `window.alert` nativos. Usar modales ZH estándar.

---

## i18n

- Locales soportados: **`es`** y **`en`**.
- Claves nuevas: **siempre** `es.json` y `en.json`.
- `es.json` es la fuente de verdad y el gate arquitectónico valida paridad con `en.json`.
- Prohibido texto duro visible al usuario.

---

## Auth refresh (frontend)

Solo `authRefreshManager` (Web Locks + BroadcastChannel). Detalle tokens: [SECURITY.md](./security.md).

---

## Validación formularios

Zod + `zodResolver` + react-hook-form. Schema en `schemas/{modulo}/`. Ver [ENFORCEMENT.md](./ENFORCEMENT.md).

---

## Manejo de errores API

Ningún componente/página interpreta `error.response.data` directamente. Pipeline obligatorio: `normalizeApiError`/`apiError.ts` → `applyServerErrors<T>()` (errores de campo) → `formatApiRequestError()` (fallback general) → `ZHFormAlert`/`ZHToast`/`ZHPageNotice`/modal según contexto (nunca `catch` vacío en una mutación). Reglas E-F1..E-F7 y tabla de canal de presentación: [`error-handling.md`](./error-handling.md) (ADR-027).

---

## CI frontend

`npm run lint`, `npx tsc --noEmit`, `npm run build`, Playwright smoke (`.github/workflows/frontend-ci.yml`).
