# UIUX-GLOBAL-STANDARD-01 — auditoría y propuesta

Fecha: 2026-11-05. **Propuesta pendiente de aprobación del propietario; implementación global no autorizada.**
Actualización acotada: UIUX-GLOBAL-HEADER-01 implementa únicamente la variante de encabezado; contrato vigente en [Frontend](../architecture/frontend.md#encabezado-compacto-predeterminado-uiux-global-header-default-01). UIUX-GLOBAL-TABS-01 incorporó la variante de tres pestañas, que UIUX-GLOBAL-TABS-DEFAULT-01 convierte en default; [contrato vigente](../architecture/frontend.md#pestañas-responsive-automáticas-uiux-global-tabs-default-01). Los demás bloques siguen pendientes y esta auditoría conserva la evidencia del estado previo. Las decisiones del propietario sobre defaults globales (reemplazan la fila «C — defaults globales») y la planificación vigente están en [UIUX-GLOBAL-ARCHITECTURE-02](UIUX-GLOBAL-ARCHITECTURE-02.md).

Documento de planificación, no nueva normativa ni segunda SSOT. El comportamiento vigente sigue en [Frontend](../architecture/frontend.md) y la [baseline de layout](../FRONTEND_ARCHITECTURE_BASELINE.md). Estado de entrega exclusivamente en [STATUS](../../STATUS.md).

## Alcance y evidencia

Revisión estática del árbol de trabajo, incluidos cambios locales de UIUX-SUPPLIERS-01/02/03. Se conserva su código, pruebas, CSS, textos y evidencia. STATUS aún los registra READY FOR VISUAL REVIEW: esta auditoría no acredita aprobación visual ni cierre. No se implementan variantes, migraciones ni correcciones de código.

Fuentes inspeccionadas: `ZHLayout.tsx` (define ZHScreenHeading), `PageShell.tsx`, `ErpPageTemplate.tsx`, `ZHDataTable.tsx`, `ZHLineAction.tsx` y su CSS, `ZHLineActionItem.tsx`, `ReportPageTemplate.tsx`, `ZHCard.tsx`, `ZHTabBar.tsx`, `ConfigTabsLayout.tsx`, `ZHForm.tsx`, `design-tokens.css`, `zh-ui.css`, `page-template.css`, `styles/shared/items-catalog.css` y componentes/CSS de Proveedores. Rutas relativas a `frontend/src/`. Inventario reproducible de referencias al final; incluye consumidores indirectos por plantillas y wrappers al evaluar riesgo.

No se ejecutan pruebas funcionales, suites generales ni SRI: cambio exclusivamente documental. La revisión de código no sustituye capturas, interacción real ni gate del propietario.

## Contraste con contratos vigentes

- KEEP: shell único y un H1, PageShell/ErpPageTemplate, ZHDataTable, ZHLineAction, ReportKpiCard, ZHCard, ZHGrid y tokens existentes. No crear shells, componentes equivalentes ni importar CSS de un módulo desde otro.
- KEEP: ReportPage tiene excepción explícita de cabecera propia; no migrarlo automáticamente a PageShell. ConfigTabsLayout conserva su contrato fijo Lista/Editor y la condición FROZEN indicada por ZHTabBar; no rediseñarlo por este trabajo.
- DEBT documental: la tabla de estándares de frontend.md aún atribuye `.prd-tabs` a items-catalog.css; el archivo compartido declara su traslado y las reglas efectivas están en zh-ui.css. También persisten referencias a `.zh-form-tabs` pese a su deprecación. Corregir referencias en el bloque documental aprobado, sin reintroducir CSS ni habilitar migraciones masivas.
- FALTA de variante: el encabezado alineado de Proveedores, el grid de tres KPI iguales, el vacío de actividad y las pestañas móviles son composiciones locales, no opciones públicas del DS. No presentarlas como capacidades globales actuales.
- DEBT de accesibilidad a evaluar en implementación: ZHTabBar declara tablist/tab y aria-selected, pero no implementa navegación por flechas, roving tabindex ni relación tab/panel. No se considera validado un patrón completo solo por sus roles; esta auditoría no lo modifica.
- Baseline recomienda 980 px, admite excepciones y conserva utilidades de vacío `pg-pad-40`. El código usa varios cortes: no existe un token único de breakpoint que autorice sustituirlos todos. No homogeneizar medidas por decreto.

## Matriz de impacto A/B/C/D

A = capacidad existente reutilizable; B = variante nueva propuesta; C = cambio de default/global con riesgo; D = adaptación específica del consumidor. Clasificación de revisión: KEEP conserva lo actual, FALTA identifica capacidad propuesta y DEBT registra desajustes sin autorizar su ejecución. No se ha reproducido un BUG REAL nuevo.

| Patrón / clasificación | Existente y límite | Consumidores afectados si se modifica | Riesgo | Recomendación y pruebas necesarias antes de implementar |
|---|---|---|---|---|
| Encabezado compacto A KEEP | ZHScreenHeading compone H1/subtítulo/kicker/right; PageShell sincroniza document.title. Proveedores omite kicker localmente. | Todo PageShell directo y vía ErpPageTemplate: Clientes, Proveedores, Ítems, finanzas, inventario, configuración, acceso y contabilidad; ReportPage es excepción. | Bajo al reutilizar; alto al cambiar default. | Reutilizar encabezado único; no borrar categorías del menú. Probar PageShell.test.tsx y navegación de las tres tabs de Proveedores, título de navegador y un H1. |
| Encabezado alineado B FALTA / C / D | CSS local `:has(> .md-suppliers-page)` alinea título/descripción; hasta 768 px apila. PageShell no expone variante para esto. | Mismos shells; riesgo especial con kicker, acciones derechas o subtítulos largos. | Medio en opt-in; alto global. | Proponer opción explícita en ZHScreenHeading propagada por PageShell/ErpPageTemplate, default actual intacto. No propagar el selector local. Validar desktop/móvil, acciones, textos largos y salto al cambiar tabs. |
| Identidad principal/secundaria A KEEP / B FALTA / D | ZHDataTable permite render por columna. Proveedores usa razón social principal y comercial distinto tras trim como secundario; wrap de nombres. | MasterDataPartnerListTab comparte código con Clientes; otros candidatos: Ítems, transportistas, listados documentales. | Bajo al componer; alto si se universaliza significado o ancho. | El dominio elige la identidad, el DS solo presenta primary/secondary. Variante visual en DS solo si hay segundo caso real, sin conocer legalName/RUC. Probar vacío, espacios, igualdad, nombres largos, búsqueda y regresión Clientes. |
| Tabla móvil A KEEP / C / D | cellClassName/tableClassName, table--compact y showRowNumber ya existen. Proveedores oculta columnas 1/2/4 hasta 640 px y reubica identificación/estado en identidad. | Todos los ZHDataTable; documentos, cartera, Carga Inicial, tablas de detalle y administración no comparten columnas ni prioridad. | Alto si se copian nth-child, table-layout o min-width globalmente. | Mantener prioridad/ocultación por consumidor y significado, nunca por posición global. Preservar paginación, rowKey y acciones; probar carga/vacío, numeración/página, 5 columnas desktop y datos móviles accesibles. |
| Indicadores compactos A KEEP | ReportKpiCard layout=horizontal elimina min-height de la vertical; acepta label/value/trend/onClick. | Clientes/Proveedores, Dashboard, Monitor, reportes, Kardex, cartera, catálogos y configuración; inventario de referencias abajo. | Bajo opt-in; alto cambiar vertical por defecto. | Reutilizar horizontal, sin nuevo componente KPI. Atención: rama horizontal no renderiza badgeNode; no migrar tarjetas con badge sin decidir soporte. ReportKpiCard.test.tsx: ambas orientaciones, valor largo, trend, badge e interacción/aria-pressed. |
| Grid compacto B FALTA / C / D | `.pg-kpis` es 4/2/1 a 900/480; `.prd-kpi-grid` es 1fr 1fr 2fr, 2/1 a 900/580. Proveedores sobrescribe a 3 iguales, 2/1 a 1024/580. | Resúmenes de Clientes, Ítems, bodegas, motivos y reportes que comparten estas clases. | Alto cambiar rejillas base; medio añadir modificador. | Proponer modificador aditivo de grid de 3 columnas iguales en estilos DS; no cambiar base ni usar ZHGrid de campos como grid universal. Probar 0/1/3/4 tarjetas, textos y zoom; métricas de negocio se conservan. |
| Acciones contextuales A KEEP / C / D | ZHLineAction en modo menú admite portal opt-in, cierre y teclado; ZHLineActionItem compone acciones. Proveedores conserva Ver fuera y Más acciones dentro. | Directos: Proveedores y PurchasesPage/ProductLineActionMenu. Indirectos: ZHRowDeleteAction, ZHLineActionRail; Ventas, ajustes, transferencias y desglose de Compras. | Bajo al reutilizar; alto activar portal/cierre globalmente (modales internos). | Mantener portal opt-in y control de open por consumidor; no copiar permisos al DS. Probar ZHLineAction.test.tsx y MasterDataSupplierListActions.test.tsx, foco, Enter/Espacio/flechas/Home/End/Tab/Escape, disabled/loading, clic fuera, scroll, resize y menú junto al borde; regresión del modal interno de Compras. |
| Apariencia del disparador B FALTA / D | Proveedores oculta chevron con CSS local e identifica proveedor dentro del panel. | Menús de líneas de Compras tienen otro significado; botones simples y eliminar no deben cambiar. | Medio. | Si se necesita segundo consumidor, opción visual explícita en ZHLineAction existente; identidad/contenido y permisos del menú siguen en owner. No globalizar el ocultamiento del chevron. Probar nombre accesible, tooltip y retorno de foco. |
| Pestañas responsive A KEEP / B FALTA / C / D | ZHTabBar tiene fill, disabled e inert; ConfigTabsLayout es Lista/Editor. Proveedores usa botones locales y hasta 480 px un grid de 3, min-height --space-12 y etiqueta Nuevo con nombre accesible completo. | Tabs de Ítems/formulario, Ventas, Compras, reportes contables, bancos, configuración, pricing y catálogos; también barras manuales con prd-tabs. | Alto forzar 3 columnas o abreviaciones a N tabs. | Proponer estrategia opt-in para pocas tabs y etiqueta breve por consumidor; para muchas tabs evaluar scroll contenido o wrap, no ocultar destinos. Mantener etiqueta accesible íntegra y disabled/inert. ZHTabBar.test.tsx, navegación real, permisos y edición con borrador. |
| Tarjetas y vacío compacto A KEEP / B FALTA / C / D | ZHCard tiene className/bodyClassName; zh-inline-empty cubre texto sin datos de sección. EmptyState/LoadingState comparten padding amplio; ZHDataTable agrega pg-pad-40. Proveedores usa párrafo de actividad local, no EmptyState. | ZHCard/TableCard y EmptyState en todos los módulos; NoAccessPage y ZHDataTable reciben efectos indirectos. | Bajo reutilización inline; alto reducir padding global o quitar bordes a todos los vacíos. | Reutilizar zh-inline-empty donde encaje. Si se exige vacío enmarcado compacto, variante explícita en EmptyState y, solo si se requiere, canal opt-in en ZHDataTable. Separar loading, error, sin registros y sin resultados. Pruebas PageShell/ZHDataTable, actividad vacía/poblada, padding acumulado y NoAccessPage sin cambio. |
| Densidad y responsive A KEEP / C | --density-control-height (36), md (32), sm (26), xs (24), --density-card-padding, --density-table-cell-padding-* y --space-* ya existen. No son autorización para bajar áreas táctiles. | Inputs, botones, tablas, cards/KPI y shell en todo ERP, además de modales. | Muy alto retocar tokens raíz para reproducir una pantalla. | Reutilizar tokens sin cambiar valores; extender opciones del componente si hace falta. No introducir escala paralela ni variables CSS de breakpoint inefectivas en media queries. Verificar foco visible, tamaño táctil, zoom 200%, textos largos y overflow. |

## Estándares propuestos para aprobación

1. **Encabezados:** un título de pantalla estable entre pestañas; descripción breve; categoría opcional según contexto. Alineación horizontal solo con espacio suficiente, apilado en estrecho; acciones derechas conservadas. Menú y sus categorías son ajenos a la decisión de mostrar kicker.
2. **Identidad:** primary y secondary son presentación, no campos de negocio del DS. No repetir texto secundario vacío/idéntico según normalización definida por owner. Wrap sin perder identificación ni estado al reducir columnas. La razón social principal es regla de Proveedores, no de todos los módulos.
3. **Indicadores:** reutilizar horizontal; grid según cantidad real, sin alturas fijas ni nuevas tarjetas. No cambiar cálculos, unidades, alcance de página o click por adoptar compactación. En Proveedores el resumen sigue siendo solo consulta; eliminar accesos rápidos en otros módulos requiere decisión particular.
4. **Acciones:** acción frecuente visible y secundarias agrupadas si el flujo lo justifica; nombre accesible contextual, teclado, foco, viewport y permisos existentes. Portal y cierre siguen siendo opciones explícitas; compactación nunca elimina confirmaciones.
5. **Pestañas:** distribución según cantidad y longitud de etiquetas; abreviación visual localizada solo con nombre accesible completo. Tres columnas móviles son una variante para tres destinos, no un default de N tabs. Revisar teclado y relación panel/tab antes de certificar el patrón compartido.
6. **Vacíos:** distinguir ausencia de registros, resultado filtrado, actividad de sesión, carga, error y acceso denegado. Vacío de sección compacto con un solo nivel de padding; vacío de página conserva su jerarquía. No transformar actividad de sesión en historial persistido ni añadir llamadas.

## Implementación propuesta por bloques (todos pendientes)

| Bloque | Cambio concreto propuesto | Entrada / salida y gate |
|---|---|---|
| 0 — decisión documental | Aprobar esta matriz, patrones y alcance de variantes; corregir referencias desactualizadas de tabs en la SSOT, manteniendo la baseline y sus excepciones. | Entrada: decisión del propietario sobre cada variante B. Salida: contratos acordados, sin consumidores migrados. Gate documental. |
| 1 — capacidades reutilizables A | Preparar ejemplos y cobertura focal de horizontal KPI, tabla compacta, menú portal y vacío inline usando componentes actuales. | No hace falta implementar nuevos componentes para estas capacidades. No cambiar defaults. Gate focal y revisión del propietario antes de migrar pantallas. |
| 2 — variantes B del DS | Encabezado alineado opt-in; grid de tres KPI iguales; tabs estrechas configurables; vacío compacto si inline no alcanza. Disparador sin chevron e identidad visual solo con necesidad confirmada de segundo consumidor. | Cambiar únicamente componentes oficiales y su CSS con defaults actuales; una variante por subfase revisable. Propagar props por plantillas cuando corresponda. Pruebas focales de opción nueva y ausencia de opción; capturas y gate del propietario. |
| 3 — piloto de integración | Aplicar variantes aprobadas primero en Proveedores y retirar solo overrides locales sustituidos tras equivalencia visual comprobada. | Preservar 01/02/03 como referencia, datos y comportamiento. Comparación antes/después; gate visual explícito. No modificar Clientes por compartir componentes. |
| 4 — adopción D | Autorizar y adaptar por separado Clientes, catálogos, reportes y documentos según prioridades de columna, métricas, navegación y acciones. | Cada bloque con lista concreta de pantallas aprobadas; módulos CLOSED no se reabren por esta propuesta. Smoke focal + gate por bloque. |
| C — defaults globales | Cambios en tokens raíz, estilos base de heading/tablas/cards/vacíos/tabs o sustitución universal de breakpoint. | No recomendados para esta incorporación. Requerirían auditoría adicional específica y aprobación separada; no son dependencia de bloques 1–4. |

Seguro para preparar como estándar: composición de capacidades A y variantes aditivas con default preservado. No significa riesgo cero ni autoriza aplicarlas ahora. Necesitan variantes: composición alineada del encabezado, grid igualitario, pestañas estrechas y vacío enmarcado compacto. Necesitan adaptación de dominio: identidad, prioridades móviles, acciones disponibles y resumen solo de consulta.

## Validación prevista para fases de implementación

- Base visual mínima: 320, 360, 390, 480, 768, 1024 y 1440 px; comprobar además ambos lados de cortes relevantes (480/481, 580/581, 640/641, 768/769, 1024/1025). Sin overflow de página; scroll interno permitido en tablas que lo requieren.
- Casos: sin datos, cargando/error, nombres/etiquetas/valores largos, locales es/en, teclado y foco, zoom 200%, permisos reducidos, estado disabled/loading y navegación sin perder borrador. Fixtures sintéticos sin operaciones externas.
- Pruebas existentes a extender solo cuando haya código: PageShell.test.tsx, ReportKpiCard.test.tsx, zh/ZHDataTable.test.tsx, zh/ZHLineAction.test.tsx, zh/ZHTabBar.test.tsx y pruebas de Proveedores/Clientes afectadas. Las capturas previas son evidencia del piloto, no de variantes todavía inexistentes.
- Gates técnicos futuros según enforcement: lint, TypeScript, build y architecture:check cuando corresponda, sin suites generales ni SRI. Selección de regresiones por diff y consumidores, no una ejecución global. El propietario ejecuta gate y decide aprobación visual; staging/commit le corresponden.

## Inventario estático de consumidores

Generado con `rg -l` sobre TSX productivo, excluyendo tests, por símbolo/clase. Es inventario de referencias (puede incluir definición/import), no prueba de montaje de todas las rutas. PageShell incluye alcance indirecto de ErpPageTemplate y ZHLineAction el de wrappers. Los cambios CSS globales pueden alcanzar nodos sin import del símbolo; por eso se incluyen clases compartidas. Rutas desde `frontend/src/`.

### Referencias: ZHScreenHeading|PageShell|ErpPageTemplate

- `templates/ErpPageTemplate.tsx`
- `main.tsx`
- `modules/access/pages/PermissionsAssignmentPage.tsx`
- `routes/lazyPage.tsx`
- `modules/access/users/pages/UserConfigPage.tsx`
- `modules/access/pages/ProfilesPage.tsx`
- `modules/access/users/pages/UsersPage.tsx`
- `components/items/ItemEditorModal/ItemEditorForm.tsx`
- `modules/admin/activity/pages/ActivityPage.tsx`
- `modules/admin/access-sessions/pages/AdminUserSessionsPage.tsx`
- `modules/emissionPoints/pages/EmissionPointsPage.tsx`
- `modules/access/users/sections/UserSecuritySection.tsx`
- `components/items/ItemEditorModal/ItemEditorModal.tsx`
- `modules/company-management/pages/CurrentCompanyCard.tsx`
- `modules/admin-core/pages/AdminCoreCompanyCreatePage.tsx`
- `modules/company-management/pages/CompanyManagementListPage.tsx`
- `modules/emissionPoints/components/EmissionPointsManagementSection.tsx`
- `modules/emissionPoints/components/EmissionPointsListSection.tsx`
- `modules/company-management/pages/CompanyManagementFormPage.tsx`
- `modules/admin-core/pages/AdminCoreDashboardPage.tsx`
- `modules/admin-core/pages/AdminCoreSystemProviderSettingsPage.tsx`
- `modules/emissionPoints/components/EmissionPointsFormPanel.tsx`
- `modules/cashRegisters/pages/CashRegistersPage.tsx`
- `modules/items/pages/ItemTypesPage.tsx`
- `components/zh/ZHDataTable.tsx`
- `modules/items/pages/ItemsPage.tsx`
- `modules/electronicDocuments/monitor/pages/ElectronicDocumentsMonitorPage.tsx`
- `modules/cashRegisters/components/CashRegistersManagementSection.tsx`
- `modules/items/detail/components/VariantsSection.tsx`
- `components/zh/notices/ZHNoticeBadge.tsx`
- `modules/cashRegisters/components/CashRegistersListSection.tsx`
- `modules/items/detail/components/CollectionSection.tsx`
- `components/zh/ZHLayout.tsx`
- `modules/supplier-payments/pages/SupplierPaymentsPage.tsx`
- `modules/supplier-payments/pages/SupplierPaymentFormPage.tsx`
- `modules/accounting/pages/reports/GeneralLedgerReportTab.tsx`
- `modules/supplier-payments/pages/SupplierPaymentDetailPage.tsx`
- `modules/items/components/ItemListTable.tsx`
- `modules/accounting/pages/PostingRulesPage.tsx`
- `modules/branches/pages/BranchesPage.tsx`
- `modules/accounting/pages/ManualJournalEditorPage.tsx`
- `modules/branches/pages/BranchDetailPage.tsx`
- `modules/electronicDocuments/monitor/components/ElectronicDocumentsTable.tsx`
- `modules/accounting/pages/JournalEntryDetailPage.tsx`
- `modules/caja/pages/CashMovementReasonsAdminPage.tsx`
- `modules/accounting/pages/JournalEntriesPage.tsx`
- `modules/caja/pages/CashFundingRequestsPage.tsx`
- `modules/accounting/pages/FiscalYearClosingCard.tsx`
- `modules/supplier-payments/components/SupplierPaymentStatusBadge.tsx`
- `modules/accounting/pages/ChartOfAccountsPage.tsx`
- `modules/caja/pages/CashFundingRequestDetailPage.tsx`
- `modules/caja/components/collectionDetailColumns.tsx`
- `modules/accounting/pages/AccountingReportsPage.tsx`
- `modules/electronicDocuments/monitor/components/ElectronicDocumentDetailPanel.tsx`
- `modules/branches/components/BranchGeneralSection.tsx`
- `modules/accounting/pages/AccountingPostingsPage.tsx`
- `modules/documentSequences/pages/DocumentSequencesPage.tsx`
- `modules/accounting/pages/AccountingPeriodsPage.tsx`
- `modules/caja/components/CashMovementReasonListTab.tsx`
- `modules/caja/pages/CajaPage.tsx`
- `components/zh/inputs/ZhWarehouseSelector.tsx`
- `modules/branches/components/BranchesManagementSection.tsx`
- `modules/accounting/pages/AccountingOperationalModePage.tsx`
- `modules/branches/components/BranchesListSection.tsx`
- `modules/branches/components/BranchConfigSection.tsx`
- `components/RuntimeModeBadge.tsx`
- `modules/supplier-payments/components/SupplierPayablesPortfolio.tsx`
- `components/RouteAccessGuard.tsx`
- `components/ReportPageTemplate.tsx`
- `components/PageShell.tsx`
- `modules/inventory/warehouses/pages/WarehousesPage.tsx`
- `modules/settings/geography/pages/GeographyPage.tsx`
- `modules/items/components/ItemForm/PrincipalCodesSummary.tsx`
- `components/zh/ZHSriServiceHealth.tsx`
- `modules/settings/banks/pages/BanksPage.tsx`
- `modules/items/components/ItemForm/ItemFormTabs.tsx`
- `modules/inventory/warehouses/components/WarehouseListTab.tsx`
- `modules/inventory/warehouses/components/WarehouseFormTab.tsx`
- `modules/dashboard/pages/DashboardPage.tsx`
- `components/zh/electronicDocuments/ElectronicDocumentXmlActions.tsx`
- `modules/configuracion/documentFlows/pages/DocumentFlowPoliciesPage.tsx`
- `modules/configuracion/comunicaciones/sections/CommunicationsEmailSettingsSection.tsx`
- `modules/security/pages/SecuritySettingsPage.tsx`
- `components/zh/electronicDocuments/ElectronicDocumentStatusBadge.tsx`
- `modules/configuracion/operaciones/pages/OperationalPreferencesPage.tsx`
- `modules/inventory/adjustmentReasons/pages/InventoryAdjustmentReasonsPage.tsx`
- `components/zh/electronicDocuments/ElectronicDocumentSriMessages.tsx`
- `modules/inventory/transfers/pages/StockTransferPage.tsx`
- `components/zh/electronicDocuments/ElectronicDocumentDiagnosticPanel.tsx`
- `modules/configuracion/comunicaciones/pages/CommunicationsEmailSettingsPage.tsx`
- `modules/configuracion/operaciones/sections/SalesPosPreferencesSection.tsx`
- `modules/items/catalog/wizard/TreeEditor.tsx`
- `modules/configuracion/operaciones/sections/PurchasesPreferencesSection.tsx`
- `modules/configuracion/operaciones/sections/PrintingPreferencesSection.tsx`
- `modules/inventory/adjustmentReasons/components/AdjustmentReasonListTab.tsx`
- `modules/configuracion/facturacionElectronica/sections/SriConfigurationSection.tsx`
- `modules/finance/pages/SupplierCreditListPage.tsx`
- `modules/configuracion/operaciones/sections/PayablesPreferencesSection.tsx`
- `modules/finance/pages/SupplierCreditDetailPage.tsx`
- `modules/configuracion/facturacionElectronica/pages/ElectronicInvoicingPage.tsx`
- `modules/configuracion/empresa/pages/CompanySettingsHubPage.tsx`
- `modules/configuracion/operaciones/sections/InventoryPreferencesSection.tsx`
- `modules/configuracion/operaciones/sections/CashPreferencesSection.tsx`
- `modules/configuracion/operaciones/sections/EmptyPreferencesSection.tsx`
- `modules/finance/pages/CreditTermsPage.tsx`
- `modules/configuracion/operaciones/sections/ElectronicDocumentsPreferencesSection.tsx`
- `modules/configuracion/empresa/sections/BrandingSettingsSection.tsx`
- `modules/finance/pages/AccountsReceivablePage.tsx`
- `modules/configuracion/empresa/sections/CompanyProfileSettingsSection.tsx`
- `modules/finance/pages/BankAccountsPage.tsx`
- `modules/items/catalog/pages/BrandsPage.tsx`
- `modules/inventory/pendingCosts/pages/PendingSaleCostsPage.tsx`
- `modules/sales/pages/SalesReturnListPage.tsx`
- `modules/sales/pages/SalesReturnFormPage.tsx`
- `modules/establishments/pages/EstablishmentsPage.tsx`
- `modules/items/catalog/pages/AttributeGroupsPage.tsx`
- `modules/sales/pages/SalesPage.tsx`
- `modules/initialLoad/components/OpeningReconciliationCard.tsx`
- `modules/items/catalog/pages/AttributeDefinitionsPage.tsx`
- `modules/sales/pages/PaymentMethodsPage.tsx`
- `modules/initialLoad/components/OpeningJournalSection.tsx`
- `modules/inventory/adjustments/components/AdjustmentLineCard.tsx`
- `modules/inventory/kardex/pages/KardexPage.tsx`
- `modules/expenses/pages/ExpenseDocumentsPage.tsx`
- `modules/expenses/pages/ExpenseDocumentFormPage.tsx`
- `modules/inventory/kardex/components/KardexMovementDetailModal.tsx`
- `modules/initialLoad/pages/InitialLoadHubPage.tsx`
- `modules/expenses/pages/ExpenseCategoriesPage.tsx`
- `modules/establishments/components/EstablishmentsListSection.tsx`
- `modules/items/catalog/components/CatalogListSection.tsx`
- `modules/documentSequences/components/DocumentSequencesListSection.tsx`
- `modules/configuracion/empresa/sections/ReadinessRequirementRow.tsx`
- `modules/initialLoad/pages/ImportWizardPage.tsx`
- `modules/configuracion/empresa/sections/FiscalSettingsSection.tsx`
- `modules/configuracion/empresa/sections/PrecisionPolicySettingsSection.tsx`
- `modules/configuracion/empresa/sections/OperationSettingsSection.tsx`
- `modules/configuracion/empresa/sections/DocumentsSettingsSection.tsx`
- `modules/configuracion/empresa/sections/OperationalReadinessSection.tsx`
- `modules/configuracion/empresa/sections/CompanySalesSettingsSection.tsx`
- `modules/logistica/transportistas/pages/CarriersPage.tsx`
- `modules/expenses/components/ExpenseCategoryFormPanel.tsx`
- `modules/payables/pages/PayablesPage.tsx`
- `modules/pricing/pages/PriceListsPage.tsx`
- `modules/masterData/components/MasterDataPartnerListTab.tsx`
- `modules/payables/pages/PayableDetailPage.tsx`
- `modules/masterData/components/SupplierSearchSelect.tsx`
- `modules/pricing/pages/PriceListExceptionsTab.tsx`
- `modules/payables/components/PayableStatusBadge.tsx`
- `modules/masterData/pages/MasterDataCustomersPage.tsx`
- `modules/expenses/components/ExpenseCategoryTree.tsx`
- `modules/payables/components/PayableOriginBadge.tsx`
- `modules/masterData/pages/MasterDataSuppliersPage.tsx`
- `modules/inventory/adjustments/pages/StockAdjustmentFormPage.tsx`
- `modules/masterData/pages/MasterDataBusinessPartnerDetailPage.tsx`
- `modules/expenses/components/ExpenseDocumentStatusBadge.tsx`
- `modules/documentSequences/components/DocumentSequencesManagementSection.tsx`
- `modules/purchases/pages/PurchaseCreditNoteFormPage.tsx`
- `modules/purchases/pages/PurchaseCreditNoteListPage.tsx`
- `modules/purchases/pages/PurchaseCreditNoteDetailPage.tsx`
- `modules/masterData/pages/PaymentTermsPage.tsx`
- `modules/purchases/pages/PurchaseReturnDetailPage.tsx`
- `modules/purchases/pages/PurchaseReceptionPage.tsx`
- `modules/sales/components/SalesElectronicDiagnosticDrawer.tsx`
- `modules/purchases/pages/PurchaseReturnListPage.tsx`
- `modules/inventory/adjustments/pages/StockAdjustmentsPage.tsx`
- `modules/purchases/pages/PurchasesPage.tsx`
- `modules/sales/components/SalesItemSearchResultsGrid.tsx`
- `modules/sales/components/SalesEmissionConfigSection.tsx`
- `modules/sales/components/SalesInvoiceLineGridRow.tsx`
- `modules/sales/components/SalesOperationalHeader.tsx`
- `modules/sales/components/SalesReturnCreditNoteSection.tsx`
- `modules/sales/components/SalesStatusBadges.tsx`
- `modules/purchases/components/ItemMatchStatusBadge.tsx`
- `modules/purchases/components/PendingProductRow.tsx`
- `modules/documentSequences/components/PhysicalDocumentAuthorizationsSection.tsx`
- `modules/retentions/components/RetentionAnnulmentPanel.tsx`
- `modules/retentions/components/RetentionElectronicStatusBadge.tsx`
- `modules/purchases/components/PurchaseFormFeedback.tsx`
- `modules/purchases/components/PurchaseReceptionActionsCell.tsx`
- `modules/purchases/components/PurchaseReceptionDocumentCell.tsx`
- `modules/purchases/components/PurchaseReceptionProcessCell.tsx`
- `modules/purchases/components/PurchaseRetentionDraft.tsx`

### Referencias: ZHDataTable

- `modules/accounting/pages/AccountingOperationalModePage.tsx`
- `modules/admin-core/pages/AdminCoreDashboardPage.tsx`
- `modules/cashRegisters/components/CashRegistersListSection.tsx`
- `modules/supplier-payments/pages/SupplierPaymentsPage.tsx`
- `modules/admin/activity/pages/ActivityPage.tsx`
- `modules/supplier-payments/pages/SupplierPaymentDetailPage.tsx`
- `modules/accounting/pages/AccountingPostingsPage.tsx`
- `modules/accounting/pages/AccountingPeriodsPage.tsx`
- `modules/admin/access-sessions/pages/AdminUserSessionsPage.tsx`
- `modules/accounting/pages/reports/TrialBalanceReportTab.tsx`
- `modules/accounting/pages/reports/IncomeStatementReportTab.tsx`
- `modules/caja/pages/CashFundingRequestsPage.tsx`
- `modules/accounting/pages/reports/GeneralLedgerReportTab.tsx`
- `modules/caja/pages/CashFundingRequestDetailPage.tsx`
- `modules/accounting/pages/reports/GeneralJournalReportTab.tsx`
- `modules/caja/pages/CajaPage.tsx`
- `modules/accounting/pages/reports/BalanceSheetReportTab.tsx`
- `modules/access/users/pages/UsersPage.tsx`
- `modules/caja/components/collectionDetailColumns.tsx`
- `modules/accounting/pages/PostingRulesPage.tsx`
- `modules/accounting/pages/PostingRuleEditorModal.tsx`
- `modules/caja/components/CashMovementReasonListTab.tsx`
- `modules/accounting/pages/ManualJournalEditorPage.tsx`
- `modules/supplier-payments/components/SupplierPayablesPortfolio.tsx`
- `modules/settings/geography/pages/GeographyPage.tsx`
- `modules/items/pages/ItemTypesPage.tsx`
- `modules/accounting/pages/AccountingReportsPage.tsx`
- `modules/branches/components/BranchesListSection.tsx`
- `modules/settings/banks/pages/BanksPage.tsx`
- `modules/accounting/pages/ChartOfAccountsPage.tsx`
- `modules/accounting/pages/JournalEntriesPage.tsx`
- `modules/accounting/pages/JournalEntryDetailPage.tsx`
- `modules/access/pages/ProfilesPage.tsx`
- `modules/items/detail/components/VariantsSection.tsx`
- `modules/items/detail/components/CollectionSection.tsx`
- `modules/items/components/ItemListTable.tsx`
- `modules/establishments/components/EstablishmentsListSection.tsx`
- `modules/sales/pages/SalesReturnListPage.tsx`
- `modules/sales/pages/SalesPage.tsx`
- `modules/sales/pages/PaymentMethodsPage.tsx`
- `modules/items/components/ItemForm/PrincipalCodesSummary.tsx`
- `modules/electronicDocuments/monitor/components/ElectronicDocumentsTable.tsx`
- `modules/inventory/warehouses/components/WarehouseListTab.tsx`
- `modules/emissionPoints/components/EmissionPointsListSection.tsx`
- `modules/accounting/pages/FiscalYearClosingCard.tsx`
- `components/zh/ZHDataTable.tsx`
- `modules/items/catalog/components/CatalogListSection.tsx`
- `modules/accounting/pages/ManualJournalActions.tsx`
- `modules/sales/components/SalesReturnSummary.tsx`
- `modules/inventory/pendingCosts/pages/PendingSaleCostsPage.tsx`
- `modules/inventory/adjustments/pages/StockAdjustmentsPage.tsx`
- `modules/inventory/kardex/pages/KardexPage.tsx`
- `modules/configuracion/empresa/sections/OperationalReadinessSection.tsx`
- `modules/documentSequences/components/PhysicalDocumentAuthorizationsSection.tsx`
- `modules/documentSequences/components/DocumentSequencesManagementSection.tsx`
- `modules/documentSequences/components/DocumentSequencesListSection.tsx`
- `modules/configuracion/documentFlows/pages/DocumentFlowPoliciesPage.tsx`
- `modules/inventory/adjustmentReasons/components/AdjustmentReasonListTab.tsx`
- `modules/pricing/pages/PriceListsPage.tsx`
- `modules/payables/pages/PayablesPage.tsx`
- `modules/logistica/transportistas/pages/CarriersPage.tsx`
- `modules/pricing/pages/PriceListExceptionsTab.tsx`
- `modules/pricing/pages/PriceListCustomersTab.tsx`
- `modules/finance/pages/SupplierCreditListPage.tsx`
- `modules/finance/pages/SupplierCreditDetailPage.tsx`
- `modules/reportes/pages/PurchasesReportPage.tsx`
- `modules/reportes/pages/SalesReportPage.tsx`
- `modules/finance/pages/CreditTermsPage.tsx`
- `modules/reportes/pages/StockReportPage.tsx`
- `modules/finance/pages/BankAccountsPage.tsx`
- `modules/finance/components/ReverseCollectionModal.tsx`
- `modules/masterData/pages/PaymentTermsPage.tsx`
- `modules/finance/pages/AccountsReceivablePage.tsx`
- `modules/initialLoad/pages/ImportWizardPage.tsx`
- `modules/masterData/components/MasterDataPartnerListTab.tsx`
- `modules/payables/components/PayableInstallmentsTable.tsx`
- `modules/purchases/pages/PurchaseCreditNoteListPage.tsx`
- `modules/purchases/pages/PurchaseCreditNoteDetailPage.tsx`
- `modules/purchases/pages/PurchaseReceptionPage.tsx`
- `modules/initialLoad/components/OpeningReconciliationCard.tsx`
- `modules/purchases/pages/PurchaseReturnDetailPage.tsx`
- `modules/purchases/pages/PurchaseReturnListPage.tsx`
- `modules/expenses/pages/ExpenseDocumentsPage.tsx`
- `modules/purchases/pages/PurchasesPage.tsx`
- `modules/purchases/components/PurchaseInvoiceLinesDetailTable.tsx`
- `modules/purchases/components/PurchaseLineBreakdownModal.tsx`
- `modules/purchases/components/PurchaseReceptionXmlViewModal.tsx`
- `modules/purchases/components/PurchaseReturnableLinesEditor.tsx`

### Referencias: ZHLineAction|ZHRowDeleteAction|ZHLineActionRail

- `components/zh/ZHLineActionItem.tsx`
- `components/zh/ZHLineActionRail.tsx`
- `components/zh/ZHLineAction.tsx`
- `modules/purchases/pages/PurchasesPage.tsx`
- `components/zh/ZHRowDeleteAction.tsx`
- `modules/sales/components/SalesInvoiceLineGridRow.tsx`
- `modules/inventory/transfers/pages/StockTransferPage.tsx`
- `modules/inventory/adjustments/components/AdjustmentLineCard.tsx`
- `modules/masterData/components/MasterDataSupplierListActions.tsx`
- `modules/purchases/components/PurchaseLineBreakdownModal.tsx`

### Referencias: ReportKpiCard

- `modules/cashRegisters/components/CashRegistersListSection.tsx`
- `modules/accounting/pages/ChartOfAccountsPage.tsx`
- `modules/caja/pages/CashMovementReasonsAdminPage.tsx`
- `modules/items/pages/ItemsPage.tsx`
- `modules/logistica/transportistas/pages/CarriersPage.tsx`
- `modules/emissionPoints/components/EmissionPointsListSection.tsx`
- `modules/supplier-payments/components/SupplierPayablesPortfolio.tsx`
- `modules/masterData/components/MasterDataPartnerResumenTab.tsx`
- `modules/settings/geography/pages/GeographyPage.tsx`
- `components/ReportPageTemplate.tsx`
- `modules/electronicDocuments/monitor/components/ElectronicDocumentsDashboardCards.tsx`
- `modules/reportes/pages/StockReportPage.tsx`
- `modules/reportes/pages/SalesReportPage.tsx`
- `modules/reportes/pages/PurchasesReportPage.tsx`
- `modules/branches/components/BranchesListSection.tsx`
- `modules/inventory/warehouses/pages/WarehousesPage.tsx`
- `modules/dashboard/pages/DashboardPage.tsx`
- `modules/purchases/pages/PurchaseReceptionPage.tsx`
- `modules/items/catalog/components/CatalogListSection.tsx`
- `modules/inventory/kardex/pages/KardexPage.tsx`
- `modules/configuracion/empresa/sections/OperationalReadinessSection.tsx`
- `modules/inventory/adjustmentReasons/pages/InventoryAdjustmentReasonsPage.tsx`
- `modules/establishments/components/EstablishmentsListSection.tsx`

### Referencias: ZHTabBar|ConfigTabsLayout|prd-tabs

- `modules/documentSequences/components/DocumentSequencesManagementSection.tsx`
- `modules/establishments/components/EstablishmentsManagementSection.tsx`
- `modules/establishments/components/EstablishmentsFormPanel.tsx`
- `modules/access/users/pages/UserConfigPage.tsx`
- `modules/caja/pages/CashMovementReasonsAdminPage.tsx`
- `modules/caja/pages/CashFundingRequestsPage.tsx`
- `modules/configuracion/operaciones/pages/OperationalPreferencesPage.tsx`
- `modules/purchases/pages/PurchasesPage.tsx`
- `modules/branches/pages/BranchDetailPage.tsx`
- `modules/emissionPoints/components/EmissionPointsManagementSection.tsx`
- `modules/emissionPoints/components/EmissionPointsFormPanel.tsx`
- `modules/branches/components/BranchFormPanel.tsx`
- `modules/branches/components/BranchesManagementSection.tsx`
- `modules/items/pages/ItemTypesPage.tsx`
- `modules/items/pages/ItemsPage.tsx`
- `modules/sales/pages/PaymentMethodsPage.tsx`
- `modules/configuracion/empresa/pages/CompanySettingsHubPage.tsx`
- `modules/configuracion/documentFlows/pages/DocumentFlowPoliciesPage.tsx`
- `modules/finance/pages/CreditTermsPage.tsx`
- `modules/finance/pages/BankAccountsPage.tsx`
- `modules/items/components/ItemForm/ItemFormTabs.tsx`
- `modules/inventory/warehouses/pages/WarehousesPage.tsx`
- `modules/cashRegisters/components/CashRegistersManagementSection.tsx`
- `modules/pricing/pages/PriceListsPage.tsx`
- `modules/inventory/adjustmentReasons/pages/InventoryAdjustmentReasonsPage.tsx`
- `modules/masterData/pages/PaymentTermsPage.tsx`
- `modules/masterData/pages/MasterDataSuppliersPage.tsx`
- `modules/sales/components/SalesOperationalHeader.tsx`
- `modules/masterData/pages/MasterDataCustomersPage.tsx`
- `components/shared/ConfigTabsLayout.tsx`
- `modules/accounting/pages/AccountingReportsPage.tsx`
- `components/zh/ZHTabBar.tsx`

### Referencias: ZHCard|TableCard|ZHGrid|prd-kpi-grid|pg-kpis

- `modules/access/pages/ProfilesPage.tsx`
- `modules/admin/access-sessions/pages/AdminUserSessionsPage.tsx`
- `modules/accounting/pages/AccountingOperationalModePage.tsx`
- `modules/branches/components/BranchGeneralSection.tsx`
- `modules/branches/components/BranchFormPanel.tsx`
- `modules/admin-core/pages/AdminCoreSystemProviderSettingsPage.tsx`
- `modules/branches/components/BranchesListSection.tsx`
- `modules/admin-core/pages/AdminCoreDashboardPage.tsx`
- `modules/admin-core/pages/AdminCoreCompanyCreatePage.tsx`
- `modules/accounting/pages/AccountingPeriodsPage.tsx`
- `modules/cashRegisters/components/CashRegistersListSection.tsx`
- `modules/cashRegisters/components/CashRegistersFormPanel.tsx`
- `modules/supplier-payments/pages/SupplierPaymentsPage.tsx`
- `modules/supplier-payments/pages/SupplierPaymentFormPage.tsx`
- `modules/supplier-payments/pages/SupplierPaymentDetailPage.tsx`
- `modules/expenses/pages/ExpenseDocumentsPage.tsx`
- `modules/expenses/pages/ExpenseDocumentFormPage.tsx`
- `modules/expenses/pages/ExpenseCategoriesPage.tsx`
- `modules/documentSequences/components/PhysicalDocumentAuthorizationsSection.tsx`
- `modules/documentSequences/components/DocumentSequencesManagementSection.tsx`
- `modules/caja/pages/CashMovementReasonsAdminPage.tsx`
- `modules/caja/pages/CashFundingRequestsPage.tsx`
- `modules/caja/pages/CashFundingRequestDetailPage.tsx`
- `modules/documentSequences/components/DocumentSequencesFormPanel.tsx`
- `modules/configuracion/operaciones/sections/PrintingPreferencesSection.tsx`
- `modules/dashboard/pages/DashboardPage.tsx`
- `modules/supplier-payments/components/SupplierPayablesPortfolio.tsx`
- `modules/caja/components/CashMovementReasonFormTab.tsx`
- `modules/expenses/components/ExpenseCategoryFormPanel.tsx`
- `modules/expenses/components/ExpenseRetentionSection.tsx`
- `modules/company-management/pages/CurrentCompanyCard.tsx`
- `components/PageShell.tsx`
- `modules/settings/geography/pages/GeographyPage.tsx`
- `modules/expenses/components/ExpenseDocumentHeader.tsx`
- `modules/company-management/pages/CompanyManagementListPage.tsx`
- `components/ReportPageTemplate.tsx`
- `modules/accounting/pages/FiscalYearClosingCard.tsx`
- `modules/company-management/pages/CompanyManagementFormPage.tsx`
- `modules/accounting/pages/ChartOfAccountsPage.tsx`
- `modules/settings/banks/pages/BanksPage.tsx`
- `modules/accounting/pages/AccountingReportsPage.tsx`
- `modules/configuracion/facturacionElectronica/sections/SriConfigPageDataTab.tsx`
- `modules/accounting/pages/AccountingPostingsPage.tsx`
- `modules/accounting/pages/ManualJournalActions.tsx`
- `modules/accounting/pages/JournalEntryDetailPage.tsx`
- `modules/security/pages/SecuritySettingsPage.tsx`
- `modules/establishments/components/EstablishmentsListSection.tsx`
- `modules/accounting/pages/JournalEntriesPage.tsx`
- `components/items/ItemEditorModal/ItemEditorPurchaseInfo.tsx`
- `modules/establishments/components/EstablishmentsFormPanel.tsx`
- `modules/accounting/pages/PostingRulesPage.tsx`
- `components/items/ItemEditorModal/ItemEditorForm.tsx`
- `modules/accounting/pages/ManualJournalEditorPage.tsx`
- `modules/accounting/pages/reports/GeneralJournalReportTab.tsx`
- `modules/accounting/pages/reports/TrialBalanceReportTab.tsx`
- `modules/accounting/pages/reports/BalanceSheetReportTab.tsx`
- `modules/accounting/pages/reports/GeneralLedgerReportTab.tsx`
- `modules/accounting/pages/reports/IncomeStatementReportTab.tsx`
- `modules/sales/pages/SalesReturnListPage.tsx`
- `modules/configuracion/empresa/sections/PrecisionPolicySettingsSection.tsx`
- `modules/sales/pages/SalesReturnFormPage.tsx`
- `modules/configuracion/empresa/sections/OperationSettingsSection.tsx`
- `modules/configuracion/empresa/sections/OperationalReadinessSection.tsx`
- `modules/emissionPoints/components/EmissionPointsListSection.tsx`
- `modules/logistica/transportistas/pages/CarriersPage.tsx`
- `modules/configuracion/empresa/sections/FiscalSettingsSection.tsx`
- `modules/emissionPoints/components/EmissionPointsFormPanel.tsx`
- `modules/configuracion/empresa/sections/DocumentsSettingsSection.tsx`
- `modules/configuracion/empresa/sections/CompanySalesSettingsSection.tsx`
- `modules/configuracion/empresa/sections/CompanyProfileSettingsSection.tsx`
- `components/zh/ZHCard.tsx`
- `modules/configuracion/empresa/sections/BrandingSettingsSection.tsx`
- `modules/reportes/pages/StockReportPage.tsx`
- `modules/items/pages/ItemTypesPage.tsx`
- `modules/reportes/pages/SalesReportPage.tsx`
- `modules/items/pages/ItemsPage.tsx`
- `modules/reportes/pages/PurchasesReportPage.tsx`
- `modules/configuracion/documentFlows/pages/DocumentFlowPoliciesPage.tsx`
- `modules/masterData/components/MasterDataPartnerWizard.tsx`
- `modules/items/detail/components/VariantsSection.tsx`
- `modules/electronicDocuments/monitor/components/RetransmitCorrectedElectronicDocumentModal.tsx`
- `modules/configuracion/comunicaciones/sections/CommunicationsEmailSettingsSection.tsx`
- `components/zh/ZHForm.tsx`
- `modules/electronicDocuments/monitor/components/ReconcileElectronicDocumentModal.tsx`
- `modules/electronicDocuments/monitor/components/ElectronicDocumentDetailPanel.tsx`
- `modules/electronicDocuments/monitor/components/ElectronicDocumentsDashboardCards.tsx`
- `modules/masterData/components/MasterDataPartnerResumenTab.tsx`
- `modules/sales/components/SalesReturnSummary.tsx`
- `modules/purchases/pages/PurchaseReturnListPage.tsx`
- `modules/sales/components/SalesReturnCreditNoteSection.tsx`
- `modules/purchases/pages/PurchaseReturnDetailPage.tsx`
- `modules/inventory/warehouses/pages/WarehousesPage.tsx`
- `modules/masterData/components/MasterDataBpFormFields.tsx`
- `modules/items/catalog/wizard/TreeEditor.tsx`
- `modules/purchases/pages/PurchaseCreditNoteListPage.tsx`
- `modules/inventory/warehouses/components/WarehouseFormTab.tsx`
- `modules/finance/pages/SupplierCreditListPage.tsx`
- `modules/purchases/pages/PurchaseCreditNoteFormPage.tsx`
- `modules/finance/pages/SupplierCreditDetailPage.tsx`
- `modules/items/catalog/pages/BrandsPage.tsx`
- `modules/items/components/ItemForm/TaxConfigTab.tsx`
- `modules/items/catalog/pages/AttributeGroupsPage.tsx`
- `modules/items/catalog/pages/AttributeDefinitionsPage.tsx`
- `modules/finance/pages/BankAccountsPage.tsx`
- `modules/items/components/ItemForm/SupplierCodesSection.tsx`
- `modules/purchases/pages/PurchaseCreditNoteDetailPage.tsx`
- `modules/inventory/transfers/pages/StockTransferPage.tsx`
- `modules/masterData/pages/MasterDataSuppliersPage.tsx`
- `modules/sales/components/QuickCustomerModal.tsx`
- `modules/items/catalog/components/CatalogListSection.tsx`
- `modules/items/components/ItemForm/SettingsTab.tsx`
- `modules/items/components/ItemForm/PricingTab.tsx`
- `modules/masterData/pages/MasterDataCustomersPage.tsx`
- `modules/items/components/ItemForm/GeneralTab.tsx`
- `modules/initialLoad/pages/InitialLoadHubPage.tsx`
- `modules/items/components/ItemForm/BarcodeListEditor.tsx`
- `modules/items/components/ItemForm/InventoryTab.tsx`
- `modules/initialLoad/pages/ImportWizardPage.tsx`
- `modules/inventory/pendingCosts/pages/PendingSaleCostsPage.tsx`
- `modules/masterData/pages/MasterDataBusinessPartnerDetailPage.tsx`
- `components/zh/electronicDocuments/ElectronicDocumentDiagnosticPanel.tsx`
- `modules/initialLoad/components/OpeningBalanceDateCard.tsx`
- `modules/initialLoad/components/OpeningReconciliationCard.tsx`
- `modules/payables/pages/PayableDetailPage.tsx`
- `modules/inventory/adjustmentReasons/pages/InventoryAdjustmentReasonsPage.tsx`
- `modules/inventory/adjustments/pages/StockAdjustmentFormPage.tsx`
- `modules/payables/pages/PayablesPage.tsx`
- `modules/inventory/adjustments/pages/StockAdjustmentsPage.tsx`
- `modules/inventory/adjustmentReasons/components/AdjustmentReasonFormTab.tsx`
- `modules/purchases/components/PurchaseRetentionDraft.tsx`

### Referencias: EmptyState|zh-inline-empty|prd-activity-empty

- `modules/admin/access-sessions/pages/AdminUserSessionsPage.tsx`
- `modules/admin/activity/pages/ActivityPage.tsx`
- `modules/cashRegisters/components/CashRegistersListSection.tsx`
- `modules/documentSequences/components/PhysicalDocumentAuthorizationsSection.tsx`
- `modules/documentSequences/components/DocumentSequencesListSection.tsx`
- `modules/supplier-payments/pages/SupplierPaymentsPage.tsx`
- `modules/supplier-payments/pages/SupplierPaymentDetailPage.tsx`
- `modules/security/pages/SecuritySettingsPage.tsx`
- `modules/payables/pages/PayablesPage.tsx`
- `modules/payables/pages/PayableDetailPage.tsx`
- `modules/caja/components/CashMovementReasonListTab.tsx`
- `modules/settings/geography/pages/GeographyPage.tsx`
- `components/zh/ZHDataTable.tsx`
- `modules/company-management/pages/CompanyManagementListPage.tsx`
- `modules/expenses/pages/ExpenseDocumentsPage.tsx`
- `modules/items/catalog/wizard/TreeEditor.tsx`
- `modules/items/detail/components/VariantsSection.tsx`
- `modules/items/detail/components/CollectionSection.tsx`
- `modules/establishments/components/EstablishmentsListSection.tsx`
- `modules/items/catalog/components/CatalogListSection.tsx`
- `modules/masterData/components/MasterDataPartnerResumenTab.tsx`
- `modules/emissionPoints/components/EmissionPointsListSection.tsx`
- `modules/items/components/ItemForm/PrincipalCodesSummary.tsx`
- `modules/inventory/adjustments/pages/StockAdjustmentsPage.tsx`
- `modules/sales/components/ReturnableLinesEditor.tsx`
- `modules/inventory/adjustments/pages/StockAdjustmentFormPage.tsx`
- `modules/inventory/transfers/pages/StockTransferPage.tsx`
- `components/PageShell.tsx`
- `modules/branches/components/BranchesListSection.tsx`
- `modules/inventory/pendingCosts/pages/PendingSaleCostsPage.tsx`
- `modules/purchases/pages/PurchaseReceptionPage.tsx`
- `modules/inventory/adjustmentReasons/components/AdjustmentReasonListTab.tsx`
- `modules/purchases/components/PurchaseLineBreakdownModal.tsx`
- `modules/purchases/components/PurchaseReturnableLinesEditor.tsx`
