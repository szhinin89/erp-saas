# Auditoría UX SSOT del frontend ERP — ZH-FRONTEND-UX-SSOT-AUDIT-01

> **Estado:** auditoría (2026-09-29). Documento de hallazgos, **no normativo**: las reglas vigentes
> siguen en [`frontend.md`](./frontend.md), [`visual-messages.md`](./visual-messages.md),
> [`modal-standard.md`](./modal-standard.md) y [`form-validation.md`](./form-validation.md).
> Ningún hallazgo se corrigió en esta tarea. Cifras sobre archivos productivos
> (`frontend/src`, sin `*.test.*`) del HEAD auditado.

Meta evaluada: 1 intención → 1 entrada clara → 1 flujo oficial → 1 acción primaria → 1 resultado →
1 forma estándar de informar éxito/error; y 1 owner por patrón visual.

---

## A. Inventario de patrones oficiales

| Patrón | Owner / ubicación | Variantes oficiales | Consumidores (archivos) | Paralelo / legacy encontrado |
|---|---|---|---|---|
| Botón | `ZHBtn`, `ZHLinkButton` — `components/zh/ZHForm.tsx` | `primary`, `secondary`, `ghost`, `danger`, `cta` (acción crítica única), `size` | 151 / 6 | 55 `<button>` crudos en `modules/` (ver B); 0 `className="zh-btn"` manual |
| Botón icono | `ZHIconButton`, `ZHRowDeleteAction`, `ZHLineAction*` | — | 29 / 4 | chips/remove locales (`items-barcode-chip__remove`, `prd-search-clear`) |
| Modal | `ZHModal` (`components/zh/ZHModal.tsx`), `ZHDrawer` | `size`, `title`/`subtitle`/`footer` | 33 / 4 | ninguno (todos los `*Modal.tsx` se apoyan en `ZHModal`) |
| Confirmación | `message.confirm()` → `ZHGlobalDialogs` → `ZHConfirmModal` | `danger`/`warning`/`default` | `message.confirm` 36 archivos (44 llamadas); `ZHConfirmModal` directo 11 | mismo visual, 2 APIs (ver C) |
| Tabla | `ZHDataTable` — `components/zh/ZHDataTable.tsx` | `showRowNumber`, paginación, `tableClassName` documentadas | 70 | 17 `<table>` crudas en módulos (ver F) |
| Notice de página | `ZHPageNotice` (wrapper de `ZHFormAlert`) | `success`/`error`/`warning`/`info`/`attention`/`neutral` | 130 (+`ZHFormAlert` directo 8) | `.prd-error-banner` (6), avisos locales `md-partner-*-notice`, `sf-cash-session-notice`, `prd-draft-banner` |
| Notices de línea / compactos | `components/zh/notices/*` | `ZHCompactNotice`, `ZHNoticeList`, `ZHNoticePopover`, `ZHNoticeBadge`, `ZHLineNoticeSummary`, `ZHActionNotice` | 2 / 1 / 3 / 3 / interno / **0** | — |
| Ayuda contextual (ZH-HELP) | `components/zh/help/*` + `help/helpRegistry.ts` | `ZHFieldHelp`, `ZHHelpIcon`, `ZHHelpPopover`, `ZHSectionHelp`, `ZHInlineHint`, `ZHHelpDrawer` | 8 / 2 / 2 / 1 / **0** / **0** — solo Ventas y Compras | `title="…"` largos como tooltip explicativo (≈50 en 35 archivos, parte legítimos) |
| Feedback de operación | `message.success/error/info/warning` — `lib/messages` | toast | 65 / 61 / 8 archivos | ninguno local (VM-6 cumplido) |
| Filtros | `ZHFilterBar` (`plain`) | chrome / plain, `onClear`, chips | 13 | filtros sueltos en páginas (no cuantificado) |
| Acciones de formulario | `ZHFormActions`, `.zh-form-actions-row(--end/--flush)`, footer de `ZHModal`, `ConfigTabsLayout` | — | 26 + otros | 4 páginas con Guardar fuera de estos patrones (ver G) |
| Error de campo | `ZHField error`/`fieldError`, `.zh-field-hint--error`, `applyServerErrors` | — | 109 (`ZHField`) | — |
| Empty / loading / error de página | `EmptyState`, `LoadingState`, `ErrorState`, `NoAccessPage` (`PageShell.tsx`); `ZHDataTable.emptyMessage`; `.zh-inline-empty` | — | 28 / 50 / 3 / 57 | — |
| Paginación | interna de `ZHDataTable` / `ReportPageTemplate` | — | — | paginación propia `pg-pagination-btn` en `admin/access-sessions`, `admin/activity` |
| Tabs | `ZHTabBar` (N pestañas) y `ConfigTabsLayout` (Lista/Editor, FROZEN) | `fill`, `disabled`, `icon` | 9 | 13 módulos con `.prd-tab-btn` a mano (sin `role="tab"`/`aria-selected`), `md-detail-tab` (masterData) |
| Cards | `ZHCard` | `title`, acciones | 44 | `TableCard` (5, `PageShell`) — distinto propósito |
| Badges | `Badge` (`PageShell.tsx`), `ElectronicDocumentStatusBadge`, `ZHNoticeBadge` | `variant` de color | 92 | — |
| Selectores / pickers | `ZhSelect`, `ZhSearchSelect`, `ZHPickerResultItem`/`ZHPickerSelectedValue`, `supplierPickerFacade`, `customerPickerFacade` | — | 74 / **1** / 8 / 4 | 37 `<select>` crudos en 16 archivos; ≥5 pickers de producto (ver H) |
| Inputs numéricos/fecha | `ZhDecimalInput`, `ZhCurrencyInput`, `ZhNumberInput`, `ZhDateInput`, `ZhDateTimeInput` | `precision` semántica | 38 / 2 / 14 / 24 / 2 | `type="number"` ×4, `type="date"` ×6 crudos (ver G) |
| Upload | `ZhFileUpload`, `ZhBatchProgress` | — | 3 / 1 | — |
| Plantillas de página | `ErpPageTemplate`, `PageShell`, `ReportPageTemplate` (+`ReportKpiCard`) | — | 47 / 35 / 21 | coexistencia `ErpPageTemplate`/`PageShell` (ver K) |

---

## B. Botones

55 `<button>` crudos en `modules/` + 41 en `components/` (estos últimos son la implementación interna
de los propios componentes DS: `ZHTabBar`, `ZHDataTable`, launcher, help, notices → **A**).

| Clase | Casos en módulos | Clasificación | Detalle |
|---|---|---|---|
| Tabs `.prd-tab-btn` a mano | 13 archivos (access, accounting reports, branches ×2, configuracion ×2, emissionPoints, establishments, finance/CreditTerms, inventory/Kardex, masterData ×3) | **B** → `ZHTabBar` | reimplementan `ZHTabBar` sin `role="tablist"`/`aria-selected` (a11y). `md-detail-tab` en `MasterDataBusinessPartnerDetailPage` es un tercer diseño de tabs |
| Paginación `.pg-pagination-btn` | `admin/access-sessions/AdminUserSessionsPage`, `admin/activity/ActivityPage` | **D** | duplica la paginación de `ZHDataTable` |
| Auth (`zh-auth-submit`, `zh-auth-back`, `zh-auth-link`, `lp-forgot-link`, `lp-password-toggle`) | auth, admin-core | **C** | patrón DS de pantallas de acceso (submit con spinner full-width) |
| Toggles de password | `sri-pass-toggle` (configuracion), `lp-password-toggle` (auth) | **E** | dos toggles de visibilidad de contraseña; candidato a una variante DS única |
| Disclosure / expand (`pf-collapsible__toggle`, `pdl-line-row__collapse-toggle`, `cat-te-expand`, `pg-section-header`) | purchases, items, access | **A/C** | controles de expandir; legítimos |
| Pickers (`pf-picker-item`, `zh-picker__result`) | inventory/Kardex, pricing ×2 | **D** | duplican `ZHPickerResultItem` (ver H) |
| Chips / celdas clicables (`items-barcode-chip__remove`, `prd-search-clear`, `acc-coa-chip`, `acc-user-cell`, `rowClass`) | items, inventory, masterData, accounting, access, expenses | **A/E** | legítimos o inconsistencia menor |

**Acción primaria única en operaciones críticas:**

| Operación | Estado |
|---|---|
| Emitir venta | ✅ `EmitButton` es el único consumidor de `variant="cta"` |
| Confirmar compra (borrador) | ⚠️ "Actualizar borrador" y "Confirmar" visibles a la vez, ambos `primary` (`PurchasesPage` L1065–1094) |
| Retención de compra | ⚠️ "Calcular" y "Emitir" `primary` en la misma sección (`PurchasesPage` L3104/3119) |
| Pago a proveedor | ✅ un solo camino (formulario → `SupplierPaymentConfirmModal`) |
| Movimiento / apertura / cierre de caja | ✅ un primario por modal |

---

## C. Modales / confirmaciones

- `window.confirm/alert/prompt`: **0** (VM-7 cumplido).
- Modales: todos los `*Modal/*Dialog/*Drawer` de módulos se apoyan en `ZHModal`/`ZHDrawer`; las
  clases `zh-modal-footer-*` en módulos son sub-slots del footer oficial (legítimo). **Sin overlays
  propios** (los `overlay` de auth son decorativos: `zh-auth-banner-overlay`).
- Confirmación: **un solo visual** (`ZHConfirmModal`) con **dos APIs**:
  `message.confirm()` (normativa, VM-3) y `ZHConfirmModal` montado con estado local en 11 archivos
  (admin sessions, caja reasons, expenses form, inventory reasons/adjustments ×2/warehouses,
  pricing ×2, purchases, sales). `visual-messages.md` ya lo declara legacy con migración incremental
  → converge a `message.confirm()`; **no** hay dos mecanismos visuales.
- Operaciones críticas revisadas (anular, confirmar, emitir, pagar, movimiento de caja, reversar):
  acción → `message.confirm`/modal de confirmación propio del flujo (`SupplierPaymentConfirmModal`,
  `AdjustmentLifecycleModals`) → ejecución → `message.success/error`. Los modales de confirmación
  con resumen de negocio (pago, ajuste) son **legítimamente distintos** de un confirm genérico.

---

## D. Notices / mensajes

| Caso | Clasificación | Nota |
|---|---|---|
| `ZHPageNotice` (130 archivos) | **A** | correcto; es wrapper de `ZHFormAlert` → mismo visual |
| `ZHFormAlert` directo (expenses ×2, items PricingTab, supplier-payments ×3) | **A/P4** | mismo visual con otra API; converger a `ZHPageNotice` al tocar |
| `.prd-error-banner` (`items-catalog.css`) en CreditTerms, PaymentTerms, PriceLists, PriceListCustomers/Exceptions (×2) | **B** | segundo diseño de "error" (sin icono, radio/colores propios) → `ZHPageNotice variant="error"` |
| `md-partner-notice-compact`, `md-partner-search-notice`, `md-partner-final-notice` (wizard de socios) | **B/E** | avisos locales info/warning; revisar contra `ZHCompactNotice`/`ZHPageNotice` |
| `sf-cash-session-notice` (Ventas: caja sin sesión) | **E** | aviso operativo del POS con CTA; candidato a `ZHPageNotice` + acción |
| `prd-draft-banner` | **E** | banner de borrador; revisar |
| `zh-auth-info-box`, `zh-auth-banner` | **A** | DS de auth |
| Errores de campo (`ZHField error`, `zh-field-hint--error`) | **C** | no son notices — correcto |
| Resultado de operación (`message.success/error`) | **D** | patrón oficial toast — correcto |
| `ZHActionNotice` | — | **0 consumidores** (exportado, sin uso) |

Regla verificada: info/warning/error/success tienen **un** diseño oficial (`zh-form-alert--*`); la
única desviación material es `.prd-error-banner`.

---

## E. ZH-HELP

- Adopción: `ZHFieldHelp` ×8, `ZHHelpIcon` ×2, `ZHHelpPopover` ×2, `ZHSectionHelp` ×1, con claves en
  `help/helpRegistry.ts` — **solo Ventas y Compras**. `ZHInlineHint` y `ZHHelpDrawer`: **0 usos**.
- Ayuda paralela: `title` largos que explican decisiones (no solo nombran la acción), p. ej.
  `sales/PaymentMethodsSection` ("Configure el mapeo SRI… en Configuración → Métodos de Pago…"),
  RIDE "Reutiliza el ya generado si no cambió nada", `supplier-payments` "Distribución medio ↔ cuota
  (automática)". Un `title` no es accesible en táctil ni se traduce por registro.
- Dónde **sí** debe existir ZH-HELP: configuración fiscal/SRI (mapeos de forma de pago, secuencias,
  emisión), condiciones de crédito/pago, política documental (`settings/document-flows`), reglas de
  asiento/`PostingRule`, parámetros operativos, retenciones en Gastos (paridad con Compras).
- Dónde **no**: `title` de botones icono que solo nombran la acción (legítimos), ayudas que dupliquen
  documentación extensa.

---

## F. Tablas

17 `<table>` crudas en módulos + `ReportPageTemplate` (DS) + `ZHDataTable` (DS).

| Archivo | Clasificación | Motivo |
|---|---|---|
| `company-management/CompanyManagementListPage` | **C** | listado de registros con estado/acciones → `ZHDataTable` |
| `sales/ReturnableLinesEditor` | **C** | mismo editor que `purchases/PurchaseReturnableLinesEditor`, que ya usa `ZHDataTable` → dos implementaciones de "líneas devolvibles" |
| `security/SecuritySettingsPage` | **D** | matriz usuarios × permisos (columnas dinámicas) — excepción documentada |
| `accounting/JournalEntryDetailPage` | **D** | necesita `<tfoot>` de totales (excepción ya documentada en el código) |
| `caja/CajaPage` | **B/D** | cuadre por método con totales; revisar si es tfoot/resumen |
| `purchases/PurchasesPage` (×5), `DistributeCostModal`, `PurchaseCreditNote{Discount,TaxSummary}LinesEditor`, `ResolvePendingProductsModal` | **D** | editores de línea con RHF/`useFieldArray`/preview calculado — excepciones admitidas por `frontend.md` |
| `sales/SalesPage`, `SalesRepricingTable`, `CreditSimulatorModal` | **D** | preview/simulación calculada en vivo |
| `items/PrincipalCodesSummary`, `items/CollectionSection` | **B** | tablas pequeñas semánticas de resumen |
| `supplier-payments/SupplierPaymentAllocationPreview` | **B/D** | preview de distribución calculada |

Paginación: `ZHDataTable` resuelve paginación, numeración (`showRowNumber`), empty y loading; los dos
listados de `admin/*` con paginación propia (ver B) son la única duplicación funcional.

---

## G. Formularios

- `<input type="number">` crudo (prohibido por el Estándar de Precisión): `finance/CreditTermsPage`
  ×2, `masterData/PaymentTermsPage` ×2 (plazos en días → `ZhNumberInput`).
- `<input type="date">` crudo: `inventory/kardex/KardexPage` ×2, `admin/access-sessions` ×2,
  `finance/RegisterSupplierCreditRefundModal` ×1 (→ `ZhDateInput`). El guard `frontend-datetime` no
  cubre el tipo de input.
- `<select>` crudo: 37 en 16 archivos (mayores: `components/items/ItemEditorModal/ItemEditorForm` ×7,
  `masterData/MasterDataSuppliersPage` ×6, `MasterDataBusinessPartnerDetailPage` ×4) → `ZhSelect`.
- Acciones Guardar/Cancelar: consistentes (`ZHFormActions`, footer de `ZHModal`, `ConfigTabsLayout`,
  `.zh-form-actions-row`) salvo `admin-core/AdminCoreDashboardPage`, `expenses/ExpenseCategoryFormPanel`,
  `items/PrincipalCodesSummary`, `items/CollectionSection`.
- Validación visual paralela: no detectada (patrón F-V1..F-V8 cerrado; errores vía `ZHField` +
  `applyServerErrors`). No se tocó Zod/RHF.

---

## H. Flujos funcionales

Método: rutas (`routes/*`), escritores HTTP por endpoint (357 endpoints distintos; **ningún endpoint de
escritura tiene dos clientes frontend**) y comandos backend.

| Dominio / objetivo | Entradas | Service / facade | Comando API | Clasificación |
|---|---|---|---|---|
| Ventas — emitir | `/sales` (`EmitButton`) | `salesService` | `SalesController` issue | **A** |
| Ventas — cobro al contado | dentro de la factura (`PaymentMethodsSection`) | `salesService` | issue | **A** (distinto de cobro de CxC) |
| Ventas — imprimir RIDE | venta, devolución (`useRideActions`) | `rideGenerationFacade` | `GET /ride`, `POST /ride/regenerate` | **A** |
| Retención — RIDE | Compras | `purchaseRetentionFacade.getRidePdfBlob` | `GET /retentions/{id}/ride/pdf` (`GenerateRetentionRidePdfQuery`, on-demand sin caché) | **B-decisión**: segundo pipeline de RIDE (el genérico `/ride` tiene caché/regenerate); render compartido (`RetentionRidePdfService`) |
| Ventas — devolución | `/sales/returns/new` | `salesReturnService` | returns | **A** |
| Compras — crear | recepción XML (`/purchases/reception`) y manual (`/purchases`) | `purchaseService` / reception | create / confirm | **C** (dos orígenes de datos al mismo comando) |
| Compras — devolución ↔ NC | `/purchases/returns/new?invoiceId`, `/purchases/credit-notes/new` (dual: manual / desde recepción) | `purchaseReturnService`, `purchaseCreditNoteService` | `POST returns/{id}/credit-note` (registra NC documental por clave) **y** `POST credit-notes/{id}/link-return` (vincula `PurchaseCreditNote`) | **B**: dos implementaciones del vínculo NC↔devolución con dos representaciones (`SupplierCreditNoteLink` vs `PurchaseCreditNote`); backend protege 1:1 e idempotencia → sin doble operación |
| Caja — apertura/cierre | `/treasury/cash` | `cajaService.open/close` | cash sessions | **A** |
| Caja — movimiento manual | `/treasury/cash`, POS (`manualCashMovementFacade`) | `useManualCashMovementFlow` único | `POST cash-sessions/{id}/movements` | **A** (shortcut legítimo, mismo flujo) — ver P1-01 |
| Caja — fondeo | `/treasury/cash/funding-requests`, pago a proveedor (modo solicitud) | `cashFundingRequestFacade` | funding requests (idempotente) | **A/C** |
| CxP — pagar | `/supplier-payments/new` (entradas desde CxP/cartera navegan al mismo form) | `supplierPaymentService` | `POST /supplier-payments` | **A** — ver P1-01 |
| CxP — aplicar crédito | `/suppliers/credits/:id` (CxP enlaza con `applyCreditRoute`) | `supplierCreditService.apply` | `POST supplier-credits/{id}/apply` (idempotente) | **A** |
| CxC — cobrar | `/finance/receivables` (`RegisterCollectionModal`) | `paymentService.registerCollection` | `POST payments/collections` | **A** — ver P1-01 |
| Inventario — ajuste/transferencia/kardex | `/inventory/adjustments`, `/inventory/transfers`, `/inventory/kardex` | services propios | stock | **A** |
| Retenciones — emitir | Compras (tras confirmar, `issueForPurchase`) y Gastos (intent al confirmar) | `retentionsService` / `expenseDocumentService.confirm` | `POST purchases/{id}/retention` vs confirm de gasto; backend comparte `RetentionIssuer` | **B-UX**: dos momentos de emisión para el mismo documento (tras confirmar vs al confirmar); `RetentionDocumentDto` declarado dos veces (expenses y retentions) |
| Contabilidad — asiento/reportes | `/accounting/journal-entries`, `/accounting/reports` | accounting | — | **A** |
| Empresa — identidad (RUC, razón social, activo) | `/companies/:id/edit` (company-management) y consola global (`admin-core`) | `companyManagementService.update` / `adminCoreService.updateCompany` | `UpdateCompanyCommand` vs `UpdateCompanyForAdminCoreCommand` | **B/C**: dos comandos para la misma edición (actores distintos justifican la autorización, no dos implementaciones) — ✅ ZH-COMPANY-IDENTITY-SSOT-01: dos contextos legítimos, una regla |
| Empresa — perfil/branding/parámetros | `/settings/company` | `companyProfileService` | `PUT companies/profile|fiscal|operation|documents|branding` | **A** (campos disjuntos de la identidad) |
| Seleccionar producto | Compras `ProductPicker`, Inventario `AdjustmentProductPicker` y `TransferProductPicker`, Kardex (inline), Pricing (inline ×2), Ventas `SalesItemSearchResultsGrid` | `itemLookupFacade` (mayoría) | `GET items` | **B** (UI): ≥5 implementaciones de un picker de ítems; `ZhSearchSelect` + un `itemPickerFacade` del owner (como `supplierPickerFacade`) sería el SSOT |

Rutas: todas las rutas duplicadas son `<Navigate>` legacy a una sola pantalla (**A/C**); no hay dos
pantallas montadas para el mismo objetivo.

---

## I. Navegación y permisos

- Menú: SSOT backend (`AppFeature` → `getSessionMenu` → `useAppLayoutNavigation`); `RouteAccessGuard`
  valida contra `mainMenuGroups`. Alias legacy documentados.
- **Desviación:** `nav/navConfig.ts` → `MENU_DESCRIPTION_BY_ROUTE_PREFIX`: 24 descripciones de menú
  hardcodeadas en español por prefijo de ruta (segunda fuente de metadatos de menú, fuera de i18n y de
  `AppFeature`).
- Roles: los checks reales pasan por `usePermissionsUi().isAdminRole` (SSOT). Excepciones:
  `modules/config/ConfigContext.tsx` (`isAdminRole(user?.role)` para leer configuración de sesión — rol
  en lugar de permiso) y `AdminCoreProtectedRoute` (detección de sesión global: tenant global + rol —
  legítimo). Los `role === "customer"|"supplier"` de masterData son el rol del socio de negocio
  (falso positivo). Access pages usan `!isAdminRole && !canManage` (bypass admin coherente con backend).

---

## J. Services / facades / state

| Hallazgo | Tipo |
|---|---|
| `GET /accounting/accounts` llamado desde `cashRegisters/hooks/useCashRegistersPage` y `finance/pages/BankAccountsPage` con `apiGet` directo, saltando `accounting/facades/accountLookupFacade` (existente). El guard de imports no lo ve (URL cruda) | dependencia cross-módulo oculta |
| `GET /catalog/{brands,barcode-types,category-nodes,sri-uom,sri-vat-rates,sri-ice-rates}` re-implementados inline en `items/components/ItemForm/ItemFormTabs` (6) y `components/items/ItemEditorModal/useItemCreationCatalogs` (4) además de `catalogService`/`categoryNodeService` | dos/tres clientes por endpoint |
| `GET /catalog/sri-supplier-types` en `items/catalog/api/catalogService` y `masterData/api/useSriSupplierTypes` | dos clientes |
| `GET /payment-methods` en `sales/api/paymentMethodService` y `sales/api/salesService` | duplicado intra-módulo |
| `GET /settings/establishments/lookups` en `establishments/api` y `emissionPoints/api` | duplicado cross-módulo sin facade |
| `RetentionDocumentDto` declarado en `retentions/api` y `expenses/api` | tipo duplicado |
| Llamadas HTTP en páginas (fuera de `api/`): `auth/{Setup,ResetPassword,ForgotPassword}Page`, `items/detail/VariantsSection` | regla "API desde capa `api/`" |
| Stores: `authStore`/`sessionStore`/`activeBranchStore`/`accessStore` + 2 stores UI de módulo — sin estado duplicado detectado | OK |

---

## K. Design System

- Tokens: 0 colores literales en CSS de módulos/shared (guard `F-04-token` efectivo).
- Valores mágicos: 47 `font-size: Npx` en 13 CSS de módulos (18px ×11, 20px ×6, 16px ×6, 22px ×5) —
  equivalen a escalas tipográficas existentes (`--text-*`); 11 `border-radius: Npx`, 3 `box-shadow`
  literales, paddings `6px`/`2px`/`1px 6px` repetidos (chips/badges locales).
- Variantes casi iguales: dos diseños de tabs (`ZHTabBar` vs `md-detail-tab`), dos de error
  (`zh-form-alert--error` vs `.prd-error-banner`), dos toggles de password, pickers de ítem locales,
  tarjetas resumen (`cj-summary-card` vs `pg-kpi`/`ReportKpiCard` — revisar antes de unificar).
- Plantillas: `ErpPageTemplate` (47) y `PageShell` (35) coexisten; `PageShell` también exporta
  `EmptyState/LoadingState/Badge` usados por ambas. Sin decisión documentada de cuál es la plantilla de
  página canónica.
- Componentes DS sin consumidores: `ZHActionNotice`, `ZHInlineHint`, `ZHHelpDrawer`.

---

## L. Tabla de hallazgos

| ID | Hallazgo | Módulo | Tipo | Duplicidad real | SSOT actual | SSOT recomendado | Riesgo | Prioridad | Acción |
|---|---|---|---|---|---|---|---|---|---|
| P1-01 | Comandos de **creación** de dinero sin idempotencia de servidor: pago a proveedor (`POST /supplier-payments`), cobro CxC (`POST payments/collections`), movimiento de caja (`POST cash-sessions/{id}/movements`). Defensa solo de UI (`saving` de React; en caja el `setSaving(true)` ocurre después del `await message.confirm`) | supplier-payments, finance, caja | Flujo crítico | S (doble registro posible) | UI state | `ClientRequestId` en comando + guard como en SupplierCredit/Purchases | doble pago/cobro/movimiento ante doble envío o reintento | **P1** (verificar con reproducción) | ticket backend+frontend dedicado |
| P2-01 | Vínculo NC↔devolución de compra con dos endpoints/representaciones | purchases (CLOSED) | Flujo | S | 2 comandos | un comando de vínculo | inconsistencia documental (backend protege 1:1) | **P2** | decisión de reapertura |
| P2-02 | Identidad de empresa editable con `UpdateCompanyCommand` y `UpdateCompanyForAdminCoreCommand` | company-management, admin-core | Flujo | S | 2 comandos | 1 operación de dominio, 2 autorizaciones | reglas divergentes | **P2** | ✅ Resuelto en ZH-COMPANY-IDENTITY-SSOT-01: contextos legítimos (tenant vs Admin Global), regla única (`CompanyIdentityRules` + `CompanyIdentityUpdate`); divergencia real encontrada y corregida: el endpoint global respondía 400 (en vez de 422/404) ante datos inválidos o empresa inexistente |
| P2-03 | Picker de producto con ≥5 implementaciones | purchases, inventory, pricing, sales | UI/flujo | S | cada módulo | `itemPickerFacade` (owner items) sobre `ZhSearchSelect` | UX y búsqueda divergentes | **P2** | diseñar picker único |
| P2-04 | HTTP crudo cross-módulo a `/accounting/accounts` saltando `accountLookupFacade` | cashRegisters, finance | Service | S | `apiGet` inline | `accountLookupFacade` | acoplamiento oculto | **P2** | ✅ Resuelto en ZH-FRONTEND-HTTP-CLIENT-SSOT-01 |
| P2-05 | Catálogos de items re-implementados inline (`ItemFormTabs`, `useItemCreationCatalogs`) + `sri-supplier-types` doble + establishments lookups doble + payment-methods doble | items, masterData, emissionPoints, sales | Service | S | varios | 1 service por endpoint + facade | divergencia de normalización | **P2** | ✅ Resuelto en ZH-FRONTEND-HTTP-CLIENT-SSOT-01 (también `sri-id-types` doble y HTTP inline en páginas de auth; guard `frontend-http-access`) |
| P2-06 | Retenciones: emisión tras confirmar (Compras) vs al confirmar (Gastos); `RetentionDocumentDto` duplicado | purchases, expenses, retentions | Flujo/UX | Parcial | backend único (`RetentionIssuer`) | decisión funcional + tipo único en facade de retentions | UX inconsistente | **P2** | decisión funcional (tipo `RetentionDocumentDto` ya unificado en ZH-FRONTEND-HTTP-CLIENT-SSOT-01) |
| P2-07 | Dos pipelines de RIDE (`/ride` con caché vs `/retentions/{id}/ride/pdf` on-demand) | ride, retentions | Flujo | Parcial | 2 endpoints | `/ride` con `sourceModule=Retention` | comportamiento de caché distinto | **P2** | decisión backend |
| P3-01 | Tabs `.prd-tab-btn` a mano (13 archivos) + `md-detail-tab` | varios | DS/a11y | S | markup local | `ZHTabBar` | a11y (sin roles ARIA) | **P3** | migrar |
| P3-02 | `.prd-error-banner` como segundo diseño de error | pricing, finance, masterData | DS | S | CSS local | `ZHPageNotice error` | inconsistencia visual | **P3** | migrar |
| P3-03 | Dos acciones `primary` simultáneas en confirmación de compra y en retención | purchases (CLOSED) | UX | — | — | 1 primaria (`cta` o secundaria para guardar) | error de operador | **P3** | decisión de reapertura |
| P3-04 | `type="number"` ×4 / `type="date"` ×6 crudos | finance, masterData, inventory, admin | Form | S | input nativo | `ZhNumberInput`/`ZhDateInput` | precisión/formato | **P3** | migrar |
| P3-05 | Paginación propia en listados admin | admin | Tabla | S | botones locales | `ZHDataTable` paginado | inconsistencia | **P3** | migrar |
| P3-06 | Listado `CompanyManagementListPage` y `ReturnableLinesEditor` (ventas) con `<table>` | company-management, sales | Tabla | S | `<table>` | `ZHDataTable` | inconsistencia / doble editor | **P3** | migrar |
| P3-07 | Descripciones de menú hardcodeadas (`MENU_DESCRIPTION_BY_ROUTE_PREFIX`) | nav | Navegación | S | frontend | `AppFeature`/i18n | i18n roto, 2 fuentes | **P3** | mover a backend/i18n |
| P3-08 | ZH-HELP solo en Ventas/Compras; ayudas en `title` largos | varios | Ayuda | Parcial | `title` | `ZHFieldHelp`/`ZHHelpPopover` | ayuda inaccesible | **P3** | por dominio (config SRI primero) |
| P3-09 | `<select>` crudos (37) | items, masterData, caja… | Form | S | nativo | `ZhSelect` | inconsistencia | **P3** | migrar al tocar |
| P4-01 | `ZHConfirmModal` directo (11) vs `message.confirm` | varios | Confirmación | Mismo visual | 2 APIs | `message.confirm` | bajo | **P4** | migrar incremental (ya documentado) |
| P4-02 | `ZHFormAlert` directo (6) | expenses, items, supplier-payments | Notice | Mismo visual | 2 APIs | `ZHPageNotice` | bajo | **P4** | al tocar |
| P4-03 | DS sin consumidores: `ZHActionNotice`, `ZHInlineHint`, `ZHHelpDrawer` | components/zh | DS | — | — | decidir uso o retiro | bajo | **P4** | decisión DS |
| P4-04 | `font-size` px mágicos (47), radios/sombras literales | módulos | DS | — | CSS local | tokens `--text-*`, `--radius-*`, `--shadow-*` | bajo | **P4** | al tocar |
| P4-05 | Dos toggles de password, avisos locales del wizard de socios, `sf-cash-session-notice`, `prd-draft-banner` | auth, configuracion, masterData, sales | DS | Parcial | local | variante DS | bajo | **P4** | revisar |
| P4-06 | HTTP en páginas de auth y `VariantsSection` fuera de `api/` | auth, items | Service | N | inline | `api/` del módulo | bajo | **P4** | al tocar |
| P4-07 | `ConfigContext` decide por rol (`isAdminRole`) en vez de permiso | config | Permisos | N | rol | permiso | bajo | **P4** | revisar |
| P4-08 | `ErpPageTemplate` y `PageShell` sin decisión de plantilla canónica | components/templates | DS | Parcial | ambos | decisión documentada | bajo | **P4** | ADR corto |
| NONE | `window.confirm` 0; modales sobre `ZHModal`; feedback por `message.*`; rutas duplicadas = redirects; tablas D justificadas (matriz, tfoot, editores RHF, previews); botones internos del DS; auth DS; caja/CxP/CxC/inventario/ventas con un flujo por operación | — | — | N | — | — | — | **NONE** | dejar igual |

---

## M. Orden recomendado de corrección

1. **P1-01** — reproducir doble envío (doble clic/Enter, reintento) en pago a proveedor, cobro y
   movimiento de caja; si se confirma, `ClientRequestId` + guard de idempotencia en los 3 comandos
   (patrón ya existente en SupplierCredit/Purchases) y generación en el frontend por intento.
2. **P2-04, P2-05** — consolidar clientes HTTP por owner (bajo riesgo, sin UX).
3. **P2-02** — alinear reglas de identidad de empresa en una operación de dominio.
4. **P2-03** — diseño del picker de ítems único (owner items), luego migrar consumidores.
5. **P3-01, P3-02, P3-04, P3-05, P3-06, P3-09** — convergencia DS mecánica por módulo, sin cambiar flujo.
6. **P3-07, P3-08** — metadatos de menú a backend/i18n; ZH-HELP en configuración SRI/fiscal.
7. **P2-01, P2-06, P2-07, P3-03** — requieren decisión funcional (Compras CLOSED, Retenciones, RIDE).
8. **P4** — al tocar cada archivo.

## N. Guards recomendados (solo propuesta)

| Guard | Regla estable | Riesgo de fragilidad |
|---|---|---|
| `window.confirm/alert/prompt` prohibido | VM-7 | bajo (hoy 0) |
| `<input type="number|date|datetime-local">` prohibido fuera de `components/zh/inputs` | Estándar de precisión/fechas | bajo |
| Clase `.prd-tab-btn` fuera de `ZHTabBar`/`ConfigTabsLayout` prohibida | DS tabs | bajo |
| `.prd-error-banner` deprecada (patrón en `design-system.json`) | DS notices | bajo |
| Endpoint `/api/v1/...` literal en más de un archivo de `api/` (salvo lista cerrada) | 1 endpoint → 1 service | medio (URLs construidas dinámicamente) |
| `apiGet/apiPost` fuera de `modules/*/api/` | "API desde capa api/" | bajo |
| Import directo de `ZHConfirmModal` en módulos (tras migrar P4-01) | VM-3 | bajo |
| Raw `zh-btn` cuando existe `ZHBtn` | ya en 0 | bajo |
