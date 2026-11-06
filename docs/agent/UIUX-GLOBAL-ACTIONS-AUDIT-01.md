# UIUX-GLOBAL-ACTIONS-AUDIT-01 — Acciones de fila en listados

Estado: **AUDITORÍA — sin implementación del patrón** (2026-11-05). La auditoría no cambió
componentes, CSS, páginas, permisos, STATUS ni WORK_QUEUE. Sin suites generales ni SRI. Sin
staging/commit/push. Seguimiento posterior: AA-D1 corregido técnicamente por FUNC-PARTNER-ROLE-01 y AA-D5
corregido técnicamente por FUNC-DEACTIVATION-CONFIRM-01 (§ 10), ambos pendientes de Git/CI (ver
[STATUS](../../STATUS.md)). El Bloque 2 (Clientes) está implementado por
UIUX-GLOBAL-ACTIONS-CLIENTS-01 (§ 11), que además resuelve AA-D2 y AA-D3, pendiente de gate del
propietario y Git/CI; los demás hallazgos siguen pendientes.

Referencia normativa: [Frontend § Proveedores](../architecture/frontend.md) (fila «Ver» +
«Más acciones», `portal` opcional), [UIUX-GLOBAL-STANDARD-01](UIUX-GLOBAL-STANDARD-01.md) y
el Bloque 5 de [UIUX-GLOBAL-ARCHITECTURE-02](UIUX-GLOBAL-ARCHITECTURE-02.md) («`portal` por
defecto en `ZHLineAction kind="menu"` — validar Compras»), que esta auditoría reevalúa.

Método: revisión de código de los consumidores de `ZHDataTable` (≈90 archivos), de
`ZHLineAction`/`ZHLineActionItem`/`ZHLineActionRail` y de las columnas de acciones manuales;
capturas de la aplicación real (Vite + `AppLayout`) con **datos simulados** (ver § 8).

---

## 1. Patrón de referencia — Proveedores

Implementación: [MasterDataSupplierListActions.tsx](../../frontend/src/modules/masterData/components/MasterDataSupplierListActions.tsx),
montado en la columna `actions` de [MasterDataPartnerListTab.tsx:120](../../frontend/src/modules/masterData/components/MasterDataPartnerListTab.tsx#L120).

| Aspecto | Contrato observado |
|---|---|
| Acción principal | `ZHBtn ghost sm` «Ver» → `/masterdata/business-partners/:id`, siempre visible, `aria-label` con razón social + RUC |
| Secundarias | `ZHLineAction kind="menu"` (`⋯`, sin chevron por CSS local) con `ZHLineActionItem`: Editar proveedor, Configuración SRI, Asignar como cliente, Activar / Desactivar (tono `danger`) |
| Permisos | Menú solo si `canUpdate \|\| (canDisable && isActive)`; cada ítem conserva su condición; sin permisos no hay disparador |
| Estado | `saving` deshabilita asignar/activar/desactivar, no editar/SRI |
| Confirmación | Activar/Desactivar llaman a los handlers de página con `message.confirm`; asignar rol **no confirma** (igual que antes del patrón) |
| Portal | `portal menuAlign="end" closeOnSelect`; panel en `document.body`, `position: fixed`, `z-index: 1000`, recolocado en `resize`/`scroll` (captura) |
| Teclado/foco | Foco al primer ítem habilitado; ↑/↓/Home/End; Tab cierra y devuelve foco al disparador; Escape cierra con retorno; clic fuera cierra |
| Identidad | Primera línea del panel: razón social + RUC (`role="presentation"`) |
| Móvil | Hasta 640 px oculta N°/identificación/estado (`nth-child`) y los integra en la celda de identidad; columna de acciones 96 px hasta 768 px |
| Pruebas | `MasterDataSupplierListActions.test.tsx`: 6 casos (portal, teclado, permisos reducidos, activación, `saving`) |

Verificado en capturas (§ 8): sin overflow horizontal de página ni de tabla a 320/390/768/1440;
panel dentro del viewport en todos los anchos (a 320 px: `left 36 – right 292`); foco inicial en
«Editar proveedor»; con ↓ el foco avanza a «Configuración SRI»; el panel sigue al disparador al
hacer scroll.

---

## 2. Inventario y matriz de clasificación

Alcance: acciones **por fila** de listados. Se excluyen acciones globales de página (Nuevo,
Exportar, Recargar), formularios, paginación y editores de líneas/edición masiva (Compras
`DistributeCostModal`, `PurchaseLineBreakdownModal`, editores de NC/devoluciones, Carga Inicial),
salvo que se indique.

Leyenda: «Máx.» = acciones visibles simultáneamente en una fila. Clase A/B/C/D según el encargo.

### 2.1 Prioritarios

| Pantalla / ruta | Componente | Máx. | Principal → secundarias | Permisos / estado | Menú / portal | Móvil (390 px) | Clase |
|---|---|---|---|---|---|---|---|
| **Proveedores** `/suppliers` | `MasterDataSupplierListActions` | 2 | Ver → menú (4) | update/disable/activo/saving | `ZHLineAction` portal | Sin scroll; acciones visibles | **A** (referencia) |
| **Clientes** `/customers` | `MasterDataPartnerListTab` rama `customer` (7 `ZHBtn` sueltos) | **6** | Ver, Editar, Empresa, Clasificación, «+ Proveedor», Desactivar \| Habilitar | update / configure-company / disable / activo / saving | Ninguno | Tabla 542 px en 358 px de ancho; acciones fuera de vista y apiladas en 6 líneas (fila ≈190 px) | **B** + **D** (AA-D1..D3) |
| **Compras — línea de factura** `/purchases` (editor) | `ZHLineActionRail` + `ProductLineActionMenu` (`kind="menu"`, inline) + Desglosar/Duplicar/Eliminar icono | 4–5 | Menú de producto (Seleccionar con `ProductPicker` inline, Crear, Ver, Actualizar, Corregir emparejamiento, Desvincular) | `lineLocked`, `distributionLocked`, `matchingKey`, modales internos | Inline, sin portal; tarjeta con `overflow: visible` | Rail vertical, sin recorte observado en código | **C** |
| **Compras — historial** `/purchases` | columna `actions` con `ZHIconButton` | 2 | Editar (borrador) \| Ver → Kardex (confirmada) | estado | Ninguno | Iconos compactos | **A** |
| **Recepción SRI** `/purchases/reception` | `PurchaseReceptionActionsCell` | ≤3 | Acción contextual por estado del documento (Ver compra, Ver proveedor, Procesar NC, Ver NC, Consultar XML, Ver XML) | estado SRI, `sriPaused`, procesamiento | Ninguno | `pur-actions-cell` | **C** (máquina de estados SRI; no tocar sin fase SRI) |

### 2.2 Candidatos B (detalle propio + ≥3 acciones o acciones de texto que desbordan)

| Pantalla / ruta | Componente | Máx. | Acciones | Confirmaciones | Observación | Clase |
|---|---|---|---|---|---|---|
| Ajustes de inventario `/inventory/adjustments` | `StockAdjustmentsPage` | 3 | Ver, Editar (borrador), Ejecutar (`primary`) \| Anular | Ejecutar/Anular abren modal de ciclo de vida | Tabla 997 px a 390 px; Ver y Editar navegan a la **misma ruta** (AA-D4) | **B** (con decisión sobre «Ejecutar», § 9) |
| Sucursales `/settings/branches` | `BranchesListSection` | 3 | Ver detalle, Editar, Desactivar \| Activar | `message.confirm` en hook | Tiene detalle `/settings/branches/:id` | **B** |
| Productos `/products/items` | `ItemListTable` | 3 iconos | Ver detalle, Editar, Habilitar/Deshabilitar | `message.confirm` | Iconos (≈100 px); bajo riesgo de desborde | **B** (baja prioridad) |
| Perfiles `/admin/roles` | `ProfilesPage` | 3 texto | Editar, Permisos, Activar/Desactivar | `message.confirm` | Sin pantalla «Ver»; la principal sería «Editar» | **B condicionado** (§ 9) |

### 2.3 Clase A — conservar (≤2 acciones directas, o 1 acción, o fila clicable)

| Grupo | Pantallas | Patrón actual |
|---|---|---|
| Una sola acción de navegación | Asientos `/accounting/journal-entries`, Solicitudes de fondos `/treasury/cash/funding-requests`, NC compra `/purchases/credit-notes`, Devoluciones compra `/purchases/returns`, Devoluciones venta `/sales/returns`, Créditos proveedor `/suppliers/credits`, Kardex (icono expediente) | «Ver» único; ya es el caso degenerado del patrón (sin menú) |
| Una acción de estado | Periodos contables (Revisar \| Bloquear), Cierre de ejercicio (Generar \| Abrir), Reglas contables (Configurar), Políticas de flujo (Editar), Autorizaciones físicas (Deshabilitar, confirma), Secuencias (Configurar \| bloqueada), Conciliación de apertura (Publicar), Sesiones de usuario (Cerrar sesión, destructiva con estado `closingId`) | Botón único condicionado por estado/permiso |
| Fila clicable | Gastos `/expenses/documents`, Pagos a proveedor `/supplier-payments`, Monitor de comprobantes (fila + icono) | `onRowClick`; sin columna de menú |
| Catálogos Editar + Activar/Desactivar (iconos) | Plan de cuentas, Bancos, Plazos de pago, Condiciones de crédito, Listas de precios, Medios de cobro (+ cuenta), Excepciones de precio, Clientes de lista | 2 `ZHIconButton`, con confirmación |
| Catálogos Editar + Activar/Desactivar (texto) | Bodegas, Motivos de ajuste, Motivos de caja, Cuentas bancarias, Cajas, Puntos de emisión, Establecimientos, Usuarios (Configurar + Revocar/Reactivar), Transportistas, Tipos de ítem, Catálogos de ítems (marcas, etc.), Admin Core (2) | 2 `ZHBtn ghost sm`; caben en 1440/768; a 390 dependen del scroll de tabla pero no se apilan en 6 líneas |
| Historial de ventas `/sales` | 3 iconos: Ver/Editar o Reintentar, Kardex, RIDE (genera/abre documento) | Iconos con `ariaLabel`; RIDE deshabilitado mientras `ridePending` |
| Caja — sesiones `/treasury/cash` | 2 iconos: Ver resumen (expande la fila), Ver sesión | Divulgación en fila |

Los catálogos de 2 acciones **no** se migran: «Ver + Más acciones» no reduciría el número de
controles (2 → 2) y añadiría un clic a la acción más frecuente.

### 2.4 Clase C — especializados (no forzar el patrón)

| Pantalla | Motivo |
|---|---|
| Compras — línea (`ProductLineActionMenu`) | El panel contiene un **combobox interactivo** (`ProductPicker`, con ↑/↓/Enter/Escape) y abre modales; el modo `portal` intercepta ↑/↓/Home/End y Tab dentro del panel y cerraría/robaría el foco del picker. Es un editor de línea, no una fila de listado |
| Recepción SRI (`PurchaseReceptionActionsCell`) | Acción contextual única por estado del documento electrónico; SRI fuera de alcance |
| Cuentas por cobrar `/finance/receivables` | «Registrar cobro» es la acción transaccional principal de la fila (no «Ver»); «Consultar / anular cobro» abre su propio modal con motivo. Ocultar el cobro tras un menú empeora el flujo de caja |
| Costos pendientes `/inventory/pending-costs` | Tres enlaces de resolución a otros módulos (Compra, Ajuste, Kardex) según permiso; son navegación guiada, no acciones sobre el registro |
| Detalle crédito proveedor (movimientos) | Reversa por movimiento dentro de un detalle, con estado `reversing` |
| Ítem — Variantes / Imágenes (editor) | Secciones de edición dentro del formulario de ítem |

---

## 3. Problemas reales identificados (clase D — independientes del patrón)

| ID | Severidad | Pantalla | Problema | Evidencia |
|---|---|---|---|---|
| AA-D1 | 🟧 Importante — ✅ **Corregido técnicamente** por FUNC-PARTNER-ROLE-01 (2026-11-05); pendiente de Git/CI | Clientes y Proveedores | «+ Proveedor» / «Asignar como cliente» se ofrecen sin consultar `canAssignAsSupplier` / `canAssignAsCustomer`, que el backend ya expone en `BusinessPartnerSummaryDto` («espeja AssignBusinessPartnerRoleHandler»). Para socios que ya tienen el rol o están inactivos, la acción termina en error del backend | [MasterDataPartnerListTab.tsx:156](../../frontend/src/modules/masterData/components/MasterDataPartnerListTab.tsx#L156), [MasterDataSupplierListActions.tsx:41](../../frontend/src/modules/masterData/components/MasterDataSupplierListActions.tsx#L41); antes de la corrección ningún consumidor no-test leía `canAssignAs*`; capturas 11, 06 (datos simulados con flags en `false`). Corrección: ambas acciones exigen además `canAssignAs*` del backend (se ocultan si no aplica); el POST conserva su validación |
| AA-D2 | 🟨 Menor — ✅ **Resuelto** por UIUX-GLOBAL-ACTIONS-CLIENTS-01 (§ 11; el menú usa «Agregar como proveedor» y se retiraron las claves `addSupplier`/`addSupplierShort`); pendiente de Git/CI | Clientes | Etiqueta «++ Proveedor»: el código antepone `+` y la traducción ya es «+ Proveedor» | [MasterDataPartnerListTab.tsx:164](../../frontend/src/modules/masterData/components/MasterDataPartnerListTab.tsx#L164), `es.json` `masterdata.customers.action.addSupplierShort`; capturas 11–17 |
| AA-D3 | 🟨 Menor — ✅ **Resuelto** por UIUX-GLOBAL-ACTIONS-CLIENTS-01 (§ 11; identidad en columna `.md-customer-identity`); pendiente de Git/CI | Clientes | Nombre comercial y razón social se renderizan pegados («Andina PacíficoDistribuidora…»): `.prd-sub-text` no tiene regla CSS en ningún archivo | [MasterDataPartnerListTab.tsx:105](../../frontend/src/modules/masterData/components/MasterDataPartnerListTab.tsx#L105); capturas 11, 12, 14 |
| AA-D4 | 🟨 Menor | Ajustes de inventario | «Ver» y «Editar» navegan a la misma ruta `/inventory/adjustments/:id` | [StockAdjustmentsPage.tsx:113](../../frontend/src/modules/inventory/adjustments/pages/StockAdjustmentsPage.tsx#L113) y [:122](../../frontend/src/modules/inventory/adjustments/pages/StockAdjustmentsPage.tsx#L122) |
| AA-D5 | 🟧 Importante — ✅ **Corregido técnicamente** por FUNC-DEACTIVATION-CONFIRM-01 (2026-11-06) en las 5 rutas (detalle por ruta en § 10); pendiente de Git/CI | Catálogos de ítems (`useCatalogCrud`), Tipos de ítem, Cajas, Variantes/Imágenes de ítem | Desactivar sin confirmación (las demás pantallas sí confirman) | `useCatalogCrud.ts:182`, `ItemTypesPage.tsx:134`, `useCashRegistersPage.ts:295`, `useItemDetail.ts:28/39` (líneas previas a la corrección) |
| AA-D6 | 🟨 Menor | Transportistas | Nombres accesibles en inglés en UI española: `aria-label="Edit"`, `title="Disable"/"Enable"` | [CarriersPage.tsx:213](../../frontend/src/modules/logistica/transportistas/pages/CarriersPage.tsx#L213) |
| AA-D7 | 🟨 Menor | `ZHLineAction` portal | Si el disparador sale del viewport con el menú abierto, el panel queda fijo en `top: 8px` sobre otras filas (no se cierra ni se oculta). La línea de identidad evita actuar sobre el registro equivocado, pero es confuso | [ZHLineAction.tsx:194](../../frontend/src/components/zh/ZHLineAction.tsx#L194); captura 22 (`menu.top = 8`) |
| AA-D8 | 🟩 Informativo | Proveedores | Regla muerta `.md-supplier-list .md-actions` (Proveedores ya no renderiza `.md-actions`) | [masterdata-pages.css:752](../../frontend/src/modules/masterData/pages/masterdata-pages.css#L752) |
| AA-D9 | 🟩 Informativo | Textos fijos | Varios listados usan `title`/etiquetas sin i18n («Ver detalle completo», «Editar», «Acciones», «Anular cobro») | Sucursales, Cajas, Puntos de emisión, Establecimientos, Ventas, CxC |

AA-D1 está corregido técnicamente (FUNC-PARTNER-ROLE-01, pendiente de Git/CI). AA-D2 y AA-D3
quedan resueltos dentro del bloque de Clientes (UIUX-GLOBAL-ACTIONS-CLIENTS-01, § 11). AA-D5 está
corregido técnicamente (FUNC-DEACTIVATION-CONFIRM-01, pendiente de Git/CI; § 10). AA-D4 y
AA-D6..AA-D9 permanecen pendientes.

---

## 4. Comparación con el patrón aprobado — Clientes antes de modificar

| Aspecto | Proveedores (aprobado) | Clientes (actual) |
|---|---|---|
| Controles por fila | 2 (Ver + ⋯) | Hasta 6 botones de texto |
| 1440 px | Columna compacta | Fila de 6 botones (≈470 px) junto a la razón social |
| 768 px | 5 columnas, acciones visibles | Acciones envueltas en 3 líneas por fila |
| 390 / 320 px | Identidad integrada, sin scroll horizontal | Tabla 542 px; acciones fuera de vista; al desplazar se apilan 6 líneas (fila ≈190 px) |
| Identidad del registro en acciones | Panel con razón social + RUC; `aria-label` de Ver con identidad | Botones sin identidad en el nombre accesible |
| Permisos | Menú oculto sin permisos | Cada botón oculto individualmente (correcto) |
| Inactivos | Ver + menú (Editar, SRI, Asignar, Activar) | Ver, Editar, Empresa, Clasificación, Habilitar, + Proveedor |
| Confirmación | Activar/Desactivar | Activar/Desactivar (mismos handlers) |
| Etiquetas | «Editar proveedor», «Desactivar proveedor» | «Editar», «Empresa», «Clasificación», «++ Proveedor», «Deshabilitar» |

Composición propuesta para Clientes (sin cambiar callbacks ni condiciones):

- Visible: **Ver**.
- Más acciones (identidad: razón social + identificación): Editar cliente (`canUpdate`),
  Condiciones / Empresa (`canConfigure && onSettings`), Clasificación (`canUpdate &&
  onCustomerConfig`), Asignar como proveedor (`canUpdate && canAssignAsSupplier`, ya aplicado por AA-D1),
  Activar (`canUpdate && !isActive`), Desactivar (`canDisable && isActive`, `danger`).
- Condición del disparador: `canUpdate || canConfigure || (canDisable && isActive)`. **No copiar**
  la condición de Proveedores tal cual: omitiría a un usuario que solo tiene
  `configure-company`.
- Móvil: reutilizar la estrategia de identidad de Proveedores **por significado de columna**, no
  por `nth-child` global (regla de UIUX-GLOBAL-STANDARD-01 § tabla móvil).

---

## 5. Recomendación sobre `ZHLineAction` y su portal

Consumidores reales de `kind="menu"`: **dos** — Proveedores (`portal`) y Compras (inline).

1. **No activar `portal` por defecto** (se reevalúa el Bloque 5 de ARCHITECTURE-02). El único
   consumidor afectado sería Compras, y el modo portal:
   - captura ↑/↓/Home/End en el panel ([ZHLineAction.tsx:243-250](../../frontend/src/components/zh/ZHLineAction.tsx#L243-L250)) y robaría la navegación del `ProductPicker` (combobox);
   - Tab cierra el menú y devuelve el foco al disparador ([:251](../../frontend/src/components/zh/ZHLineAction.tsx#L251)), lo que impide tabular dentro del picker;
   - el foco inicial salta al primer `menuitem` al abrir ([:199](../../frontend/src/components/zh/ZHLineAction.tsx#L199)).
   Beneficio del cambio de default: ninguno; riesgo: regresión en Compras (CLOSED).
2. **Componer el patrón de fila en un componente oficial del DS**, p. ej. `ZHRowActions`
   (nombre a decidir), generalizando `MasterDataSupplierListActions`: props `primary`
   (acción visible: Ver u otra), `identity` (texto del encabezado), `moreLabel`, `items`
   (o `children` con `ZHLineActionItem`). Internamente siempre `ZHLineAction kind="menu" portal
   menuAlign="end" closeOnSelect`. Proveedores pasa a consumirlo sin cambio visual.
   El CSS `.md-supplier-actions*` sube al DS (`zh-ui.css`/`ZHLineAction.css`), sin duplicar.
3. Mejoras al portal, opt-in y sin cambio de API: cerrar (o devolver foco) cuando el disparador
   sale del viewport o de su contenedor con scroll (AA-D7); `aria-orientation` innecesario.
   Mantener el glifo `⋯` o usar ligadura `more_horiz` de Material Symbols — decisión visual.
4. `ZHLineActionRail` / `kind="button"` siguen siendo el patrón de **editores de líneas**
   (Compras, Ventas); no se mezclan con el de filas de listado.

---

## 6. Propuesta de implementación por bloques

| Bloque | Alcance | Reutiliza | Pruebas focales |
|---|---|---|---|
| 0 — Correcciones D sin cambio de patrón | AA-D2 (doble `+`), AA-D3 (CSS de `prd-sub-text` o usar la identidad de Proveedores), AA-D4, AA-D6, AA-D8 | — | `MasterDataPartnerListTab.test`, `StockAdjustmentsPage` test existente |
| 1 — Componente DS de fila | Extraer `ZHRowActions` desde `MasterDataSupplierListActions`; migrar Proveedores sin cambio visual; CSS al DS | `ZHLineAction` portal, `ZHLineActionItem`, `ZHBtn` | `ZHLineAction.test` (+ casos portal), test nuevo del componente, `MasterDataSupplierListActions.test` intacto en comportamiento; capturas idénticas a las aprobadas |
| 2 — Clientes | Migrar rama `customer` a `ZHRowActions`; conservar AA-D1 (ya corregido en ambos roles por FUNC-PARTNER-ROLE-01); identidad móvil por significado | Bloque 1 | Permisos reducidos (solo configure-company, solo disable, solo update), inactivo, `saving`, `canAssignAsSupplier=false`, teclado/portal; 320/390/640/641/768/1440 |
| 3 — Candidatos B | Sucursales, Ajustes de inventario, Productos; Perfiles según decisión | Bloque 1 | Por pantalla: permisos, estado (borrador/ejecutado/anulado), confirmaciones de Ejecutar/Anular, navegación |
| 4 — Portal | AA-D7 (cerrar/ocultar al salir del viewport), opt-in | `ZHLineAction` | `ZHLineAction.test` portal + scroll; Compras sin cambios (inline) |
| 5 — Confirmaciones | AA-D5 — ✅ realizado por FUNC-DEACTIVATION-CONFIRM-01 (§ 10) | `message.confirm` | Tests de cada hook |

Cada bloque: `npx tsc -b`, ESLint focal, `architecture:check`,
`check-architecture-guardrails.ps1 -SkipFrontendChunk`, `git diff --check`, capturas y gate del
propietario. Actualizar la SSOT [Frontend](../architecture/frontend.md) en la misma entrega.

---

## 7. Riesgos y pruebas necesarias

| Riesgo | Mitigación / prueba |
|---|---|
| Ocultar acciones al reducir permisos (condición del disparador incompleta) | Matriz de permisos por pantalla en tests (como los 6 casos de Proveedores) |
| Acción principal transaccional escondida (Ejecutar ajuste, Registrar cobro) | Decisión explícita por pantalla (§ 9); CxC queda en C |
| Pérdida de confirmaciones al mover a `ZHLineActionItem` | Reusar los mismos callbacks; test que verifique `message.confirm` antes de la llamada |
| `closeOnSelect` + modal: el foco vuelve al disparador antes de abrir el modal | Verificar foco tras cerrar el modal de confirmación (manual + test) |
| Menús en contenedores con scroll horizontal (`.table-scroll`) | Ya cubierto por listener `scroll` en captura; validar con tablas anchas (Ajustes, 997 px a 390) |
| Disparador fuera de vista (AA-D7) | Bloque 4 |
| `onRowClick` + botones en la fila | No hay consumidor con ambos; si aparece, `stopPropagation` en el componente DS y test |
| Compras (CLOSED) | No migrar; no cambiar default de `portal` |
| Etiquetas duplicadas o sin identidad para lectores de pantalla | `aria-label` con identidad en «Ver» y en el disparador, como Proveedores |
| Visual | 320, 390, 768, 1440 + cortes 640/641, 768/769; zoom 200 %; es/en |

---

## 8. Capturas comparativas

**Datos simulados.** Aplicación real (Vite dev en `localhost:5173`, `AppLayout` real) con
Playwright interceptando **todas** las peticiones `/api/**` y respondiendo datos ficticios
(6 socios, 5 ajustes, empresa «Supermercado Simulado S.A.», rol `User` con permisos
explícitos). Ninguna petición llegó al backend; sin login real ni tokens. Totales de costo y
tipo de movimiento de Ajustes son artefactos de los datos simulados. No se modificó código.

Ubicación (fuera del repositorio): `C:\Users\Sebas\AppData\Local\Temp\claude\caa27144e47ed194\4820452f-cfc8-4715-bd47-d5919a4c974f\scratchpad\shots\final\`
— métricas por captura en `metrics.jsonl`; hoja comparativa en `index.html`.

| # | Captura | Lo que muestra |
|---|---|---|
| 01–03 | Proveedores 1440 (lista, menú abierto, menú en última fila con alto 560) | Referencia; panel en `body`, dentro del viewport |
| 04, 05, 09 | Proveedores 768 / 390 / 320 | Sin scroll horizontal (tabla = contenedor) |
| 06, 10 | Menú abierto 390 / 320 | Panel 312 / 256 px, dentro del viewport |
| 07 | Teclado 390 | Foco en «Configuración SRI» tras ↓ |
| 08 | Menú + scroll 390 | El panel sigue al disparador |
| 22 | Menú + scroll hasta sacar el disparador | AA-D7: panel fijado arriba sobre otra fila |
| 11, 12, 13 | Clientes 1440 / 768 / 768 con scroll | 6 botones; envoltura en 3 líneas; AA-D2, AA-D3 |
| 14–17 | Clientes 390 / 320 (inicio y scroll a acciones) | Tabla 542 px; acciones apiladas en 6 líneas |
| 18–21 | Ajustes de inventario 1440 / 768 / 390 | Candidato B: Ver + Editar + Ejecutar; tabla 997 px a 390 |

Compras (líneas) no se capturó: requiere simular el editor completo de factura y es clase C sin
cambios propuestos.

---

## 9. Decisiones pendientes del propietario

1. Nombre y API del componente DS de fila (`ZHRowActions` u otro) y si la acción principal es
   configurable (Perfiles: «Editar» como principal) o siempre «Ver».
2. Ajustes de inventario: ¿«Ejecutar» queda visible como acción primaria junto a «Ver», o pasa
   al menú? (Recomendación: visible solo en borrador — es la acción de negocio de la fila.)
3. Asignar rol (cliente ↔ proveedor): ¿requiere confirmación? Hoy no confirma en ninguno de los dos.
4. ~~AA-D5: ¿agregar confirmación a Desactivar en catálogos de ítems, tipos de ítem, cajas y
   variantes/imágenes?~~ Resuelto por FUNC-DEACTIVATION-CONFIRM-01; quedan las decisiones del § 10.3.
5. Confirmar que el Bloque 5 de ARCHITECTURE-02 («`portal` por defecto») se reemplaza por la
   recomendación del § 5.

## 10. Seguimiento FUNC-DEACTIVATION-CONFIRM-01 (AA-D5, 2026-11-06)

Corrección focal de frontend: desactivar pide `message.confirm` (componente oficial; sin
`window.confirm`) con el registro identificado, la consecuencia, `Cancelar`/`Desactivar` y
variante `danger`. Cancelar no envía petición; un guard síncrono (`useRef`) impide ejecuciones
duplicadas y un estado pendiente deshabilita los botones de estado. Permisos, endpoints,
validaciones del backend, mensajes de éxito/error y la activación (directa, sin confirmar) se
conservan. Sin cambios en backend, APIs, permisos, `ZHRowActions`, tablas responsive ni estilos.

### 10.1 Diagnóstico por pantalla

| Ruta | Botón → handler | Endpoint | Permiso / elegibilidad | Consecuencia / reversión | Estado |
|---|---|---|---|---|---|
| Marcas, Grupos y Definiciones de atributo (`CatalogListSection` → `useCatalogCrud.toggleDisable`) | Icono `block` / `check_circle` | `disable` / `enable` del servicio de cada catálogo | Desactivar: `<prefijo>.delete`; activar: `<prefijo>.update` (ahora también verificado en el hook) | Baja lógica (`IsActive=false`), sin reglas de bloqueo; reversible con Activar | ✅ Confirmación |
| Tipos de ítem (`ItemTypesPage.handleToggle`) | Icono `toggle_on` / `toggle_off` | `POST /api/v1/item-types/{id}/toggle` | `items.edit`; el backend rechaza si existen ítems activos con el tipo (mensaje mostrado tal cual) | Baja lógica; reversible | ✅ Confirmación |
| Cajas (`CashRegistersListSection` → `useCashRegistersPage.toggleDisable`) | Icono `block` / `check_circle` | `disableCashRegister` / `enableCashRegister` | `caja.manage`; el backend rechaza con sesión abierta (motivo exacto mostrado) | Baja lógica; reversible | ✅ Confirmación |
| Variantes de ítem (`VariantsSection` → `useItemDetailPage.toggleVariant`) | Icono `block` / `check_circle` | `disableVariant` / `enableVariant` | `disabled` de la sección (modo/permiso de edición del ítem) | Baja lógica con evento de dominio; reversible | ✅ Confirmación |
| Imágenes de ítem (`ImagesSection` → `useItemDetailPage.disableImage`) | Icono `block` | `PATCH /api/v1/items/{id}/images/{imageId}/disable` (`items.edit`) | `disabled` de la sección; solo se listan imágenes activas | Baja lógica, **sin endpoint ni UI para reactivar** | ✅ Confirmación (advierte la irreversibilidad en UI y si es la principal) |
| Categorías (`TreeEditor.handleToggleStatus`) | — | — | — | — | Ya confirmaba; sin cambios |

### 10.2 Validación

Vitest focal (catálogos, Tipos de ítem, Cajas, variantes/imágenes y consumidores de Ítems):
cancelar sin petición, confirmación con petición única ante doble clic, error del backend, estado
de carga, sin permiso, registro no elegible (motivo del backend) y activación sin cambios.
Traducciones es/en: claves presentes, mismos parámetros y textos distintos por idioma
(`deactivationConfirmKeys.test.ts`). `tsc -b`, ESLint focal, `architecture:check` (incluye
`i18n-keys`), guardrail oficial `-SkipFrontendChunk` y `git diff --check`. Datos ficticios; sin
backend real.

### 10.3 Decisiones del propietario (2026-11-06)

1. **Activar permanece sin confirmación** en estas cinco rutas por decisión del propietario.
   Sucursales, Categorías y demás módulos no se modifican.
2. Se mantienen las confirmaciones de desactivación y sus protecciones contra doble ejecución.
3. **Imágenes:** la desactivación no tiene reactivación en API ni UI; se mantiene la advertencia
   explícita en la confirmación. La reactivación queda registrada como pendiente independiente
   **FUNC-ITEM-IMAGE-REACTIVATION-01** (no implementado; requiere cambio de backend).
4. **i18n:** los textos nuevos de confirmación usan claves oficiales en `es.json`/`en.json` con
   parámetros `{{name}}`, `{{code}}`, `{{label}}`: `catalog.deactivate.confirm.*`,
   `itemTypes.deactivate.confirm.*`, `cashRegisters.deactivate.confirm.*`,
   `items.variants.deactivate.*`, `items.images.deactivate.*` (incluye `messageMain` para la
   imagen principal); botones con `common.deactivate`/`common.cancel`. Los hooks leen `t` de
   `useI18n`, como `useBranchesPage`. Los textos previos (errores, éxito) no cambian; AA-D9 sigue
   pendiente para el resto.
5. AA-D5 queda **corregido técnicamente, pendiente de Git/CI**.

## 11. Seguimiento UIUX-GLOBAL-ACTIONS-CLIENTS-01 (Bloque 2 — Clientes, 2026-11-06)

Implementa el diseño aprobado por el propietario, que difiere de la propuesta del § 4: **Ver** y
**Editar** (con `update`) permanecen visibles; «Más acciones» agrupa Empresa, Clasificación,
Agregar como proveedor y Activar/Desactivar. Sin `ZHRowActions` (Bloque 1 diferido), sin cambiar
el valor por defecto de `portal` ni Proveedores/Compras. Contrato vigente en la SSOT
[Frontend § Listado de Clientes](../architecture/frontend.md#listado-de-clientes).

| Acción | Condición (idéntica a la previa) | Callback / confirmación |
|---|---|---|
| Ver (visible) | siempre | navega a `/masterdata/business-partners/:id` |
| Editar (visible) | `update` | `startEdit` del store (sin confirmación) |
| Empresa | `configure-company` y callback | `openSettings` → modal de condiciones |
| Clasificación | `update` y callback | `openCustomerConfig` → modal |
| Agregar como proveedor | `update`, callback y `canAssignAsSupplier` | `addAsSupplier` (sin confirmación, como antes); `saving` lo deshabilita |
| Activar cliente | `update` e inactivo | `handleActivate` con `message.confirm`; `saving` lo deshabilita |
| Desactivar cliente (`danger`) | `disable` y activo | `handleDisable` con `message.confirm`; `saving` lo deshabilita |

Disparador solo si hay al menos un ítem autorizado; un usuario con solo `configure-company` ve
«Ver» + menú con Empresa. Nombres accesibles con razón social + identificación. Retorno de foco
al disparador tras cerrar el modal/confirmación abiertos desde el menú (`useReturnFocusAfterDialog`,
local al módulo; no modifica `ZHModal` ni `message`). Responsive por clase de columna
(`md-customer-col-*`): ≤768 px oculta N° e identificación; ≤640 px también estado; ambos se
integran en la identidad. Con puntero táctil, Ver/Editar a `--density-control-height`.

### 11.1 Validación

- Vitest focal: `MasterDataCustomerListActions` (nuevo), `MasterDataPartnerListTab`,
  `MasterDataCustomersPage` (+ clasificación), `MasterDataSupplierListActions`,
  `MasterDataSuppliersPage` (+ SRI), `ZHLineAction` — 8 archivos, 107/107; i18n 4 archivos 31/31;
  Compras `PurchaseLineCard.compact` y `ProductPicker` PASS. `PurchaseFormFeedback.test.tsx`
  falla 1 caso («base-unit equivalence») **igual en `HEAD` limpio** (worktree temporal): preexistente,
  ajeno a este bloque.
- `tsc -b`, ESLint focal, `architecture:check` (0 violaciones nuevas, `i18n-keys` PASS),
  guardrail oficial `-SkipFrontendChunk` y `git diff --check` PASS.
- Capturas en la aplicación real (Vite + `AppLayout`) con **API 100 % simulada** (Playwright;
  solo `POST /auth/refresh` simulado como no-GET; ninguna petición al backend, 0 abortadas) a
  320, 390, 640, 641, 768, 769 y 1440 px: sin desbordamiento horizontal de página ni de tabla
  (tabla = contenedor en todos los anchos); menú dentro del viewport (320: `left 36 – right 292`);
  foco inicial en el primer ítem, ↓ avanza, Escape devuelve el foco al disparador; con solo
  `configure-company` el menú contiene únicamente Empresa y no hay Editar; con solo `view` no hay
  menú; táctil 390: Ver/Editar 36 px de alto; tras cerrar el modal Empresa y tras cancelar la
  confirmación de Desactivar, el foco vuelve al disparador de la fila.

### 11.2 Limitaciones visuales que permanecen

1. ~~Filas inactivas con acciones atenuadas~~ — ✅ **Corregido en la revisión visual final**
   (§ 11.3). Queda, fuera de alcance, que el token `--color-text-secondary` de los botones `ghost`
   mide 4.34:1 sobre el fondo de la tabla (filas activas e inactivas, y Proveedores): es del DS.
2. En pantallas táctiles la fila de Clientes usa botones de 36 px y la de Proveedores conserva
   26 px («Ver»): divergencia deliberada para no cambiar Proveedores; se unifica en el Bloque 1.
3. AA-D7 (panel fijo si el disparador sale del viewport) sigue presente: Bloque 4.
4. «Editar» visible a la izquierda del menú añade ~60 px por fila respecto de Proveedores; a 320 px
   la identidad dispone de ~140 px y nombres largos ocupan varias líneas (sin desbordar).

### 11.3 Revisión visual final — legibilidad de Clientes inactivos (2026-11-06)

`prd-row--inactive` (`items-catalog.css`, compartida) aplica `opacity: 0.5` a toda la fila.
Contraste efectivo medido en la aplicación real (WCAG 2.x, datos simulados, 1440/768/390/320 px):

| Elemento (fila inactiva) | Antes | Después | Fila activa (referencia) |
|---|---|---|---|
| Ver / Editar (texto) | 1.9:1 | 4.34:1 | 4.34:1 |
| «⋯» (icono del disparador) | 1.9:1 | 4.34:1 | 4.34:1 |
| Nombre | 2.57:1 | 4.34:1 (`--color-text-secondary`) | 9.45:1 |
| Identificación / estado «Inactivo» | 1.9:1 | 4.34:1 | 4.34 / 4.88:1 |

Defecto confirmado (texto < 4.5:1 y controles < 3:1). Corrección acotada a Clientes en
`masterdata-pages.css`: `.md-customer-list .prd-row--inactive { opacity: 1 }` y el nombre con
`--color-text-secondary`. La señal de inactivo se conserva (nombre atenuado + «Inactivo» con
punto gris); las acciones quedan con el mismo contraste que en filas activas. La regla global y
Proveedores no cambian. A 320/390/768/1440 px: sin scroll horizontal ni celdas recortadas; menú
de la fila inactiva dentro del viewport (Empresa, Clasificación, Activar cliente), foco inicial
en el primer ítem y retorno al disparador con Escape.

## Fuera de alcance / no realizado

- Sin cambios de código, CSS, permisos ni SSOT. STATUS y WORK_QUEUE no se actualizan hasta que
  el propietario apruebe un bloque (el árbol debe quedar limpio salvo este documento).
- Sin suites generales ni SRI; sin pruebas ejecutadas (auditoría documental + capturas).
