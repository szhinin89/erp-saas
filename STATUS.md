# Project Status

## B-02 — pruebas obsoletas de Compras actualizadas — CLOSED (2026-11-06)

Bloqueante B-02 de la auditoría de lanzamiento. Compras V1 **continúa CLOSED**
([DOC-CLEANUP-COMPRAS-01](#doc-cleanup-compras-01--compras-v1-aprobado-y-closed-2026-11-03)): los seis
fallos deterministas eran expectativas anteriores a decisiones aprobadas, no defectos funcionales.
Reproducción previa en `4ab84b0ba`: 69/75. `purchaseLinePresentation.test.ts` (5) pasa a esperar la regla
vigente de [Compras § 2](docs/architecture/purchases.md#2-borrador) (`a55057a9c`): editar cantidad,
costo o presentación de una línea XML cambia la base operativa y conserva IVA/ICE/IRBPNR fiscales del
XML. `PurchaseFormFeedback.test.tsx` (1) expande «Detalle» antes de buscar el aviso de equivalencia en
unidad base (detalle contraído, 2026-10-29). En `PurchasesPage.tsx` solo se corrige el comentario que
describía el recálculo anterior. Sin cambios de lógica, impuestos, backend, API, contabilidad ni diseño.

Validación agente: Vitest focal 139/139 en 4 archivos (`purchaseLinePresentation`,
`PurchaseFormFeedback`, `purchaseCalc`, `PurchaseLineCard.compact`); `tsc -b`; ESLint focal (solo el
aviso `max-lines` existente de `PurchasesPage.tsx`); `architecture:check` (0 violaciones nuevas),
guardrail oficial `-SkipFrontendChunk` y `git diff --check` PASS. Sin SRI ni datos reales.

Cierre (2026-11-06): commit `db6d33d32` publicado en `main`; CI #1108 SUCCESS; suite frontend
completa de Vitest sobre `db6d33d32` (el CI frontend no ejecuta Vitest) 3.699 PASS, 0 FAIL, 0 SKIP
en 363 archivos; focales 139/139; TypeScript, ESLint, controles de arquitectura y documentación
PASS. Las seis pruebas obsoletas quedaron actualizadas sin cambiar la lógica funcional. Compras
V1 continúa CLOSED y sus reglas no cambian.

## UIUX-GLOBAL-ACTIONS-CLIENTS-01 — IMPLEMENTADO; PENDIENTE DE GATE DEL PROPIETARIO Y CONSOLIDACIÓN GIT/CI (2026-11-06)

Bloque 2 de [UIUX-GLOBAL-ACTIONS-AUDIT-01](docs/agent/UIUX-GLOBAL-ACTIONS-AUDIT-01.md#11-seguimiento-uiux-global-actions-clients-01-bloque-2--clientes-2026-11-06):
la fila de Clientes muestra Ver y Editar (con permiso) y agrupa Empresa, Clasificación, Agregar
como proveedor y Activar/Desactivar en «Más acciones» (`MasterDataCustomerListActions`, `portal`
solo en este menú). Permisos, `canAssignAsSupplier`, callbacks, confirmaciones y estados de
guardado se conservan; un usuario con solo `configure-company` accede a Empresa; sin ítems no hay
menú. Responsive por clase de columna (320–1440 px sin scroll horizontal), foco devuelto al
disparador tras cerrar menú, modal o confirmación. Resuelve AA-D2 («++ Proveedor») y AA-D3.
Contrato en [Frontend § Listado de Clientes](docs/architecture/frontend.md#listado-de-clientes).
Sin cambios en backend, endpoints, permisos, reglas de negocio, estilos globales, Proveedores ni
Compras; sin `ZHRowActions`.

Validación agente: Vitest focal 107/107 en 8 archivos (acciones de Clientes/Proveedores, lista,
páginas de Clientes y Proveedores, `ZHLineAction`) e i18n 31/31; Compras `PurchaseLineCard` y
`ProductPicker` PASS; `PurchaseFormFeedback` 1 fallo preexistente, reproducido en `HEAD` limpio.
`tsc -b`, ESLint focal, `architecture:check` (0 violaciones nuevas, `i18n-keys` PASS), guardrail
oficial `-SkipFrontendChunk` y `git diff --check` PASS. Capturas con API simulada a 320, 390, 640,
641, 768, 769 y 1440 px. Sin suites generales ni SRI. Sin staging/commit/push.

Revisión visual final (aprobación técnica recibida): en filas inactivas la opacidad compartida
dejaba Ver/Editar/«⋯» en 1.9:1; corrección acotada a Clientes (opacidad 1 y nombre con
`--color-text-secondary`) los deja en 4.34:1, igual que las filas activas, conservando la señal
de inactivo. Regla global y Proveedores sin cambios. Revalidado a 320/390/768/1440 px sin scroll,
recortes ni pérdida de foco. `PurchaseFormFeedback` mantiene su fallo preexistente (no tocado).

## FUNC-ITEM-IMAGE-REACTIVATION-01 — ETAPA 2 FRONTEND IMPLEMENTADA; PENDIENTE DE GATE DEL PROPIETARIO Y CONSOLIDACIÓN GIT/CI (2026-11-06)

Filtro, estado y activación de imágenes conectados al endpoint existente; contrato en
[Ítems](docs/architecture/items.md#imágenes-estado-y-recuperación-de-registros).
Se extrae únicamente ImagesSection del archivo de colecciones que había alcanzado su techo
PR-7; reutiliza los componentes globales sin cambiar las otras secciones. Sin cambios en
backend, migraciones, permisos ni almacenamiento. No garantiza la existencia física del archivo.
Validación agente: Vitest focal 83/83 en 9 archivos (sección y hook de imágenes, service,
traducciones, regresión de desactivación/empaques, formulario real y página de Ítems);
TypeScript `tsc -b`, ESLint focal, build, `architecture:check` (147 pruebas de guardrails,
0 nuevas violaciones), guardrail PowerShell oficial `-SkipFrontendChunk`, documentación y
`git diff --check` PASS. Build conserva avisos de chunk grande e import estático/dinámico;
el reporte arquitectónico global conserva score 19/100 (critical), con gate PASS.
Sin suites funcionales generales ni SRI; sin nuevas pruebas backend/PostgreSQL en esta etapa.
Flujo completo NO CLOSED: pendiente de gate del propietario, revisión y consolidación Git/CI
del frontend. Sin staging/commit/push.

## FUNC-ITEM-IMAGE-REACTIVATION-01 — ETAPA 1 BACKEND APROBADA TÉCNICAMENTE; PENDIENTE DE CONSOLIDACIÓN GIT/CI (2026-11-06)

Endpoint y coordinación transaccional de reactivación/desactivación/reemplazo implementados.
Contrato único en [Ítems](docs/architecture/items.md#imágenes-estado-y-recuperación-de-registros).
Sin migraciones, índice nuevo, cambios históricos ni frontend. Validación agente: dominio 2/2,
Application 5/5, contrato API/ruta/permisos 4/4, arquitectura .NET 145/145. PostgreSQL 16 real
en Testcontainers, esquema creado con EnsureCreated (sin ejecutar migraciones), datos ficticios:
11/11 PASS — activación simultánea, carreras con desactivación/reemplazo, dos reemplazos de
colección vacía, espera del lock observada en pg_stat_activity para los tres handlers,
independencia entre productos, rollback tras guardado y fallo de mapeo, no-op con xmin y
auditoría intactos, principal vigente, pertenencia y scope tenant/company. Compilación API sin
errores. `architecture:check` PASS (147 pruebas de guardrails, 0 nuevas violaciones), guardrail
oficial PowerShell `-SkipFrontendChunk`, documentación y `git diff --check` PASS. El reporte
global conserva score 19/100 (critical); el gate de nuevas violaciones pasa. Sin staging/commit/push.
Gate PostgreSQL real del propietario: 11/11 PASS, cero omitidas, exit code 0; aprobación
técnica recibida el 2026-11-06. No se repitieron las pruebas para preparar la consolidación.
No CLOSED: pendiente de staging selectivo y commit del propietario y consolidación Git/CI.

## FUNC-DEACTIVATION-CONFIRM-01 — CORRECCIÓN TÉCNICA APROBADA; PENDIENTE DE CONSOLIDACIÓN GIT/CI (2026-11-06)

Corrige AA-D5 de [UIUX-GLOBAL-ACTIONS-AUDIT-01](docs/agent/UIUX-GLOBAL-ACTIONS-AUDIT-01.md#10-seguimiento-func-deactivation-confirm-01-aa-d5-2026-11-06):
Desactivar en Marcas/Grupos/Definiciones de atributo (`useCatalogCrud`), Tipos de ítem, Cajas y
Variantes/Imágenes de ítem ahora pide `message.confirm` con el registro y la consecuencia;
cancelar no llama al backend, un doble clic produce una sola petición y los botones de estado se
deshabilitan mientras está pendiente. Permisos, endpoints, validaciones y mensajes previos se
conservan. Sin cambios en backend, APIs, permisos, `ZHRowActions` ni estilos. Textos nuevos
internacionalizados (es/en, claves `*.deactivate.confirm.*`). Por decisión del propietario,
**Activar permanece sin confirmación** en estas rutas; Sucursales, Categorías y otros módulos no
se tocan.

Seguimiento independiente: **FUNC-ITEM-IMAGE-REACTIVATION-01**, backend aprobado técnicamente;
frontend de recuperación implementado en Etapa 2, con validación y consolidación pendientes.
La confirmación actual explica que se puede recuperar como secundaria desde Inactivas.

Validación agente: Vitest focal (5 archivos nuevos + catálogos, detalle de ítem, Tipos de ítem,
Cajas, páginas/formulario de Ítems y `ItemEditorModal.update`; 16 archivos) 124/124; `tsc -b`;
ESLint focal; `architecture:check` (0 violaciones nuevas, `i18n-keys` PASS), guardrail oficial
`-SkipFrontendChunk` y `git diff --check` PASS. Datos ficticios.

## FUNC-PARTNER-ROLE-01 — CORRECCIÓN TÉCNICA APROBADA; PENDIENTE DE CONSOLIDACIÓN GIT/CI (2026-11-05)

Corrige AA-D1 de [UIUX-GLOBAL-ACTIONS-AUDIT-01](docs/agent/UIUX-GLOBAL-ACTIONS-AUDIT-01.md):
«+ Proveedor» (Clientes, `MasterDataPartnerListTab`) y «Asignar como cliente» (Proveedores,
`MasterDataSupplierListActions`) se ofrecían solo según `masterdata.businesspartners.update`,
sin leer `canAssignAsSupplier`/`canAssignAsCustomer` que `BusinessPartnerSummaryDto` ya deriva
(activo y sin el rol activo, espejo de `AssignBusinessPartnerRoleHandler`). Ahora ambas acciones
exigen además el flag del backend y se ocultan si no aplica; el POST sigue siendo la protección
definitiva. Sin cambios en backend, APIs, permisos, reglas comerciales, etiquetas ni apariencia;
«++ Proveedor» (AA-D2), `ZHRowActions` y AA-D3..AA-D9 siguen pendientes.

Validación agente: Vitest focal (lista de socios, acciones de proveedor, páginas de Clientes y
Proveedores, clasificación, configuración SRI) 70/70 — socio activo sin el rol contrario, con el
rol, inactivo, con/sin permiso, error del backend sin falso refresco y demás acciones intactas;
`tsc -b`; ESLint focal; `architecture:check` (0 violaciones nuevas), guardrail oficial
`-SkipFrontendChunk` y `git diff --check` PASS. Sin operaciones sobre datos reales.

## UIUX-GLOBAL-KPI-DEFAULT-01 — APROBADO VISUAL Y TÉCNICAMENTE; PENDIENTE DE CONSOLIDACIÓN GIT/CI (2026-11-05)

Aprobación visual del propietario registrada el 2026-11-05: Proveedores a 960 px con tres KPI en
una fila; Clientes a 390 px con KPI horizontales compactos; Stock a 540 px con KPI compactos de
82 px; Dashboard conserva la variante vertical explícita; grilla global adaptativa aprobada.
Pendiente de staging selectivo, commit y CI.

Bloque 3 de [UIUX-GLOBAL-ARCHITECTURE-02](docs/agent/UIUX-GLOBAL-ARCHITECTURE-02.md): el KPI
horizontal compacto pasa a ser el default de `ReportKpiCard` y el grid `.pg-kpis` adapta columnas
a la cantidad (1 → ancho completo; 2 y 3 → columnas iguales sin columna vacía, apilan a 480/640 px;
4 y 5+ sin cambio); contrato en
[Frontend](docs/architecture/frontend.md#kpi-compactos-predeterminados-uiux-global-kpi-default-01).
Excepción vertical explícita: `DashboardPage` (badge, `trend` y `sub` condicionales). Se retiran
los overrides locales de `StockReportPage.css`, `.prd-kpi-grid` (sin otros consumidores),
`.prd-resumen`/`.md-supplier-summary` y el `min-height: 0` redundante del Monitor SRI. El Resumen de
Clientes y Proveedores usa `.pg-kpis` y el KPI compacto; Clientes conserva actividad y acciones
rápidas (su retirada queda fuera). Sin cambios en encabezados, pestañas, lógica, permisos, APIs ni
backend.

Validación agente: Vitest focal (ReportKpiCard, Resumen Clientes/Proveedores, Stock visual,
Dashboard, Plan de cuentas, Monitor SRI, Ítems) 84/84; `tsc -b`; ESLint focal; `architecture:check`,
guardrail oficial `-SkipFrontendChunk` (techo PR-7 de `ReportPageTemplate.tsx` respetado), Stack
Audit y `git diff --check` PASS. Visual en AppLayout real con API simulada (sin backend ni
credenciales), 13 pantallas a 390/540/768/960/1440 px antes/después
(`tmp/uiux-global-kpi-default-01/capturas.html`, evidencia local temporal): sin columnas vacías
(antes 236–356 px libres con 3 KPI), altura de KPI simples 112–122 → 82 px, sin overflow ni
recortes; Dashboard, Plan de cuentas (6), Bodegas (4) y la sección secundaria del Monitor SRI con
geometría idéntica. Proveedores pasa de 3/2/1 columnas (cortes 1024/580 px) a 3/1 (corte
640 px). Stock conserva su distribución (una columna hasta 640 px, ya la aplicaba su override
local); solo cambia la altura de los KPI. Corrige la afirmación previa de un paso de 2+1 a 1.

Manifiesto `tmp/uiux-global-standard-01-preservation.json` (no eliminado): SHA-256 de 17 archivos
del conjunto UIUX-SUPPLIERS/FINAL-REVIEW (incluidos los dos reportes generados excluidos del
commit), tomado como línea base de preservación durante la auditoría UIUX-GLOBAL-STANDARD-01 para
demostrar que no se alteraban. Hoy 9 coinciden y 8 difieren por commits posteriores
(`d7b07f6c8` en adelante); ya no protege trabajo sin commit. Eliminación pendiente de decisión
del propietario.

## UIUX-GLOBAL-TABS-DEFAULT-01 — APROBADO VISUAL Y TÉCNICAMENTE; PENDIENTE DE CONSOLIDACIÓN GIT/CI (2026-11-05)

Aprobación visual final del propietario registrada el 2026-11-05 (pestañas del detalle de
Sucursal Principal a 393 px). Pendiente de staging selectivo, commit y CI.

Bloque 2 de [UIUX-GLOBAL-ARCHITECTURE-02](docs/agent/UIUX-GLOBAL-ARCHITECTURE-02.md): el patrón
responsive de tres pestañas pasa a ser el default de `ZHTabBar` (`variant?: "auto" | "standard"`,
default `auto`); contrato en
[Frontend](docs/architecture/frontend.md#pestañas-responsive-automáticas-uiux-global-tabs-default-01).
Se retira `"responsive-three"`/`"default"` y las props explícitas de Proveedores y Clientes. Ítems
añade `mobileLabel` «Nuevo» (`items.tabs.nuevoShort`, es/en); `BranchDetailPage` migra sus botones
locales `prd-tabs` a `ZHTabBar` («Secciones de la sucursal»). Solicitudes de efectivo (1–3 según
permisos) y Listas de precios (3 ↔ 6 al editar) adoptan el automático sin exclusión: el cambio de
cantidad conserva nodo, foco y selección, y a 320 px la barra de tres dejaba pestañas fuera de
vista. Exclusión `standard` disponible y probada; consumidores: ninguno. Sin cambios en tokens,
breakpoints, KPI, tablas, encabezados, permisos, rutas, APIs ni backend.

Validación agente: Vitest focal (ZHTabBar, Sucursal, Ítems, Solicitudes de efectivo, Clientes,
Proveedores ×2, Listas de precios) 76/76; `tsc -b`; ESLint focal sin errores (aviso `max-lines`
preexistente de Proveedores); `architecture:check`, guardrail oficial `-SkipFrontendChunk`,
Stack Audit (`verify-stack-allowlist.ps1` sobre el árbol de trabajo) y `git diff --check` PASS. Visual en AppLayout real con API simulada (sin backend ni
credenciales) a 320/390/480/481/768/1440 px, antes/después
(`tmp/uiux-global-tabs-default-01/capturas.html`, ya no conservada; se conservan las capturas
PNG en `shots/`): Proveedores y Clientes con geometría idéntica
y píxeles iguales salvo antialiasing; las barras de tres pestañas quedan en una fila, sin overflow,
48 px hasta 480 px. Sin suites generales, staging, commit ni push.

Decisiones del propietario (2026-11-05): `auto` se mantiene como default; `standard` se conserva
como exclusión explícita sin consumidores; Listas de precios sigue en automático. Ajuste final
autorizado: en Detalle de sucursal a 320 px «Establecimientos» y «Configuraciones» se partían a
mitad de palabra; ahora usan `mobileLabel` «Establ.» y «Config.»
(`branches.detail.tabs.establishmentsShort` / `configurationsShort`; inglés «Estab.»/«Config.»),
con nombres accesibles y etiquetas de escritorio completos. Sin cambios en otras pantallas ni
en estilos globales. Vitest focal (ZHTabBar, Sucursal) 21/21, `tsc -b`, ESLint focal,
`architecture:check`, guardrail oficial, Stack Audit y `git diff --check` PASS. Capturas
finales (`tmp/uiux-global-tabs-default-01/capturas-final.html`, ya no conservada): 320, 360, 390 y 480 px con
las tres pestañas completas en una línea, sin recorte ni overflow, 48 px; 481, 768 y 1440 px
con etiquetas completas, píxeles idénticos a la entrega previa. El desborde de Listas de
precios con seis pestañas se registra aparte (UIUX-PRICELISTS-TABS-OVERFLOW).

## UIUX-PRICELISTS-TABS-OVERFLOW — REGISTRADO, SIN CORREGIR (2026-11-05)

Hallazgo preexistente detectado en la validación de UIUX-GLOBAL-TABS-DEFAULT-01, fuera de su
alcance: en Listas de precios (`/products/pricing`, `PriceListsPage`) al editar una lista la
barra tiene seis pestañas (Resumen, Listado, Editar Lista, Productos de la lista, Excepciones,
Clientes de la lista) y desborda su contenedor de 320 a 768 px: las últimas quedan fuera de
vista sin scroll visible (la página no desborda). Idéntico antes y después del cambio
(evidencia `tmp/uiux-global-tabs-default-01/shots/{before,after}/pricelists-edit-*.png`); a
1440 px cabe. Con tres pestañas (sin editar) el patrón automático lo resuelve. No afecta datos,
permisos ni navegación por teclado. Pendiente de decisión y alcance propios del propietario.

## UIUX-PERMISSIONS-HEADER-390 — REGISTRADO, SIN CORREGIR (2026-11-05)

Hallazgo preexistente detectado en la validación de UIUX-GLOBAL-HEADER-DEFAULT-01, fuera de su
alcance: en Asignación de permisos (`/admin/permissions`, `PermissionsAssignmentPage`) a 390 px la
toolbar de la acción del encabezado (selector «Perfil» y búsqueda, `.access-pa-header-toolbar`)
no baja de línea y comprime la columna del título, que queda recortado («Asignació…»). Se
reproduce igual con el diseño `stacked` original (evidencia local temporal en
`tmp/uiux-global-header-default-01/app/shots/stacked-check/`). No afecta datos ni permisos.
Pendiente de decisión y alcance propios del propietario; no autorizado ni corregido.

## UIUX-GLOBAL-HEADER-DEFAULT-01 — CERRADO: commit e1b34d4d6, CI #1098 SUCCESS (2026-11-05)

Cierre: aprobación visual del propietario y CI #1098 SUCCESS sobre `e1b34d4d6` (HEAD publicado en `origin/main`; resultado informado por el propietario).

Aprobación visual final del propietario registrada el 2026-11-05: compacto predeterminado,
kicker no renderizado, acciones y responsive conservados. Bloque 1 de [UIUX-GLOBAL-ARCHITECTURE-02](docs/agent/UIUX-GLOBAL-ARCHITECTURE-02.md): el
encabezado compacto pasa a ser el default de `ZHScreenHeading` para los 102 sitios
`PageShell`/`ErpPageTemplate` (84 archivos) y `NoAccessPage`; contrato en
[Frontend](docs/architecture/frontend.md#encabezado-compacto-predeterminado-uiux-global-header-default-01).
Variantes `compact` | `stacked`; `"default"` se retira porque ningún consumidor productivo lo
usaba (solo una prueba). Cambian el modificador `.zh-screen-heading--compact` (sin flex en el
`<header>`; separación de 4 px cuando la descripción envuelve desde 769 px), el kicker (no
renderizado; ver corrección final) y la retirada de las props redundantes de Proveedores y Clientes. Excepciones `stacked`: ninguna.
Sin cambios en textos, tokens, breakpoints, KPI, tablas, pestañas, permisos, rutas, APIs ni backend.

Validación agente: Vitest focal (PageShell, AppLayout, Clientes, Proveedores) 37/37; `tsc -b`;
ESLint focal sin errores (aviso `max-lines` preexistente de Proveedores); `architecture:check`,
guardrail oficial `-SkipFrontendChunk`, Stack Audit y `git diff --check` PASS. Visual:
480 capturas antes/después de 30 pantallas (perfiles P1–P9) a 320/360/390/480/768/769/1024/1440 px
(`tmp/uiux-global-header-default-01/capturas.html`, ya no conservada; harness con componentes oficiales, props
copiados de cada pantalla y datos sintéticos, sin AppLayout ni sesión). Proveedores y Clientes:
geometría del encabezado idéntica en los 8 anchos (Δ ≤ 0,1 px, igual al ruido entre corridas) y
píxeles iguales a las capturas aprobadas salvo antialiasing. Sin incidencias nuevas; desborde
horizontal preexistente e idéntico antes/después en `PurchaseCreditNoteListPage` (320 px),
`ExpenseDocumentFormPage` (320–390 px) y `SupplierPaymentDetailPage` (320 px), medido sin el
padding del shell. Sin suites generales/SRI, staging, commit ni push.

Corrección final del propietario: `ZHScreenHeading` no renderiza el kicker (ni elemento ni
texto en el DOM) y se elimina la regla `.zh-screen-heading-kicker` de `zh-ui.css`; la prop
`kicker` se conserva temporalmente en la interfaz pública, sin efecto, para no modificar
consumidores. Sin dependencias funcionales del elemento (solo el componente, su CSS y pruebas).
Revisados los 69 kickers: todos son contexto de módulo o listado padre, sin información
exclusiva; sin excepciones. Validación en AppLayout real
(frontend real con API simulada, sesión Admin sintética, sin backend ni credenciales) de Clientes,
Proveedores, Asignación de permisos y Monitor de Documentos Electrónicos a 390 y 1440 px
(`tmp/uiux-global-header-default-01/app/capturas-app.html`, ya no conservada; quedan el script y
las capturas PNG en `app/`): solo desaparece el kicker; título,
subtítulo, barra azul y acciones conservan su posición relativa (Δ ≤ 0,1 px) y suben 28 px;
Clientes y Proveedores sin cambio (Δ 0,0 px); sin overflow. Hallazgo preexistente fuera de alcance:
en Asignación de permisos a 390 px la toolbar de la acción no baja de línea y recorta el título,
igual con el diseño `stacked` original. Paso de «oculto» a «no renderizado» verificado en las
mismas cuatro pantallas y anchos: geometría Δ 0,0 px y píxeles idénticos (diff 0) frente a la
presentación aprobada. Vitest focal (PageShell, AppLayout, Clientes, Proveedores, Saldos a favor,
Flujos documentales) 56/56, `tsc -b`, ESLint focal, `architecture:check`, guardrail oficial,
Stack Audit y `git diff --check` PASS.

## UIUX-GLOBAL-ARCHITECTURE-02 — PLAN APROBADO; BLOQUE 1 CERRADO (CI #1098); BLOQUE 2 APROBADO, PENDIENTE DE GIT/CI (2026-11-05)

Planificación para aplicar por defecto las mejoras aprobadas del DS (encabezado compacto,
tres pestañas responsive, KPI simples horizontales, identidad y acciones de Clientes), con
decisiones del propietario y matriz por consumidor en
[UIUX-GLOBAL-ARCHITECTURE-02](docs/agent/UIUX-GLOBAL-ARCHITECTURE-02.md). Encabezado verificado:
102 sitios en 84 archivos, más 23 archivos solo vía `NoAccessPage`. Bloque 1 cerrado en
UIUX-GLOBAL-HEADER-DEFAULT-01 (commit `e1b34d4d6`, CI #1098 SUCCESS); bloque 2 en
UIUX-GLOBAL-TABS-DEFAULT-01 (aprobado visual y técnicamente, pendiente de consolidación Git/CI);
bloque 3 en UIUX-GLOBAL-KPI-DEFAULT-01 (aprobado visual y técnicamente, pendiente de consolidación
Git/CI); bloques 4–5
pendientes. UIUX-CUSTOMERS-02 y los pendientes
de Clasificación y CI se conservan en sus propias entradas.

## UIUX-CUSTOMERS-02 — CERRADO: commit e1b34d4d6, CI #1098 SUCCESS (2026-11-05)

Cierre: aprobación visual del propietario y CI #1098 SUCCESS sobre `e1b34d4d6` (HEAD publicado en `origin/main`; resultado informado por el propietario).

Aprobación visual del propietario registrada el 2026-11-05. Su `headingVariant="compact"`
explícito se retiró en UIUX-GLOBAL-HEADER-DEFAULT-01 (el compacto es ahora el default);
encabezado verificado idéntico. Su `variant="responsive-three"` se retiró en
UIUX-GLOBAL-TABS-DEFAULT-01 (patrón automático con tres pestañas); píxeles idénticos. Registro original:


Clientes adopta `headingVariant="compact"` sin kicker y `ZHTabBar variant="responsive-three"`
con `masterdata.customers.tabs.nuevoShort`; contrato en
[Frontend](docs/architecture/frontend.md#encabezado-y-pestañas-de-clientes). Sin cambios
en componentes/CSS globales, Proveedores, Listado, Resumen, formulario, permisos ni backend.
Pruebas focales Clientes/Proveedores/ZHTabBar 57/57 PASS; `tsc -b`, lint focal,
`architecture:check` y guardrail oficial PASS. Capturas antes/después/Proveedores en
evidencia local temporal (`tmp/uiux-customers-02/capturas.html`, ya no conservada): 320, 360, 390, 480,
768 y 1440 px, tres pestañas visibles, sin overflow, 48 px hasta 480 px; harness con
componentes reales y datos sintéticos, sin sesión autenticada ni backend.

## CI-1096-FIX — CERRADO: commit 7bd7664e2, cubierto por CI #1098 SUCCESS (2026-11-05)

CI #1096 (commit `dd10a0f9b`) falló en Stack Audit: `tools/architecture/check-staged-guardrails.ps1`
no figuraba en `scriptsAllowed`. Registrado en `scripts/stack-allowlist.json` (orden alfabético),
el mapa de [DEVELOPMENT](docs/DEVELOPMENT.md#scripts-powershell-canónicos) y `CONTEXT.md`, sin
excepciones genéricas. El control de staging ahora ejecuta también Stack Audit
(`verify-stack-allowlist.ps1 -RepoRoot <snapshot>`) además de Architecture guardrails; el
snapshot solo contiene archivos versionados, como el checkout de CI. No cubre el chunk Vite ni
otros jobs. Sin cambios en Clientes, límites ni reglas.

Validación agente: ambos controles oficiales PASS sobre el árbol; sobre el índice de `dd10a0f9b`
el control reproduce CI #1096 (Stack Audit FAIL, Architecture PASS) sin alterar índice ni árbol.
En clon temporal: corrección preparada PASS; `.ps1` no autorizado preparado FAIL, también si se
borra del árbol (pwsh 7 y Windows PowerShell 5.1). Cierre: gate del propietario con commit
`7bd7664e2`; CI #1098 SUCCESS sobre `e1b34d4d6`, que lo incluye (informado por el propietario).

## CI-1095-FIX + PRECOMMIT-GUARD — CERRADO: commit dd10a0f9b, cubierto por CI #1098 SUCCESS (2026-11-05)

CI #1095 falló en PR-7: `MasterDataCustomersPage.tsx` del commit `9d4308935` tiene 511 líneas
no vacías (536 físicas; el guardrail cuenta con `Measure-Object -Line`). Reproducido en local
con el mismo comando de CI (exit 1, única violación). Corrección: extracción literal del modal
de Clasificación (con `PersistedValueOption`) a
`modules/masterData/components/MasterDataCustomerConfigModal.tsx`; solo cambia el nombre
exportado y su uso (diff verificado). Página en 288 líneas contadas por PR-7, sin excepción
ni cambio del límite; diseño, permisos y los siete campos intactos.

Causa de la discrepancia: los gates locales anteriores ejecutaron `npm run architecture:check`,
que no corre `check-architecture-guardrails.ps1` sobre el repositorio (solo prueba el script con
fixtures); CI lo ejecuta en un paso aparte. El contenido commiteado era idéntico al probado.
Prevención: `tools/architecture/check-staged-guardrails.ps1` ejecuta el guardrail oficial sobre
el índice materializado en un temporal; documentado en
[enforcement](docs/architecture/enforcement.md#tests-pre-merge). Sin hook instalado.

Validación agente: Vitest focal 38/38, `tsc -b`, ESLint focal, guardrail oficial completo
`-SkipFrontendChunk` PASS, architecture:check y check documental PASS, git diff --check PASS.
Caso controlado en un clon temporal (pwsh 7 y Windows PowerShell 5.1): HEAD de CI #1095 falla,
la corrección preparada pasa, un `.tsx` preparado de 501 líneas falla aunque se borre del árbol,
retirarlo vuelve a pasar y un guardrail ausente del índice falla cerrado (exit 2). Cierre: gate
del propietario con commit `dd10a0f9b`; su Stack Audit (CI #1096) se corrigió en CI-1096-FIX y
CI #1098 SUCCESS sobre `e1b34d4d6` los incluye (informado por el propietario). Sigue abierta,
fuera del cierre, la decisión sobre instalar un hook.

## BUG-CUSTOMERS-CLASSIFICATION-01 — CERRADO: commit 9d4308935, cubierto por CI #1098 SUCCESS (2026-11-05)

BUG REAL (integridad de datos): Listado de Clientes → «Clasificación» abría con los siete
campos vacíos porque `openCustomerConfig` descartaba `customerConfig` leído de GET roles, y
PATCH `customer-config` reemplaza la configuración completa; «Guardar» dejaba en null
categoría, segmento, zona, rating, fidelización, formato y clasificación. Corrección solo
frontend: precarga desde la configuración persistida, error visible y sin ventana si falla la
lectura o no hay rol cliente activo, guardado bloqueado con catálogos cargando o en error y
valor persistido fuera del catálogo activo conservado como opción. Sin cambios de backend,
contrato API, semántica de reemplazo, permisos ni diseño.

Pruebas agente: Vitest 38/38 (11 nuevas; 10 fallaban antes del fix); backend Application
42/42 (3 nuevas que fijan el contrato de reemplazo) y Architecture.Tests 145/145;
`tsc -b`, ESLint focal, architecture:check (0 violaciones nuevas) y git diff --check PASS.
Navegador real (Chromium, página real, servicios HTTP reales, backend simulado en memoria con
datos sintéticos, sin base de datos): 23/23 PASS en carga, guardar sin cambios, cambio de un
campo, limpieza deliberada, errores de lectura/guardado, valores fuera del catálogo y diseño
idéntico a HEAD (1440/390 px). No acredita persistencia PostgreSQL ni sesión backend real.

Pendientes: el validador backend solo exime sin cambios a CustomerClassification; los otros
cinco campos de catálogo con valor desactivado responden 422 al reguardar (sin pérdida; el
usuario debe elegir un valor activo); decisión del propietario. La etiqueta «(fuera del
catálogo activo)» se recorta en el ancho del select. Sin control de concurrencia (último
guardado gana). Cierre: gate del propietario con commit `9d4308935` (el exceso de líneas de
CI #1095 se corrigió en CI-1095-FIX); CI #1098 SUCCESS sobre `e1b34d4d6`, que lo incluye
(informado por el propietario). Los pendientes anteriores quedan fuera del cierre, sin autorizar.

## UIUX-FINAL-REVIEW-01 — CERRADO: commits d7b07f6c8 + 13b8707a2, CI #1094 SUCCESS (2026-11-05)

Revisión final del diff acumulado UIUX-SUPPLIERS-01/02/03, UIUX-GLOBAL-HEADER-01 y
UIUX-GLOBAL-TABS-01, sin cambios de implementación ni nuevas modificaciones visuales.
Inventario selectivo: 23 archivos (14 implementación, 6 pruebas, 3 documentación).
Excluir del commit UI/UX `docs/ci/PLATFORM_GUARD_REPORT.md`,
`docs/future-platform/API_USAGE_GRAPH.json` y todo `tmp/`; diferencias revisadas,
sin restauraciones ni eliminaciones. No se detectan otros cambios productivos ajenos.
El inventario se integró en el commit `d7b07f6c8` (23 archivos; `git show --stat d7b07f6c8`)
y la división de pruebas exigida por el guardrail en `13b8707a2`. El README, la lista de
archivos y las capturas locales de `tmp/uiux-final-review-01/` y de las carpetas `tmp/uiux-*`
citadas abajo ya no existen en el árbol de trabajo (verificado 2026-11-05) y no se conservó
copia; la evidencia persistente son esos commits, las pruebas versionadas, los contratos en
[Frontend](docs/architecture/frontend.md) y CI #1094 SUCCESS (dato del propietario).

Evidencia anterior vigente: menú/permisos y confirmaciones 85/85; resumen de consulta
42/42; encabezado/integración 51/51; tabs/navegación 32/32. Son ejecuciones superpuestas,
no se suman como pruebas únicas. Verificados hashes de los archivos originales de
Proveedores y de las 20 comparaciones de encabezado y 42 de pestañas. TypeScript/build,
lint y arquitectura de la última implementación siguen vigentes (147/147 guardrails,
cero violaciones nuevas); no se repiten pruebas válidas. Gate documental y diff check
verificados en esta revisión; evidencia local, con datos sintéticos, no sesión backend real.

Cierre: gate y aprobación visual del propietario completados; commit autorizado y CI #1094
SUCCESS sobre `13b8707a2`. Quedan fuera del cierre, como pendientes, los recortes internos
del formulario a 320 px, flechas/roving tabindex y asociación tab-panel, y las mejoras de
otros módulos no implementadas.


## UIUX-GLOBAL-TABS-01 — CERRADO con UIUX-FINAL-REVIEW-01 (CI #1094 SUCCESS) (2026-11-05)

Variante opcional responsive-three en ZHTabBar; solo Proveedores la solicita.
Contrato vigente en [Frontend](docs/architecture/frontend.md#pestañas-responsive-automáticas-uiux-global-tabs-default-01):
UIUX-GLOBAL-TABS-DEFAULT-01 convierte la variante en el default de `ZHTabBar` con tres pestañas.
Defaults y cantidades distintas de tres conservan su render anterior. Reglas móviles
trasladadas al DS, sin duplicación local, cambios de tokens o breakpoints globales.
UIUX-SUPPLIERS-01/02/03 y UIUX-GLOBAL-HEADER-01 preservados; formularios, permisos,
traducciones, Listado y Resumen mantienen su comportamiento.

Evidencia local temporal (`tmp/uiux-global-tabs-01/capturas.html`, ya no conservada): 320, 360, 390, 480, 768 y 1440 px,
tres estados activos de Proveedores y comparación de Clientes/Condiciones de pago reales,
además de fixtures ConfigTabsLayout y ZHTabBar de cuatro destinos. 42/42 recortes de
pestañas idénticos en píxeles y geometría antes/después, encabezados preservados y
markup predeterminado sin cambios. Fixtures sintéticos, componentes reales y fuentes
oficiales; no valida una sesión autenticada con backend. La limitación interna del
formulario a 320 px es preexistente y no se modifica.

Pruebas focales 32/32 PASS; teclado real Enter/Espacio en las tres pestañas a seis
anchos, Tab/Shift+Tab y etiquetas largas sin recorte en móvil PASS. TypeScript/build,
lint focal (0 errores, 1 warning previo por tamaño) y arquitectura PASS: 147/147
guardrails, cero violaciones nuevas. Pruebas anteriores ajenas al cambio conservan
su evidencia; no se repiten suites de encabezado, Listado ni Resumen sin cambios.
El README local con detalle y comandos de gate (`tmp/uiux-global-tabs-01/`) ya no se conserva;
limitaciones de accesibilidad vigentes en el contrato de Frontend y en UIUX-FINAL-REVIEW-01.
Cerrado con UIUX-FINAL-REVIEW-01: commit `d7b07f6c8`, CI #1094 SUCCESS.


## UIUX-GLOBAL-HEADER-01 — CERRADO con UIUX-FINAL-REVIEW-01 (CI #1094 SUCCESS) (2026-11-05)

Variante opcional de encabezado implementada en los componentes oficiales; contrato en
[Frontend](docs/architecture/frontend.md#encabezado-compacto-predeterminado-uiux-global-header-default-01).
Solo Proveedores activa compact; las reglas locales del encabezado se trasladaron al DS,
sin duplicación ni cambios de defaults, tokens o breakpoints globales. El resto de
UIUX-SUPPLIERS-01/02/03 se conserva, incluida su evidencia y pruebas previas.

Capturas antes/después en evidencia local temporal (`tmp/uiux-global-header-01/capturas.html`, ya no conservada): tres pestañas de
Proveedores y consumidores predeterminados Clientes/Condiciones de pago a 1440, 768,
390 y 320 px. Los 20 encabezados coinciden exactamente en píxeles, geometría y estilos;
los dos consumidores predeterminados conservan también su markup. Fixtures sintéticos,
componentes reales, LayoutFrame y fuentes oficiales; no es una sesión backend real.
Sin overflow de página en los casos capturados; se conservan limitaciones internas
preexistentes del formulario estrecho, fuera de alcance.

Pruebas focales 51/51 PASS; TypeScript (`tsc --noEmit` y `tsc -b`), build, lint y
arquitectura PASS (147/147 guardrails y cero violaciones nuevas). El README local con
evidencia, inventario y comandos de gate (`tmp/uiux-global-header-01/`) ya no se conserva.
El build actualiza automáticamente los timestamps de los dos reportes Platform ya
modificados antes de esta tarea. Sin suites generales/SRI.
Cerrado con UIUX-FINAL-REVIEW-01: commit `d7b07f6c8`, CI #1094 SUCCESS.


## UIUX-GLOBAL-STANDARD-01 — AUDIT READY FOR OWNER REVIEW (2026-11-05)

Auditoría documental y propuesta por bloques, sin implementación global.
[Matriz de impacto y consumidores](docs/agent/UIUX-GLOBAL-STANDARD-01.md).
UIUX-SUPPLIERS-01/02/03 conservados; su revisión visual y gates mantienen el estado
registrado abajo. Variantes y adopción pendientes de aprobación del propietario.
Sin cambios de código, componentes, estilos, pantallas, negocio, permisos, APIs ni menú.
Validación documental 1/1 PASS y git diff --check PASS; preservación SHA256 de los archivos previos verificada (salvo esta actualización de estado).
Sin suites generales/SRI, staging, commit ni push; no CLOSED.


## UIUX-SUPPLIERS-03 — CERRADO con UIUX-FINAL-REVIEW-01 (CI #1094 SUCCESS) (2026-11-05)

Ajuste local de pestañas autorizado para Proveedores; contrato en
[Frontend](docs/architecture/frontend.md#resumen-de-proveedores). Conserva los cambios
pendientes de UIUX-SUPPLIERS-01/02, encabezado, navegación y formularios.
Capturas antes/después en evidencia local temporal (`tmp/uiux-suppliers-03/capturas.html`, ya no conservada): 320, 360, 390,
480, 768 y 1440 px, componente real con datos sintéticos y fuentes oficiales.
Navegación de las tres pestañas, selección y ausencia de overflow de página/pestañas
verificadas en los seis anchos. El formulario conserva recortes internos preexistentes
a 320 px, fuera del alcance autorizado. Pruebas focales 42/42 PASS; TypeScript
(`tsc --noEmit` y `tsc -b`) PASS; lint focal sin errores y con un warning previo
por tamaño de archivo. `git diff --check` PASS. Arquitectura: 147/147 guardrails
y runner PASS, cero violaciones nuevas; los logs de esa carpeta temporal tampoco se conservan.
Cerrado con UIUX-FINAL-REVIEW-01: commit `d7b07f6c8`, CI #1094 SUCCESS.

## UIUX-SUPPLIERS-02 — CERRADO con UIUX-FINAL-REVIEW-01 (CI #1094 SUCCESS) (2026-11-05)

Resumen compacto autorizado solo para Proveedores. Causas: cuatro columnas para tres
indicadores, variante vertical con altura mínima, panel lateral de 280 px y accesos
duplicados; padding acumulado en el estado vacío. Se reutilizan `ReportKpiCard`
horizontal y `ZHCard`; CSS local, métricas y actividad de sesión conservadas. Contrato:
[Frontend](docs/architecture/frontend.md#resumen-de-proveedores).
Decisión final del propietario: Resumen exclusivamente de consulta, sin «Nuevo
proveedor», «Ver listado» ni panel de acciones rápidas. Se conservan las tres métricas
iguales y Actividad reciente compacta; la navegación se realiza desde las pestañas
Listado y Nuevo proveedor, con los permisos existentes. Clientes, encabezado, Listado,
menú contextual, formulario, backend y datos no se modifican funcionalmente.

Pruebas focales: 42/42 PASS (Resumen sin botones, métricas, permisos, navegación real entre pestañas,
actividad vacía/poblada y regresiones de Clientes y Listado); TypeScript/build y lint focal
PASS (0 errores, 1 warning de tamaño preexistente). Arquitectura: 147/147 guardrails y
runner PASS, cero violaciones nuevas; enlaces documentales y `git diff --check` PASS.
Capturas de la página real a 1440,
768 y 390 px, con fuentes oficiales y datos sintéticos: tres columnas iguales, dos
columnas y una columna respectivamente, sin desbordamiento horizontal ni alturas fijas
de tarjetas. Esas capturas eran evidencia local temporal (`tmp/uiux-suppliers-summary-readonly/`
y la previa `tmp/uiux-suppliers-summary/`), ya no conservada.
Cerrado con UIUX-FINAL-REVIEW-01: commit `d7b07f6c8`, CI #1094 SUCCESS.

## UIUX-SUPPLIERS-01 - CERRADO con UIUX-FINAL-REVIEW-01 (CI #1094 SUCCESS) (2026-11-05)

Listado de Proveedores, incluidos los ajustes visuales 1 y 2 autorizados, aislado de
Clientes mediante el rol supplier y CSS local. Contrato:
[Frontend](docs/architecture/frontend.md#listado-de-proveedores).
KEEP: ZHDataTable, operaciones, permisos, rutas, formularios, otras tabs y API.
BUG REAL: identidad invertida y duplicada, corregida solo en Proveedores. Ajuste 2:
acciones agrupadas con el menú oficial del DS. Corrección final del encabezado:
único y compacto en las tres pestañas, sin categoría y con el texto aprobado;
se elimina la condición exclusiva de Listado.
Pruebas focales: 85/85 PASS (incluye confirmaciones, permisos, teclado, menú y regresiones
de componentes compartidos); TypeScript/build y lint completo PASS (0 errores,
39 warnings). Arquitectura: 147/147 pruebas de guardrails y runner PASS, cero violaciones
nuevas; enlaces documentales y `git diff --check` PASS.
Capturas antes/después en navegador, con componentes reales, LayoutFrame y
datos sintéticos: 1440, 768 y 390 px; menú abierto dentro del viewport en los tres anchos,
sin desbordamiento de página; móvil sin scroll horizontal a 390 px.
Corrección final: 33/33 pruebas focales PASS (incluida navegación por las tres pestañas
con el mismo nodo y markup del encabezado), TypeScript/build, arquitectura
(147/147 guardrails, cero violaciones nuevas), enlaces documentales y `git diff --check`
PASS; lint focal PASS (0 errores, 1 warning de tamaño preexistente).
Seis capturas de la página real, con datos sintéticos
y las fuentes oficiales, a 1440 y 390 px: encabezado estable de 44 y 89 px respectivamente,
sin desbordamiento horizontal y con navegación intacta.
Esa evidencia y la de los ajustes anteriores eran locales y temporales
(`tmp/uiux-suppliers-heading/` y carpetas `tmp/uiux-suppliers-*`), ya no conservadas.
Cerrado con UIUX-FINAL-REVIEW-01: commit `d7b07f6c8`, CI #1094 SUCCESS.


## NAV-COVERAGE-FINAL-REVIEW-01 — READY FOR REVIEW (2026-11-04)

Revisión documental e integridad del conjunto NAV-CONSOLIDATION-A01, H1–H4, corrección CI y
SECURITY-PRECISION-POLICY-01; sin cambios funcionales. [MENU_MAP](docs/navigation/MENU_MAP.md)
sincronizado con `Kernel/Modules/*.cs` (96 NavItems, 12 módulos, sin huérfanos ni rutas
duplicadas; orden por `SortOrder`, etiquetas visibles en español): siete pantallas ya
implementadas documentadas (Costos pendientes, Solicitudes de efectivo, Motivos de movimientos,
Contabilizaciones, Modo operativo, Períodos contables, Secuencias documentales), Destinos
financieros retirado, una sola entrada `/settings/operations`, política de redirects con
`LEGACY_REDIRECT_ALIASES` (H4) y `CONTEXTUAL_ROUTES` (H1).

Verificación focal: `KernelRegistryTests` **48/48**; `PurchasesMenuVisibilityTests` +
`CompanySettingsMenuVisibilityTests` **8/8**; `CompanyPrecisionPolicyPermissionContractTests` +
`CompaniesApiSurfaceSnapshotTests` **11/11**; `CompanyPrecisionPolicyUseCasesTests` **24/24**;
manifiesto CI `backend_tests.py --group infrastructure-06 --verify-only` **PASS** (209 casos,
inventario completo, sin ejecutar pruebas); frontend (guard H1/H4, `navigationAccess`,
`AppLayout`, `src/routes`, `navLabelTaxonomy`, `configuracion/empresa`,
`configuracion/operaciones`, `caja`, pago con solicitud de efectivo) **322/322**; `tsc -b`,
ESLint (0 errores), `architecture:check` (0 violaciones nuevas), enlaces de MENU_MAP/STATUS y
`git diff --check` **PASS**.

Discrepancia pendiente (sección LOCKED, no editada): `security.md` § Security & Access Contract
V1 describe `canShow`/`has` como shim «siempre `true`» y `SessionContext.authorization.permissions`
como no consumido. El código real (desde el commit inicial 2026-07-24) verifica la clave con
fallback permisivo sin sesión/permisos cargados, y H1 (`RouteAccessGuard`), H2
(`companySettingsTabAccess`) y `AccountsReceivablePage` leen esos permisos fail-closed. Cambiar la
sección exige discusión explícita + actualización con tests de regresión: requiere decisión del
propietario/revisión arquitectónica.
Fuera de alcance: **H5 (Transportistas)**. Hallazgos sin corregir: `LEGACY_REDIRECT_PREFIXES`
abre segmentos completos (`/masterdata` deja montar `/masterdata/business-partners/:id` sin
NavItem); el guard abre todo `/treasury` con cualquier NavItem de Tesorería; los `<Navigate>`
legacy no conservan query/hash; `NavigationBuilder` no poda contenedores anidados vacíos (A01);
integraciones PostgreSQL de solicitudes de efectivo y `NavigationSyncService` no ejecutados aquí.
Sin migraciones, SRI, suites generales, staging, commit ni push. **No CLOSED**: pendiente
aprobación del propietario.

## NAV-COVERAGE-H4 — READY FOR REVIEW (2026-11-04)

Causa: `isRouteAllowed` decide por primer segmento; un redirect legacy cuyo segmento difiere del
destino y no está en `LEGACY_REDIRECT_PREFIXES` mostraba `NoAccessPage` antes de montar su
`<Navigate>`, aunque el usuario tuviera el destino (11 bloqueos reproducidos).
Cambio (solo `RouteAccessGuard.tsx`): `LEGACY_REDIRECT_ALIASES` — rutas EXACTAS (`matchPath`,
`end` + case-sensitive, sin prefijos) de redirects registrados: `/admin/users`,
`/sales/payment-methods`, `/cash`, `/cash/registers`, `/finance/credit-terms`,
`/finance/supplier-credits`, `/finance/supplier-credits/:id`, `/inventory/items`,
`/inventory/item-types`, `/settings/electronic-invoicing`, `/electronic-documents/monitor`.
Solo permite montar el redirect; el destino canónico se revalida contra el menú. Rutas,
destinos, NavItems, backend, permisos y H1 sin cambios.

Pruebas focales: `RouteAccessGuard.legacyAliases` (anti-deriva contra las rutas registradas;
por cada redirect: autorizado llega al destino, no autorizado queda bloqueado en el destino,
query/hash no eluden; 12 URLs no registradas bloqueadas) + H1 + `AppLayout` + `src/routes` +
solicitudes de efectivo **98/98**; `tsc -b` y ESLint (0 errores) **PASS**.
Limitaciones: los `<Navigate to="…">` existentes no conservan query/hash (preexistente, no se
alteraron). Hallazgo para el propietario (sin corregir): `LEGACY_REDIRECT_PREFIXES` abre
segmentos completos; `/masterdata` además deja montar la página real
`/masterdata/business-partners/:id` sin NavItem (usada por Compras y Datos maestros).
Sin migraciones, SRI, suites generales, staging, commit ni push. **No CLOSED**.

## NAV-COVERAGE-H1 — APROBACIÓN TÉCNICA (2026-11-04)

Causa: el enlace «Ver solicitud» de Pagos a proveedores abre
`/treasury/cash/funding-requests/:id`, pero `RouteAccessGuard` decide por primer segmento y el
solicitante sin permisos de Caja no tiene `/treasury` en su menú → `NoAccessPage`. Backend ya
correcto (sin cambios): `GET {id}` solo para el solicitante o `caja.funding-requests.view` en la
sucursal activa (otro caso → 404); `mine`/crear/cancelar con `supplier-payments.create`;
entregar/rechazar con `caja.funding-requests.fulfill` + control de la sesión.
Cambio (solo frontend): `CONTEXTUAL_ROUTES` + `isContextualRouteAllowed` en `RouteAccessGuard` —
ruta EXACTA de lista y detalle (`^/treasury/cash/funding-requests(/:id)?/?$`, sin prefijo) con
`supplier-payments.create` efectivo (Admin/`*` también); fail-closed sin sesión cargada.
Ownership, políticas, permisos, NavItems y contratos API sin cambios.

Pruebas focales **42/42**: `RouteAccessGuard.contextual` (solicitante abre detalle y
`?tab=mine`; 6 rutas de Tesorería no relacionadas bloqueadas; sin permiso o sin sesión
bloqueado; cajero conserva acceso por menú), `CashFundingRequestDetailPage` (+404 de solicitud
ajena sin datos ni acciones, «Volver a solicitudes»), `CashFundingRequestsPage`,
`SupplierPaymentFormPage.cashFunding` (enlace), `cashFundingRequestsRoutes`, `AppLayout`;
`tsc -b` y ESLint (0 errores) **PASS**.
Limitaciones: el 404 de solicitud ajena lo cubren las integraciones backend existentes (no
ejecutadas aquí, requieren PostgreSQL); el guard sigue abriendo todo `/treasury` a quien tenga
cualquier NavItem de Tesorería (preexistente, la API sigue siendo la autoridad).
Sin migraciones, SRI, suites generales, staging, commit ni push. **No CLOSED**: aprobación
técnica registrada; cierre pendiente del propietario.

## SECURITY-PRECISION-POLICY-01 — READY FOR REVIEW (2026-11-04)

Confirmado: `PUT /api/v1/config/precision-policy` solo heredaba el `[Authorize]` de clase
(cualquier sesión con tenant podía escribir); ni handler ni pipeline validaban permiso.
Único consumidor: `PrecisionPolicySettingsSection`, cuya UI ya exigía `erp.companies.update`,
el mismo permiso de las demás escrituras de configuración de la empresa activa.
Cambio: `[Authorize(Policy = "perm:erp.companies.update")]` solo en el PUT. Los GET (política
y metadata) siguen solo autenticados; handler, bloqueo, rangos, redondeos, aislamiento por
empresa, asignaciones y respuesta 403 estándar sin cambios. SSOT:
[data-standards](docs/architecture/data-standards.md).

Pruebas: `CompanyPrecisionPolicyPermissionContractTests` **8/8** (proveedor y handlers reales
de `perm:*`, policies del controller; sin HTTP ni BD): PUT denegado sin permisos, con
`erp.companies.view` y con ambas lecturas, permitido con `erp.companies.update`; GET y metadata
permitidos sin permisos. `CompaniesApiSurfaceSnapshotTests` **3/3**,
`CompanyPrecisionPolicyUseCasesTests` **24/24**, `architecture:check` **PASS**.
Sin migraciones, SRI, suites generales, staging, commit ni push. **No CLOSED**: pendiente
revisión del propietario.

## NAV-COVERAGE-H2 — READY FOR REVIEW (2026-11-04)

Decisión del propietario: `settings.company.view` y `erp.companies.view` siguen siendo
capacidades independientes. La entrada Datos de la empresa se muestra con cualquiera
(`Permission` CompanyView conservado + `PermissionsAnyCsv` con ambas, OR existente de
`NavigationBuilder`); cada pestaña aplica el permiso de lectura de su API.

| Pestaña | Lectura (API) | Permiso de pestaña | Edición (sin cambios) |
|---|---|---|---|
| General, Marca, Fiscal, Operación, Documentos | `GET /companies/profile*` | `erp.companies.view` | `erp.companies.update` |
| Preparación operativa | `GET /companies/operational-readiness` | `erp.companies.view` | — |
| Precisión operativa | `GET /config/precision-policy` (solo autenticado) | `erp.companies.view` (gate previo) | `erp.companies.update` en UI |
| Ventas | `GET /org-config/company/invoice-defaults` | `settings.company.view` | `erp.companies.update`; lista default `pricing.update` |

| Lectura | Menú | Pestañas |
|---|---|---|
| Solo `settings.company.view` | Visible | Ventas |
| Solo `erp.companies.view` | Visible | Las siete restantes |
| Ambos | Visible | Las ocho |
| Ninguno | Oculta | Sin acceso |

El hub no usa el fallback permisivo de `canShow`: sin sesión resuelta muestra carga,
y una lista vacía no concede pestañas (Admin y `*` ven todo). Una pestaña sin permiso
pedida por URL responde sin acceso; sin `tab` abre la primera visible. Los enlaces
`?tab=` de Readiness/apertura de caja respetan el mismo permiso. Catálogos de Ventas,
APIs, asignaciones y aislamiento por empresa sin cambios.

Pruebas focales: `CompanySettingsMenuVisibilityTests` **5/5** (filtro real del menú,
cuatro combinaciones) + H3 **3/3**; `KernelRegistryTests` **48/48**; frontend
`configuracion/empresa` + `caja/components` **108/108** y apertura de caja **10/10**;
`tsc -b`, ESLint (0 errores) y `architecture:check` **PASS**. Pruebas UI con hook real de
permisos, no autorización HTTP.

Hallazgos para el propietario (sin corregir): `PUT /config/precision-policy` solo exige
autenticación (la UI exige `erp.companies.update`); security.md describe
`canShow` como shim "siempre true", desactualizado frente al código.
Fase A y H3 preservados; sin migraciones, SRI, suites generales, staging, commit ni push.
**H2 no CLOSED**: pendiente revisión final del propietario.

NAV-COVERAGE-H3-CI-FIX: `PurchasesMenuVisibilityTests` registrada en `infrastructure-06`
junto a `CompanySettingsMenuVisibilityTests`; `backend_tests.py --verify-only` **PASS**
(209 casos, sin pruebas sin asignar ni duplicadas). Solo cambia el manifiesto.

## NAV-COVERAGE-H3 — READY FOR REVIEW (2026-11-04)

BUG REAL reproducido con el filtro real de `NavigationBuilder`: `finance.view` sin
`purchases.view` mostraba el agrupador operativo de Compras sin hijos. La prueba de
regresión falló en ese caso antes del fix; los otros dos escenarios pasaron.
Corrección productiva de una línea en `PurchasesModule.PurchasesGroup`: se retira
`FinancePermissions.View` del OR. Categoría, ID, jerarquía, tres hijos y permisos
individuales conservados; ningún cambio en `NavigationBuilder` ni otros agrupadores.
Esto resuelve el hallazgo previo de Compras registrado en NAV-CONSOLIDATION-A01.

Pruebas focales: `PurchasesMenuVisibilityTests` **3/3 PASS** (solo Finanzas: sin grupo;
solo Compras y ambos permisos: tres opciones operativas y Reportes conservados);
`KernelRegistryTests` **48/48 PASS**. El filtro se prueba sin BD mediante su método
real de construcción del árbol; no acredita sincronización ni caché de un entorno.
`architecture:check` **PASS**, cero violaciones nuevas; documentación y
`git diff --check` **PASS**.
Cambios de Fase A preservados; sin migraciones, suites funcionales generales, pruebas
SRI, staging, commit ni push. Pendientes gate y aprobación del propietario: **H3 y
Fase A no CLOSED**. Repetición focal desde la raíz:

```powershell
dotnet test backend/src/ERP.Infrastructure.Tests/ERP.Infrastructure.Tests.csproj --no-restore --filter FullyQualifiedName~PurchasesMenuVisibilityTests
dotnet test backend/src/ERP.Domain.Tests/ERP.Domain.Tests.csproj --no-restore --filter FullyQualifiedName~KernelRegistryTests
git diff --check
git status --short
```

## NAV-CONSOLIDATION-A01 — READY FOR REVIEW (2026-11-04)

Consolidación solicitada de los cuatro accesos duplicados a Preferencias Operativas;
árbol vigente en [MENU_MAP](docs/navigation/MENU_MAP.md). Se retiran también los
contenedores sin hijos de Ventas y Compras; Inventario y Caja conservan sus catálogos,
con permisos de contenedor alineados con sus hijos. IDs y permisos restantes conservados.
KEEP: pantalla, pestañas, ruta y enlaces contextuales, permisos de lectura/edición,
preferencias y persistencia, APIs y flujos documentales sin cambios.

Evidencia del agente: `KernelRegistryTests` **48/48 PASS** (entrada única, ausencia de
duplicados, jerarquía, contenedores con hijos y permisos de los grupos conservados);
frontend focal **24/24 PASS** (navegación contextual, etiquetas, sección Ventas/POS y
esquemas). `architecture:check` **PASS**, 147 pruebas de guardrails y cero violaciones
nuevas; check documental **PASS**; `git diff --check` **PASS**. Sin suites funcionales
generales ni cambios de frontend productivo.

Dependencia: `NavigationSyncService` existente desactiva los registros retirados y
actualiza permisos durante el arranque normal; no se ejecutó sincronización ni se
modificó base de datos. Sin migraciones, producción, staging, commit ni push.
Hallazgo previo fuera de alcance: `/purchases/operation-group` admite `finance.view`
sin hijos con ese permiso; `NavigationBuilder` no poda contenedores anidados vacíos.
No se modifica este comportamiento compartido. Los grupos afectados quedan sin ese riesgo.

Pendientes gate y aprobación del propietario; **no CLOSED**. Porcentajes y cierre
funcional V1 intactos. Repetición focal desde la raíz (PowerShell):

```powershell
dotnet test backend/src/ERP.Domain.Tests/ERP.Domain.Tests.csproj --no-restore --filter FullyQualifiedName~KernelRegistryTests
Push-Location frontend
npx.cmd vitest run src/components/navigationAccess.test.ts src/i18n/navLabelTaxonomy.test.ts src/modules/configuracion/operaciones/sections/SalesPosPreferencesSection.test.tsx src/modules/configuracion/operaciones/schemas/operationalPreferencesSchemas.test.ts
npm.cmd run architecture:check
Pop-Location
node tools/architecture/run-all.mjs --only documentation
git diff --check
git status --short
```

## RIDE-GOLDEN-DETERMINISM-01 — READY FOR REVIEW (2026-11-04)

Casos iniciales exactos: `Invoice_with_mixed_vat_rates_matches_the_approved_golden_file`
(`03-invoice-with-vat.pdf`) e `Invoice_with_additional_info_matches_the_approved_golden_file`
(`05-invoice-with-additional-info.pdf`), registrados en un log local temporal
(`tmp/pdfs/infrastructure.log`), ya no conservado en el árbol de trabajo.
Investigación exclusivamente focal: la intermitencia se reprodujo y se capturó en 03
y también en 01 (estándar); el 05 inicial solo conserva el log, no el binario fallido.

**Causa confirmada de la intermitencia:** render concurrente QuestPDF/Skia con estado
de fuentes compartido. En las capturas fallidas solo difieren streams `/ToUnicode`
(03: objetos 16 y 20; 01: objeto 24), con asociaciones Unicode sustituidas por `0000`.
Los demás objetos PDF serializados coinciden; metadata normalizada idéntica y píxeles
idénticos al rasterizar a escala 2. Es un defecto de extracción de texto, no un cambio
visual ni una diferencia de fechas. No se atribuye a un método interno de la biblioteca
sin evidencia: aislar únicamente SVG siguió fallando con PDF concurrente.

Fix mínimo de pruebas: colección xUnit `RideGoldenRendering` sin ejecución paralela
para la clase golden; comparación binaria y golden originales intactos. Ningún cambio
productivo en esta investigación; ajustes de barras y NC preservados.
**PASS: 6 ejecuciones conjuntas consecutivas de 14/14 (84/84)**; los 36 candidatos
golden exportados coinciden byte a byte con los seis originales. Las capturas y logs
locales (`tmp/pdfs/golden-investigation/` y `tmp/pdfs/golden-investigation-*.log`) ya no
se conservan; la evidencia persistente es la colección `RideGoldenRendering` en
`backend/src/ERP.Infrastructure.Tests/Ride/RideGoldenFileTests.cs` (commit `b681f5b36`).
Este aislamiento estabiliza el gate; no acredita corregir la biblioteca ni probar
renderizado productivo concurrente. Sin suites generales, SRI, staging, commit ni push.
**Producción NO-GO**; gate/aprobación del propietario pendientes, no CLOSED.

Repetición desde la raíz:

```powershell
dotnet test backend/src/ERP.Infrastructure.Tests/ERP.Infrastructure.Tests.csproj --no-restore --filter "FullyQualifiedName~AccessKeyBarcodeRenderingTests|FullyQualifiedName~RideGoldenFileTests|FullyQualifiedName~QuestPdfRideRendererRetentionTests"
node tools/architecture/run-all.mjs --only documentation
git diff --check
```

## RIDE-CREDIT-NOTE-01 — READY FOR REVIEW (2026-11-04)

BUG REAL reproducido: título fijo «FACTURA» y omisión de referencia/motivo en NC.
Corregidos en presentación desde el modelo existente; contrato en
[Backend — RIDE](docs/architecture/backend.md#ride). `AccessKeyBarcodeSection` se conserva.
Revisión de caché `credit-note-layout-3`. Sin cambios de XML, firmas, datos fiscales ni llamadas al SRI.

Validación focal del agente: Application **25/25 PASS**; render y barras 01/04/07 +
retención **8/8 PASS**; golden de factura **6/6 PASS** en ejecución separada.
La ejecución conjunta inicial tuvo dos discrepancias binarias golden (12/14 PASS);
resueltas para el gate en RIDE-GOLDEN-DETERMINISM-01, arriba. No se sustituyeron los golden.
Revisión visual de las tres páginas **PASS**: título, referencia, motivo y barras legibles;
decodificación Code128 exacta de 49 dígitos en los tres tipos.
Muestras sintéticas no versionadas en `output/pdf/ride-credit-note/`.

**Producción NO-GO.** Pendientes aprobación visual y gate del propietario; no CLOSED.
Sin staging, commit ni push. Repetición focal desde la raíz (golden por separado):

```powershell
$env:RIDE_BARCODE_SAMPLES = "$PWD/output/pdf/ride-credit-note"
dotnet test backend/src/ERP.Infrastructure.Tests/ERP.Infrastructure.Tests.csproj --no-restore --filter "FullyQualifiedName~AccessKeyBarcodeRenderingTests|FullyQualifiedName~QuestPdfRideRendererRetentionTests"
dotnet test backend/src/ERP.Infrastructure.Tests/ERP.Infrastructure.Tests.csproj --no-build --no-restore --filter "FullyQualifiedName~RideGoldenFileTests"
dotnet test backend/src/ERP.Application.Tests/ERP.Application.Tests.csproj --no-restore --filter "FullyQualifiedName~RidePipelineTests|FullyQualifiedName~CreditNoteRideXmlParserTests|FullyQualifiedName~CreditNoteRideTemplateTests"
node tools/architecture/run-all.mjs --only documentation
git diff --check
```

## RIDE-BARCODE-LAYOUT-01 — IMPLEMENTADA Y APROBADA TÉCNICAMENTE (2026-11-04)

Mejora global del código de barras Code128 de la clave de acceso en los RIDE de
factura, nota de crédito y retención: bloque compartido `AccessKeyBarcodeSection`
(título, barras centradas con márgenes laterales y clave de 49 dígitos centrada),
PNG de 680×120 px con módulos enteros y revisión de renderer `barcode-layout-2`,
que invalida los PDFs anteriores en caché. Contrato en
[backend — RIDE](docs/architecture/backend.md). XML, firmas, autorizaciones SRI,
datos tributarios y lógica fiscal sin cambios; no se llamó al SRI.

Resultados: infraestructura RIDE **58/58 PASS** (incluye los **seis golden PDF PASS**),
`RidePipelineTests` **16/16 PASS**, inventario CI (`infrastructure-08`, descubrimiento)
**PASS** y decodificación Code128 a 300 dpi de los **49 dígitos PASS** en factura, nota
de crédito y retención. `git diff --check` PASS. Muestras para revisión visual en
`output/pdf/ride-barcode/` (no versionadas).

El defecto independiente de presentación de nota de crédito se atiende en
RIDE-CREDIT-NOTE-01, arriba, pendiente de revisión del propietario.

**Producción NO-GO.** Sin staging, commit ni push. Repetición focal desde la raíz:

```powershell
dotnet test backend/src/ERP.Infrastructure.Tests/ERP.Infrastructure.Tests.csproj --filter "FullyQualifiedName~ERP.Infrastructure.Tests.Ride.AccessKeyBarcodeRenderingTests|FullyQualifiedName~ERP.Infrastructure.Tests.Ride.QuestPdfRideRenderer|FullyQualifiedName~ERP.Infrastructure.Tests.Ride.RetentionRideRenderingSectionsTests|FullyQualifiedName~ERP.Infrastructure.Tests.Ride.RideGoldenFileTests|FullyQualifiedName~ERP.Infrastructure.Tests.Ride.RideRenderingSectionsTests"
dotnet test backend/src/ERP.Application.Tests/ERP.Application.Tests.csproj --filter "FullyQualifiedName~ERP.Application.Tests.Ride.RidePipelineTests"
python tools/ci/backend_tests.py --group infrastructure-08 --verify-only
git diff --check
```

## V1-FUNCTIONAL-CLOSE-01 — cierre funcional aprobado (2026-11-04)

**Retentions-02 APROBADA [V1-APPROVED:Retentions-02]**, por instrucción expresa del
propietario y con la evidencia real que confirma: retención 07 **001-001-000000001**,
compra origen **001-001-000006222**, ambiente SRI **Pruebas**, estado **AUTORIZADO**,
fecha oficial **2026-11-04T13:56:35-05:00** (`2026-11-04T18:56:35Z`), número de
autorización igual a la clave de acceso, comprobante firmado XAdES descargado y
comprobado y RIDE con **04/11/2026 13:56:35**. La respuesta no contiene errores.
La comprobación operativa fue aportada por el propietario; este cierre no ejecuta
llamadas al SRI ni modifica la retención real. Evidencia de aceptación en el
[registro SRI existente](docs/sri/SRI-2.34-COMPLIANCE-CLOSURE.md#8-estado-final);
corrección y pruebas en [RETENTION-AUTHORIZED-EVIDENCE-01](#retention-authorized-evidence-01--closed-commit-a84a0aeb9-cubierto-por-ci-1094-success-2026-11-04).

Se aprueba la capacidad catalogada completa, conservando la evidencia previa de su
implementación y de anulación asistida; la aceptación real del comprobante 07 resuelve
el último pendiente funcional, sin afirmar una nueva anulación SRI en esta sesión.
El pendiente Retentions-02 de los registros anteriores queda sustituido por esta aceptación.
Matriz única: **Web 70/70 = 100 % funcional V1; Core 43/43 = 100 %; Retentions 2/2 = 100 %**.
No se añaden capacidades ni se cambian denominadores, fórmulas o diseño del dashboard.

**Producción NO-GO.** La aprobación funcional no sustituye los controles independientes
de seguridad, despliegue y operación, ni cierra otros incidentes o pendientes SRI.
Se conservan los cambios locales de banner SRI, XML autorizado/RIDE y extracción PR-7
del CI #1087. No se cambian ADR-047, sus límites ni el gate. Sin staging, commit ni push;
el propietario realizará la revisión final y ambas operaciones. No se declara CLOSED
la entrega Git acumulada.

Validación focal repetida en este cierre: **Application 25/25 PASS**, **controlador
17/17 PASS**, **frontend 36/36 PASS** (cuatro archivos de Ventas y `RetentionSection`),
**matriz funcional 6/6 PASS**, gate `check-architecture-guardrails.ps1 -SkipFrontendChunk`
**PASS** y documentación **PASS**. PR-7: `moneyValues` **545/597** y `ride` **527/533**;
archivos extraídos **316** y **390** líneas, sin pérdida de casos. Los intentos iniciales
backend con `--no-build` no se ejecutaron por ausencia de binarios; la repetición con
compilación focal produjo los PASS indicados. No son fallos funcionales ni se ocultan
como ejecuciones exitosas. Los comandos focales exactos permanecen en las entradas
CI-1087-PR7 y RETENTION-AUTHORIZED-EVIDENCE-01 siguientes.

Regeneración y revisión de la salida por el pipeline oficial:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/dashboard/render-dashboard.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File tools/dashboard/test-curated-dashboard.ps1
node tools/dashboard/test-functional-v1-ui.mjs
git diff --check
```

## CI-1087-PR7 — READY FOR REVIEW (2026-11-04)

Extracción exclusiva de pruebas de Ventas: `SalesPage.moneyValues.test.tsx` queda en
545/597 líneas no vacías y `SalesPage.ride.test.tsx` en 527/533. Los archivos nuevos
`SalesPage.discountConsistency.test.tsx` (316) y `SalesPage.electronicStatus.test.tsx`
(390) conservan los casos extraídos. **34/34 PASS** antes y después;
`check-architecture-guardrails.ps1 -SkipFrontendChunk` **PASS**. No se ejecutó build
ni suites generales. ADR-047, límites y gate sin cambios; cambios locales previos de
retenciones/banner preservados. Sin staging, commit ni push. Gate del propietario
pendiente; no CLOSED. Repetición desde la raíz:

```powershell
Push-Location frontend
npx.cmd vitest run src/modules/sales/pages/SalesPage.moneyValues.test.tsx src/modules/sales/pages/SalesPage.discountConsistency.test.tsx src/modules/sales/pages/SalesPage.ride.test.tsx src/modules/sales/pages/SalesPage.electronicStatus.test.tsx
Pop-Location
powershell -NoProfile -ExecutionPolicy Bypass -File tools/architecture/check-architecture-guardrails.ps1 -SkipFrontendChunk
```

## RETENTION-AUTHORIZED-EVIDENCE-01 — CLOSED: commit a84a0aeb9, cubierto por CI #1094 SUCCESS (2026-11-04)

Cierre: aprobación del propietario en V1-FUNCTIONAL-CLOSE-01 (Retentions-02), commit
`a84a0aeb9` y CI #1094 SUCCESS sobre `d7b07f6c8`, que lo incluye (dato del propietario).

BUG REAL: la descarga XML de Retenciones regeneraba el comprobante sin firma; el RIDE
usaba esa vista previa y el parser dejaba la fecha de autorización en null. Se corrigieron
las lecturas existentes mediante `IElectronicDocumentInternalReader`, sin nuevo pipeline.
Contrato en [Retenciones — XML/RIDE](docs/architecture/backend.md#retenciones-una-capacidad-una-política-de-emisión-zh-retention-emission-ssot-01-zh-purchase-retention-confirm-01).
KEEP: permisos por origen, XML firmado, firma, transmisión, secuenciales, cálculos y
contabilidad. No se llamó al SRI ni se leyó/modificó la retención operativa.

Pruebas focales: Application **25/25 PASS**, controlador API **17/17 PASS**; cero fallos
u omitidas. Se reconstruyó una respuesta SOAP sanitizada a partir de la referencia del
propietario, con identidades sintéticas y marcadores XAdES (sin validar criptografía).
El parser SOAP existente conserva el comprobante y convierte la fecha de referencia a
`2026-11-04T18:56:35Z`; el layout que recibe el renderer muestra **04/11/2026 13:56:35**
en `America/Guayaquil`. Se comprueban igualdad exacta del XML, variantes separadas y
404 sin regeneración cuando falta la evidencia autorizada. Repositorio/almacenamiento
simulados para lectura; no acredita una nueva prueba de persistencia real ni una
descarga del PDF de la retención operativa. Documentación y `git diff --check` PASS.
Sin suites generales. Publicado y cerrado según lo indicado al inicio.
Repetición focal desde la raíz:

```powershell
dotnet test backend/src/ERP.Application.Tests/ERP.Application.Tests.csproj --no-restore --filter "FullyQualifiedName~RetentionAuthorizedEvidenceTests|FullyQualifiedName~GenerateRetentionXmlHandlerTests|FullyQualifiedName~GenerateRetentionRidePdfHandlerTests|FullyQualifiedName~RetentionRidePdfServiceTests|FullyQualifiedName~RetentionSourceAccessTests"
dotnet test backend/src/ERP.API.Tests/ERP.API.Tests.csproj --no-restore --filter "FullyQualifiedName~RetentionsControllerTests"
node tools/architecture/run-all.mjs --only documentation
git diff --check
```

## PURCHASE-RETENTION-ENVIRONMENT-01 — READY FOR REVIEW (2026-11-04)

FALTA de presentación resuelta: reutilización del banner global en la configuración de
retención del borrador de Compra. Contrato en
[banner electrónico](docs/frontend/ELECTRONIC_ENVIRONMENT_BANNER.md#reutilización).
KEEP: lógica funcional, permisos, cálculos y pipeline fiscal sin cambios.
Una prueba focal nueva de visibilidad **1/1 PASS**, con cambio Pruebas→Producción desde
el store global y ocultación al desactivar la intención o confirmar la compra.
ESLint de los dos archivos TypeScript afectados, documentación y `git diff --check` PASS.
Sin suites generales.
Publicado en `a84a0aeb9`; CI #1094 SUCCESS cubre lint/build, no la prueba Vitest
(el CI frontend no ejecuta Vitest). Gate visual del propietario pendiente; no CLOSED.
Repetición focal desde `frontend/`:

```powershell
npx.cmd vitest run src/modules/purchases/pages/RetentionSection.test.tsx -t 'muestra el ambiente del store'
npx.cmd eslint src/modules/purchases/components/PurchaseRetentionDraft.tsx src/modules/purchases/pages/RetentionSection.test.tsx
git diff --check
```


## COMPANY-FISCAL-HELP-01 — READY FOR REVIEW (2026-11-04)

BUG REAL corregido: dependencia visual de retenciones respecto de comercio exterior.
KEEP: bindings, payload y persistencia actuales. Ayuda fiscal bilingüe incorporada
con los componentes existentes; contrato en
[Frontend](docs/architecture/frontend.md#ayuda-fiscal-de-empresa).
Validación del agente: **51/51 PASS** en tres archivos focales; lint de los siete
archivos TypeScript afectados **PASS**, cero errores y una advertencia preexistente
(`toFormValues`, exhaustive-deps); `git diff --check` **PASS**. La prueba de guardado
verifica payload y recarga del formulario con servicio simulado; no acredita una
nueva prueba de persistencia PostgreSQL. Sin cambios de backend, datos operativos,
XML, firma, SRI, migraciones ni configuración de SUMAK ZH. Sin staging, commit o push.
Gate del propietario pendiente; no se declara CLOSED. Repetición desde `frontend/`:

```powershell
npx.cmd vitest run src/modules/configuracion/empresa/sections/FiscalSettingsSection.test.tsx src/components/zh/help/ZHHelpPopover.test.tsx src/modules/configuracion/empresa/pages/CompanySettingsHubPage.i18n.test.tsx
npx.cmd eslint src/modules/configuracion/empresa/sections/FiscalSettingsSection.tsx src/modules/configuracion/empresa/sections/FiscalSettingsSection.test.tsx src/help/helpKeys.ts src/help/helpRegistry.ts src/help/helpResolver.ts src/help/helpTypes.ts src/components/zh/help/ZHFieldHelp.tsx
git diff --check
```

## SALES-ELECTRONIC-STATUS-CLARITY-01 — estado de la venta vs. estado SRI — READY FOR REVIEW (2026-11-04)

Solo presentación, más una lectura aditiva: Ventas muestra el estado comercial `Authorized` como
«Confirmada» y, en una columna o etiqueta separada, el estado SRI real (`Draft` = «Sin emitir»;
«Autorizada por SRI» solo si `ElectronicDocument` está `Authorized`). El listado recibe
`SalesListDto.ElectronicStatus` mediante una lectura en lote por página. El Monitor explica
Borrador, Firmado y los estados en trámite. Sin cambios en enums, estados persistidos, contabilidad,
secuenciales, firma, transmisión, reintentos ni reglas SRI; sin migraciones; las facturas 015/016 no
se procesaron. Pruebas focales: Vitest 26 archivos, 213/213; Application 4/4; PostgreSQL 1/1;
Architecture 145/145; `tsc -b`, lint (0 errores) y build en PASS. Regla en
[backend.md](docs/architecture/backend.md#estado-comercial-vs-estado-sri-de-una-venta-sales-electronic-status-clarity-01).
Sin staging, commit ni push.

## ED-V1-CLOSE — ElectronicDocuments-04 y -07 aprobadas — READY FOR REVIEW (2026-11-04)

Aprobación expresa del propietario, condicionada a PASS focal (cumplida): ElectronicDocuments-04
(Caso #32, respuestas inciertas y conciliación) y ElectronicDocuments-07 (R12, corrección y
retransmisión 01/04/07). Detalle y evidencia en
[Caso #32](#caso-32--implementado--v1-aprobado-2026-11-04) y
[R12 Fase B](#sri-234-r12-fase-b--corrección-y-retransmisión-010407--ready-for-review-2026-10-31).
Solo se añadieron pruebas (NC 04 en R12 y 403 HTTP de `/reconciliation` y `/corrected-retransmission`):
Application 71/71, PostgreSQL 15/15 y HTTP 3/3, ejecución focal en contenedores efímeros; **BUG REAL:
ninguno**; sin código productivo, migraciones, bases persistentes ni llamadas al SRI. El caso #30 no
tiene definición oficial: se retiró de los pendientes sin crear requisitos.

Matriz y Dashboard: **Web 69/70 = 98.57 %** (antes 67/70 = 95.71 %); **Core 42/43 = 97.67 %**
(antes 40/43 = 93.02 %). Pendiente V1 exacto: **Retentions-02**, que requiere la aceptación real en
`celcer` de un comprobante 07 en un entorno aislado, después de la resolución de SRI-INCIDENT-01 y con
autorización del propietario. Producción NO-GO. Sin staging, commit ni push.

## V1-CONFIGURATION-INITIALLOAD-01 — capacidades aprobadas — READY FOR REVIEW (2026-11-04)

Aceptaciones expresas del propietario en esta sesión, condicionadas a los PASS focales:
Configuration-01 mantiene la API actual sin pantalla de historial; InitialLoad-05
conserva OpeningBalancePosting/ASI sin migración ni cambio de arquitectura. Condiciones
funcionales satisfechas con datos sintéticos en contenedores PostgreSQL 16 desechables.
No se ejecutó el cierre sobre empresas existentes ni se accedió a sus bases operativas.

| Capacidad | Resultado y evidencia |
|---|---|
| Configuración organizacional [V1-APPROVED:Configuration-01] | API y persistencia existentes, catálogo de keys/scopes/tipos, auditoría con actor real, no-op sin log duplicado y rollback ante fallo del log. Historial consultable por HTTP; guardar/reabrir conserva valores, inválido devuelve 422 sin alterar valor ni historial, acceso ajeno 403 y anónimo 401. Pruebas: 20 PostgreSQL de repositorio/auditoría, 2 HTTP, 2 Application de scope y 4 guardrails arquitectónicos PASS. Alcance exclusivamente en [Backend](docs/architecture/backend.md#configuración-organizacional) y [defaults congelados](docs/architecture/frozen-infrastructure.md#valores-por-defecto-de-facturación). |
| Carga inicial [V1-APPROVED:InitialLoad-05] | 9 casos PostgreSQL PASS de cierre: Enabled con ASI/conciliación, Disabled sin asientos, actor/instante persistidos, idempotencia, ASI ausente, cuatro blockers sin rastro, cancelar lote en curso y rechazo posterior de fecha/posting/publicación/reverso sin consumir números ni cambiar historial. 8 casos Application PASS del guard: saldos bloqueados y catálogos disponibles. Se reutiliza R1 únicamente para importaciones ya aprobadas; R1 no se usa como prueba del cierre. [Alcance uniforme aprobado](docs/architecture/backend.md#alcance-v1-de-initialload-05); reglas contables en [Accounting §20](docs/architecture/accounting.md#20-costos-apertura-y-disabled-accounting-continuity-05). |

**45/45 PASS, 0 omitidas**, ejecución focal del agente (no suite general):

- [PostgreSQL: 29/29](TestResults/configuration-initialload-01/configuration-initialload.trx).
- [API aislamiento: 1/1](TestResults/configuration-initialload-01/configuration-http.trx).
- [API guardar/reabrir e historial: 1/1](TestResults/configuration-initialload-01/configuration-workflow-http.trx).
- [Application: 10/10](TestResults/configuration-initialload-01/configuration-initialload-application.trx).
- [Guardrails de configuración: 4/4](TestResults/configuration-initialload-01/configuration-architecture.trx).

Clasificación: **KEEP** implementación y navegación; **BUG REAL: ninguno reproducido**;
**FALTA funcional: ninguna** dentro de estos dos alcances. La expresión ambigua del
catálogo se resolvió mediante la decisión del propietario, sin requisitos nuevos.
Se añadió solo una prueba HTTP focal; no se modificaron implementaciones CLOSED/FROZEN.
El primer intento de esa prueba falló por consultar fuera del contexto de empresa del
test; la verificación final usa HTTP autenticado y pasa. Se conserva ese resultado
como diagnóstico del test, no como defecto productivo ni evidencia PASS.

Matriz y Dashboard consolidados una sola vez al finalizar: **Web 67/70 = 95.71 %**
(antes 65/70 = 92.86 %); **Core 40/43 = 93.02 %**; **InitialLoad 7/7 = 100 %**;
**Configuration 1/1 = 100 %**. Pendientes funcionales V1 exactos: Retentions-02,
ElectronicDocuments-04 y ElectronicDocuments-07; conservan sus gates SRI y decisiones
ya registrados, sin reauditoría. **GO funcional para las dos capacidades; producción
NO-GO** por los bloqueos SRI y de despliegue existentes.

La aprobación por capacidad no declara CLOSED la fase acumulada: revisión/gate del
propietario y staging/commit siguen a su cargo conforme a enforcement. Sin staging,
commit ni push. Comandos focales para el gate del propietario desde la raíz:

```powershell
dotnet test backend/src/ERP.Infrastructure.Tests/ERP.Infrastructure.Tests.csproj --no-build --filter "FullyQualifiedName~ConfigurationChangeLogTransactionalIntegrityTests|FullyQualifiedName~OrgSettingsRepositoryConfigurationGuardrailTests|FullyQualifiedName~PublishOpeningJournalEntryPostgreSqlTests.Cierre_|FullyQualifiedName~PublishOpeningJournalEntryPostgreSqlTests.Cada_blocker|FullyQualifiedName~PublishOpeningJournalEntryPostgreSqlTests.Despues_del_cierre|FullyQualifiedName~PublishOpeningJournalEntryPostgreSqlTests.C05_apertura|FullyQualifiedName~PublishOpeningJournalEntryPostgreSqlTests.ASI_vigente|FullyQualifiedName~PublishOpeningJournalEntryPostgreSqlTests.Lote_en_curso"
dotnet test backend/src/ERP.API.Tests/ERP.API.Tests.csproj --no-build --filter "FullyQualifiedName~OrganizationalConfigurationV1HttpTests|FullyQualifiedName~CompanySettingsIsolationHttpTests.OrgConfig_de_empresa_y_sucursal_no_cruza_a_B"
dotnet test backend/src/ERP.Application.Tests/ERP.Application.Tests.csproj --no-build --filter "FullyQualifiedName~CompanyInvoiceOrgSettingsCompanyScopeTests|FullyQualifiedName~InitialLoadClosedGuardTests"
dotnet test backend/src/ERP.Architecture.Tests/ERP.Architecture.Tests.csproj --no-build --filter "FullyQualifiedName~OrgSettingsAccessGuardrailTests|FullyQualifiedName~ConfigurationChangeLogGuardrailTests"
node tools/architecture/run-all.mjs --only documentation
node --test tools/dashboard/functional-v1.test.mjs
powershell -NoProfile -ExecutionPolicy Bypass -File tools/dashboard/test-curated-dashboard.ps1
git diff --check
```

## V1-LANG-FINANCE-ACCOUNTING-01 — bloque acumulado — READY FOR REVIEW (2026-11-04)

Alcance autorizado: Navigation-03, Finance-05/06 y Accounting-05/06/07/08. Se conservan
todos los cambios locales y las aprobaciones anteriores. Base CI #1084 SUCCESS aportada
por el propietario; R2/R4 y las pruebas previas se reutilizan exclusivamente para sus
alcances. Sin suites generales, auditorías globales, reapertura de módulos ni cambios
de reglas fiscales/contables. Sin staging, commit o push; cierre de la fase acumulada
y entrega conjunta a cargo del propietario.

**Siete aprobaciones V1 acreditadas individualmente:**

| Capacidad | Evidencia PASS exacta y cobertura |
|---|---|
| Interfaz español/inglés [V1-APPROVED:Navigation-03] | Alcance de interfaz solicitado en este bloque: pantalla Empresa completa (ocho pestañas) y Dashboard del bloque previo. `CompanySettingsHubPage.i18n.test.tsx` **21/21**: es/en, cambios de idioma, modal Ventas, errores de Fiscal/Marca/Documentos/Precisión y formato de imagen. `CompanyProfileSettingsSection.test.tsx` **5/5**: perfil completo es/en, edición y errores conservados al cambiar idioma, guardar/reabrir y correo inválido. Los KPIs es/en **4/4** previos permanecen acreditados. No se afirma una auditoría del resto de pantallas. |
| Anticipo/remanente [V1-APPROVED:Finance-05] | `Excedente_confirmado_aplica_la_CxP_crea_SupplierCredit_por_el_remanente_y_postea_Anticipos_una_sola_vez`: pago **320**, aplicación **300**, anticipo **20**; persistencia de crédito/origen y un asiento balanceado. Caso nuevo `Anticipo_100_aplica_30_reembolsa_40_y_reversa_restaura_70_sin_duplicados`: anticipo real de origen SupplierPayment **100**, aplicación idempotente **30**, saldo **70**, reembolso **40**, saldo **30**, reversa y saldo **70**, CxP restante **30**, tres movimientos/obligaciones/asientos sin duplicación. |
| Reembolso/reversa [V1-APPROVED:Finance-06] | `Reembolso_banco_parcial_Debe_banco_real_Haber_anticipos_de_la_regla` y `Reversa_banco_es_espejo_exacto_con_la_cuenta_congelada_aunque_el_banco_cambie_de_cuenta` PASS; el caso encadenado anterior agrega consistencia tras aplicar parte del mismo anticipo, obligaciones durables Posted y reverso histórico. Nunca se usó anticipo 0.00 como prueba del remanente. |
| Plan y reglas [V1-APPROVED:Accounting-05] | `Plan_y_reglas_CRUD_persistidos_jerarquia_scope_y_cuentas_en_uso_por_HTTP` PASS: alta de raíz/hija, edición y reapertura, unicidad y rechazo de ciclo; crear/editar/desactivar/reactivar regla con líneas; bloqueo de baja de cuenta referenciada, baja/reactivación tras desactivar regla y denegación en otra empresa. `Cada_posting_rule_referencia_solo_cuentas_del_plan_sembrado` PASS. Se reutilizan AccountUseCasesTests/PostingRuleUseCasesTests del CI para negativos y contrato de catálogo. |
| Modo operativo [V1-APPROVED:Accounting-06] | `Empresa_nueva_elige_Enabled_explicitamente_con_vigencia_y_sin_historia` y `C05_Disabled_se_bloquea_por_pendientes_y_retroactividad_y_procede_al_resolverlas` PASS PostgreSQL: decisión explícita, vigencia, rechazo retroactivo/con pendientes, transición válida e historial. Las invariantes fiscales y de primera activación restantes se reutilizan de AccountingContinuityIntegrationTests/05 del CI. |
| Continuidad/lote [V1-APPROVED:Accounting-07] | `Batch_mixed_deduplicates_and_records_actor_reason_correlation`, `Batch_concurrent_same_ids_posts_once` y `Batch_internal_failure_does_not_stop_next_obligation` PASS: lote mixto Failed/Posted, deduplicación, otra empresa/tenant/ID inexistente, ya resueltas, historial/actor/motivo/correlación, concurrencia sin doble asiento y fallo aislado que permite continuar. Continuidad de productores y cancelación se reutilizan del CI; no se atribuye a R4 la prueba del lote. |
| Cierre anual [V1-APPROVED:Accounting-08] | `A12D_06_ejercicio_2025_terminado_publica_cierre_y_cierra_periodo_sin_duplicar_resultado` PASS PostgreSQL: empresa/libro aislados, ejercicio **2025** y fecha Company de prueba **2026-11-04**; ingresos **1000**, gasto **150.25**, resultado **849.75**, generación Draft, publicación, nominales a cero, Estado de Resultados conserva utilidad, Balance la refleja una sola vez, período Closed persistido y reversa tardía rechazada. Casos 12D anteriores de utilidad/pérdida, duplicado/desactualizado, precondiciones y reverso se reutilizan del CI. No se cerró un ejercicio de datos reales ni el 2026 en curso. |

**BUG REAL corregido (solo presentación):** textos fijos de Empresa, incluido «Guardar
Configuración». Etiquetas, opciones, ayudas, placeholders, acciones, avisos y validación
local usan el proveedor/diccionarios existentes; los errores locales cambian con el
idioma sin perder la edición. Límites, regex, tipos y reglas de negocio se conservan.
Contrato en [Frontend — i18n](docs/architecture/frontend.md#i18n). No se creó traductor,
estado de idioma ni mecanismo de mensajes paralelo.

**Finanzas/Contabilidad KEEP:** no se modificó código funcional backend. Se añadieron
solo tres recorridos focales, reutilizando fixtures/repositorios/handlers existentes;
el fixture Manual08 acepta ahora año explícito con su default previo intacto. El
primer FAIL del ciclo financiero era una lectura de saldo derivado sin cargar cuotas
en el arnés; corregida con Include, sin alterar el cálculo. Fallos iniciales de imports
y sincronización de formularios fueron del arnés y quedaron resueltos. Ningún FAIL
funcional permanece en estas siete capacidades.

**Ejecuciones nuevas:** **12 casos PostgreSQL PASS** (nueve focales existentes + cierre
2025 + ciclo del anticipo + configuración HTTP), **26 frontend PASS**, sin omitidas
en los resultados finales. Evidencias locales, no enlaces Markdown a archivos ignorados:
`.local/v1-finance-accounting/finance-accounting-focal.trx` (9/9),
`finance-year2025-gap.trx` (cierre 2025 PASS y FAIL histórico del arnés de saldo),
`advance-lifecycle.trx` (ciclo corregido 1/1), `accounting-configuration-http.trx` (1/1),
y `.local/v1-finance-accounting-ui.json` (26/26). Referencias versionadas:
[cierre 12D](backend/src/ERP.Infrastructure.Tests/Accounting/AccountingFiscalYearClosing12DTests.cs),
[ciclo del anticipo](backend/src/ERP.Infrastructure.Tests/Accounting/SupplierCreditRefundPostingIntegrationTests.AdvanceLifecycle.cs),
[plan/reglas HTTP](backend/src/ERP.API.Tests/Integration/InternalCapabilitiesHttpTests.AccountingConfiguration.cs)
y [pantalla Empresa](frontend/src/modules/configuracion/empresa/pages/CompanySettingsHubPage.i18n.test.tsx).

Persistencia y concurrencia sobre PostgreSQL 16 Testcontainers migrado, datos sintéticos
por empresa y almacenamiento efímero; sin conexiones a bases instaladas, modificación
de información real ni cambios de reloj del sistema. Para dinero/fechas se conservan
la política única y el reloj Company; el cierre no calcula impuestos ni participación.

**Resultado acumulado: 65/70 V1 (92.86 %); Core 40/43 (93.02 %).** Este bloque no deja
ninguna de sus siete capacidades pendiente. Las cinco restantes de la matriz conservan
sus razones y estados, fuera de alcance. Dashboard: una sola regeneración al finalizar,
manteniendo diseño, navegación, once nodos, conexiones y breadcrumbs.

Gates técnicos: TypeScript/build PASS, ESLint PASS (0 errores, 37 advertencias previas),
i18n-keys, frontend-precision y documentación PASS; cálculo focal **6/6 PASS** y
`git diff --check` PASS. Composición curada y navegación del Dashboard se verifican
tras su única regeneración final. Estado exacto Git y resultados en la entrega del bloque.

## V1-INTERNAL-SOLUTIONS-01 — bloque acumulado — READY FOR REVIEW (2026-11-04)

> Su pendiente Navigation-03 queda resuelto por V1-LANG-FINANCE-ACCOUNTING-01. Evidencia
> del ciclo anterior conservada; no se sustituyen sus cinco aprobaciones.

Conserva íntegros los cambios del ciclo anterior y sus dos aprobaciones. Esta entrada
actualiza exclusivamente las seis pendientes de V1-INTERNAL-CLOSE-01. CI #1084 SUCCESS
se reutiliza como base comunicada por el propietario; no se repitieron sus suites ni
R1–R4. Sin cambios funcionales de seguridad, permisos, persistencia ni diseño del
ProgressDashboard; sin staging, commit o push. No declara CLOSED la fase acumulada.

**BUG REAL corregido:** DashboardPage tenía literales españoles en KPIs, conteos,
avisos, antigüedad, resumen anual y pie. Usa ahora `dashboard.*` en los diccionarios
existentes es/en y el mismo I18nProvider. Regresión RED de inglés antes del fix →
GREEN **4/4**: español, inglés, cambio es→en→es sin remount y precisión monetaria.
Contrato en [Frontend — i18n](docs/architecture/frontend.md#i18n).

**Cinco aprobaciones V1 acreditadas**, sin aceptación duplicada:

| Capacidad | Evidencia PASS y alcance interno cerrado |
|---|---|
| Login, JWT y refresh [V1-APPROVED:Session-01] | `Login_JWT_rotacion_y_reuso_de_familia_persisten_por_HTTP`: alta de usuario de prueba/primer login, JWT aceptado por endpoint protegido, refresh HTTP con cookie, sucesor persistido con familia/padre/profundidad/límite absoluto; reuso inmediato 401 sin revocar sucesor; reuso tardío revoca familia y rechaza sucesor; otra familia sigue operativa. PostgreSQL real; sin fake de repositorio. |
| Administración de acceso [V1-APPROVED:Security-01] | `Alta_membership_sucursales_perfiles_menu_y_autorizacion_por_HTTP`: alta por endpoint administrativo, perfil y permisos, membership, sucursal autorizada, preferencia DirectToDefault y contexto del usuario; comprobación de persistencia mediante nuevo contexto/lectura HTTP. Dos perfiles reales: consulta de perfiles y consulta de sesiones, con 200/403 complementarios. |
| Menú con permisos [V1-APPROVED:Navigation-01] | Mismo recorrido: GET `/me/menu` real contiene `/admin/roles` para el lector de perfiles y `/admin/access/sessions` para el lector de sesiones, excluyendo el destino opuesto. Se reutilizan 58/58 del ciclo anterior para jerarquía/destinos/bootstrap; ningún árbol backend simulado. |
| Design System y tablas compactas [V1-APPROVED:Navigation-02] | Chromium **2/2**, a 390/1440 px: componentes reales y CSS productivo, carga efectiva de Inter y Material Symbols originales de index.html, ligadura del icono, formulario, error, toggle, botones/disabled, aviso y `ZHDataTable` con `table--compact`; sin overflow. Capturas inspeccionadas visualmente, sin defectos reproducibles ni rediseño. ZHDataTable/ZHModal **22/22** (interacción/estados). |
| Perfil, branding y fiscal interno [V1-APPROVED:Companies-01] | HTTP: identidad comercial, contacto, representante, moneda/zona, fiscal/designaciones, idioma, leyenda y branding; guardar y GET con nueva sesión/contexto conserva todos los valores y normaliza resolución. Otro caso sube logos principal/alterno y verifica IDs, MIME y bytes al reabrir/descargar. UI **2/2**: envío de campos, recarga/reapertura y bloqueo por correo inválido. Modal global 15/15 previo reutilizado. |

Pruebas HTTP acreditadas: [InternalCapabilitiesHttpTests](backend/src/ERP.API.Tests/Integration/InternalCapabilitiesHttpTests.cs),
**4/4 PASS, 0 omitidas**, resultado local `.local/internal-v1/internal-final.trx`.
Reutilizan el fixture SEC-04 con PostgreSQL 16 Testcontainers y migraciones; el host
de prueba resuelve usuario/tenant del JWT real. Altas, sesiones, cambio inicial de
contraseña y archivos son exclusivamente sintéticos en el entorno desechable;
no se consumieron credenciales existentes ni se modificaron bases instaladas.
El reloj de reuso se simula envejeciendo solo el timestamp del token del test.

**Separación interna/externa de Companies-01:** los datos fiscales sintéticos validan
formato, normalización y persistencia interna, nunca validez de una resolución ni
aceptación del SRI/celcer. Esa comprobación externa R21/R22 conserva su pendiente y no
bloquea este comportamiento interno. La precondición de Contabilidad se respetó:
el intento de marcar obligación contable con Accounting deshabilitado fue rechazado
422 como exige el contrato; la prueba usa el catálogo fiscal existente y configuración
compatible, sin modificar Accounting. Los fallos iniciales por régimen inexistente y
lectura de sucursales sin contexto fueron del arnés; corregidos sin tocar código ERP.

**Navigation-03 continúa pendiente.** El defecto de KPIs está resuelto, pero la capacidad
registrada es «Interfaz en español e inglés». En la misma validación del perfil se
reprodujo otro defecto con `en` cargado: CompanyProfileSettingsSection conserva
«Identificación», «Información Corporativa», «Descartar Cambios» y «Guardar Configuración»
en español. Prueba focal temporal **1/1 FAIL** al exigir ausencia de este último literal
(`.local/internal-v1-english-company.log`); retirada después de reproducir. No se convirtió
el PASS del Dashboard en aprobación global ni se inició auditoría del resto de pantallas.

Frontend focal: **30 PASS** en cinco archivos (Dashboard 4, perfil 2, tabla/modal 22,
visual 2). Evidencia visual local: `.local/internal-v1/visual/zh-components-390.png` y
`zh-components-1440.png`; se verificó carga real de fuentes, no solo fallback.
La primera ejecución visual restringida no cargó las fuentes; fuera del sandbox,
ambas resoluciones PASS. Se conserva la distinción entre error de entorno y defecto UI.

Resultado acumulado: **58/70 V1 (82.86 %); Core 34/43 (79.07 %)**. Los siete cierres
internos acumulados no cambian el denominador Core. Matriz actualizada solo por los
cinco nuevos cierres; Dashboard regenerado una única vez al finalizar este bloque.
El propietario conserva el cierre del bloque y la autorización de su único commit.

Gates técnicos ejecutados: TypeScript y build frontend PASS; ESLint PASS (0 errores,
37 advertencias existentes, sin ampliación de alcance); checks focales i18n-keys,
design-system y documentation PASS; cálculo funcional **6/6 PASS** y `git diff --check`
PASS. Sin suite general ERP ni nueva auditoría de módulos. Gate final reproducible
del Dashboard: renderer oficial, test-curated-dashboard y test-functional-v1-ui;
se conservan las once tarjetas, CSS, conexiones, rutas y breadcrumbs.

Gate HTTP focal reproducible desde la raíz:
`dotnet test backend/src/ERP.API.Tests/ERP.API.Tests.csproj --no-restore --filter FullyQualifiedName~InternalCapabilitiesHttpTests`.
Frontend desde `frontend/`: `npx.cmd vitest run src/modules/dashboard/pages/DashboardPage.test.tsx
src/modules/configuracion/empresa/sections/CompanyProfileSettingsSection.test.tsx
src/components/zh/ZHDataTable.test.tsx src/components/zh/ZHModal.test.tsx
src/components/zh/InternalCapabilities.visual.test.tsx` (el visual requiere acceso a Google Fonts).

## V1-INTERNAL-CLOSE-01 — bloque interno 1 — READY FOR REVIEW (2026-11-04)

> Evidencia del ciclo anterior preservada. Estado posterior de sus seis pendientes:
> [bloque acumulado](#v1-internal-solutions-01--bloque-acumulado--ready-for-review-2026-11-04).

Alcance exclusivo: Companies-01, Dashboard-01, Security-01, Session-01, Session-02 y
Navigation-01/02/03. Base: **CI #1084 SUCCESS comunicada por el propietario**, commit
`d42990d95f36df42ac0b81e10ce82d14080bf632`. No se descargaron sus TRX: el conector no
devolvió runs para el commit y gh local carece de autenticación. Se reutiliza esa base
declarada, con correspondencia de clases en el [inventario CI](tools/ci/backend-test-groups.json)
y el [workflow](.github/workflows/backend-ci.yml), cuyo reporte verifica cobertura y
rechaza resultados fallidos/omitidos. No se atribuyen nuevas ejecuciones backend.
R1–R4 conservan sus alcances; no se repitieron ni se usaron para aprobar estas capacidades.

Cierres acreditados conforme a la autorización de esta tarea (implementación completa
+ PASS del alcance, sin aceptación duplicada; no cierre global de módulos):

- **KPIs reales del ERP [V1-APPROVED:Dashboard-01].** KEEP. La brecha era no haber
  conciliado las pruebas existentes: `DashboardKpiReaderTests` (grupo infrastructure-02)
  cubre PostgreSQL real: ceros, ventas del mes/año, exclusión de anuladas, CxC/CxP
  pendientes/vencidas, stock bajo/sin stock sin duplicar bodegas y aislamiento de CxP
  entre empresas. `GetDashboardKpisQueryHandlerTests` (application) cubre fecha operativa
  y AsOf explícito. Focal nueva: `DashboardPage.test.tsx` PASS, presentación de montos,
  conteos y cambio de precisión. No equivale a certificar producción ni todo el Dashboard.
- **Sesión operativa y preferencias [V1-APPROVED:Session-02].** KEEP. Cobertura existente
  CI: `LoginHandlerTests` (DirectToDefault, AskBranch, ausencia de preferencias y sucursal
  no autorizada), `CompanyUserPreferencesTests`, `CreateAuthenticatedSessionTests`,
  handlers de contexto/cambio de sucursal; PostgreSQL real en
  `CreateAuthenticatedSessionIntegrationTests` (enlace refresh/sesión y login concurrente),
  `UserSessionAndCompanyUserBranchRepositoryTests` (empresa/sucursal/terminal, autorización
  y unicidad), `CompanyUserPreferencesRepositoryTests` (persistencia, AskBranch, unicidad
  y aislamiento), más `SessionContextCompanyBranchIsolationHttpTests` (api).
  Brecha UI cubierta focalmente: `SessionBootstrap.test.tsx`, `CompanySelectPage.test.tsx`,
  `BranchSelectorModal.test.tsx` y `useBranchGate.test.ts` PASS; selección, contexto en carga,
  error/reintento y bloqueo hasta resolver contexto/política. No se recrearon datos persistidos.

Pendientes, sin autorización de implementación:

| Capacidad | Clasificación y brecha exacta restante |
|---|---|
| Companies-01 | FALTA de validación: el 15/15 de componentes no verifica guardar/reabrir perfil completo, branding y datos fiscales; R21/R22 conserva la comprobación con resoluciones reales en celcer pendiente. No se usaron datos fiscales inventados ni se cambiaron empresas. |
| Security-01 | FALTA de validación: SEC-03..06 y la focal de SecuritySettings (PASS) no enlazan alta de usuario → membership → sucursales/preferencias → perfil/permisos → acceso efectivo del usuario. No existe en la evidencia revisada un PASS integrado de ese recorrido completo. |
| Session-01 | FALTA de validación integrada: CI cubre login/JWT, contrato cookie y RefreshTokenService con repositorio fake; falta acreditar login HTTP → JWT utilizable → rotación persistida → reutilización/revocación de familia en PostgreSQL real. No se presentó el fake como prueba de persistencia ni se consumieron credenciales. |
| Navigation-01 | FALTA de validación integrada: navigationAccess y launcherHierarchy PASS prueban destinos anidados, rutas ausentes/fail-closed, jerarquía y favoritos; falta el recorrido del menú real generado por permisos para dos perfiles y acceso/denegación correspondientes. El árbol simulado no acredita el filtrado backend. |
| Navigation-02 | FALTA de validación visual: los PASS de componentes no acreditan el conjunto ZH/tablas compactas con fuentes e iconos reales cargados; se conserva la limitación visual registrada. No se ejecutó otra suite general ni se alteró el diseño. |
| Navigation-03 | BUG REAL reproducido: con locale activo `en`, DashboardPage conserva «Ventas del mes», «Cuentas por cobrar/pagar», «vencido», «sin stock» y «Resumen Anual» en español. El check i18n-keys PASS y lazyLocale PASS solo acreditan claves/carga, no traducción de literales JSX. |

Validación nueva: **74/74 PASS** en nueve archivos Vitest focales (los siete de
DashboardPage, SessionBootstrap, CompanySelectPage, SecuritySettingsPage, navigationAccess,
launcherHierarchy y lazyLocale: 58/58; BranchSelectorModal/useBranchGate: 16/16).
Navigation-03: **1/1 FAIL esperado** en prueba temporal sobre el mismo fixture de
DashboardPage, `ensureLocaleLoaded("en")` + storageKey=`en`; verificado locale=`en`, falla
`expect(text).not.toContain("Ventas del mes")`. Fixture temporal retirado tras reproducir;
código funcional intacto. Sin E2E contra datos reales, cambios de BD ni suites generales.

Resultado: **53/70 V1 (75.71 %); Core 34/43 (79.07 %)**. Los dos cierres son internos,
fuera del denominador Core. Matriz y derivados actualizados únicamente por ellos;
diagrama y navegación del ProgressDashboard preservados. Sin staging, commit ni push.

Gates focales PASS: cálculo 6/6 (expectativas actualizadas a 53 y fixture sintético de
aprobación independiente del cierre real), regeneración oficial, composición curada,
Chromium (21 rutas de módulos, Web/Core, evidencias, breadcrumbs, once nodos y hashes
de estructura/CSS protegidos), documentation y `git diff --check`. GO para un único
commit documental/de evidencia con sus derivados y ajuste de expectativas del cálculo;
no acredita cierre de las seis pendientes ni GO de producción.

## V1-EVIDENCE-CLOSE-01 — cierre por evidencia PASS + corrección CI #1083 — READY FOR REVIEW (2026-11-04)

**BUG REAL CI #1083:** `ratchet.test.mjs` (Architecture guardrails) fallaba porque
[Caso #20](#caso-20--closed-integral-por-aprobación-del-propietario-2026-11-03) enlazaba
como Markdown tres `.trx` de `.local/caso20-results/`, no versionados. Ahora son rutas de
evidencia local en texto; contenido y resultados históricos intactos. Guardrail sin cambios.

**Cierres V1 autorizados por el propietario en esta tarea**, con el criterio implementación
completa + PASS que cubre el alcance + sin requisitos ni bloqueos pendientes (sin exigir una
aceptación duplicada; no reabre módulos CLOSED/FROZEN):

- Pausa total de emisión por empresa y reanudación explícita [V1-APPROVED:ElectronicDocuments-05]:
  [SRI-TOGGLE-02](#sri-toggle-02--emisión-electrónica-habilitable-por-empresa--ready-for-review-2026-11-02),
  -04, [-06](#sri-toggle-06--pausa-total-de-la-emisión-electrónica-por-empresa--ready-for-review-2026-11-02)
  y [-07](#sri-toggle-07--pausa-total-sin-excepciones-y-corte-garantizado--ready-for-review-2026-11-02),
  con el gate [SRI-TOGGLE-08](#sri-toggle-08--gate-final-de-sri-toggle-0607--pass-agente-listo-para-gate-del-propietario-2026-11-02):
  PostgreSQL 300/300, Architecture 145/145 (incl. `SriAccessEmissionSwitchBoundaryTests`),
  Application 1242/1242, API 186/186, `architecture:check` 140/140; C-03 publicado en
  [CI #1065](#ci-fix-sri-01--c-03-corregido--closed-2026-11-03) SUCCESS. Los fallos Vitest
  conservados son deuda de Compras, fuera de este alcance.
- Salud técnica SRI centralizada [V1-APPROVED:ElectronicDocuments-06]:
  [entrada](#salud-técnica-sri-centralizada--ready-for-review-2026-11-01) con Application
  36/36, decoradores 17/17, API aislamiento 8/8, Architecture 143/143 y frontend focal
  (Ventas, Configuración, NC, Retenciones, Monitor, store, componente). Sus límites
  declarados (estado por proceso, ConsultaComprobante no observada) son de diseño, no requisitos abiertos.

Las otras 19 siguen pendientes por evidencia parcial o bloqueo; motivo por capacidad en
[functional-v1-source.json](docs/ProgressDashboard/data/functional-v1-source.json).
Resultado: 51/70 capacidades V1 (72.86 %); Core 34/43 (79.07 %). Focales: `ratchet.test.mjs`,
`functional-v1.test.mjs`, regeneración del Dashboard y `git diff --check`. Sin SRI real,
suites generales, staging, commit ni push.

## DASHBOARD-V1-FINAL — READY FOR REVIEW (2026-11-04)

BUG REAL: el dibujo no calculaba avance funcional verificable. KEEP: las once tarjetas,
CSS, posiciones, flechas, conexiones, navegación y detalle existentes; sin código ERP.
FALTA cubierta: registro único de capacidades de FEATURES y del inventario existente,
conciliado con aprobaciones por alcance en este documento y evidencias previas.
Futuro y preparación de producción excluidos. La falta de aceptación no se presenta
como código faltante ni se reabren módulos CLOSED. Este bloque supera la solución
provisional de etiquetas de DASHBOARD-FIX-02 sin modificar sus evidencias históricas.

Generador y dataset funcional integrados en ambos orquestadores y en cada render rápido.
Contrato: [DASHBOARD-CONTRACT](docs/ProgressDashboard/DASHBOARD-CONTRACT.md#dashboard-v1-final--contrato-vigente-del-avance-funcional).
Resultado de esta revisión: 49/70 capacidades V1 aprobadas (70 %); Core 32/43 (74.42 %).
21 sin aprobación: 19 implementadas sin aceptación, una bloqueada (#32) y una capacidad
de carga inicial pendiente de implementación/aceptación. No acredita producción,
nuevos cierres funcionales ni ejecución de suites ERP.

Pruebas focales del agente: cálculo 6/6 PASS (duplicados, denominadores, cambio de
aprobación, exclusiones y fallo ante catálogo/evidencia inválidos); navegador Chromium
PASS para 21 módulos, Web/Core, evidencias, hash routes y breadcrumbs; estructura de
once nodos/CSS protegidos y posiciones preservadas; composición curada y documentation
PASS; git diff --check PASS. Sin suites generales, staging, commit ni push.
Gate del propietario pendiente; no CLOSED. Desde la raíz:

~~~powershell
node --test tools/dashboard/functional-v1.test.mjs
powershell -NoProfile -ExecutionPolicy Bypass -File tools/dashboard/render-dashboard.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File tools/dashboard/test-curated-dashboard.ps1
node tools/dashboard/test-functional-v1-ui.mjs
node tools/architecture/run-all.mjs --only documentation
git diff --check
~~~


## DASHBOARD-FIX-02 - READY FOR REVIEW (2026-11-04)

BUG REAL documental: Web ERP, Servicios Core y sus fichas mostraban porcentajes
manuales historicos sin formula verificable. Sustituidos por CLOSED o PENDIENTE
segun el alcance acreditado aqui y en FEATURES.md; sin declarar cierres globales
a partir de R3/R4 focales. Evidencia enlazada desde las fichas; diseno y navegacion
conservados. KEEP: datasets y metricas automaticas, referencias historicas y
cambios previos de la plantilla. Sin cambios de codigo ERP.

Renderer oficial, composicion curada, sintaxis JavaScript, render focal Web/Core,
enlaces de evidencia, check documentation y git diff --check PASS agente.
Sin suites generales, staging, commit ni push. Gate del propietario pendiente;
no CLOSED. Comandos desde raiz:

~~~powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/dashboard/test-curated-dashboard.ps1
node tools/architecture/run-all.mjs --only documentation
git diff --check
~~~

## DOC-GOVERNANCE-CONSOLIDATION — READY FOR REVIEW (2026-11-04)

BUG REAL documental: entradas Cursor/Copilot y catálogo PR con jerarquías paralelas,
requisitos SaaS/i18n obsoletos, enlaces con mayúsculas incorrectas y procedimientos
alternativos de reset/validación. Consolidación bajo CLAUDE.md; entradas Codex y Claude
separadas, con las mismas lecturas. Detalles únicos de adaptadores y Carga Inicial
trasladados a las SSOT existentes; no cambia el comportamiento del ERP.
KEEP: documentos con consumidores y evidencia histórica; ninguno eliminado.
FALTA cubierta: el runner de arquitectura incorpora enlaces/anclas y referencias
estructurales de autoridad; el analizador del dashboard no ofrecía ese gate.
DEBT: la coherencia semántica de negocio requiere revisión humana; no se automatiza
mediante heurísticas ni se reabren módulos CLOSED/FROZEN.

Validación focal agente: check documentation PASS; pruebas del control, ratchet e
integridad grandfather 31/31 PASS. Sin suites generales, BD ni migraciones.
Revisión final del diff y git diff --check PASS. Gate reproducible del propietario:

```powershell
node tools/architecture/run-all.mjs --only documentation
node --test tools/architecture/ratchet.test.mjs tools/architecture/check-grandfather-integrity.test.mjs
git diff --check
```

Sin staging, commit ni push; gate del propietario pendiente, no CLOSED.

## CUSTOMER-SALES-SETTINGS-UX-FIX-01 — READY FOR REVIEW (2026-11-03)

BUG REAL: `saveSettings()` iniciaba `openSettings()` sin esperarlo; su respuesta
restablecía el modal después del cierre. Eliminada esa recarga automática;
confirmación de éxito y cierre tras guardar, error visible sin cerrar y bloqueo
de cierre/envíos duplicados durante la solicitud. Reapertura manual conserva el GET.
Sin backend, endpoints, reglas de crédito ni cambios de condiciones de pago.

Vitest focal agente: 12/12 PASS (4 nuevas). Antes del fix, 2 regresiones fallaron
por el GET adicional. APIs simuladas para comprobar el ciclo de vida de la UI;
no acredita persistencia PostgreSQL ni smoke manual. Gate del propietario pendiente;
no CLOSED. Lint PASS (0 errores, 37 warnings ajenos al cambio); TypeScript y build
PASS (advertencias de chunks y fullLogout existentes). Architecture:check PASS
(140/140, 0 violaciones nuevas); git diff --check PASS. Sin staging, commit, push ni despliegue.

## SEC-A01 — límites de intentos de login — IMPLEMENTACIÓN V1 CLOSED / PRODUCCIÓN NO-GO (2026-11-03)

`login` y `global-login` con admisión previa al handler: reserva atómica K1 cuenta+IP (5/15 min),
K2 IP (desactivado) y K3 cuenta (30/15 min), concurrencia por instancia configurable, 429
`RATE_LIMITED` y 503 `LOGIN_THROTTLE_UNAVAILABLE` (Redis configurado e inaccesible, o sin Redis
fuera de Development/Testing). `X-Forwarded-For` solo desde proxies declarados (lista vacía).
Handlers, BCrypt, JWT, refresh, sesiones, permisos y frontend sin cambios.
Contrato: [Seguridad — Límites de intentos de login](docs/architecture/security.md#límites-de-intentos-de-login-sec-a01--implementación-v1-closed--producción-no-go).

PASS agente (focales): Infrastructure 22/22 (reserva en memoria, throttle de login, regresión
`TryAcquireAsync`) y 9/9 Redis vía Testcontainers; API 233/233 (contrato 429/503, proxies
confiables, AuthController, cookie de refresh, matriz de errores); arquitectura 10/10 (controllers,
contrato de respuestas, superficie de auth); HTTP real de login 1/1. Sin suite completa.
Corrección CI #1070 (guardrail temporal: instante como `DateTime` UTC, no `DateTimeOffset`):
guardrail 1/1, Infrastructure 22/22 y Redis 9/9.
Publicado por el propietario: implementación `dc239ebc3573b8508e878cbd7603af986a37b4ae` y
corrección `db87df627d8de52cdc96248edfd69f157038c477`;
[CI #1071](https://github.com/szhinin89/erp-saas/actions/runs/41737883781) SUCCESS (19 jobs
success, 0 fallidos; despliegue Azure omitido; backend architecture/API/infrastructure, frontend e
identity guardrails PASS). Cierre de la implementación aprobado expresamente por el propietario.
Cierre documental publicado en `9cb872adac3cd82992c52ba76387c69b969cb3e5`;
[CI #1072](https://github.com/szhinin89/erp-saas/actions/runs/41743143238) SUCCESS
(ejecución `41743143238`: 19 jobs exitosos, 0 fallidos y 1 omitido: Deploy — Azure Web App).
No acredita despliegue ni ERP V1 listo para producción.

**Producción NO-GO — requisitos abiertos antes del despliegue:**
- Verificar la IP real del cliente detrás del proxy y configurar `ForwardedHeaders:KnownProxies`/
  `KnownNetworks` reales; K2 por IP sigue desactivado hasta entonces.
- Verificar Redis disponible en el entorno de despliegue y el número real de instancias API
  (`Redis:Required` acorde).
- Medir `t_bcrypt` en el hardware real y validar los parámetros definitivos de concurrencia, cola
  y límites (los actuales son candidatos).
- Evaluar el riesgo residual de bloqueo temporal dirigido contra cuentas (K3, sin cookie de
  dispositivo).
- Validar en el entorno de despliegue los aspectos sin evidencia (proxy, HTTPS/
  `X-Forwarded-Proto`, mensajes 429/503 en la pantalla de login).

## AUTH-INTERCEPTOR-FIX-01 — corrección focal — CLOSED (2026-11-03)

BUG REAL reproducido antes del fix: 401 de `global-login` y `complete-password-reset`
activaban refresh automático; las dos regresiones nuevas fallaron (15/17 PASS).
Añadidas ambas rutas a la lista pública existente, sin cambiar el interceptor ni backend.
Contrato: [Seguridad — Refresh rotation](docs/architecture/security.md#refresh-rotation).
Sin rate limiting SEC-A01 ni cambios en sesiones/permisos. No declara SEC-A01 CLOSED.

PASS agente: Vitest focal 39/39 (política, interceptor, gestor de refresh, reautenticación
y flujos públicos), TypeScript (`tsc -b`, incluido en build), lint (0 errores, 37 warnings
en archivos no modificados), build y architecture:check (140/140 pruebas, gate con 0
violaciones nuevas), `git diff --check`. Build conserva advertencias de chunk y de import
dinámico de fullLogout; sin cambios para resolverlas. No acredita despliegue.
Publicado por el propietario en `bdd2f8915291901a6ef93369dfee40ba04812dc4`;
[CI #1067](https://github.com/szhinin89/erp-saas/actions/runs/41645351527) SUCCESS
(19 jobs success; despliegue Azure omitido). El CI cubre lint, `tsc -b`/build y
`architecture:check`, no Vitest. Gate focal del propietario: Vitest 39/39 PASS en los cinco
archivos (primer comando); lint, TypeScript, build y arquitectura acreditados por CI #1067.
Cierre aprobado expresamente por el propietario. Gate del propietario desde `frontend/`:

```powershell
node node_modules/vitest/vitest.mjs run src/modules/lib/api.test.ts src/modules/lib/authRefreshPolicy.test.ts src/lib/session/authRefreshManager.test.ts src/lib/session/reauthenticate.test.ts src/modules/auth/api/authPublicFlows.test.ts --no-file-parallelism --maxWorkers=1 --no-cache
npx.cmd tsc -b
npm.cmd run lint
npm.cmd run build
npm.cmd run architecture:check
git diff --check
```

## DOC-CLEANUP-COMPRAS-01 — Compras V1 aprobado y CLOSED (2026-11-03)

El propietario confirma Compras V1 aprobado y **CLOSED** en esta tarea. Este es el
estado vigente del módulo y prevalece sobre los registros anteriores READY FOR REVIEW,
PENDING, pendientes de validación o «no CLOSED» de Compras. Esos registros conservan
el estado y la evidencia de su fecha; no representan trabajo funcional abierto.
No reauditar ni rediseñar salvo BUG REAL directamente relacionado.

Conciliación documental:

- **BUG REAL documental:** retirados de WORK_QUEUE los plazos XML/cuotas y las promociones
  DISURGAL al 100 %, incluidos en el módulo aprobado. La aceptación específica ya registrada
  de DISURGAL y ALPINA se conserva; la confirmación global no inventa gates focales,
  aceptación visual específica, CI, commits ni ejecuciones adicionales.
- **KEEP:** evidencia de detalle compacto, XML V1, identidad de líneas repetidas,
  fixtures XML, distribución de costos, NC y cierres R2/CxP. Sus límites y resultados
  históricos permanecen; sus pendientes funcionales de Compras quedan superados por
  el cierre global, sin extenderlo a SRI, otros módulos o ampliaciones FUTURE.
- **DEBT:** los fallos deterministas de Vitest de Compras registrados en plazos XML y
  SRI-TOGGLE-08 (5 en `purchaseLinePresentation.test.ts`, 1 en `PurchaseFormFeedback.test.tsx`)
  son expectativas obsoletas frente a decisiones ya aprobadas (importes fiscales XML
  conservados, `a55057a9c`; detalle contraído, 2026-10-29); no indican defecto funcional ni
  reabren Compras. Su actualización forma parte del gate B-02 de lanzamiento. Se conserva el
  intermitente bajo carga, que pasa aislado. No se corrigieron ni se declaran PASS.
  Resuelto después en [B-02](#b-02--pruebas-obsoletas-de-compras-actualizadas--closed-2026-11-06).
- **FALTA:** procedencia estructurada XML/default/manual y ampliaciones no operativas
  siguen fuera del alcance implementado; no se convierten en obligaciones V1.

Solo documentación y derivados del Dashboard, cuyas métricas no son autoridad de cierre.
Sin nuevas pruebas funcionales, BD ni validación SRI. El cierre confirmado no acredita
un despliegue. Las tareas abiertas de otros ámbitos conservan su estado.
Verificación documental: `run-dashboard-final.ps1` completo, terminado con
`Dashboard completed successfully`; `git diff --check` PASS. Sin staging, commit ni push.

## CI-FIX-SRI-01 — C-03 corregido — CLOSED (2026-11-03)

BUG REAL reproducido en `d81e2adbf`: dos techos excedidos y una excepción obsoleta.
Extracción focal de diagnóstico del certificado, análisis/cotejo de NC y composición de respuesta de descarga.
`Handle`: ValidateSriConfiguration 180 → 103; DownloadPurchaseReceptionXml 192 → 131;
UpdateOperationalPreferences 80 (sin cambiar código). Retiradas las tres excepciones C-03;
sin elevar límites ni cambiar contratos, pausa SRI, XML/XAdES/SOAP, BD o secuenciales.
PASS agente: Application focal 32/32, frontera SRI Architecture 2/2, fixtures C-03/integridad 59/59,
PostgreSQL real 26/26 (interruptor/locks/scope y repositorio de recepción), check-handler-size,
integridad grandfather y `git diff --check`. Sin llamadas reales al SRI.
Publicado por el propietario en `8d4301fb32f73926a4b209014fd62f11e66f3139`;
[CI #1065](https://github.com/szhinin89/erp-saas/actions/runs/41626052962) SUCCESS
(19 jobs success; despliegue Azure omitido), con handler-size, integridad grandfather y
grupos backend. El propietario acepta expresamente CI #1065 como gate técnico y aprueba el
cierre. No acredita despliegue. Gate reproducible desde raíz:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/quality/check-handler-size.ps1
node tools/architecture/check-grandfather-integrity.mjs
node --test tools/quality/check-handler-size.test.mjs tools/architecture/check-grandfather-integrity.test.mjs
dotnet test backend/src/ERP.Application.Tests/ERP.Application.Tests.csproj --no-restore --filter "FullyQualifiedName~ValidateSriConfigurationQueryHandlerTests|FullyQualifiedName~DownloadPurchaseReceptionXmlHandlerTests|FullyQualifiedName~UpdateOperationalPreferencesCommandHandlerTests"
dotnet test backend/src/ERP.Architecture.Tests/ERP.Architecture.Tests.csproj --no-restore --filter FullyQualifiedName~SriAccessEmissionSwitchBoundaryTests
dotnet test backend/src/ERP.Infrastructure.Tests/ERP.Infrastructure.Tests.csproj --no-restore --filter "FullyQualifiedName~ElectronicEmissionSwitchPostgreSqlTests|FullyQualifiedName~PurchaseReceptionDocumentRepositoryTests"
git diff --check
```

## SRI-TOGGLE-08 — gate final de SRI-TOGGLE-06/07 — PASS agente, listo para gate del propietario (2026-11-02)

PostgreSQL afectado 300/300 (interruptor, candidatos, recuperación y anulaciones de retenciones/gastos, reclamos,
SRI incierto, retransmisión corregida, decoradores y XML de proveedores). Architecture 145/145, `architecture:check`
140/140, `tsc -b` y `git diff --check` limpios. Sin cambios en XML, XAdES, firma, SOAP, migraciones, secuenciales ni
históricos (`docs/archive`, snapshots previos del Dashboard).
**Fallos de vitest de Compras — preexistentes, no regresión:** reproducidos en el commit base `fa1890bc4` (código
extraído con `git archive` en un directorio aislado, mismas dependencias) con resultado idéntico al árbol actual.
6 deterministas: 5 en `purchaseLinePresentation.test.ts` (PURCHASE-LINE-HEADER-EDIT-RECALCULATE-TAX-TOTAL-01: IVA/Total
de cabecera quedan en el snapshot XML, p. ej. IVA 6.13 en lugar de 15) y 1 en `PurchaseFormFeedback.test.tsx`
(equivalencia en unidad base). El 7.º es intermitente bajo carga y cambia de archivo entre corridas
(`ResolvePendingProductsModal.flows` en la base, `ProductPicker` en el árbol actual); ambos pasan 3/3 aislados en los
dos árboles. Ningún archivo involucrado difiere de la base. Quedan como deuda del módulo Compras (CLOSED), fuera de
este alcance; no se corrigieron. Los 6 deterministas se resolvieron después en
[B-02](#b-02--pruebas-obsoletas-de-compras-actualizadas--closed-2026-11-06).

## SRI-TOGGLE-07 — pausa total sin excepciones y corte garantizado — READY FOR REVIEW (2026-11-02)

Con `electronic_documents.emission_enabled=false` tampoco se descarga XML de proveedores (422
`ELECTRONIC_DOCUMENT_EMISSION_DISABLED`, el comprobante sigue Importado), ni se verifica la vigencia de NC de proveedor
(reprocesamiento bloqueado fail-closed, sin suponer vigencia), ni se hace ping de diagnóstico (conectividad "no
verificada"). Garantía de corte: todo consumidor llama al SRI dentro de `IElectronicEmissionSwitch.EnterSriCallAsync`
(lock consultivo compartido de sesión por empresa, desde el control hasta el fin de la llamada); el cambio del
interruptor espera a las llamadas en curso y, confirmado el apagado, no inicia ninguna nueva. Inventario cerrado
sin excepciones (`SriAccessEmissionSwitchBoundaryTests`). UI: recepción de compras muestra la pausa y deshabilita la
consulta/descarga de XML. Contrato: [backend.md § Límites y finalización](docs/architecture/backend.md#límites-y-finalización),
[ADR-023 addendum SRI-TOGGLE-07](docs/decisions/ADR-023-electronic-documents-v1-closure.md). Sin migraciones,
XML/XAdES/firma/SOAP/secuenciales, SRI real, commit ni push. Pendiente gate del propietario.
PASS agente: Application 1242/1242 (ElectronicDocuments, Retentions, SalesReturn, ElectronicInvoicing, preferencias,
Purchase*), API 186/186 (job de reintentos, contrato de errores incl. download-xml pausado, ElectronicInvoicing,
OpenAPI, PurchaseReturnEndToEnd incl. reprocesamiento con interruptor real, recepción), Architecture 145/145,
Infrastructure PostgreSQL 299/300 + rerun 6/6 del test de 04 adaptado al nuevo contrato; frontend `tsc -b`, lint
(0 errores), build, `architecture:check` 140/140, vitest focal 722/729 (fallos preexistentes, ver SRI-TOGGLE-08).
Dashboard regenerado.

## SRI-TOGGLE-06 — pausa total de la emisión electrónica por empresa — READY FOR REVIEW (2026-11-02)

`electronic_documents.emission_enabled` en OFF bloquea toda llamada al SRI sobre comprobantes propios de la empresa:
transmisiones, reintentos, consultas de autorización manuales y automáticas, historial R12, retransmisiones,
ConsultaComprobante (anulaciones de retención) y los jobs (reintentos y polling de anulaciones filtran en SQL en todo
estado). Conserva datos, estados, secuenciales, auditoría, lectura local y operaciones comerciales; sin DeadLetter
por exhaución mientras está pausada. ON restablece funciones, pero los procesos automáticos no retoman pendientes
anteriores a la última habilitación (transmisión ni consulta): requieren acción explícita. UI: aviso de pausa y
acciones SRI deshabilitadas en Monitor y anulación. Inventario de rutas SRI verificado por reflexión
(`SriAccessEmissionSwitchBoundaryTests`); fuera del alcance: XML/NC de proveedores. Contrato:
[backend.md § Límites y finalización](docs/architecture/backend.md#límites-y-finalización),
[ADR-023 addendum SRI-TOGGLE-06](docs/decisions/ADR-023-electronic-documents-v1-closure.md). Sin migraciones,
XML/XAdES/firma/SOAP/secuenciales, SRI real, commit ni push. Pendiente gate del propietario.
PASS agente: Application 617/617 (ElectronicDocuments, Retentions, SalesReturn, estado, preferencias), API 131/131
(job de reintentos PostgreSQL, contrato de errores, ElectronicInvoicing, OpenAPI), Architecture 18/18 (incl.
`SriAccessEmissionSwitchBoundaryTests`), Infrastructure PostgreSQL 258/259 + rerun del interruptor 15/15 (el único
fallo era la siembra de un test nuevo, corregida); frontend `tsc -b`, lint, vitest focal 72/72, build,
`architecture:check` 140/140; Dashboard regenerado.

## SRI-TOGGLE-04 — reanudación automática solo con actividad posterior a la habilitación — READY FOR REVIEW (2026-11-02)

Cierra sin migración las ventanas de Retenciones y de la recuperación de Ventas, incluida la re-habilitación entre
precheck e INSERT del Draft; sustituye los parches de SRI-TOGGLE-03. La decisión automática usa el último intento o el
instante persistido del origen (`UpdatedAt` de autorización/emisión), nunca la creación del comprobante; lecturas y
cambios del interruptor se serializan con lock consultivo por empresa (exclusivo antes de fechar el registro auditado).
Contrato: [backend.md § Límites y finalización](docs/architecture/backend.md#límites-y-finalización),
[ADR-023 addendum SRI-TOGGLE-02/03/04](docs/decisions/ADR-023-electronic-documents-v1-closure.md). Sin migraciones,
XML/XAdES/SOAP/secuencias, SRI real, commit ni push. Pendiente gate del propietario.

## SRI-TOGGLE-02 — emisión electrónica habilitable por empresa — READY FOR REVIEW (2026-11-02)

FALTA implementada con intervención FROZEN autorizada y limitada al control: `electronic_documents.emission_enabled`
(org_settings, scope Company, auditado, **ausente = deshabilitada**) en Preferencias operativas
(`settings.operations.configure`). `ElectronicDocumentIssuer` consulta `IElectronicEmissionSwitch` antes de toda
transmisión nueva (pipeline Draft/Failed, reclamo de envío y de retransmisión con `FOR SHARE`, reactivación de
DeadLetter) y responde `ELECTRONIC_DOCUMENT_EMISSION_DISABLED` (422) sin escribir. Consultas de autorización,
conciliación y operaciones comerciales siguen. Al rehabilitar, los pendientes solo se reanudan explícitamente:
los jobs (reintentos, recuperación Sales/Retentions) excluyen actividad anterior a la habilitación. Banner `Disabled`.
Contrato: [backend.md § Límites y finalización](docs/architecture/backend.md#límites-y-finalización),
[ADR-023 addendum SRI-TOGGLE-02](docs/decisions/ADR-023-electronic-documents-v1-closure.md).
PASS agente: Application 124/124 (issuer, retransmisión, estado, preferencias, política), Domain 169/169
(configuración/ED), API 136/136 (contrato de errores, snapshots, job de reintentos PostgreSQL, ElectronicInvoicing),
Architecture 87/87 (filtro), Infrastructure PostgreSQL 209/209 (interruptor, candidatos, recuperación, reclamos,
retenciones); frontend `tsc -b`, lint (0 errores), vitest focal 523/523, build, `architecture:check` PASS.
**Impacto de despliegue:** toda empresa deja de transmitir al SRI hasta habilitar el interruptor.
Sin migraciones, cambios XML/XAdES/SOAP/secuencias/estados, SRI real, staging, commit ni push. Pendiente gate del propietario.

## RESP-01 — código SRI 45 por colisión de secuencial — READY FOR REVIEW (2026-11-02)

BUG demostrado (logs 2026-10-31): `[45] SECUENCIAL REGISTRADO` se trataba como "ya existe" y las facturas 001/002
(secuencia reiniciada) quedaron en `Received` consultando una clave que el SRI nunca guardó. Ahora un 45 sin 43/70
en el primer envío → `Rejected` con el mensaje original, sin consulta. Conservan la consulta: retransmisión corregida
R12 (`TransmissionAttempt > 1`) y respuesta tras un POST incierto del mismo envío (reintento HTTP de `SriSoapClient`,
`SriReceptionResult.PrecededByUncertainPost`). Contrato: [backend.md § Caso #32](docs/architecture/backend.md),
[ADR-023 addendum 2026-11-02](docs/decisions/ADR-023-electronic-documents-v1-closure.md).
PASS agente: Application 91/91 (Reception, CorrectedRetransmission, IssuerRetry, RetransmissionPolicy,
ReceptionService), Infrastructure 48/48 (SriSoapClient, SriReceptionClient, decoradores), `architecture:check`.
Sin migraciones, BD, R12/R32 ni rutas de retransmisión nuevas; 001/002 históricas sin tocar (resolución operativa
pendiente). Sin commit. Pendiente gate del propietario.

Pendiente P0 aparte: facturas del 01/11/2026 (010–013) con SOAP Fault `GenericJDBCException` en Recepción de
Pruebas. XML, XSD, XAdES y SOAP verificados conformes; diagnóstico de bloqueo externo del SRI, pendiente prueba
discriminante autorizada por el propietario y reporte al SRI.

## Salud técnica SRI centralizada — READY FOR REVIEW (2026-11-01)

FALTA implementada: estado técnico único de Recepción y Autorización SRI por ambiente (Disponible, Lento,
Intermitente, No disponible, Sin verificar), con motivo y última observación. Monitor en memoria por host+servicio
alimentado solo por decoradores de DI sobre los adaptadores existentes (resultado intacto, sin clave de acceso,
XML ni empresa); polling agotado con respuestas válidas = respuesta válida; un ping fallido nunca diagnostica caída.
Endpoint `GET /api/v1/electronic-invoicing/sri-health` (autenticado, empresa propia). Componente
`ZHSriServiceHealth` en Ventas (reemplaza el badge básico; ping y aviso al emitir se conservan), NC y Retenciones
(solo en tránsito al SRI), Monitor y Configuración SRI. Contrato:
[ELECTRONIC_ENVIRONMENT_BANNER.md § Salud técnica SRI](docs/frontend/ELECTRONIC_ENVIRONMENT_BANNER.md#salud-técnica-sri--monitor-central-zhsriservicehealth);
extensión registrada en [frozen-infrastructure.md](docs/architecture/frozen-infrastructure.md#electronicdocuments-v10-facturación-electrónica-sri).
PASS agente: Application 36/36 (monitor, handler, estado previo), Infrastructure decoradores 17/17, API
aislamiento 8/8, Architecture 143/143; frontend focal 205 + 119 + 15 + 14 (Ventas, Configuración, NC,
Retenciones, Monitor, Caja, store, componente), `tsc -b`, lint (0 errores), build, `architecture:check` 140/140.
Limitaciones: estado por proceso (reinicio o varias instancias → vistas independientes, "Sin verificar" al arrancar);
ConsultaComprobante no se observa; la consulta de decisiones fallida solo puede producir "Intermitente".
Sin migraciones, tablas, jobs, SOAP nuevo, cambios R11/R12/R32, staging, commit ni push. Pendiente gate del propietario.

Gate focal del propietario (PowerShell desde la raíz; Docker para la prueba API):
```powershell
dotnet test backend/src/ERP.Application.Tests/ERP.Application.Tests.csproj --no-restore --filter "FullyQualifiedName~SriServiceHealth|FullyQualifiedName~GetSriServiceHealthQueryHandlerTests|FullyQualifiedName~GetElectronicInvoicingStatusQueryHandlerTests" --verbosity quiet
dotnet test backend/src/ERP.Infrastructure.Tests/ERP.Infrastructure.Tests.csproj --no-restore --filter FullyQualifiedName~ObservedSriClientDecoratorTests --verbosity quiet
dotnet test backend/src/ERP.API.Tests/ERP.API.Tests.csproj --no-restore --filter FullyQualifiedName~CompanySettingsIsolationHttpTests --verbosity quiet
Push-Location frontend
npm.cmd run test:unit -- src/components/zh/ZHSriServiceHealth.test.tsx src/store/electronicInvoicingStatusStore.test.ts src/modules/sales/pages/SalesPage.sriConnectivity.test.tsx
npm.cmd run lint
npm.cmd run build
npm.cmd run architecture:check
Pop-Location
git diff --check
```

## Compras — plazos XML y cuotas — READY FOR REVIEW (2026-10-31)

> Registro histórico: estado vigente [Compras V1 CLOSED](#doc-cleanup-compras-01--compras-v1-aprobado-y-closed-2026-11-03).
> Los pendientes corresponden a su fecha; se conservan resultados y límites sin acreditar nuevos gates.

BUG REAL: plazo XML desconectado del formulario y default del proveedor omitido al cargar recepción.
Implementado: propuesta explícita para un pago con días enteros válidos (19/10 + 8 = 27/10),
default activo por empresa/proveedor como fallback, edición manual prioritaria, conservación al
guardar/reabrir, veto de Contado con vencimientos a crédito y cabecera alineada al último vencimiento.
KEEP: condición activa obligatoria, validación de cuotas, permisos y traslado del cronograma a CxP.
SSOT: [Compras § 2](docs/architecture/purchases.md#2-borrador). Sin interpretación de otras unidades
ni múltiples pagos; sin selección de catálogo por coincidencia de días.

PASS agente: Application focal 94/94 (propuesta XML, recepción, defaults y confirmación), frontend
focal 75/75 (cuotas, reapertura, formulario y retención), PostgreSQL 16 desechable 4/4 (27/10,
vencimiento manual 30/10, reapertura, CxP, rechazo de Contado y cuotas inconsistentes con rollback),
lint (0 errores; 37 advertencias existentes), build con TypeScript, `architecture:check` (140/140,
0 nuevas violaciones), `git diff --check`. Las pruebas PostgreSQL usan Testcontainers y el handler
real de confirmación; no usan bases persistentes ni SRI real.

Limitación de regresión ampliada: Compras frontend 587/594; reejecución aislada de los tres archivos
afectados 77/83 (el timeout del lote desaparece). Persisten 5 fallos en
`purchaseLinePresentation.test.ts` y 1 en `PurchaseFormFeedback.test.tsx`, con pruebas/lógica
correspondientes sin cambios en esta tarea. Fuera del alcance de plazos; no se investigaron ni
modificaron reglas de presentación, impuestos o importes. No se acredita suite general verde.
FALTA opcional: procedencia estructurada del vencimiento XML/default/manual requeriría persistencia
nueva; no implementada ni necesaria para el vencimiento operativo.

Archivos de implementación: `PurchaseDraftDto.cs`, `CreatePurchaseReceptionDraftHandler.cs`,
`PurchaseXmlPaymentTerms.cs`, `ConfirmPurchaseUseCases.cs`, `PurchaseInvoice.cs`,
`purchaseReceptionService.ts`, `usePurchasesPage.ts`, `PurchasesPage.tsx`.
Pruebas: `PurchaseXmlPaymentTermsTests.cs`, `PurchaseRetentionConfirmIntegrationTests.PaymentTerms.cs`,
`usePurchasesPage.draftRecovery.test.ts`, `PaymentScheduleSection.test.tsx`.
Documentación existente: esta entrada, `docs/architecture/purchases.md`, `docs/agent/WORK_QUEUE.md`.
Sin migraciones, tablas/columnas, bases persistentes, históricos confirmados, cambios en lógica de
inventario/contabilidad/fiscalidad, formas de pago SRI, redondeos, staging, commit ni push.
Pendiente gate focal y validación funcional del propietario; no CLOSED.

Gate del propietario desde la raíz (Docker requerido únicamente para PostgreSQL desechable):
```powershell
dotnet test backend/src/ERP.Application.Tests/ERP.Application.Tests.csproj --no-restore --filter "FullyQualifiedName~PurchaseXmlPaymentTermsTests|FullyQualifiedName~CreatePurchaseReceptionDraftHandlerTests|FullyQualifiedName~ConfirmPurchaseHandlerTests|FullyQualifiedName~PaymentTermDefaultResolverTests" --verbosity quiet
dotnet test backend/src/ERP.Infrastructure.Tests/ERP.Infrastructure.Tests.csproj --no-restore --filter FullyQualifiedName~Payment_terms_ --verbosity quiet
Push-Location frontend
npm.cmd run test:unit -- src/modules/purchases/hooks/usePurchasesPage.draftRecovery.test.ts src/modules/purchases/pages/PaymentScheduleSection.test.tsx src/modules/purchases/hooks/usePurchasesPage.pilotFix.test.ts src/modules/purchases/hooks/usePurchasesPage.retention.test.ts
npm.cmd run lint
npm.cmd run build
npm.cmd run architecture:check
Pop-Location
git diff --check
```

## SRI 2.34 R32 — cierre operativo de conciliación — READY FOR REVIEW (2026-10-31)

Base `main@fe5d20b8a`. BUG REAL documental corregido en §5.1 de la matriz SRI; decisión del propietario sustituye
las menciones inferiores a una decisión pendiente de R32 o reenvío de lo nunca registrado.
KEEP: Monitor con reconsulta, mensajes, intentos y registro de evidencia con permisos existentes.
FALTA resuelta: el timeline ahora muestra el motivo persistido de auditoría (referencia y observación);
aviso de conciliación y escalamiento en el detalle, sin nuevas acciones fiscales ni infraestructura.
Contrato: [backend.md § Límites y finalización](docs/architecture/backend.md#límites-y-finalización).
PASS agente: Application timeline 5/5, frontend Monitor 18/18, lint (0 errores, advertencias existentes),
build (`tsc -b`, guard de plataforma y Vite), `architecture:check` (140/140, 0 nuevas), `git diff --check`.
Sin migraciones, bases de datos, lógica R11/R12, SRI real, staging, commit ni push.
CI de la consolidación anterior pendiente según el propietario; no CLOSED, pendiente gate del propietario.

Gate focal del propietario (PowerShell desde la raíz; sin BD ni SRI real):
```powershell
dotnet test backend/src/ERP.Application.Tests/ERP.Application.Tests.csproj --no-restore --filter FullyQualifiedName~GetElectronicDocumentTimelineQueryHandlerTests --verbosity quiet
Push-Location frontend
npm.cmd run test:unit -- src/modules/electronicDocuments/monitor/components/ElectronicDocumentDetailPanel.test.tsx src/modules/electronicDocuments/monitor/components/ElectronicDocumentsTable.test.tsx
npm.cmd run lint
npm.cmd run build
npm.cmd run architecture:check
Pop-Location
git diff --check
```

## Consolidación de migraciones EF — migración única — READY FOR REVIEW (2026-11-01)

Base `main@8ded8783e`. Decisión del propietario: las 4 migraciones (`20261021162651_InitialProductionBaseline`,
`…_PhysicalDocumentAuthorization`, `…_IssuerFiscalDesignations`, `…_CorrectedRetransmission`) se sustituyen por la
única `20261101012214_InitialProductionBaseline` + ModelSnapshot regenerado; los objetos raw SQL (Up y Down) se
copiaron byte a byte. Deja sin efecto la decisión P0 de conservar la incremental `PhysicalDocumentAuthorization` y
las menciones a migraciones aditivas de R12, R21/R22 y F-1..F-5 en las entradas inferiores. Sin cambios de modelo,
módulos, reglas SRI ni bootstrap. Detalle y equivalencia: [DATABASE.md § Migraciones](docs/DATABASE.md#migraciones).
PASS agente: oráculo PostgreSQL 16 desechable (cadena anterior frente a la única) con catálogo y hash de datos de las
217 tablas idénticos; `has-pending-model-changes` sin cambios; ensayo de `Down`; Architecture 2/2 (raw SQL),
Infrastructure PostgreSQL 165/165 (raw SQL físicos, First Run, catálogos SRI, R12, R21/R22, Compras, precisión),
API 12/12 (First Run HTTP, readiness), build de la solución, `architecture:check` (0 nuevas), `git diff --check`.
Base local `erp_zh_dev` (localhost:5435, vacía) recreada con la migración única. `erp_zh_test` **no** se recreó:
contiene datos (1 tenant, 2 empresas, 7 ventas, 4 compras) y una cadena anterior; queda a decisión del propietario.
Sin producción, suites completas, staging, commit ni push. No CLOSED.

## SRI 2.34 R12 Fase B — corrección y retransmisión 01/04/07 — READY FOR REVIEW (2026-10-31)

Base `main@6505e5734`. Única salida de `Rejected`: comando explícito del Monitor (permiso
`electronic-documents.retransmit-corrected`, motivo, clave de idempotencia) para errores del archivo
35/36/47/48/52/58/67 con procedencia registrada. Consulta única del historial SRI, XML regenerado por el mismo
pipeline, corrección semántica demostrada (sin firma; clave, contraparte y valores comerciales intactos),
evidencia versionada con SHA-256, reclamo bajo el lock de Fase A y una transmisión; NO AUTORIZADO históricos
descartados por fecha del SRI; R11 por intento. Migración aditiva `CorrectedRetransmission`. Contrato:
[backend.md § Límites y finalización](docs/architecture/backend.md#límites-y-finalización); addendum R12 Fase B
de [ADR-023](docs/decisions/ADR-023-electronic-documents-v1-closure.md); límites en §5.2 de la
[matriz](docs/sri/SRI-2.34-COMPLIANCE-CLOSURE.md).
PASS agente: Domain 62/62, Application 992/992 (Sales, Retentions, ElectronicDocuments, Communications, SRI),
Infrastructure PostgreSQL + SRI 497/497 y nuevas suites 29/29 en el build final, API focal 95/95, Architecture
143/143, frontend Monitor 20/20, lint, `tsc -b`, build, guardrails PowerShell, `architecture:check` (0 nuevas),
manifiesto CI (cobertura PASS), `git diff --check`. Cierre previo al commit: `fechaAutorizacion` de cada
decisión solo se acepta en ISO 8601 con desplazamiento explícito, truncada al microsegundo (sin zona o en otro
formato → null, fail-closed); recuperación tras reclamo con caída/timeout y reinicio del worker (una sola
transmisión) y orden historial → commit → Recepción verificados. Re-ejecución: Application 996/996,
Infrastructure ElectronicDocuments/SRI/Retenciones 456/456. Pendiente: aceptación real en `celcer` de un reenvío
corregido. Sin SRI real, suites completas, staging, commit ni push.

**ED-V1-CLOSE-07 (2026-11-04) — V1 aprobado por el propietario `[V1-APPROVED:ElectronicDocuments-07]`.**
Decisión del propietario: el alcance V1 se acredita con SRI simulado, sin provocar un rechazo artificial
en `celcer` (los códigos corregibles son estructurales y el XSD local los detiene antes del envío). Se
cerró el único hueco de cobertura, la nota de crédito 04, solo con pruebas; sin código productivo ni
defectos reales. PASS focal: Application 71/71 (`ElectronicDocumentRetransmissionPolicyTests`,
`ElectronicDocumentXmlCorrectionComparerTests`, `ElectronicDocumentCorrectedRetransmissionTests`: NC
elegible por Autorización y Recepción, código no corregible y devolución anulada bloquean; 13 campos
protegidos de la NC abortan; recorrido con una transmisión, fallo de transporte solo consulta, devolución
anulada bajo el lock e idempotencia); PostgreSQL 15/15 (`ElectronicDocumentCorrectedRetransmissionPostgreSqlTests`,
incluye reclamo concurrente bajo el lock de la devolución e idempotencia de la NC); HTTP 3/3 (403 sin
`electronic-documents.retransmit-corrected`, sin efectos). Resto de R12 reutilizado sin reejecutar. La
aceptación real en `celcer` de un reenvío corregido queda como evidencia futura oportunista, no como
requisito V1. Evidencia: [Application 71/71](TestResults/ed-v1-closure-01/r12-nc-application.trx),
[PostgreSQL 15/15](TestResults/ed-v1-closure-01/r12-nc-postgresql.trx) y
[HTTP 3/3](TestResults/ed-v1-closure-01/ed-actions-http-403.trx).

## SRI 2.34 R12 Fase A — concurrencia fiscal 01/04 — READY FOR REVIEW (2026-10-31)

Base `main@98f47a750`. Guard `Sales` (Factura 01 y NC 04): el reclamo de envío revalida la factura/devolución bajo
su lock y persiste `Signed` (sin `Dispatching`; job, consulta, R11 y Caso #32 sin cambios). `CancelSalesInvoice`
decide por el estado fiscal: `Authorized` e incierto bloquean; `Draft`/`Failed` → `Discarded`; `Rejected` anula.
Contrato: [backend.md § Revertir una venta](docs/architecture/backend.md#revertir-una-venta-un-solo-camino-por-factura-zh-sales-cancel-authorized-return-rule-01);
addendum R12 Fase A de [ADR-023](docs/decisions/ADR-023-electronic-documents-v1-closure.md).
PASS agente: Application focal 512/512, PostgreSQL real 274/274 (nueva suite 19/19 + regresión 01/04/07, R11/#32),
API focal 33/33, Architecture 143/143, guardrails y `architecture:check` (0 nuevas), `git diff --check`; RED
controlado (protocolo Dispatching en Ventas: 4 FAIL; sin decisión fiscal: 9 FAIL). Fase B (corrección y
retransmisión desde `Rejected`) no implementada. Sin SRI real, suites completas, migraciones, staging, commit ni push.

## SRI 2.34 — cierre de conformidad — READY FOR REVIEW (2026-10-31)

Base `main@8e5bfde66`. Matriz R01–R35, evidencia y bloqueos: [SRI-2.34-COMPLIANCE-CLOSURE](docs/sri/SRI-2.34-COMPLIANCE-CLOSURE.md).
Contrato: [backend.md § Límites y finalización](docs/architecture/backend.md#límites-y-finalización);
addendum 2026-10-31 de [ADR-023](docs/decisions/ADR-023-electronic-documents-v1-closure.md).
CORREGIDO (focal, núcleo FROZEN): R11 consulta de resultado SRI pendiente durante 24 h desde RECIBIDA (mismo job
y política), R13 DEVUELTA 50, R14 varias `<autorizacion>`, R16 tamaño del XML firmado (límite provisional), R23
`contribuyenteEspecial` en 01/04; ambiente ↔ host oficial SRI validado al guardar y antes de emitir.

PASS agente: Application 528/528 (ajustes finales), PostgreSQL real R11 28/28, Infrastructure SRI + PostgreSQL
185/185 (entrega previa), API job 9/9, Architecture
143/143, guardrails PowerShell y `architecture:check` (0 nuevas), `git diff --check`; RED controlado de R11, R13
y R14. Sin SRI real, suites completas, migraciones, frontend, staging, commit ni push.

BLOCKED (detalle en la matriz): R12, R20 (incompatibilidad oficial ficha ↔ XSD; protección implementada: RIMPE
Negocio Popular no emite 01/04/07), R32 (reenvío de lo nunca registrado), §N; limitados: R16
(unidad), R11 sin evidencia de recepción (consulta sin plazo) y hosts no oficiales. Pendiente aceptación real en `celcer` de 04, 07 y de 01/04 con `contribuyenteEspecial`. No CLOSED.

## SRI 2.34 R21/R22 — perfil fiscal del emisor — READY FOR REVIEW (2026-10-31)

Base `main@a65b00310`. Agente de retención (Anexo 21) y Gran Contribuyente (Anexo 24) como pares número + vigencia
en `Company` (migración aditiva `IssuerFiscalDesignations`), en Configuración › Empresa › Fiscal; XML 01/04/07
(`agenteRetencion`) y 01/04 (`campoAdicional` "Gran Contribuyente") solo con resolución vigente a la fecha de
emisión; RIDE con agente de retención y contribuyente especial; gate de emisión y formulario con el formato XSD de
`contribuyenteEspecial`. Detalle: [§3.1 de la matriz](docs/sri/SRI-2.34-COMPLIANCE-CLOSURE.md); addendum R21/R22 de
[ADR-023](docs/decisions/ADR-023-electronic-documents-v1-closure.md). Pendiente: resoluciones reales del piloto y
aceptación en `celcer`. Sin SRI real, suites completas, staging, commit ni push.

## P0 instalación limpia — READY FOR REVIEW (2026-10-31)

Base `main@d462cddde19a0b8196cf75089f388a4316dae2ad`, CI SUCCESS confirmado por el
propietario. Esta confirmación actualiza los pendientes de commit/CI de las entradas
históricas incluidas en ese commit (F-1..F-5 y corrección PR-7 `2e0977ec5`, entre ellas);
no acredita aceptación visual, E2E SRI ni nuevos cierres CLOSED. Se conservan abajo
las entradas originales como historial de la evidencia disponible en cada entrega.

Decisión del propietario para P0: conservar `PhysicalDocumentAuthorization` incremental,
sin consolidar la baseline ni modificar bases persistentes. Evidencia de ejecución:
[P0 instalación limpia](docs/evidence/p0-clean-install-20261031/README.md).

PASS agente: PostgreSQL 16 desechable vacío → ambas migraciones aplicadas, sin cambios
pendientes del modelo EF; 28 tablas GLOBAL y política física bloqueada preservadas.
Infrastructure 84/84, API 23/23 (arranque First Run y consulta operativa HTTP real),
Application 5/5, frontend público 4/4 y cuatro scripts oficiales del launcher PASS.
Sin error reproducido de instalación. Sin cambios de código ni bases persistentes,
emisión SRI, staging, commit o push. Pendiente gate/revisión del propietario; no CLOSED.

## F-1..F-5 — corrección final: captura acotada y Régimen General — READY FOR REVIEW (2026-10-30)

1. Numeración física: el rango autorizado se comprueba bajo el lock de Secuencias Documentales
   (sobrecarga `CaptureNextAsync(…, authorizedRanges)`, [extensión](docs/architecture/frozen-infrastructure.md#secuencias-documentales));
   prueba de concurrencia real PostgreSQL (12 consumidores por el último número: uno lo obtiene, ninguno lo supera;
   RED confirmado sin la comprobación).
2. Régimen General: una autorización registrada habilitaba la factura física ordinaria. Fila General → 01
   inactiva (BLOCKED por falta de fundamento oficial verificable); hoy ningún régimen emite en físico.
   Nota de venta 02 sigue no implementada. Migración incremental conservada (editada, no aplicada aún).
Pendiente revisión/gate del propietario y CI; no CLOSED. Sin staging, commit ni push.

## F-1..F-5 — política fiscal de emisión física — READY FOR REVIEW (2026-10-30)

Política propietaria `IPhysicalEmissionRequirementsValidator` (Company), integrada en la apertura de caja,
Readiness y el gate de autorización de ventas antes de `CaptureNextAsync`; SSOT:
[Política fiscal de emisión física](docs/architecture/company-readiness.md#política-fiscal-de-emisión-física-f-1f-5).
Nuevos datos: `physical_document_authorization` y catálogo `global.sri_tax_regime_physical_doc_type`
(migración incremental `PhysicalDocumentAuthorization`; consolidación en la baseline a decisión del propietario).
UI: autorizaciones de impresión en Secuencias documentales. ElectronicDocuments sin cambios.
BLOCKED (fail-closed): factura física ordinaria del Régimen General (ver entrada superior), factura física RIMPE Emprendedor y cupo transitorio de preimpresos 2024.
Nota de venta 02 no implementada (sigue inactiva). La emisión física no queda certificada legalmente:
la autorización y su circunstancia excepcional no se verifican ante el SRI.

Evidencia agente (focal): Application 133, Infrastructure 62 + 151 (PostgreSQL real: ReadinessGap,
catálogos GLOBAL, objetos raw, secuencias), API 86 (E2E de caja/ventas/devoluciones/cobros con la
política física real), Architecture.Tests 143; frontend vitest 31 + 51, `tsc -b`, lint (0 errores), build,
`architecture:check` (140/140, gate PASS, 0 violaciones nuevas). Sin suites completas.
Pendiente revisión/gate del propietario y CI; no CLOSED. Sin staging, commit ni push.

## CI 2B-3 — corrección focal PR-7 — READY FOR REVIEW (2026-10-30)

Clasificación: BUG REAL del gate en `598941f6f` (CI `41151151161`); KEEP comportamiento
y cobertura de [2B-3](docs/architecture/company-readiness.md#habilitación-preventiva-de-cajas-apertura-de-jornada).
Extracción literal de `SummaryCard` y columnas del detalle de cobros a `caja/components`, y de fixtures a
`useCajaPage.testFixtures.ts`, conservando setup, cleanup y cuerpos de pruebas.
PR-7: `CajaPage.tsx` 834/886 líneas no vacías; `useCajaPage.test.tsx` 1248/1390.
Sin cambios en límites, excepciones, ADR-047, reglas fiscales, backend ni contratos.

Evidencia agente: 72/72 pruebas focales, TypeScript, lint (0 errores; 37 advertencias),
build, architecture:check (140/140 fixtures; cero infracciones nuevas), guardrails PowerShell (árbol completo y chunk) y `git diff --check` PASS.
Gate del propietario desde `frontend`:
`npx.cmd vitest run src/modules/caja/hooks/useCajaPage.test.tsx src/modules/caja/hooks/useCajaPage.closingCounts.test.tsx src/modules/caja/pages/CajaPage.openingReadiness.test.tsx`;
`npx.cmd tsc -b --pretty false`; `npm.cmd run lint`; `npm.cmd run build`;
`npm.cmd run architecture:check`.
Desde raíz: `powershell -NoProfile -ExecutionPolicy Bypass -File tools/architecture/check-architecture-guardrails.ps1`;
`git diff --check`.
Pendiente revisión/gate del propietario y CI; no CLOSED. Sin staging, commit ni push;
F-1 a F-5 en la entrada superior.

## BUG REAL — diagnóstico de certificado SRI — IMPLEMENTADO — PASS FOCAL — PENDIENTE COMMIT/CI (2026-10-30)

`ValidateSriConfigurationQueryHandler` podía devolver `IsValid=true` con certificado
instalado y contraseña ausente: omitía el control de contraseña. La corrección agrega
el control obligatorio `certificatePassword=false` en ese caso; contrato vigente en
[Backend — diagnóstico de configuración SRI](docs/architecture/backend.md#diagnóstico-de-configuración-sri).

Evidencia del agente: RED de la regresión antes de corregir; GREEN 15/15 pruebas focales
del handler y aislamiento por empresa. `ValidateSriConfigurationQueryHandlerTests`
comprueba contraseña ausente con resolver real y almacenamiento sintético, y conserva
los casos de certificado válido, vencido, contraseña incorrecta y archivo no instalado.
Comando ejecutado desde la raíz:
`dotnet test backend/src/ERP.Application.Tests/ERP.Application.Tests.csproj --no-restore --filter "FullyQualifiedName~ValidateSriConfigurationQueryHandlerTests|FullyQualifiedName~ElectronicInvoicingCompanyScopeTests" --verbosity minimal`.
`git diff --check` PASS. El PASS focal no sustituye el gate del propietario ni acredita E2E.

Sin cambios en firma, XML/XSD, SOAP ni transmisión al SRI; sin acceso a certificados
reales ni secretos, suites generales, migraciones, configuración operativa, staging,
commit ni push. Pendiente commit/CI; no CLOSED.

## BUG REAL — recuperación de notas de crédito — IMPLEMENTADO — PASS FOCAL — PENDIENTE COMMIT/CI (2026-10-30)

Una devolución autorizada con número definitivo de nota de crédito, pero sin
`ElectronicDocument`, no era recuperada automáticamente: `SalesElectronicDocumentRecovery`
descubría únicamente facturas. La regresión PostgreSQL
`Authorized_return_without_electronic_document_is_recovered_once_by_registered_sales_recovery`
reprodujo FAIL con 0 documentos recuperados, cuando se esperaba exactamente un CreditNote.
La corrección amplía el recuperador existente a notas de crédito elegibles mediante
`RegisterMissingAsync` y el mismo job; contrato vigente en
[Backend — ownership de recuperación](docs/architecture/backend.md#lotes-fairness-y-ownership).

PASS focal del agente: 21/21 pruebas PostgreSQL (regresión y casos A9 del recuperador)
y 1/1 prueba del job. La regresión comprueba exactamente un CreditNote en ambas
rondas, estado Draft, número y snapshots comerciales intactos. Se conservan
aislamiento, idempotencia, protección ante carreras y checkpoint; sin nuevos
secuenciales ni repetición de efectos comerciales. Sin cambios en el motor SRI ni
en el esquema de BD. Revisión final: APTO PARA GATE; no sustituye el gate del propietario.

Evidencia local: `artifacts/credit-note-recovery/sales-recovery-focal.trx` y
`artifacts/credit-note-recovery/sales-recovery-job.trx`. Las pruebas no certifican
transmisión real al SRI ni E2E comercial completo; el harness usa contexto fijo y
publisher vacío, y las carreras existentes cubren facturas. Sin suites generales,
staging, commit ni push. Pendiente commit/CI; no CLOSED. No cambia el alcance ni
el estado del caso #32 y no reabre otros módulos CLOSED.

## Compras — detalle compacto y resaltado — IMPLEMENTADO — PENDIENTE VALIDACIÓN VISUAL DEL PROPIETARIO (2026-10-29)

> Registro histórico: estado vigente [Compras V1 CLOSED](#doc-cleanup-compras-01--compras-v1-aprobado-y-closed-2026-11-03).
> Los pendientes corresponden a su fecha; se conservan resultados y límites sin acreditar nuevos gates.

Clasificación: FALTA autorizada en la presentación; KEEP datos, edición, guardado,
reapertura y lógica operativa/fiscal. `PurchasesPage.tsx` reutiliza las tarjetas y
botones ZH existentes, con el patrón de expansión por identificador del modal de
pendientes: inicio contraído, Detalle individual y control alternante Expandir todo /
Contraer todo limitado a las líneas de la compra. La cabecera esencial permanece
visible; contraer solo cambia estado de UI. Estado efímero, reiniciado al abrir otra
compra. `purchases-invoice.css` aplica el token compartido `--color-surface-hover`
a la tarjeta y rail por hover/focus-within, sin sobrescribir el foco de los controles.

PASS agente: `PurchaseLineCard.compact.test.tsx` 18/18 (incluye inicio contraído,
expansión global/individual, aislamiento de otra tarjeta, foco conservado, líneas
sin mutaciones y remontaje contraído); TypeScript y diff --check PASS. ESLint focal
sin errores; advertencia existente de tamaño de `PurchasesPage.tsx` (max-lines).
Gate técnico del propietario desde `frontend`:
`npx.cmd vitest run src/modules/purchases/pages/PurchaseLineCard.compact.test.tsx`;
`npx.cmd tsc -b --pretty false`;
`npx.cmd eslint src/modules/purchases/pages/PurchasesPage.tsx src/modules/purchases/pages/PurchaseLineCard.compact.test.tsx`.
Pendiente revisión visual de hover, foco por teclado y expansión con compra real,
incluida edición/guardado/reapertura; las pruebas de componente no acreditan ese
recorrido persistido. Sin navegador, suites generales, backend, BD, migraciones,
baseline, caso #32, otros módulos, staging, commit ni push; no CLOSED.

## BUG REAL DISURGAL — corregido; validación funcional del propietario PASS DISURGAL y ALPINA (2026-10-29)

> Registro histórico: el estado funcional vigente es [Compras V1 CLOSED](#doc-cleanup-compras-01--compras-v1-aprobado-y-closed-2026-11-03).
> Pendientes y límites de esta entrada corresponden a su fecha; no acreditan nuevos gates ni reabren el módulo.

El rechazo reproducido provenía de StockMovement.Create: PurchaseEntry exigía costo > 0
sin reconocer las promociones XML legítimas. El repositorio valida evidencia de la compra
confirmada/detalle congelado y del origen fiscal antes de habilitar exclusivamente el cero
documentado; compras ordinarias a cero, costo desconocido y negativo siguen bloqueados.
Regla vigente: [Compras § 4](docs/architecture/purchases.md#4-confirmación).
RED: las dos variantes DISURGAL reprodujeron el mensaje exacto; GREEN Application 74/74,
Domain 13/13 y PostgreSQL desechable 2/2. La prueba real de confirmación conserva 13 líneas,
4 promociones, XML intacto, Kardex 473.77 y CxP/asiento balanceado 544.84; la compra ordinaria
con línea a cero revierte sin efectos persistidos. Compilación API con salida aislada PASS
(la salida habitual estaba bloqueada por la API en ejecución); architecture:check PASS, cero
infracciones nuevas, y git diff --check PASS. Sin cambios en bases existentes,
modelo EF, migraciones, baseline, caso #32, staging, commit ni push; sin E2E/navegador/suites generales.
Gate del propietario desde la raíz:
`dotnet test backend/src/ERP.Application.Tests/ERP.Application.Tests.csproj --no-restore --filter "FullyQualifiedName~RealSupplierXmlRegressionTests|FullyQualifiedName~ConfirmPurchaseHandlerTests|FullyQualifiedName~PurchaseXmlConfirmationGuardTests"`
`dotnet test backend/src/ERP.Domain.Tests/ERP.Domain.Tests.csproj --no-restore --filter "FullyQualifiedName~StockMovementValuationCostTests"`
`dotnet test backend/src/ERP.Infrastructure.Tests/ERP.Infrastructure.Tests.csproj --no-restore --filter "FullyQualifiedName~Disurgal_promotions_confirm|FullyQualifiedName~Ordinary_zero_cost_purchase"`
El último comando requiere Docker/PostgreSQL desechable.
Validación funcional del propietario (DISURGAL `001-001-000005545`): PASS. Confirmación real
con 13 líneas y promociones del 100 %; inventario 858 unidades en 9 productos; Kardex e
inventario valorado $473.77; CxP pendiente $544.84; asiento N.º 1 Debe Inventario $473.77 +
IVA crédito tributario $71.07, Haber CxP $544.84; Libro Diario y Balance de Comprobación PASS;
diferencia fiscal de $0.01 conciliada al confirmar.
Validación funcional integral del propietario (ALPINA `012-004-004253387`): PASS. XML real con
4 líneas, IVA 0 % y total $16.15; confirmación PASS; inventario y Kardex 20 unidades en 4 productos,
$16.15; CxP una cuota de $16.15, sin pagos, saldo $16.15; asiento N.º 2 Debe Inventario $16.15,
Haber CxP $16.15; Libro Diario, Balance de Comprobación y Balance General PASS. Supera el bloqueo
de carga ALPINA registrado el 2026-10-28 (informe histórico conservado abajo).
Acumulado DISURGAL + ALPINA: inventario 878 unidades en 13 productos, $489.92; Debe/Haber
$560.99 / $560.99. El PASS cubre solo estos dos escenarios: siguen pendientes los demás
escenarios de Compras; Compras V1 no CLOSED.

## CI Infrastructure-01/06 Compras XML — fixtures alineados; pendiente gate del propietario (2026-10-29)

> Registro histórico: el estado funcional vigente es [Compras V1 CLOSED](#doc-cleanup-compras-01--compras-v1-aprobado-y-closed-2026-11-03).
> Pendientes y límites de esta entrada corresponden a su fecha; no acreditan nuevos gates ni reabren el módulo.

Reproducidos localmente los 3 FAIL del CI `40973768743` (PostgreSQL desechable). Sin BUG REAL de producción;
la corrección de promociones (entrada anterior) se conserva intacta.
- `Xml_service_without_global_warehouse_confirms_without_inventory`: fixture incorrecto; la línea de
  recepción no persistía su snapshot de impuestos y el XML no tenía `totalConImpuestos`.
- `Resolved_XML_saves_and_confirms_with_correct_base_stock_cost_and_Kardex`: fixture incorrecto; el XML
  sintético compartido (`BuildXml`) no tenía `totalConImpuestos`, una cabecera incompleta que
  [Compras § 2](docs/architecture/purchases.md#2-borrador) rechaza.
- `Draft_saved_before_a_correction_…`: expectativa desactualizada; `a55057a9c` retiró a propósito la
  exigencia de volver a guardar (el producto operativo puede diferir del vínculo XML). Decisión del
  propietario: alinear con la SSOT. El test pasa a llamarse
  `Correction_after_saving_keeps_the_saved_operational_product` (borrador sin reescritura, confirma con B y
  la recepción conserva C).
KEEP: guard de snapshot XML, conciliación fiscal, XML original y código productivo; solo cambian pruebas.
RED 0/3 → GREEN 3/3. Regresiones PASS: ambas clases de integración PostgreSQL 112/112 (incluyen promociones
DISURGAL) y Application focal 74/74; `git diff --check` PASS.
Gate del propietario (Docker):
`dotnet test backend/src/ERP.Infrastructure.Tests/ERP.Infrastructure.Tests.csproj --no-restore --filter "FullyQualifiedName~ResolvePurchaseReceptionLinesIntegrationTests|FullyQualifiedName~PurchaseRetentionConfirmIntegrationTests"`.
Sin BD, migraciones, baseline, caso #32, Ítems, suites generales, E2E, navegador, staging, commit ni push; no CLOSED.

## CI Compras XML V1 — pruebas alineadas; pendiente gate del propietario (2026-10-29)

> Registro histórico: el estado funcional vigente es [Compras V1 CLOSED](#doc-cleanup-compras-01--compras-v1-aprobado-y-closed-2026-11-03).
> Pendientes y límites de esta entrada corresponden a su fecha; no acreditan nuevos gates ni reabren el módulo.

Reproducidos los 3 FAIL reportados para `217d81cd8` (CI `40969761501`, Backend application).
Clasificación: BUG REAL en expectativas obsoletas de pruebas; KEEP implementación fiscal V1.
La SSOT [Compras §2](docs/architecture/purchases.md#2-borrador) conserva importes fiscales
al editar cantidad/precio operativo y exige conciliación al confirmar. Se retiraron las
expectativas de IVA recalculado y Source=Calculated de esos tres casos, manteniendo
comprobaciones de edición operativa, procedencia XML y recepción original intacta.
Sin cambios productivos, de SSOT, BD ni migraciones. PASS agente: 3/3 corregidas y 76/76
focales (incluyen esas tres, recuperación, precisión/costos, guard de confirmación,
XML reales DISURGAL/ALPINA y desglose). Comando para el gate del propietario:
`dotnet test backend/src/ERP.Application.Tests/ERP.Application.Tests.csproj --no-restore --filter "FullyQualifiedName~PurchaseDraftRecoveryTests|FullyQualifiedName~PurchaseDraftExplicitCostAllocationTests|FullyQualifiedName~PurchaseXmlConfirmationGuardTests|FullyQualifiedName~RealSupplierXmlRegressionTests|FullyQualifiedName~PurchaseXmlBreakdownTests"`.
Sin suites generales, E2E, navegador, staging, commit ni push; no CLOSED.


## Compras XML: identidad de lineas repetidas — pendiente validacion del propietario (2026-10-29)

> Registro histórico: el estado funcional vigente es [Compras V1 CLOSED](#doc-cleanup-compras-01--compras-v1-aprobado-y-closed-2026-11-03).
> Pendientes y límites de esta entrada corresponden a su fecha; no acreditan nuevos gates ni reabren el módulo.

BUG REAL: el merger emparejaba por posicion grupos de igual codigo/descripcion y conteo,
cruzando normales/promociones cuando cambiaba la enumeracion de la recepcion.
Los grupos repetidos ahora conservan el snapshot de cada PurchaseReceptionLineId y sus
PurchaseReceptionLineTaxId. KEEP: payload, lectura por ID, edicion operativa, XML inmutable
y conciliacion fiscal. Sin cambios de BD/EF/migraciones ni reparacion de borradores existentes.
PASS focal: backend 35/35 (incluye XML DISURGAL invertido y conciliacion 544.84), frontend
25/25 (guardar/reabrir las seis lineas normales/gratuitas en orden inverso).
Compilacion backend incluida en el test focal; TypeScript y diff --check PASS. Gate real PostgreSQL y funcional a cargo del
propietario; no CLOSED, sin staging, commit ni push. Comandos tecnicos:
`dotnet test backend/src/ERP.Application.Tests/ERP.Application.Tests.csproj --no-restore --filter "FullyQualifiedName~RealSupplierXmlRegressionTests|FullyQualifiedName~CreatePurchaseReceptionDraftHandlerTests"`
y desde frontend: `npx.cmd vitest run src/modules/purchases/hooks/usePurchasesPage.draftRecovery.test.ts`.

## Modal Editar empresa — IMPLEMENTADO — PENDIENTE VALIDACIÓN VISUAL DEL PROPIETARIO (2026-10-29)

BUG REAL de composición: campos sin `ZHGrid` y botones dentro del cuerpo, fuera del footer
oficial. Adaptado a `ZHGrid`, `ZHField`, `ZhTextInput`, checkbox compartido y `footer` de
`ZHModal`, con Guardar asociado al formulario y acciones deshabilitadas durante el envío.
Estilos globales existentes reutilizados, sin CSS nuevo ni cambios de negocio/API/permisos.
PASS focal: componentes 15/15, TypeScript, ESLint de los dos archivos afectados y compilación
frontend. Sin suites generales, E2E ni navegador. Validación visual real pendiente del propietario;
sin commit ni push. Contrato de composición: [modales](docs/architecture/modal-standard.md#formularios-dentro-del-modal).


## Compras XML V1 — IMPLEMENTADO — PENDIENTE VALIDACIÓN FUNCIONAL DEL PROPIETARIO (2026-10-29)

> Registro histórico: estado vigente [Compras V1 CLOSED](#doc-cleanup-compras-01--compras-v1-aprobado-y-closed-2026-11-03).
> Los pendientes corresponden a su fecha; se conservan resultados y límites sin acreditar nuevos gates.

- Regla única vigente: producto, cantidad, presentación, bodega y costo operativo editables
  antes de confirmar, sin igualdad literal con las líneas XML. Se mantienen conversiones reales,
  conciliación fiscal por categoría/tarifa, CxP exacta y persistencia normal de los costos finales
  usados por inventario, Kardex y contabilidad. XML/recepción fiscal intactos;
  desglose reutilizado. Sin EF, baseline, migraciones, escrituras de BD ni cambios al caso #32.
- DISURGAL `001-001-000005545`: regresión con XML real, descuentos del 100 %, cantidades,
  presentaciones y costos editados: costo final 473.77, IVA 71.07, CxP 544.84; ambos snapshots
  originales del XML conservados (detalle base/IVA 473.78/71.06). No hay asiento ficticio.
- PASS técnico focal: Application 98 casos (97 + regresión nueva de documento duplicado),
  Domain 32 y frontend 94; compilación API y frontend, TypeScript, ESLint de archivos afectados,
  guardrails de capas/precisión/imports y diff sin errores. Los tests comprueban también el
  evento contable balanceado, la base retenible y las unidades reales de inventario de DISURGAL.
- Reglas y limitaciones: [SSOT Compras](docs/architecture/purchases.md#2-borrador).
  Pendiente el gate manual del propietario; no CLOSED. No se ejecutan suites generales,
  E2E, navegador, API ni PostgreSQL; no se repiten R1–R4. Sin staging, commit ni push.
- Sigue vigente el bloqueo de esquema ALPINA descrito abajo: esta corrección no modifica bases.


## Compras XML reales — antecedentes DISURGAL y bloqueo de esquema ALPINA (2026-10-28)

> Registro histórico: estado vigente [Compras V1 CLOSED](#doc-cleanup-compras-01--compras-v1-aprobado-y-closed-2026-11-03).
> Los pendientes corresponden a su fecha; se conservan resultados y límites sin acreditar nuevos gates.

Revisión focal de DISURGAL `001-001-000005545` y ALPINA `012-004-004253387`;
no reabre el cierre histórico R2 ni continúa #32.

- **ALPINA — BUG REAL de esquema:** el log del POST de 18:37:49 (respuesta correlacionada
  `77767d8d-0cd0-4cbb-be8b-cf9b4ec33f0a`) muestra PostgreSQL `23502`, no duplicidad:
  `purchase_invoice_details.freight_allocated` recibe NULL. API efectiva en
  `localhost:5435/erp_zh_dev` (Development, user-secrets sin override de conexión).
  Inspección expresamente autorizada, mediante `BEGIN READ ONLY` y `ROLLBACK`: tanto
  `freight_allocated` como `other_costs_allocated` son `numeric(18,6) NOT NULL`, sin default;
  una fila existente, cero filas con importes distintos de cero en ambas columnas, sin
  dependencias de columna registradas en `pg_depend`. EF las ignora y la baseline/snapshot
  vigentes no las contienen. En aquella inspección, el historial EF de la base contenía dos registros;
  la migración nueva no aplicada fue retirada junto con su prueba de contrato por orden
  del propietario. Reparación SQL directa autorizada en `localhost:5435/erp_zh_dev`:
  respaldo completo `.local/alpina-backup/erp_zh_dev-before.dump` (ignorado por Git),
  verificado por lectura/descompresión con `pg_restore`, SHA-256
  `CDCF2F0172B14902B90BADCFC1501AC1F9F48094D0ADCA65738125531941584A`.
  Transacción con locks y guardas: ambas columnas solo ceros, sin NULL ni dependencias;
  eliminadas exclusivamente con `RESTRICT`, COMMIT confirmado. Conteos y huellas de
  226 tablas / 9570 filas idénticos antes/después, excluyendo solo las dos columnas.
  Verificación posterior: columnas ausentes, detalle existente conservado (1 fila),
  historial EF intacto (2 registros). Baseline, snapshot y migraciones anteriores intactos.
  Reintento del propietario: POST 201 a las 03:06:44.760Z; borrador ALPINA
  `739de567-9683-48c2-a499-36546854011e` persistido con 4 líneas. GET por id a las
  03:06:45.016Z falla con PostgreSQL `42P01`: falta `purchase_cost_distributions`
  (`PurchaseInvoiceRepository.cs:29`); el listado también falla (`:185`).
  El correlationId reportado `6d463e49-a677-4074-bbce-71d0ee50f9d2` no aparece
  literalmente en el log de archivo; asociación por hora y endpoint, no por id probado.
  Inspección PostgreSQL READ ONLY: faltan ambas tablas canónicas de distribuciones y
  las claves únicas compuestas de factura/detalle requeridas por sus FK. La baseline
  vigente sí las define; el respaldo previo tampoco contiene las tablas. BUG REAL de
  esquema independiente del DROP anterior. BLOQUEO: pendiente autorización expresa
  para reparación focal de esas tablas, claves e índices; sin nuevas escrituras de BD,
  cambios EF, migraciones ni código. Carga funcional todavía no PASS.
- **DISURGAL:** la nueva regla operativa y su conciliación están implementadas según la
  entrada vigente superior. Se conservan los fixtures firmados, el descuento exacto y la
  evidencia de diferencia cabecera/detalle; no se mantiene el bloqueo del guard anterior.

Gates focales previos: regresiones de XML reales/descuento/contrato de migración 7/7; Domain
`PurchaseInvoiceDetailMultiTaxTests` + `PurchaseInvoiceCostProrationConsistencyTests` 18/18;
build API PASS; `architecture:check` PASS (0 nuevas); `git diff --check` PASS. El test de
migración fue retirado con ella; corrección ALPINA validada en PostgreSQL real según lo
anterior. Sin suites generales, aplicación de migraciones, commit ni push. No CLOSED.
ALPINA continúa bloqueada en la carga por tablas ausentes según lo anterior. DISURGAL queda
pendiente de validación manual del propietario con la regla vigente de conciliación.

## Caso #32 — IMPLEMENTADO — V1 APROBADO (2026-11-04)

Validación integrada focal de recuperación y conciliación de respuestas inciertas del SRI.
Clasificación: **KEEP** implementación existente; cobertura de integración faltante incorporada.
Sin BUG REAL identificado ni cambios de código productivo en esta validación. Contrato:
[backend.md](docs/architecture/backend.md#límites-y-finalización); addendum ADR-023.

**Decisiones del propietario (2026-11-04), definitivas para V1:** D1 — comprobante que no consta
recibido: permanece en "Requiere conciliación" con escalamiento, sin reenvío ni cierre manual
(reenvío explícito: FUTURE). D2 — retención anterior al ciclo de ADR-036 con origen anulado: se
conserva el tratamiento vigente (evidencia sin consulta), sin cambios en ADR-036 ni en código. D3 —
aprobada y ejecutada (2026-11-04): consulta única de solo lectura (`BEGIN TRANSACTION READ ONLY` …
`ROLLBACK`) sobre `erp_zh_dev` (contenedor `postgreszh`, puerto 5435, entorno Development), comprobantes
`Retentions` en estado incierto (Signed/Sent/Received/Dispatching o DeadLetter desde ellos) con retención
`Cancelled` o inexistente: terminó correctamente, **0 registros**; no hay casos heredados que tratar en
ese entorno. Sin escrituras ni migraciones; no acredita otros entornos. Retira el bloqueo R32 del
Dashboard; ElectronicDocuments-04 sigue sin aprobación V1 hasta la E2E SRI integrada y el gate del
propietario. Sin código productivo.

**PASS focal del agente:** 1/1 integración PostgreSQL en
`backend/src/ERP.Infrastructure.Tests/ElectronicDocuments/ElectronicDocumentUncertainSriPostgreSqlTests.cs`
y 16/16 pruebas relacionadas de `ElectronicDocumentIssuerRetryTests` y
`ReconcileElectronicDocumentCommandHandlerTests`. Evidencias:
`artifacts/case32/case32-integration.trx` y `artifacts/case32/case32-related.trx`.
La prueba verificó dos consultas de autorización con la misma clave, cero llamadas a Recepción,
conservación de `Signed` y conciliación pendiente; evidencia de conciliación persistida con
usuario, tenant, empresa y fecha, sin autorización ni rechazo manual. Aislamiento comprobado
entre tenants y empresas mediante repositorios y persistencia PostgreSQL reales.

**Validación técnica previa (no reejecutada en esta fase):** Domain ElectronicDocuments 52/52 (incluye Kernel/permisos 50/50 en otra
corrida); Application ElectronicDocuments + Retenciones 368/368 y catálogo de permisos +
conciliación 11/11; Infrastructure ED/Retención electrónica/A9 90/90; API Admin permisos + Monitor
11/11; Architecture 143/143; vitest Monitor/diagnóstico/NC 25/25 e i18n 15/15; `tsc -b`, ESLint
focal, build y `architecture:check` (0 violaciones nuevas). `ERP.API` y las pruebas Architecture/API
se compilaron con salida alternativa porque un `ERP.API.exe` ajeno a la sesión (PID 25680) bloqueaba
`ERP.API/bin`.

Limitación (2026-10-30): la integración focal utiliza el SRI simulado; no acredita comunicación real con
los servicios del SRI ni E2E funcional. Los pendientes de entonces (permiso efectivo/denegación,
evidencia visible en el Monitor y reconsulta real sin reenvío) se resuelven en el cierre siguiente.

**ED-V1-CLOSE-04 (2026-11-04) — V1 aprobado por el propietario
`[V1-APPROVED:ElectronicDocuments-04]`.** Alcance V1: comportamiento vigente más las decisiones
D1/D2 (D3 sin casos heredados). Evidencia, sin nuevas llamadas al SRI ni repetición de PASS:
- **Reconsulta real sin reenvío (SRI de Pruebas, `celcer`):** logs originales con SHA-256 de
  `artifacts/sri-incident-01` (SRI-INCIDENT-01, 01–02/11/2026): 5 facturas 01 con SOAP Fault de
  Recepción, exactamente 5 llamadas a Recepción (una por comprobante) y 28 reintentos «solo consulta
  de autorización, sin reenvío»; los 5 comprobantes siguen en `Signed` en `erp_zh_dev`. La NC 04 usa
  el mismo `RetryAsync` del guard `Sales`; su ruta está cubierta por
  `SalesElectronicSourceConcurrencyPostgreSqlTests` (fallo de transporte en 01/04: solo consulta).
- **Permiso efectivo y denegación:** nueva `ElectronicDocumentActionAuthorizationHttpTests` (pipeline
  HTTP real, `RuntimePermissionAuthorizer`, PostgreSQL Testcontainers) 3/3 PASS: sin
  `electronic-documents.reconcile` → 403 sin efectos (estado, contadores, auditoría, intentos); con
  el permiso la solicitud supera la autorización y la rechaza el negocio sin llamar al SRI. UI:
  `ElectronicDocumentDetailPanel.test.tsx` (acción solo con `reconcile`).
- **Conciliación, auditoría, aislamiento y Monitor:** PASS previos de este caso (integración
  PostgreSQL 1/1, relacionadas 16/16, vitest Monitor) sin reejecutar.
Evidencia: [HTTP 3/3](TestResults/ed-v1-closure-01/ed-actions-http-403.trx); los logs de
SRI-INCIDENT-01 se conservan fuera del repositorio (`artifacts/`, excluido) con sus SHA-256. Sin código productivo,
migraciones ni bases persistentes; las pruebas usaron contenedores PostgreSQL efímeros.
El PASS del agente no sustituye el gate del propietario. Sin migraciones, staging, commit ni
push. **No CLOSED.**

## Caso #20 — CLOSED integral por aprobación del propietario (2026-11-03)

**CASO-20-CLOSE:** el propietario aprueba expresamente el cierre integral de la anulación
documental de cobros, incluida la evidencia técnica de CASO-20-DEV-E2E-01 y la aceptación
funcional manual previa. Queda retirado el pendiente técnico del caso #20. Este cierre
no modifica el alcance ni el estado de #32 o #31 y no acredita despliegue ni SRI.
Los pendientes históricos de este caso descritos debajo quedan superados por este cierre.
**KEEP:** implementación productiva existente; sin BUG REAL productivo reproducible detectado.

Test y cierre documental incorporados en `469bd053ad2d889b99f77e159bc6d2ffdbd5233e`.
Confirmación posterior de publicación: `main` limpio y sincronizado con `origin/main`
en `ab8f4fb1bbb9a0690c83f64067bc481d14f84aaf`;
[CI #1076](https://github.com/szhinin89/erp-saas/actions/runs/41765246734) **SUCCESS**
(run `41765246734`, intento 1: 19 jobs exitosos, 0 fallidos y 1 omitido:
Deploy — Azure Web App). Incluye API e `infrastructure-01` exitosos. No acredita
despliegue ni amplía los límites de TestServer y datos sintéticos indicados debajo.
En esta confirmación no se repiten pruebas ni se vuelven a incorporar archivos ya versionados.

**Pruebas efectivamente ejecutadas y aprobadas:** 11 escenarios API/PostgreSQL PASS
en `CollectionReversalTechnicalTests.cs`, reutilizando `FinancialCommandIdempotencyFixture`
y `PostgreSqlTestWebAppFactory`, más 3 pruebas PostgreSQL existentes PASS de
`CollectionPostingIntegrationTests` (restauración del saldo, asiento inverso y republicación
del evento sin duplicar el reverso).

| Escenario API/PostgreSQL | Evidencia | Resultado |
|---|---|---|
| Permiso específico y denegación | Sin permiso 403; usuario con permiso específico 200 | PASS |
| Contexto de otro tenant | 403; cobro Applied y CxC sin alteración | PASS |
| Contexto de otra empresa | 403; cobro Applied y CxC sin alteración | PASS |
| Contexto válido sobre cobro de empresa ajena | 404; cobro Applied conservado | PASS |
| Concurrencia, auditoría, cuotas, nuevo cobro y contabilidad | Dos reversas: 200/422; motivo, actor, fecha UTC y Reversed persistidos; dos cuotas restauradas, saldo 100; nuevo cobro 25 con 201; original conservado y único reverso con débitos/créditos intercambiados | PASS |
| Período efectivo cerrado | 422; rollback de cobro/CxC y ausencia de asiento inverso | PASS |
| Destino bancario | Reversa 200; cuenta bancaria y movimientos de caja conservados | PASS |
| Turno cerrado sin movimiento vinculado | Reversa 200; turno, movimientos y saldo operativo conservados | PASS |
| Turno cerrado con movimiento vinculado | Reversa 422; cobro y estado operativo conservados | PASS |
| Histórico de caja sin trazabilidad | 422; cobro y CxC conservados | PASS |
| Histórico bancario sin trazabilidad | 422; cobro y CxC conservados | PASS |

Evidencias locales no versionadas (no implican nuevas ejecuciones de CI):
- `.local/caso20-results/caso20-http-final.trx`: 10 PASS y un fallo
  de preparación del escenario de empresa ajena por modo/contexto contable incompletos.
- `.local/caso20-results/caso20-foreign-company-final.trx`:
  ese único escenario repetido tras completar sus datos/contexto sintéticos, 1/1 PASS.
  Los 11 escenarios quedan acreditados entre ambas ejecuciones; no se presenta una
  ejecución única 11/11 ni se ocultan los fallos anteriores del arnés/aserciones.
- `.local/caso20-results/caso20-posting.trx`: 3/3 PASS.

**Entorno y límites:** API iniciada en proceso mediante TestServer, pipeline HTTP,
autorización, handlers y persistencia reales; JWT, usuarios y proveedores de contexto
tenant/empresa/actor sintéticos de la fábrica existente. No acredita login real ni un
servidor Kestrel accesible por red. PostgreSQL real vía Testcontainers `postgres:16-alpine`,
en bases nuevas y efímeras `erp_test` y `erp_collection_posting_test`, separadas del
PostgreSQL habitual. Migraciones y escrituras exclusivamente en esas bases autorizadas;
contenedores PostgreSQL de pruebas eliminados automáticamente. Sin uso de `erp_zh_test`,
base habitual ni producción. Sin llamadas al SRI, repetición de la prueba manual ni pruebas
de #32/#31. El único cambio de código es el archivo de pruebas; sin código productivo.

En CASO-20-CLOSE no se repiten pruebas aprobadas: solo documentación de cierre,
revisión del diff y `git diff --check`. El propietario autoriza expresamente al agente
a preparar un único commit local del test y estos dos documentos; sin despliegue ni push.

### Evidencia histórica — aceptación manual e implementación

Aceptación expresa del propietario el 03/11/2026: la anulación documental del cobro
funcionó correctamente en navegador real y se aprueba el cierre funcional manual
(CASO-20-FUNCTIONAL-CLOSE-01). Cliente: Sebastian Zhinin; factura `001-001-000000016`;
cobro de referencia `001458848`.

| Evidencia manual | Antes de anular | Después de anular |
|---|---|---|
| Factura | $21,00 | $21,00 |
| Cobrado | $10,50 | $0,00 |
| Pendiente | $10,50 | $21,00 |
| Estado de factura | Parcial | Pendiente |
| Asiento original | 21 | 21 conservado |

Cobro observado como **Anulado**, motivo `anulacion de pruebas`, fecha visible
03/11/2026 20:41:13. Asiento inverso 22 contabilizado por $10,50; balance de
comprobación: Debe $79,77 / Haber $79,77.

El cierre acredita exclusivamente el comportamiento funcional observado y aceptado
por el propietario; no acredita E2E completa ni nuevas verificaciones técnicas de
API/PostgreSQL. El gate técnico integrado sigue pendiente junto con #32 y #31,
sin duplicar la tarea ni cambiar sus estados. Se conserva debajo la evidencia técnica
de la implementación del 2026-10-28 y sus límites.

Publicado en `6b590644ca0b6ccad97b8eca57d18b2ab3e78b10`;
[CI #1074](https://github.com/szhinin89/erp-saas/actions/runs/41757329707) SUCCESS
(run `41757329707`: 19 jobs exitosos, 0 fallidos y 1 omitido: Azure Web App Deploy),
según evidencia verificada en GitHub por el propietario. No acredita despliegue ni
cierre del gate técnico integrado API/PostgreSQL. Todas las pruebas que dependan
del SRI permanecen suspendidas hasta autorización expresa del propietario.

Anulación documental de cobros erróneos desde CxC, con permiso específico, selección,
confirmación y motivo obligatorio. **KEEP:** `ReverseCollectionCommand`, restauración de
CxC, locks, auditoría y traducción contable existentes. **FALTA autorizada implementada:**
API, permiso asignable, consulta/UI y precondiciones de seguridad operativa. Sin devolución
de dinero. Los históricos con destino caja/banco sin trazabilidad de origen se bloquean.
Reglas y límites: [receivables.md](docs/architecture/receivables.md#reversa-de-un-cobro).

**PASS técnico del agente:** Application 15/15 (handler de reversa, registro posterior correcto
y traductor existente); controller 7/7 (unitarias, sin servidor HTTP); modal 4/4 (jsdom).
Compilación backend mediante los proyectos de prueba, frontend `tsc -b`/build, lint
(0 errores, 34 warnings), `architecture:check` (0 violaciones nuevas) y `git diff --check` PASS.
Sin BUG REAL funcional acreditado por E2E en esta entrega; los rechazos nuevos se verificaron
unitariamente. Comandos focales desde la raíz:

```powershell
dotnet test backend/src/ERP.Application.Tests/ERP.Application.Tests.csproj --filter "FullyQualifiedName~ReverseCollectionCommandHandlerTests|FullyQualifiedName~CollectionReversedPostingTranslatorTests" --no-restore
dotnet test backend/src/ERP.API.Tests/ERP.API.Tests.csproj --filter FullyQualifiedName~FinancePaymentsControllerTests --no-restore
cd frontend
npx.cmd vitest run src/modules/finance/components/ReverseCollectionModal.test.tsx
```

En la entrega del 2026-10-28, por ajuste expreso del propietario, **no se ejecutó E2E
funcional de navegador, HTTP ni PostgreSQL**. La aceptación manual del 03/11/2026
registrada arriba no sustituye el gate técnico. Pendiente para la cadena integrada:
permiso efectivo/denegación, aislamiento
tenant/empresa, duplicados concurrentes, persistencia de motivo/actor/fecha, restauración de
cuotas y nuevo cobro, reversión contable/gate de período y ausencia de cambios operativos
en caja/banco (incluidos turnos cerrados e históricos bloqueados). Estas propiedades no se
acreditan con mocks. Sin modificaciones de bases, migraciones, repetición de R1–R4,
suites funcionales completas, staging, commit ni push en aquella entrega. **Validación
funcional manual CLOSED; gate técnico integrado pendiente**, incluida su aprobación
por el propietario conforme al protocolo. No se declara el caso validado E2E completo.

## R4 — Contabilidad — CLOSED (2026-10-28)

Validación UI/API/PostgreSQL en `erp_zh_test`, empresa B: #26 manual 25 y reverso 26
por 7.25; #27 Diario (62 líneas, 1820.15 por lado), Mayor (109 cuentas), Balance
y Resultados conciliados con SQL y el libro R1–R3; #28 fallo controlado de un gasto
de 2.00, configuración restaurada y reproceso UI a Posted (asiento 27, dos intentos),
readiness sin POSTINGS_UNRESOLVED; #42 doce períodos mensuales Open de 2027,
regeneración idempotente. Aislamiento API PASS. Resultado 96.43, activo 780.63,
pasivo 315.70 y patrimonio incluido resultado 464.93. Se conserva la anomalía
histórica de R3. Cierre 2026 bloqueado correctamente por ejercicio no terminado y
cierre anual pendiente; no se cerró ningún período. Acceso desbloqueado mediante
restablecimiento de solo e2e.admin expresamente autorizado. Sin BUG REAL reproducido
ni cambios de código, commit o push. El propietario aprueba **4/4 PASS** y autoriza
el cierre, limitado exclusivamente a #26, #27, #28 y #42 ejecutados.
Evidencia y límites:
[r4-accounting-e2e-01](docs/evidence/r4-accounting-e2e-01/README.md).

## R3 — Caja y ventas de contado — CLOSED (2026-10-28)

E2E en navegador, API y PostgreSQL sobre la empresa B de `erp_zh_test`, con los datos de R1 y R2,
aprobado expresamente por el propietario: **6/6 PASS**. Escenarios: #17 venta de contado con vuelto, #19 devolución con reembolso en efectivo, #22
movimiento manual con motivo y asiento, #23 depósito bancario, #21 cierre con arqueo y diferencia, y #39
reporte de ventas conciliado con caja y contabilidad. Aislamiento entre empresas PASS. Evidencia:
[r3-cash-sales-e2e-01](docs/evidence/r3-cash-sales-e2e-01/README.md). Tres **BUG REAL** corregidos con
regresión focal RED → GREEN: (1) un turno abierto en otra empresa del tenant impedía abrir caja; ahora
hay un turno por TenantId + CompanyId (índice `ux_cash_sessions_open_per_company_user`, ahora
consolidado en `20261021162651_InitialProductionBaseline` y su Designer, sin incremental adicional;
EF Core y ModelSnapshot conservan el mismo índice). Comprobaciones técnicas focales de consolidación
PASS; estructuras de Compras preservadas. Instalación limpia pendiente del propietario, sin ejecución
sobre bases ni nueva validación funcional de R3; (2) depósito bancario, egreso manual y retiro permitían sobregirar la
caja; ahora aplican la misma regla que el pago a proveedor; (3) el arqueo mostraba «Total contado» $0.00
en la UI. SSOT: [backend.md § Turnos de caja](docs/architecture/backend.md#turnos-de-caja-uno-por-empresa-y-sin-sobregiro)
y [accounting.md §25](docs/architecture/accounting.md#25-caja-contable-accounting-12c). Gates focales:
PostgreSQL 10/10, Application 168/168, API HTTP 36/36, frontend Caja 108/108, lint/tsc/build,
`architecture:check` (0 nuevas) y `git diff --check`. Límites: no se valida la transmisión SRI (B sin
configuración SRI); no se probó con un segundo usuario; anomalía histórica E2E conservada: el depósito de
prueba de 100.00 previo a la corrección (asiento 20) permanece sin reversa bancaria, y el sobrante de 100.00 del
arqueo del turno 1 (asiento 21) solo repone el saldo contable de Caja. No acredita suite completa ni
despliegue. Sin commit ni push.

## R2 — Compras XML, retención, NC de proveedor, ajuste y transferencia — CLOSED (2026-10-28)

E2E en navegador, API y PostgreSQL sobre la empresa B de `erp_zh_test`, con datos sintéticos y los
datos de R1, aprobado expresamente por el propietario: **5/5 PASS** en #14 compra desde XML
(vincular, crear pendiente, flete, confirmar y reabrir), #31 retención funcional con CxP neta 24.95,
#16 NC de proveedor con devolución parcial, #10 ajuste de egreso con anulación y #11 transferencia
entre bodegas; aislamiento entre empresas PASS. Evidencia:
[r2-purchases-inventory-e2e-01](docs/evidence/r2-purchases-inventory-e2e-01/README.md).
Dos correcciones con regresión focal RED → GREEN: (1) **BUG REAL** #16,
`AccountsPayable.ApplyReturnCredit` no recalculaba el estado y la CxP quedaba «Pendiente» tras una
devolución autorizada; (2) **H-1** autorizado, `CreateItemCommandHandler` crea la presentación base
de todo Product nuevo (Ítems, IL-1 y Resolver productos pendientes), conforme a
[items.md §5](docs/architecture/items.md#5-variantes-empaques-y-conversiones); los Products
históricos no se migran. Gates focales PASS: Domain 13/13 y 60/60; Application 387/387 (CxP/NC) y
562/562 (Items, InitialLoad, ItemMatching, PurchaseReception, Packaging); Infrastructure PostgreSQL
8/8 (devoluciones), 62/62 (`ConfirmItemsAtomicPostgreSqlTests` + `ResolvePurchaseReceptionLinesIntegrationTests`)
y 31/31 (ajustes, alcance de Items, códigos de proveedor y búsqueda); `architecture:check` (0
nuevas) y `git diff --check`.
Límites: el XML autorizado se sirvió desde un mock local del WS de autorización; la transmisión
electrónica de la retención no se acredita (se detuvo antes del XML por falta de dirección del
emisor) y nada se envió al SRI. La configuración SRI temporal de la empresa B se retiró al cerrar
(estado previo: sin configuración). El arreglo H-1 no se repitió en E2E. Diferidos sin bloquear R2:
H-2 (retención de otra empresa: 200 con `data: null`, sin fuga) y H-3 (transferencias sin listado ni
detalle); las observaciones UX del README siguen sin cambios. No acredita suite completa ni
despliegue. Sin commit ni push.

## R1 — Arranque: carga inicial, apertura contable y usuario limitado — CLOSED (2026-10-28)

E2E en navegador, API y PostgreSQL sobre la empresa B de `erp_zh_test`, con datos sintéticos,
aprobado expresamente por el propietario: **5/5 PASS** en #37 IL-1, #6 IL-2/IL-3, #7 IL-4, #8
IL-5/IL-6 con apertura contable, cobro y pago parcial, y #3 usuario con perfil limitado. Evidencia:
[r1-initial-load-e2e-01](docs/evidence/r1-initial-load-e2e-01/README.md).
Dos **BUG REAL** corregidos con regresión focal RED → GREEN: (1) IL-1, el aviso de creación de
catálogo contaba avisos por fila («3 categorías y 3 marcas» cuando eran 1 y 1), en
`ImportWizardPage.tsx`; (2) CxC, «Fecha factura» de una CxC de saldo inicial mostraba la fecha de
carga en lugar de la emisión del documento, en `AccountsReceivablePage.tsx`. La corrección de CxC
fue autorizada expresamente y se limita a esa columna: las CxC con factura conservan la fecha y hora
de la factura. Con aprobación del propietario y conforme a ADR-034 (sufijo `At` solo para instantes),
el campo de fecha de negocio de la respuesta de CxC se renombró de `InvoiceIssuedAt` a
`InvoiceIssueDate` (`invoiceIssueDate` en JSON) en `SalesReceivableDto` y sus consumidores directos;
mismo tipo y valor; sin cambios de guardrails. Gates focales PASS: Vitest initialLoad + finance 82/82,
Application `SalesReceivable*` 9/9, Infrastructure `ValidateInitialReceivablesPostgreSqlTests` 28/28,
compilación de ERP.API, ESLint, `tsc -b`, build, `architecture:check` (0 violaciones nuevas) y
`git diff --check`.
Límites: no se ejecutó el cierre irreversible de la carga inicial (queda abierta y conciliada), ni la
liquidación total de saldos iniciales de CxC/CxP. No acredita suite completa ni despliegue. Sin
commit ni push.

## Ventas — E2E de devolución con crédito a CxC cobrada — CLOSED (2026-10-27)

Resultado E2E **14/14 PASS** en navegador y **23/23 PASS** en rechazos y aislamiento (37/37), con
verificación en PostgreSQL, aprobado expresamente por el propietario; CLOSED solo para la
devolución parcial con crédito a una CxC parcialmente cobrada. Sin BUG REAL ni cambios de código.
Evidencia: [sales-return-receivable-e2e-01](docs/evidence/sales-return-receivable-e2e-01/README.md).
Alcance: factura a crédito de un producto con IVA 15% por 115.00 en 2 cuotas; cobro parcial de
40.00; devolución parcial de 2 u (46.00) desde `/sales/returns` con crédito a CxC: CxC 69.00 /
40.00 / 29.00 con cuotas reconstruidas (14.50 + 14.50), Kardex +2 al costo de la venta, IVA 6.00
revertido, asientos de devolución y de costo balanceados, persistencia tras recargar. Rechazo de
crédito mayor al saldo, de exceso sobre el remanente y de anulación de la factura con devolución
autorizada; aislamiento frente a la empresa B. Límites: la emisión electrónica SRI de la Nota de
Crédito no queda acreditada (la empresa E2E no tiene configuración SRI) y las cuotas se reconstruyen
sobre el saldo conforme a la SSOT (lo cobrado queda en la CxC, no en las cuotas). Observación UX no
bloqueante: el listado de devoluciones muestra el GUID del cliente en vez del nombre; sin ticket ni
cambio de contrato. No acredita reembolso en efectivo o mixto, devolución total, mora, suite
completa ni despliegue. Sin commit ni push.

## Cuentas por cobrar — E2E de cobros a caja y sin destino — CLOSED (2026-10-27)

Resultado E2E **28/28 PASS** en navegador y **14/14 PASS** en rechazos, aislamiento y PostgreSQL,
aprobado expresamente por el propietario; CLOSED solo para el alcance descrito a continuación.
Sin BUG REAL ni cambios de código.
Evidencia: [receivables-e2e-03](docs/evidence/receivables-e2e-03/README.md).
Alcance: factura a crédito por 90.00 (1 cuota); cobro parcial de 40.00 en efectivo a «Caja
Principal» y cobro del saldo de 50.00 sin forma de pago ni destino (permitido por la SSOT), con
recarga y reapertura, asientos balanceados (Debe 1.1.01.001 Caja general en ambos: cuenta de la
caja y cuenta de la regla) y rechazo de caja ajena, de banco y caja a la vez y de cobros desde otra
empresa. No acredita mora, reversa de cobros, devolución con CxC cobrada, saldo inicial IL-5, suite
completa ni despliegue. Sin commit ni push.

## Cuentas por cobrar — E2E de factura con varias cuotas — CLOSED (2026-10-27)

Resultado E2E **58/58 PASS** y aislamiento **7/7 PASS**, aprobado expresamente por el propietario;
CLOSED solo para el alcance descrito a continuación.
Sin BUG REAL ni cambios de código de producción; se agregó un test focal de mora con varias cuotas.
Evidencia: [receivables-e2e-02](docs/evidence/receivables-e2e-02/README.md).
Alcance: factura a crédito por 300.00 en 3 cuotas con vencimientos distintos; cobro de 30.00 a la
cuota #2 (tras rechazar un monto mayor al saldo de la cuota), 120.00 sin cuota aplicado por FIFO y
liquidación de 150.00 sin cuota, con recarga y reapertura tras cada paso, «Vence» siguiendo a la
primera cuota no saldada, asientos de venta y cobro balanceados y aislamiento entre empresas. La
mora no se reprodujo en E2E (las fechas de los datos no lo permiten) y solo se acredita por test
focal. No acredita cobro a caja o sin destino, reversa de cobros, devolución con CxC cobrada, saldo
inicial IL-5, suite completa ni despliegue. Sin commit ni push.

## Compras/Gastos → CxP — E2E de reversa de pagos y anulación — CLOSED (2026-10-27)

Resultado E2E **46/46 PASS** (17 comprobaciones en navegador, 20 por API y 9 en PostgreSQL),
aprobado expresamente por el propietario. No se encontró BUG REAL ni hubo cambios de código. Evidencia:
[payables-reversal-cancel-e2e-01](docs/evidence/payables-reversal-cancel-e2e-01/README.md).
Se probaron una compra (115.00) y un gasto (80.00) a crédito, cada uno con un pago parcial por
transferencia. Con pagos activos, la anulación se bloqueó y no hubo cambios en la base. Luego se
reversaron los pagos desde «Reversar pago» del detalle del pago: las CxP recuperaron su saldo y se
generaron asientos inversos. Después se anularon compra y gasto: CxP y cuotas anuladas, kardex
revertido y asientos originales reversados. También se comprobaron las restricciones posteriores
(doble reversa, doble anulación y pago sobre una CxP anulada) y el aislamiento entre empresas. No
acredita pagos en efectivo o cheque, varias cuotas, retenciones, saldo a favor aplicado, suite
completa ni despliegue. Quedan dos DEBT no bloqueantes, documentados en la evidencia: «Reversado
por» muestra el UUID del usuario, y el bloqueo del gasto devuelve un código y un mensaje distintos
de los de la compra. No abren trabajos ni reabren módulos CLOSED. Sin commit ni push.

## Compras → CxP → Pago — E2E de compra a crédito — CLOSED (2026-10-27)

Resultado E2E **40/40 PASS** (flujo, asientos y aislamiento), aprobado expresamente por el
propietario junto con las dos correcciones, tras corregir dos **BUG REAL** de
UI en Compras: la cuota regenerada mostraba y re-confirmaba un monto obsoleto (0.00), lo que dejaba
«Las cuotas no cuadran» y bloqueaba «Confirmar compra»; y la compra confirmada sin retención mostraba
la clave i18n cruda `purchases.retention.noneIssued`. Corrección focal en `PaymentScheduleSection`
(`PurchasesPage.tsx`) y en `es.json`/`en.json`, con tests focales. Evidencia:
[purchases-payables-e2e-01](docs/evidence/purchases-payables-e2e-01/README.md).
Alcance: compra a crédito de una cuota por 115.00, pago parcial 50.00 y del saldo 65.00 por
transferencia, reapertura de compra, CxP y pagos, asientos de compra y pago balanceados y
aislamiento entre empresas. No acredita compras de varias cuotas, retención, reversa de pagos,
bloqueo de anulación, suite completa ni despliegue. Los DEBT observados en la evidencia no abren
trabajos ni reabren módulos CLOSED. Sin commit ni push.

## Cuentas por cobrar — E2E de cobros — CLOSED (2026-10-27)

Resultado E2E **21/21 PASS** y aislamiento **7/7 PASS**, aprobado expresamente por el
propietario, tras corregir un **BUG REAL**: el cobro no se acumulaba en la cuota de la CxC (cuota
`pending` con saldo cobrado). Corrección focal en `SalesReceivable`, `SalesReceivableInstallment`
y `RegisterCollectionCommandHandler`, con tests focales. Comportamiento: [SSOT de
CxC](docs/architecture/receivables.md); cobertura: [matriz QA](docs/QA/RECEIVABLES-COLLECTIONS-CLOSEOUT.md);
evidencia: [receivables-e2e-01](docs/evidence/receivables-e2e-01/README.md).
Alcance acreditado: factura a crédito de una cuota por 100.00, cobro parcial 40.00 y del saldo
60.00 con destino banco, reapertura de factura y CxC, asientos de venta y cobro balanceados y
aislamiento entre empresas. No acredita facturas de varias cuotas, cobro a caja o sin destino,
reversa de cobros (sin endpoint ni UI), devolución con CxC cobrada, saldo inicial IL-5, listado de
cobros (no existe), suite completa ni despliegue. No se reparan cuotas cobradas antes de la
corrección. Sin commit ni push.

## Pagos a proveedores — cierre E2E de gastos — CLOSED (2026-10-27)

Resultado E2E **17/17 PASS**, aprobado expresamente por el propietario. Clasificación:
**KEEP**, sin BUG REAL. Evidencia existente incorporada en
[supplier-payments-e2e-01](docs/evidence/supplier-payments-e2e-01/README.md)
([resultados](docs/evidence/supplier-payments-e2e-01/e2e-results.json) y seis capturas).
Alcance: gasto confirmado por 100.00, pago parcial de 40.00, reapertura del pago y de la CxP
con saldo 60.00, pago total del saldo y cartera sin pendientes. En la
[matriz QA](docs/QA/PURCHASES-EXPENSES-PAYABLES-CLOSEOUT.md) se acredita únicamente
el pago parcial/total de gastos.
No acredita pagos de compras, reversas, bloqueo de anulación, aislamiento entre empresas,
suite completa ni despliegue. Se conservan las observaciones y límites de la evidencia,
sin abrir pendientes ni reabrir módulos CLOSED. Cierre documental: sin cambios de código,
sin repetir pruebas y sin tocar el archivo RED histórico. Staging autorizado expresamente
para esta entrega; sin commit ni push.

## GOV-002 — CLOSED

Cierre documental del seguimiento de gobernanza aprobado expresamente por el propietario
(2026-10-27), sobre el contrato central implementado y las validaciones previamente acreditadas.
No certifica cumplimiento exhaustivo de todos los módulos ni acredita nuevas pruebas o despliegues.
Las dos deudas conocidas (códigos de módulo sin catalogar y formateador frontend alternativo)
se conservan como deuda no bloqueante en [error-handling.md](docs/architecture/error-handling.md#deudas-conocidas-y-límite-del-cierre).
No autoriza reauditar ni refactorizar módulos CLOSED. Esta fase solo modifica documentación;
staging autorizado por el propietario, commit y push no realizados.

## SEGURIDAD — SEC-06: revocación de permisos con caché — CLOSED — revisión técnica final PASS (2026-10-27)

La caché de permisos efectivos (TTL 300 s) podía seguir autorizando permisos ya revocados: la
entrada se publicaba con la versión vigente al escribir (una carga anterior a la revocación
quedaba válida), el cambio de perfil solo borraba la entrada (una carga concurrente del perfil
anterior la recreaba), el bump de versión era GET+1+SET (incrementos perdidos), los fallos de
invalidación se ignoraban y `UpsertCompanyUserMembership` podía guardar rol/perfil y salir sin
invalidar. Corregido en el mecanismo existente, sin cachés ni autorizadores nuevos: sello de
versiones (tenant, empresa, usuario) observado antes de cargar la BD, perfil dentro de la entrada,
versiones y marcas atómicas (Lua sobre el `IConnectionMultiplexer` del host; contadores del
proceso sin Redis), protocolo de invalidación en dos fases fail-closed en todos los handlers que
cambian permisos (503 `PERMISSIONS_INVALIDATION_UNAVAILABLE` sin persistir / 503
`PERMISSIONS_INVALIDATION_INCOMPLETE` con el alcance protegido), membership validada antes de
persistir y guardada en transacción, y regla de despliegue verificable `Redis:Required=true` para
más de una instancia de API. TTL sin cambios; sin cambios de base de datos ni migraciones.
Ajustes finales (opción A aprobada): una restauración o failover de Redis podía revivir versiones y
entradas anteriores a una revocación; ahora el Redis de autorización debe ser `noeviction`, sin
RDB/AOF y primario sin réplicas, y API/worker no arrancan si no cumple o no es verificable
(`CONFIG GET` + `INFO`); el Redis local de compose quedó declarado así (el contenedor existente
conserva la configuración anterior hasta recrearlo). Frontend: los dos 503 se presentan en las
pantallas IAM (perfiles, permisos, usuarios, configuración de usuario) sin éxito falso ni reintento
automático; el estado incierto recarga desde el servidor y deja la advertencia. Focales de los
ajustes PASS: Infrastructure 39/39 (worktree aislado; incluye Redis conforme/incompatible/sin
CONFIG/inaccesible, presión de memoria con `noeviction` y reinicio), API 21/21 (registro del
validador, contrato DevCache con Redis conforme e inaccesible), Vitest access 75/75, lint (0
errores), `tsc -b`, build, Arquitectura 143/143 y `architecture:check` PASS. Focales previos PASS: Application 214/214 (Access) + 161/161 (sesión, alta de usuario, scope),
Infrastructure (worktree aislado, sin el archivo RED) `PermissionsCacheRevocationTests` +
`PermissionsCacheRevocationRedisTests` 21/21 con Redis efímero, API 41/41 (regla de despliegue,
host, `CompanyAccessProfilesIsolationHttpTests`, `CrossCompanyAdminPrivilegeHttpTests`, contrato de
errores, sesión), Arquitectura 143/143, `architecture:check` PASS, tamaño de handlers PASS.
Gate independiente del propietario (PowerShell): Infrastructure `Security.PermissionsCache` 39/39
(worktree temporal sin el archivo RED), API Redis/hosts 14/14, Frontend Access 75/75, Architecture
frontend 140/140, `git diff --check` PASS. Revisión técnica final PASS. **SEC-06 CLOSED: commit `bbaa708cd`, CI #1011 SUCCESS.**
Sin despliegue: el Redis de producción (Coolify) no fue verificado contra el contrato. Riesgos en
[Riesgos y limitaciones vigentes](#riesgos-y-limitaciones-vigentes).
[SSOT Seguridad](docs/architecture/security.md#aislamiento-entre-empresas-del-mismo-tenant).

## SEGURIDAD — SEC-05: perfiles de acceso por empresa — CORREGIDO — revisión técnica final PASS (2026-10-26)

Los perfiles de acceso eran TENANT: un Admin de A podía consultar, editar, desactivar o cambiar
permisos de un perfil usado (o usado solo) por B, con efecto inmediato en B; un usuario Admin en A
y User en B podía autoescalarse en B editando el perfil de su membership de B; y un no Admin con
`access.company_user_memberships.view` podía asignarse rol Admin o un perfil más amplio que el suyo.
Decisión del propietario: perfiles independientes por `TenantId + CompanyId`. Corregido en el
mecanismo existente: `AccessProfile` es `ICompanyOperationalEntity` (unicidad por empresa, FK
compuesta membership → perfil de la misma empresa, esquema inicial y snapshot adaptados, sin
migración nueva ni cambios en bases existentes); los casos de uso de perfiles exigen empresa activa
(`IRequiresCompanyContext`) y resuelven el perfil por tenant + empresa; un perfil inactivo no
concede permisos; `UpsertCompanyUserMembership` y la revocación aplican anti-escalamiento con el
mismo bypass de SEC-04; el perfil por defecto "DataEntry" se siembra por empresa. Sin cambios de
contrato en frontend. Focales PASS: Application 213/213 (Access, sesión, `CompanyScopeBehavior`),
Arquitectura 143/143, API HTTP/PostgreSQL `CompanyAccessProfilesIsolationHttpTests` 12/12 +
`CrossCompanyAdminPrivilegeHttpTests` 8/8 y clases con fixtures de perfiles 103/103 + unitarias/
snapshot API 102/102, `architecture:check` PASS, `git diff --check` PASS. Infrastructure.Tests no
compilado en este árbol (archivo RED histórico). Gate independiente del propietario (PowerShell):
Application 22/22, API HTTP 20/20, compilación de ERP.API y coherencia EF/baseline PASS,
`git diff --check` PASS. Revisión técnica final PASS: aislamiento empresarial de perfiles aceptado
técnicamente. **CI del commit SEC-05: aún no ejecutado.** La revocación de permisos frente a la caché se
corrige en SEC-06. Riesgos en
[Riesgos y limitaciones vigentes](#riesgos-y-limitaciones-vigentes).
[SSOT Seguridad](docs/architecture/security.md#aislamiento-entre-empresas-del-mismo-tenant).

## SEGURIDAD — SEC-04: privilegios administrativos entre empresas — CORREGIDO — revisión técnica final PASS (2026-10-26)

`RuntimePermissionAuthorizer` concedía el bypass Admin (`*`) por el rol del JWT, emitido para la
empresa de apertura de sesión: un usuario Admin en A y User limitado en B podía, con el JWT de A y
`X-Company-Id: B`, ejecutar en B cualquier operación `perm:*` (y `GetMyPermissions` devolvía `*`).
Además, `CompanyContextProvider` sustituía en silencio un `X-Company-Id` sin membership por la única
membership del usuario. Corregido en el mecanismo existente: el bypass sale de la membership de la
empresa operativa u operador global (`IOperatorCompanyAccessPolicy`), alineando `GetMyPermissions`,
sesión, navegación y anti-escalamiento de `UpsertProfilePermissions`; un header sin membership activa
del tenant se rechaza con 403 `COMPANY_SCOPE_FORBIDDEN`, sin fallback. Sin cambios en tokens, first-run
ni base de datos. Focales PASS: Application 97/97 (incluye `CompanyScopedAdminAuthorizationTests`),
API HTTP/PostgreSQL 290/290 en las clases con host real (incluye `CrossCompanyAdminPrivilegeHttpTests`
8/8; fixtures de Caja, Ventas y BusinessPartner alineados: su JWT usaba un `sub` sin membership).
Regla funcional aprobada por el propietario: únicamente el Administrador Global de instalación
(modo operador) atraviesa tenants y empresas; los Administradores de Empresa quedan limitados a las
empresas donde su membership tiene rol Admin. Gate independiente del propietario (PowerShell):
Application 37/37, API HTTP 15/15, `architecture:check` 140/140, `git diff --check` PASS; revisión
técnica final PASS sin defectos bloqueantes. **CI del commit SEC-04: aún no ejecutado.** Riesgos en
[Riesgos y limitaciones vigentes](#riesgos-y-limitaciones-vigentes).
[SSOT Seguridad](docs/architecture/security.md#aislamiento-entre-empresas-del-mismo-tenant).

## SEGURIDAD — SEC-03: aislamiento entre empresas en configuración y retenciones — CORREGIDO (2026-10-26)

13 operaciones COMPANY no declaraban marcador de scope y `CompanyScopeBehavior` no validaba la
membership de la empresa de `X-Company-Id`: un usuario de A podía leer/modificar el SMTP, la política
fiscal de Consumidor Final, las preferencias operativas y los defaults de facturación de B, consultar
el estado de facturación electrónica de B y generar/registrar comprobantes de retención con el
contexto de B. Corregido protegiendo los 13 requests con el marcador existente
`IRequiresCompanyContext` (Communications ×3, Retenciones ×3, OrgConfig ×2, política fiscal ×2,
preferencias operativas ×2, estado FE ×1); `INV_E2` alineado a los namespaces reales (`Purchases`,
`Caja`) y ampliado a esas operaciones. Evidencia: `CompanySettingsIsolationHttpTests` RED 7/7 (fallos
esperados sin la corrección); focales PASS: API HTTP/PostgreSQL 15/15, Application 49/49,
`RetentionsControllerTests` 16/16, Arquitectura 17/17 y `architecture:check`; gate independiente del
propietario en PowerShell 32/32 PASS. Revisión técnica final PASS y alcance de la corrección aceptado
por el propietario (2026-10-26). **Estado: SEC-03 corregido y técnicamente validado.** Sin evidencia
de CI para este cambio hasta publicar su commit. Riesgos conocidos en
[Riesgos y limitaciones vigentes](#riesgos-y-limitaciones-vigentes).
[SSOT Seguridad](docs/architecture/security.md#aislamiento-entre-empresas-del-mismo-tenant).

## INVENTARIO V1 — D-1: aislamiento por empresa de motivos de ajuste — CORREGIDO (2026-10-26)

`InventoryAdjustmentReason` admitía `CompanyId` nulo y se filtraba solo por tenant: una empresa podía
listar, editar, desactivar y usar motivos de otra del mismo tenant. Corregido como dato de empresa
reutilizando el filtro global y `CompanyScopeBehavior` existentes; baseline única actualizada
(`company_id` NOT NULL, índice único Tenant + Company + código), sin migración incremental. Focales PASS:
Application 106/106, API HTTP/PostgreSQL 21/21 (incluye `InventoryAdjustmentReasonCompanyIsolationHttpTests`),
OpenAPI 3/3, Arquitectura backend 31/31, Infrastructure 59/59, frontend 10/10 y `architecture:check`.
Revisión técnica final PASS y aceptación del propietario (2026-10-26).
**Estado: D-1 corregido y técnicamente validado.** Las bases de desarrollo ya migradas conservan el
esquema anterior hasta reinstalarse.
[SSOT Inventario](docs/architecture/inventory.md#6-ajustes-de-inventario).

## INVENTARIO V1 — B4: Cuadre monetario Compras/Kardex/Contabilidad — CLOSED (2026-10-26)

12000 u × 0.1234 con 1.25 % de descuento e IVA 0: Compras y el asiento registraban 1462.29 y el Kardex
1462.30 (cantidad × `LandedUnitCost` redondeado); 3 × 100 + flete 10 prorrateado dejaba el Kardex en
309.99 frente a 310.00. Alcance: compras exclusivamente Product inventariable. El importe de
`TotalLineCost` se conserva entre Compras, Kardex y Contabilidad; la anulación revierte el importe
original persistido y un histórico ambiguo se rechaza en la transacción, sin efectos parciales. Sin
migración ni cambios en `PurchaseCostDistribution`. Compras mixtas Product + Service, ICE e IRBPNR
no forman parte de este cierre. Focales B4 en `PurchaseRetentionConfirmIntegrationTests.InventoryRounding.cs`:
6/6 PASS en CI #1001 (`infrastructure-01` 208/208 PASS, `application` SUCCESS). Revisión técnica final
PASS (2026-10-26), sin defectos reproducidos.
**Estado: B4 CLOSED; publicado en `8a2c3692d8fb5c870e2284e06832b60dc014cac5`.**
CI: [#999, #1000 y #1001](#inventario-v1--evidencia-ci-de-referencia).
[SSOT Inventario](docs/architecture/inventory.md#4-valoración-promedio-ponderado) · [SSOT Compras](docs/architecture/purchases.md#5-anulación-confirmed--cancelled).

## INVENTARIO V1 — B3: importe monetario canónico del Kardex — CLOSED (2026-10-26)

3 u por 10.00 y tres ventas dejaban cantidad 0 con valor 0.01 (y la compra siguiente lo heredaba);
una transferencia del saldo dejaba 0.01 en origen; A2 y el reproceso de NC usaban importes sin escala.
Corregidos los residuos y unificado el importe operativo; reglas exclusivamente en la SSOT enlazada.
Sin migración ni revalorización histórica. Implementación alineada con la SSOT. Focales PostgreSQL:
`InventoryValuationPostgreSqlTests` 6/6 y reproceso 2/2 PASS. Gates del propietario PASS (Domain 33/33,
Application 1080/1080, Infrastructure 150/150, API 98/98, Arquitectura backend 143/143, Arquitectura
frontend PASS). Revisión técnica final PASS (2026-10-26), sin defectos reproducidos.
**Estado: B3 CLOSED; publicado en `8dd2f91e2fc1de4cc36e5cc227e3f8c4f29f6ac5`.**
CI acumulativo #999 y #1000 SUCCESS ([referencia](#inventario-v1--evidencia-ci-de-referencia)); no sustituye los gates focales.
[SSOT Inventario](docs/architecture/inventory.md#4-valoración-promedio-ponderado).

## INVENTARIO V1 — Etapa 1: anulación con servicios y concurrencia de documentos — BUG REAL (2026-10-25)

B1: anular una compra Product + Service con bodega global fallaba ("El ítem no es un producto…");
corregida la selección de líneas inventariables. B2: ejecutar/anular ajuste y confirmar transferencia
concurrentes duplicaban Kardex y stock (5→10, 10→13, 6→2); corregida la concurrencia documental.
Sin migración. Gate del propietario aprobado: Application 137/137, Infrastructure 98/98,
API 71/71 y Arquitectura backend 143/143 PASS; revisión final del código PASS.
**Estado: B1/B2 CLOSED; publicados en `b16a412a0`, con gate y revisión final PASS registrados.**
CI acumulativo: [CI #999](#inventario-v1--evidencia-ci-de-referencia).
[SSOT Inventario](docs/architecture/inventory.md#6-ajustes-de-inventario) · [SSOT Compras](docs/architecture/purchases.md#5-anulación-confirmed--cancelled).

## INVENTARIO V1 — Kardex y Reporte de Stock UX/UI (2026-10-25)

Publicados en `50a1af3d9`, sobre la base histórica `8a2c3692d`. Kardex y selector: 32/32 PASS
focales registrados; Reporte de Stock: 17/17 PASS. La excepción obsoleta de tamaño de Kardex
se retiró en `6c5c8c111`. Sin cambios funcionales de inventario.
**Estado: PUBLICADO; Kardex y Reporte de Stock aceptados por el propietario (comunicado 2026-10-26).**
La aceptación se apoya en la evidencia ya registrada; no se ejecutó un nuevo smoke autenticado ni
se obtuvieron evidencias nuevas.
Limitaciones de fixtures, fuentes e hidratación en las evidencias de
[Kardex](docs/evidence/kardex-ux-01/README.md), [selector](docs/evidence/kardex-ux-01/selector/README.md)
y [Stock](docs/evidence/stock-report-ux-01/README.md).
CI: [CI #999](#inventario-v1--evidencia-ci-de-referencia). El workflow frontend no ejecuta las
pruebas Vitest focales; CI no sustituye esos resultados ni el smoke autenticado.

Buscador de documento (2026-10-29): **IMPLEMENTADO — PENDIENTE VALIDACIÓN
VISUAL DEL PROPIETARIO**. FALTA autorizada: alternativa A.
Ajuste visual final: dos lineas agrupadas a la izquierda; numero destacado y badge
juntos arriba, tercero y fecha con separador discreto debajo. Padding compacto,
nombres largos con ajuste de linea; hover/foco y ancho del buscador conservados.
Solo presentacion local sobre ZHPickerResultItem/Badge; logica del hook intacta.
Cada sugerencia muestra numero completo, proveedor/cliente, fecha (formatDate) y
estado en espanol: Compras Confirmed/Cancelled (Confirmada/Anulada), Ventas
Authorized/Cancelled (Autorizada/Anulada). Draft y Discarded quedan excluidos.
KEEP seleccion por ID, busqueda parcial/completa manual (boton/Enter), scope
existente y desplegable ZHPickerResultItem/Badge con estilos compartidos.
Dos consultas mediante las fachadas existentes, filtradas por estado ANTES de
paginar: pagina 1, 10 por estado, maximo 20 sugerencias. Sin N+1, detalles de
facturas ni consultas a Kardex para sugerencias. Respuestas antiguas invalidadas
ante otra busqueda, cambio de texto/tipo/modo o desmontaje.
Limites: lista no exhaustiva; mas de 10 coincidencias por estado requieren precisar
el texto. El estado no acredita movimientos: documentos solo de servicios pueden
aparecer y devolver Kardex vacio. Sin ampliar backend ni alterar inventario.
PASS agente: 14/14 pruebas focales hook/pagina, TypeScript, ESLint focal y diff --check.
Gate del propietario desde frontend:
`npx.cmd vitest run src/modules/inventory/kardex/pages/KardexPage.test.tsx src/modules/inventory/kardex/hooks/useInventoryInvestigationPage.documents.test.ts`;
`npx.cmd tsc -b --pretty false`;
`npx.cmd eslint src/modules/inventory/kardex/pages/KardexPage.tsx src/modules/inventory/kardex/pages/KardexPage.test.tsx src/modules/inventory/kardex/hooks/useInventoryInvestigationPage.ts src/modules/inventory/kardex/hooks/useInventoryInvestigationPage.documents.test.ts`.
Pendiente gate y validacion visual/funcional con datos reales. Mejoras pendientes de
Compras intactas. Sin suites generales, E2E, backend, BD, migraciones, baseline,
caso #32, staging, commit ni push; no CLOSED.

## INVENTARIO V1 — Evidencia CI de referencia

Referencia publicada: `6c5c8c1113f802a0f3ab2a9d795c261d0c8c05ab`.
CI #999, segundo intento: **SUCCESS, 19 PASS, 0 FAIL, deploy SKIPPED**, verificado en GitHub
por el propietario y aportado para esta corrección documental (2026-10-26). Es evidencia
acumulativa de la referencia, no una ejecución individual atribuida a cada commit anterior.
No acredita gates del propietario pendientes ni un despliegue.

Referencia publicada: `ba06799ac99b883bcca289d914c69f4cb261f672`.
[CI #1000](https://github.com/szhinin89/erp-saas/actions/runs/40322826093), intento 1:
**SUCCESS, 19 PASS, 0 FAIL, deploy Azure SKIPPED**. Resultado acumulativo del pipeline;
no sustituye los gates focales ni acredita un despliegue.

Referencia publicada: `fa7b2d842b457c7d2fbe2faef8d065cd01ecf4da`.
[CI #1001](https://github.com/szhinin89/erp-saas/actions/runs/40327986902): **SUCCESS**;
`backend / Backend (infrastructure-01)` 208/208 PASS, con las seis pruebas B4 de
`PurchaseRetentionConfirmIntegrationTests.InventoryRounding.cs` identificadas individualmente como PASS
en los logs; `backend / Backend (application)` SUCCESS. Acredita solo las pruebas que ejecutó el
pipeline; no acredita un despliegue.

Referencia publicada: `fce60ea1d52614fee5ce0f84c231df2643b18485`.
[CI #1002](https://github.com/szhinin89/erp-saas/actions/runs/40357020744), intento 1: **SUCCESS**.
Resultado acumulativo del pipeline; no acredita un smoke autenticado, una aceptación del
propietario ni un despliegue.

## Estado de entrega

Preproducción. Actualización documental: 2026-10-26. Este archivo conserva resultados
ya registrados; no acredita nuevas ejecuciones ni despliegues. Las aceptaciones del
propietario se registran solo donde se indican expresamente.
Las filas de Compras son históricas: estado vigente
[Compras V1 CLOSED](#doc-cleanup-compras-01--compras-v1-aprobado-y-closed-2026-11-03).
Reglas funcionales únicamente en las [SSOT](docs/architecture/README.md).

| Entrega | Evidencia registrada | Estado / límite pendiente |
|---|---|---|
| Compras: distribución canónica de costos A/B | Publicado en `29a69fcaa`; Backend Domain 34, Application 99, API 8, PostgreSQL 20 + 5 y arquitectura PASS; frontend implementado; [CI #1002](#inventario-v1--evidencia-ci-de-referencia) SUCCESS acumulativo | **CLOSED (2026-10-26)**; aceptación formal del propietario 2026-10-26. `PurchaseCostDistribution` es la única autoridad de distribución ([SSOT Compras](docs/architecture/purchases.md)) |
| Compras: precisión de prorrateo/base cero | Publicado en `617253d38`; reproducción 6/6 FAIL; focal 9/9 y PostgreSQL 8/8 PASS en [evidencia](docs/evidence/purchases-cost-precision-01/README.md); revisión técnica final PASS; [CI #1002](#inventario-v1--evidencia-ci-de-referencia) SUCCESS acumulativo | **CLOSED (2026-10-26)**; aceptación formal del propietario 2026-10-26. El frontend actual no calcula el reparto: usa el resultado del backend. El simulador frontend y su 178/178 pertenecen a una implementación sustituida en `29a69fcaa`; no son pruebas vigentes del cálculo |
| Compras XML: confirmación atómica y Service sin bodega | Publicado en `10afb8425`; PostgreSQL 7/7, Application 66/66, Domain 30/30; guardrails backend PASS; revisión técnica final PASS; [CI #1002](#inventario-v1--evidencia-ci-de-referencia) SUCCESS acumulativo | **CLOSED (2026-10-26)**; aceptación formal del propietario 2026-10-26. No resuelve la integración operativa futura de ICE mixto ni IRBPNR |
| Frontend: timeouts de Compras/Contabilidad | Publicado en `746a1f1c6`; focales, TypeScript, ESLint, build y arquitectura PASS; suite frontend 3252/3252 PASS según [evidencia](docs/evidence/frontend-timeouts-01/README.md); el CI frontend no ejecuta Vitest | **CLOSED (2026-10-26)**; aceptación formal del propietario 2026-10-26. Limitación independiente vigente: en la suite, `ZHOverlayGeometry.test.tsx` falló en `afterAll` y pasó 20/20 aislado; no corregida |
| Frontend: cuatro pruebas desactualizadas de Caja/Contabilidad | 3248/3248 PASS, TypeScript, lint, build y arquitectura; [evidencia](docs/evidence/frontend-preexisting-fails-01/README.md) | Corrección verificada en la entrega previa; sin nueva ejecución |
| Ítems: E2E visual Product/Service | Chromium 48/48 PASS sobre `erp_zh_test`; 2 BUG REAL corregidos (precio base perdido al reabrir; 13 claves `items.validation.*` sin traducir); `PricingTab` 17/17, lint, `tsc -b`, build y arquitectura PASS; [evidencia](docs/evidence/items-e2e-visual-01/README.md) | Implementado y verificado 2026-10-27; sin commit; suite completa no ejecutada |
| Ítems: formularios y catálogos asíncronos | Reproducción 4/4 FAIL; editor 32/32, frontend 313/313, Application 446/446 PASS; [evidencia](docs/evidence/items-forms-alignment-01/README.md) | Guardar/reabrir en ERP real verificado en [E2E visual](docs/evidence/items-e2e-visual-01/README.md) |
| Compras: conflictos de códigos | Frontend 767/767 y gates técnicos PASS; [evidencia](docs/evidence/purchases-code-conflicts-sync-01/README.md) | Validación previa, sin acreditar otro gate |
| Compras: avisos compactos de duplicados | Frontend 763/763, focal 37/37 PASS; [evidencia](docs/evidence/purchases-compact-code-conflicts-01/README.md) | Validación previa; capturas con límites documentados |
| Ítems: diagnóstico unificado de códigos | Application 423/423, PostgreSQL 22/22, frontend 755 y arquitectura 143/143 PASS; [evidencia](docs/evidence/items-code-conflicts-01/README.md) | Implementado; sin nueva ejecución |
| Compras: Internos provisionales | Application 63/63, frontend 86/86, PostgreSQL 11/11 PASS; [evidencia](docs/evidence/purchases-internal-barcodes-01/README.md) | Implementado; sin decisiones pendientes en esa entrega |
| Compras: desglose XML | Commits 478f5cc0b / 77f80985f; Domain 59, Application 121, frontend 77 y objetos SQL 2/2 PASS | Implementado; sin acreditar despliegue |
| Compras: vinculación estricta por proveedor | dc643c455; PostgreSQL 6/6, Application 100/100, selección manual 6/6 PASS | Implementado para la conciliación de recepción/código de proveedor; no exige que el producto, costo o cantidad operativos coincidan con el XML. Rige la regla única de Compras V1 indicada al inicio |
| Compras: productos pendientes compactos | 77f80985f; frontend 589/589 y PR-7 PASS; [evidencia](docs/evidence/purchases-pending-products-compact-01/README.md) | Implementado; excepción del modal retirada |
| Compras: modal Distribuir | Focal 16/16 y gates PASS; [evidencia](docs/evidence/purchases-distribute-cost-01/README.md) | Implementado; no amplía cálculo |
| Compras: validaciones comerciales | Focal 368 y gates PASS; [evidencia](docs/evidence/purchases-commercial-risk-01/README.md) | Observación histórica sobre precisión del costo mostrado, no resuelta en aquella consolidación; no constituye trabajo abierto tras el cierre global de Compras V1 |
| Design System: incidencias de línea | [Evidencia](docs/evidence/zh-ds-line-issues-01/README.md) | Capturas con límites de fuentes/iconos; sin nueva aceptación visual |
| Unificación de NC de compra | PostgreSQL 21/21 con SRI simulado + contabilización 6/6, API 24/24, Application 79/79, frontend 35/35; build/lint/TypeScript/arquitectura PASS | **CLOSED (2026-10-27)**; aceptación funcional expresa del propietario para el alcance implementado de [Compras §§6–7](docs/architecture/purchases.md#6-nota-de-crédito-del-proveedor--entrada-única), publicado en `51678b165`. Recuento frontend conciliado en [evidencia SSOT](docs/architecture/purchases.md#9-evidencia). El límite histórico externo de Gastos (516 > 514) ya no se observa: conteo puntual actual 514, techo 514; no acredita un nuevo gate completo. Sin nuevas pruebas, validación SRI real ni despliegue. |
| Anulación completa de compra | Validación focal PostgreSQL PASS | Implementada; calendario oficial y ventana interna Company siguen fuera del comportamiento operativo |

## Instalación inicial y GLOBAL

GREENFIELD-INSTALL: instalación única validada manualmente por el propietario en
`erp_zh_dev` (25/224/1048 INEC 2026 y SRI cargados). Oráculo PostgreSQL desechable:
212 tablas equivalentes salvo metadatos de procedencia/orden de columnas.
Infrastructure focal 256/256 y Architecture 2/2 PASS; suite previa 7371/7372,
snapshot OpenAPI corregido y focal 3/3 PASS. Regresión de arranque INEC 6/6 PASS.
No implica despliegue ni vuelve a autorizar recrear bases.
Instalación y pendientes normativos: [GLOBAL](docs/architecture/architecture.md#global-immutable-schema-global)
y [First Run](docs/architecture/first-run.md).
GLOBAL OFFICIAL-ALIGNMENT: Infrastructure 190/190, API 9/9, frontend 2/2 PASS;
ICE mixto representado en catálogo (65/65 PASS), sin integración de cálculo operativo.
Corrección Tabla 5: PostgreSQL 5/5, HTTP 1/1, frontend 46/46 PASS.

## Background Processing y despliegue

Implementación validada de catálogo/runner/locks/colas/host separado. Resultados de
los grupos originales solapados: 30/30, 19/19, 1/1, 20/20, 39/39 y 12/12 PASS;
no sumarlos como pruebas únicas. Aceptación posterior: API 20/20 e Infrastructure 4/4
con PostgreSQL 16 y SMTP simulado. La consulta de solo lectura del entorno anterior
observó siete recurring jobs, checkpoint durable y cero workers activos; no prueba
entrega SMTP/SRI ni un host local funcionando.
Fase D: Compose 4/4, focal 21/21 y PostgreSQL 4/4, imagen Linux y smoke Compose aislado PASS.
El arranque local sobre datos existentes no quedó acreditado; la aceptación aislada no lo sustituye.
Transición Coolify real pendiente de inspección, respaldo y autorización de operación.
No se instruye aplicar migraciones sustituidas. [Runbook](docs/deployment/README.md).
Collector del worker y ajuste de carga real pendientes de operación; no existe benchmark de producción.

## Contabilidad y Retenciones

Accounting: capacidades implementadas de continuidad 01–06, períodos 07, manuales 08,
reglas/reportes 11, ajustes 12A, caja 12C y cierre anual 12D documentadas en
[Accounting](docs/architecture/accounting.md). Son capacidades implementadas, no nuevos cierres.
Corrección de finalización SRI por job (2026-10-20): PostgreSQL focal 7 PASS registrados;
contabilización pendiente/fallida no equivale a anulación operativa fallida.
Retenciones: emisión, XML/RIDE, transmisión y anulación asistida implementados en el
alcance de [Backend](docs/architecture/backend.md); no quedan como tareas futuras genéricas.

## A3–A8 — CLOSED (2026-10-27)

Cierre documental y funcional aprobado expresamente por el propietario, con gate
independiente A5 **10/10 PASS en PostgreSQL real** comunicado en esta fase. Se conservan
los PASS ya acreditados de los demás casos, sin repetir pruebas.

| Caso | Evidencia focal acreditada | Estado |
|---|---|---|
| A3 — actualización atómica del borrador de venta | Application 24/24 + PostgreSQL 1/1 PASS | CLOSED |
| A4 — ICE/IRBPNR en devoluciones de venta | Application 7/7 PASS | CLOSED |
| A5 — búsqueda y selección por barcode de presentación | RED 2/2 FAIL esperados → GREEN PostgreSQL 10/10 PASS; gate independiente del propietario 10/10 PASS | CLOSED |
| A6 — guardas de bodega y carreras UI | Application 10/10 + PostgreSQL 6/6 PASS; carreras frontend PASS en el grupo indicado debajo | CLOSED |
| A7 — concurrencia de autorización de ventas | PostgreSQL 6/6 PASS | CLOSED |
| A8 — retry/idempotencia de autorización | PostgreSQL 13/13 + HTTP/PostgreSQL 3/3 PASS | CLOSED |

Frontend focal agrupado previo: 113/113 PASS (incluye autoselección de presentación y
carreras de búsqueda/bodega); no sumarlo como ejecuciones independientes por caso.
A5 conserva el fix de una línea y sus cuatro casos de regresión en
`InvoiceItemSearchRepositoryTests`; evidencias [RED](docs/evidence/a5-barcode-search-01/red.trx)
y [GREEN](docs/evidence/a5-barcode-search-01/green.trx). Contrato funcional únicamente en
[Backend](docs/architecture/backend.md#búsqueda-por-barcode-de-presentación-a5).
Validación del agente en copia temporal sin leer/copiar/compilar el archivo RED histórico;
revisión focal del diff y `git diff --check` PASS. Esta consolidación no ejecuta pruebas,
no acredita despliegue y no modifica otros cierres. Commit y push no realizados;
el propietario conserva su ejecución. CLOSED: no reauditar salvo BUG REAL relacionado.

## Cierres acreditados y límites

| Ámbito | Estado previamente registrado |
|---|---|
| A1 Ítems | CLOSED, 43cce57a9 |
| A2 costo negativo | CLOSED, 64e1b21ca |
| B1/B2 Inventario | CLOSED; evidencia en los bloques precedentes |
| First Run | Cerrado; SSOT en first-run.md |
| QA-INVENTORY-INFRA-SUITES-01 | CLOSED, 509ac6d75; PostgreSQL 39/39 PASS |
| Compras/Gastos/CxP/Pagos, cierre 2026-09-12 | LISTO / CERRADO para ese alcance; [matriz QA](docs/QA/PURCHASES-EXPENSES-PAYABLES-CLOSEOUT.md). No cierra ampliaciones posteriores ni Accounting global |
| CxC y cobros, cierre 2026-10-27 | **CLOSED** para el alcance de la sección precedente; [SSOT](docs/architecture/receivables.md) y [matriz QA](docs/QA/RECEIVABLES-COLLECTIONS-CLOSEOUT.md) |
| Pricing, BP, ElectronicDocuments e infraestructuras FROZEN | Conservan sus cierres y límites; [infraestructuras](docs/architecture/frozen-infrastructure.md) |
| A9 recovery comercial | **CLOSED (2026-10-27)**; cierre funcional aprobado expresamente por el propietario, exclusivamente para el alcance create-only de [Sales recovery](docs/architecture/backend.md#lotes-fairness-y-ownership). Evidencia PASS existente: PostgreSQL 19/19, guard 1/1, job 1/1, issuer 31/31, arquitectura 143/143 y gates técnicos. No incluye envío, reenvío ni nuevas funcionalidades; no acredita nuevas ejecuciones ni despliegue. |

A3–A8 quedan acreditados por el cierre aprobado en su sección precedente; la discrepancia
documental anterior queda resuelta, sin ampliar el alcance de A9.
AUTH sensitive logging: fix focal 21/21 PASS; sin modificar autenticación ni persistencia.
No usar resultados anteriores como evidencia de que el árbol actual pasó una suite.

## Riesgos y limitaciones vigentes

Registrados sin autorización de implementación; no constituyen trabajo abierto.

- **Aprobación documental:** elegir `SingleStep`/`MultiStep` en Documentos y flujos bloquea
  la confirmación de Gastos, porque no existe flujo de aprobación. Contrato:
  [security.md](docs/architecture/security.md#permisos-vs-política-de-flujo-documental-document-flow-policy-01).
  La aprobación es intención futura en [ROADMAP](docs/ROADMAP.md#etapa-1--completar-erp-core).
- **Comprobantes electrónicos sin respuesta concluyente (Caso #32, V1 aprobado 2026-11-04; límites aceptados):** se
  consultan durante el plazo de 24 h del SRI, nunca se reenvían; la conciliación manual solo registra evidencia
  y reconsulta. Límites V1 aceptados por el propietario (2026-11-04): un comprobante que no consta recibido
  permanece en "Requiere conciliación" y se escala al responsable fiscal o soporte, sin reenvío ni cierre
  manual (R32, D1; el reenvío explícito es intención futura en [ROADMAP](docs/ROADMAP.md#etapa-1--completar-erp-core)).
  Una retención anterior al ciclo de ADR-036 cuyo origen ya fue anulado admite evidencia, no consulta (D2);
  con el ciclo vigente la anulación del origen se bloquea mientras su comprobante es incierto. Verificación de
  solo lectura D3 (2026-11-04, `erp_zh_dev` Development): 0 registros heredados; otros entornos no verificados.
  Contrato: [backend.md](docs/architecture/backend.md#límites-y-finalización).
- **Comprobante electrónico que excede el tamaño admitido (R16):** se bloquea antes del envío y no tiene
  recuperación automática; Ventas no limita líneas (≈ 420 ítems en el peor caso medido). Decisión pendiente del
  propietario. Detalle: [matriz SRI §5.6](docs/sri/SRI-2.34-COMPLIANCE-CLOSURE.md#5-limitaciones-externas-y-puntos-blocked).
- **Logs de autenticación anteriores a la corrección de AUTH sensitive logging:** pueden
  contener el campo `successorHash` del evento de rotación de refresh token. No se acredita
  exposición ni incidente; su revisión o depuración depende de la operación de cada entorno.
- **Aislamiento entre empresas — límites conocidos tras SEC-03** (contrato:
  [security.md](docs/architecture/security.md#aislamiento-entre-empresas-del-mismo-tenant)):
  `CreateCompanyUserPreferences` es un consumidor interno sin marcador de
  empresa; `INV_E2` no cubre todos los módulos ni detecta un marcador TENANT puesto por error.
  Tras SEC-04 la resolución de permisos ya no usa la única membership cuando
  el header no tiene membership; la firma `IRuntimePermissionAuthorizer.IsAuthorizedAsync`
  conserva el parámetro `role`, sin efecto. Los perfiles de acceso son por empresa desde SEC-05.
  Retirado (2026-11-03): `UpdateCustomerRoleConfigValidator` (request TENANT) ya no revela
  códigos de otra empresa; SEC-04 rechaza con 403 un `X-Company-Id` sin membership activa antes
  del validator y, sin header, la consulta de catálogo no devuelve filas.
  Verificación de cobertura (2026-11-03): fuera de los namespaces de `INV_E2`, los requests que
  operan datos de una empresa declaran marcador o validan con el guard en el handler; el guardrail
  no impide que un request nuevo de esos módulos omita ambos. Corregido (2026-11-03):
  `UpdateCompany` (`PUT /companies/{id}`) evaluaba `erp.companies.update` solo en la empresa del
  header — un Admin de A con membership limitada en B podía renombrar o desactivar B; ahora exige
  el permiso también en la empresa del id (`CrossCompanyAdminPrivilegeHttpTests`,
  `UpdateCompanyHandlerTests`).
- **Seguridad — revocación de permisos con caché (SEC-06):** garantías y
  contrato en [security.md](docs/architecture/security.md#aislamiento-entre-empresas-del-mismo-tenant).
  Límites: (1) cumplir "Redis obligatorio con varias instancias" depende de que el despliegue declare
  `Redis:Required=true`; el repositorio no conoce la cantidad real de instancias. (2) Si Redis
  desaloja una marca de invalidación en curso (política de memoria `volatile-*`/`allkeys-*`) mientras
  Complete falló, la protección de ese alcance termina antes de los 900 s — excluido por el
  contrato `noeviction` verificado al arrancar. (3) En el alta de usuario
  (transacción ambiente) la invalidación se completa antes del commit externo; el usuario es nuevo y
  no tiene entradas previas. (4) La caché de navegación se invalida solo en el proceso local (no
  autoriza). (5) Escrituras de caché con más de 60 s entre la observación del sello y la escritura se
  descartan (más lecturas a BD, nunca más permisos). (6) Garantías solo operativas: no cambiar la
  configuración de Redis en caliente, no restaurar respaldos ni promover réplicas, monitoreo de
  memoria y alertas de disponibilidad; con `noeviction` y memoria llena los cambios de permisos
  responden 503 sin guardar. (7) Un Redis gestionado que bloquee `CONFIG` impide el arranque hasta
  aprobar una verificación equivalente. (8) El Redis de producción (Coolify) no está inspeccionado:
  si no cumple el contrato, la API no arrancará tras desplegar este cambio.

## Consolidación documental

2026-10-26: edición autorizada de documentos e índice ADR, sin lógica, pruebas funcionales,
base de datos, migraciones, capturas o logs. Publicada en `ba06799ac99b883bcca289d914c69f4cb261f672`;
[CI #1000](#inventario-v1--evidencia-ci-de-referencia) SUCCESS.
Trabajo abierto: [WORK_QUEUE](docs/agent/WORK_QUEUE.md). Futuro: [ROADMAP](docs/ROADMAP.md).
