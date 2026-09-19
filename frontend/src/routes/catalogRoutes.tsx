import { Route, Navigate } from "react-router-dom";
import { lazyNamedPage } from "./lazyPage";
import { SupplierCreditDetailLegacyRedirect } from "./legacyRedirects";

const GeographyPage = lazyNamedPage(
  () => import("../modules/settings/geography/pages/GeographyPage"),
  "GeographyPage",
);
const BanksPage = lazyNamedPage(
  () => import("../modules/settings/banks/pages/BanksPage"),
  "BanksPage",
);
const ActivityPage = lazyNamedPage(
  () => import("../modules/admin/activity/pages/ActivityPage"),
  "ActivityPage",
);
const AdminUserSessionsPage = lazyNamedPage(
  () => import("../modules/admin/access-sessions/pages/AdminUserSessionsPage"),
  "AdminUserSessionsPage",
);
const BodegasPage = lazyNamedPage(
  () => import("../modules/inventory/warehouses/pages/WarehousesPage"),
  "BodegasPage",
);
const CarriersPage = lazyNamedPage(
  () => import("../modules/logistica/transportistas/pages/CarriersPage"),
  "CarriersPage",
);
const PriceListsPage = lazyNamedPage(
  () => import("../modules/pricing/pages/PriceListsPage"),
  "PriceListsPage",
);
const CreditTermsPage = lazyNamedPage(
  () => import("../modules/finance/pages/CreditTermsPage"),
  "CreditTermsPage",
);
const JournalEntriesPage = lazyNamedPage(
  () => import("../modules/accounting/pages/JournalEntriesPage"),
  "JournalEntriesPage",
);
const JournalEntryDetailPage = lazyNamedPage(
  () => import("../modules/accounting/pages/JournalEntryDetailPage"),
  "JournalEntryDetailPage",
);
const ChartOfAccountsPage = lazyNamedPage(
  () => import("../modules/accounting/pages/ChartOfAccountsPage"),
  "ChartOfAccountsPage",
);
const PostingRulesPage = lazyNamedPage(
  () => import("../modules/accounting/pages/PostingRulesPage"),
  "PostingRulesPage",
);
const AccountingReportsPage = lazyNamedPage(
  () => import("../modules/accounting/pages/AccountingReportsPage"),
  "AccountingReportsPage",
);
const AccountsReceivablePage = lazyNamedPage(
  () => import("../modules/finance/pages/AccountsReceivablePage"),
  "AccountsReceivablePage",
);
const SupplierCreditListPage = lazyNamedPage(
  () => import("../modules/finance/pages/SupplierCreditListPage"),
  "SupplierCreditListPage",
);
const SupplierCreditDetailPage = lazyNamedPage(
  () => import("../modules/finance/pages/SupplierCreditDetailPage"),
  "SupplierCreditDetailPage",
);
const BankAccountsPage = lazyNamedPage(
  () => import("../modules/finance/pages/BankAccountsPage"),
  "BankAccountsPage",
);
const ExpenseCategoriesPage = lazyNamedPage(
  () => import("../modules/expenses/pages/ExpenseCategoriesPage"),
  "ExpenseCategoriesPage",
);
const ExpenseDocumentsPage = lazyNamedPage(
  () => import("../modules/expenses/pages/ExpenseDocumentsPage"),
  "ExpenseDocumentsPage",
);
const ExpenseDocumentFormPage = lazyNamedPage(
  () => import("../modules/expenses/pages/ExpenseDocumentFormPage"),
  "ExpenseDocumentFormPage",
);
const PaymentTermsPage = lazyNamedPage(
  () => import("../modules/masterData/pages/PaymentTermsPage"),
  "PaymentTermsPage",
);
const PayablesPage = lazyNamedPage(
  () => import("../modules/payables/pages/PayablesPage"),
  "PayablesPage",
);
const PayableDetailPage = lazyNamedPage(
  () => import("../modules/payables/pages/PayableDetailPage"),
  "PayableDetailPage",
);
const SupplierPaymentsPage = lazyNamedPage(
  () => import("../modules/supplier-payments/pages/SupplierPaymentsPage"),
  "SupplierPaymentsPage",
);
const SupplierPaymentFormPage = lazyNamedPage(
  () => import("../modules/supplier-payments/pages/SupplierPaymentFormPage"),
  "SupplierPaymentFormPage",
);
const SupplierPaymentDetailPage = lazyNamedPage(
  () => import("../modules/supplier-payments/pages/SupplierPaymentDetailPage"),
  "SupplierPaymentDetailPage",
);
const PurchasesPage = lazyNamedPage(
  () => import("../modules/purchases/pages/PurchasesPage"),
  "PurchasesPage",
);
const PurchaseReceptionPage = lazyNamedPage(
  () => import("../modules/purchases/pages/PurchaseReceptionPage"),
  "PurchaseReceptionPage",
);
const SalesPage = lazyNamedPage(
  () => import("../modules/sales/pages/SalesPage"),
  "SalesPage",
);
const PaymentMethodsPage = lazyNamedPage(
  () => import("../modules/sales/pages/PaymentMethodsPage"),
  "PaymentMethodsPage",
);
const SalesReturnListPage = lazyNamedPage(
  () => import("../modules/sales/pages/SalesReturnListPage"),
  "SalesReturnListPage",
);
const SalesReturnFormPage = lazyNamedPage(
  () => import("../modules/sales/pages/SalesReturnFormPage"),
  "SalesReturnFormPage",
);
const PurchaseReturnListPage = lazyNamedPage(
  () => import("../modules/purchases/pages/PurchaseReturnListPage"),
  "PurchaseReturnListPage",
);
const PurchaseReturnFormPage = lazyNamedPage(
  () => import("../modules/purchases/pages/PurchaseReturnFormPage"),
  "PurchaseReturnFormPage",
);
const PurchaseReturnDetailPage = lazyNamedPage(
  () => import("../modules/purchases/pages/PurchaseReturnDetailPage"),
  "PurchaseReturnDetailPage",
);
const PurchaseCreditNoteListPage = lazyNamedPage(
  () => import("../modules/purchases/pages/PurchaseCreditNoteListPage"),
  "PurchaseCreditNoteListPage",
);
const PurchaseCreditNoteFormPage = lazyNamedPage(
  () => import("../modules/purchases/pages/PurchaseCreditNoteFormPage"),
  "PurchaseCreditNoteFormPage",
);
const PurchaseCreditNoteDetailPage = lazyNamedPage(
  () => import("../modules/purchases/pages/PurchaseCreditNoteDetailPage"),
  "PurchaseCreditNoteDetailPage",
);
const CajaPage = lazyNamedPage(
  () => import("../modules/caja/pages/CajaPage"),
  "CajaPage",
);
const CashRegistersPage = lazyNamedPage(
  () => import("../modules/cashRegisters/pages/CashRegistersPage"),
  "CashRegistersPage",
);
const CashMovementReasonsAdminPage = lazyNamedPage(
  () => import("../modules/caja/pages/CashMovementReasonsAdminPage"),
  "CashMovementReasonsAdminPage",
);
const BrandsPage = lazyNamedPage(
  () => import("../modules/items/catalog/pages/BrandsPage"),
  "BrandsPage",
);
const AttributeGroupsPage = lazyNamedPage(
  () => import("../modules/items/catalog/pages/AttributeGroupsPage"),
  "AttributeGroupsPage",
);
const AttributeDefinitionsPage = lazyNamedPage(
  () => import("../modules/items/catalog/pages/AttributeDefinitionsPage"),
  "AttributeDefinitionsPage",
);
const InventoryAdjustmentReasonsPage = lazyNamedPage(
  () =>
    import(
      "../modules/inventory/adjustmentReasons/pages/InventoryAdjustmentReasonsPage"
    ),
  "InventoryAdjustmentReasonsPage",
);
const TreeEditorPage = lazyNamedPage(
  () => import("../modules/items/catalog/wizard/TreeEditor"),
  "TreeEditorPage",
);
export const catalogRoutes = [
  // -- Inventory / Warehouses ---------------------------------------------
  <Route
    key="inventory-warehouses"
    path="/inventory/warehouses"
    element={<BodegasPage />}
  />,
  // INVENTORY-ADJUSTMENTS-03 — Inventario / Configuración (junto a Bodegas, mismo split
  // Operación/Configuración ya establecido entre mainRoutes y catalogRoutes).
  <Route
    key="inventory-adjustment-reasons"
    path="/inventory/adjustment-reasons"
    element={<InventoryAdjustmentReasonsPage />}
  />,
  <Route
    key="inventario-bodegas"
    path="/inventario/bodegas"
    element={<Navigate to="/inventory/warehouses" replace />}
  />,
  <Route
    key="logistica-bodegas"
    path="/logistica/bodegas"
    element={<Navigate to="/inventory/warehouses" replace />}
  />,

  // -- Logistics / Transportistas -----------------------------------------
  <Route
    key="logistics-carriers"
    path="/logistics/carriers"
    element={<CarriersPage />}
  />,
  <Route
    key="logistica-transportistas"
    path="/logistica/transportistas"
    element={<Navigate to="/logistics/carriers" replace />}
  />,

  // -- Settings -----------------------------------------------------------
  <Route
    key="settings-geography"
    path="/settings/geography"
    element={<GeographyPage />}
  />,
  <Route
    key="geo-legacy"
    path="/configuracion/geografia"
    element={<Navigate to="/settings/geography" replace />}
  />,
  // BANK-CATALOG-01: catálogo maestro de Bancos — Configuración > Catálogos > Bancos. No existe
  // ruta antigua para este catálogo (pantalla nueva) — sin redirect legacy, per alcance del ticket.
  <Route
    key="settings-catalogs-banks"
    path="/settings/catalogs/banks"
    element={<BanksPage />}
  />,

  // -- Admin / Activity ---------------------------------------------------
  <Route
    key="admin-activity"
    path="/admin/activity"
    element={<ActivityPage />}
  />,
  <Route
    key="actividad-legacy"
    path="/actividad"
    element={<Navigate to="/admin/activity" replace />}
  />,
  <Route
    key="admin-access-sessions"
    path="/admin/access/sessions"
    element={<AdminUserSessionsPage />}
  />,

  // -- Pricing ------------------------------------------------------------
  // URLS-MENU-ALIGNMENT-01: reubicado bajo /products/pricing (antes /pricing, un módulo "pricing"
  // que ya no existe como tal) — misma pantalla/endpoints; ruta anterior queda como redirect.
  <Route key="products-pricing" path="/products/pricing" element={<PriceListsPage />} />,
  <Route
    key="pricing-legacy"
    path="/pricing"
    element={<Navigate to="/products/pricing" replace />}
  />,

  // -- Purchases -----------------------------------------------------------
  <Route key="purchases" path="/purchases" element={<PurchasesPage />} />,
  <Route
    key="purchase-reception"
    path="/purchases/reception"
    element={<PurchaseReceptionPage />}
  />,
  <Route
    key="purchase-returns"
    path="/purchases/returns"
    element={<PurchaseReturnListPage />}
  />,
  <Route
    key="purchase-returns-new"
    path="/purchases/returns/new"
    element={<PurchaseReturnFormPage />}
  />,
  <Route
    key="purchase-returns-detail"
    path="/purchases/returns/:id"
    element={<PurchaseReturnDetailPage />}
  />,
  <Route
    key="purchase-credit-notes"
    path="/purchases/credit-notes"
    element={<PurchaseCreditNoteListPage />}
  />,
  <Route
    key="purchase-credit-notes-new"
    path="/purchases/credit-notes/new"
    element={<PurchaseCreditNoteFormPage />}
  />,
  <Route
    key="purchase-credit-notes-detail"
    path="/purchases/credit-notes/:id"
    element={<PurchaseCreditNoteDetailPage />}
  />,

  // -- Sales ---------------------------------------------------------------
  <Route key="sales" path="/sales" element={<SalesPage />} />,
  // DESTINOS-CONTABLES-COBROS-VENTAS-01: ruta canónica movida a
  // /accounting/configuration/sales-collection-destinations (ver bloque Accounting más abajo) —
  // se conserva este redirect por enlaces/tests legacy, sin duplicar la pantalla.
  <Route
    key="sales-payment-methods"
    path="/sales/payment-methods"
    element={<Navigate to="/accounting/configuration/sales-collection-destinations" replace />}
  />,
  <Route
    key="sales-returns"
    path="/sales/returns"
    element={<SalesReturnListPage />}
  />,
  <Route
    key="sales-returns-new"
    path="/sales/returns/new"
    element={<SalesReturnFormPage />}
  />,
  <Route
    key="sales-returns-detail"
    path="/sales/returns/:id"
    element={<SalesReturnFormPage />}
  />,

  // -- Treasury (Caja / Bancos) ---------------------------------------------
  // MAPA-MENU-ERP-SSOT-01: Caja se reubicó bajo Tesorería (antes vivía bajo Ventas) — mismas
  // pantallas/endpoints, solo cambia la URL visible; /cash y /cash/registers quedan como redirect
  // para enlaces/tests legacy, no como segunda ruta funcional.
  <Route key="treasury-cash" path="/treasury/cash" element={<CajaPage />} />,
  <Route
    key="treasury-cash-registers"
    path="/treasury/cash/registers"
    element={<CashRegistersPage />}
  />,
  <Route
    key="treasury-cash-movement-reasons"
    path="/treasury/cash/movement-reasons"
    element={<CashMovementReasonsAdminPage />}
  />,
  <Route
    key="cash-legacy"
    path="/cash"
    element={<Navigate to="/treasury/cash" replace />}
  />,
  <Route
    key="cash-registers-legacy"
    path="/cash/registers"
    element={<Navigate to="/treasury/cash/registers" replace />}
  />,

  // -- Accounting -----------------------------------------------------------
  // ACCOUNTING-NAVIGATION-CANONICAL-AUDIT-11C: /accounting ya no es una pantalla funcional
  // propia (el hub de tarjetas se eliminó — duplicaba el menú) — solo redirige a la pantalla
  // canónica principal del módulo. No aparece en el menú (AccountingModule.cs, backend, ya
  // registra las pantallas principales directamente, sin este ítem — 4 desde
  // ACCOUNTING-POSTING-RULES-UI-12: Asientos/Plan de cuentas/Reglas contables/Reportes).
  <Route
    key="accounting-root"
    path="/accounting"
    element={<Navigate to="/accounting/journal-entries" replace />}
  />,
  <Route
    key="accounting-journal-entries"
    path="/accounting/journal-entries"
    element={<JournalEntriesPage />}
  />,
  <Route
    key="accounting-journal-entry-detail"
    path="/accounting/journal-entries/:id"
    element={<JournalEntryDetailPage />}
  />,
  // DESTINOS-CONTABLES-COBROS-VENTAS-01: cuenta contable por forma de cobro (PaymentMethodAccount)
  // — misma pantalla/endpoints que antes (PaymentMethodsPage.tsx, /api/v1/payment-methods*), solo
  // se reubica bajo Contabilidad > Configuración > Destinos contables > Cobros de ventas.
  <Route
    key="accounting-sales-collection-destinations"
    path="/accounting/configuration/sales-collection-destinations"
    element={<PaymentMethodsPage />}
  />,
  <Route
    key="accounting-chart-of-accounts"
    path="/accounting/chart-of-accounts"
    element={<ChartOfAccountsPage />}
  />,
  // MAPA-MENU-ERP-SSOT-01: reubicado bajo Contabilidad > Configuración (antes bajo Plan de
  // cuentas) — misma pantalla/endpoints; /accounting/posting-rules queda como redirect legacy.
  <Route
    key="accounting-posting-rules"
    path="/accounting/configuration/posting-rules"
    element={<PostingRulesPage />}
  />,
  <Route
    key="accounting-posting-rules-legacy"
    path="/accounting/posting-rules"
    element={<Navigate to="/accounting/configuration/posting-rules" replace />}
  />,
  <Route
    key="accounting-reports"
    path="/accounting/reports"
    element={<AccountingReportsPage />}
  />,

  // -- Finance ------------------------------------------------------------
  // URLS-MENU-ALIGNMENT-01: Condiciones de Crédito y Créditos de proveedor reubicados bajo
  // /suppliers/* (antes /finance/*, heredado del módulo "finance" ya disuelto) — mismas
  // pantallas/endpoints; rutas anteriores quedan como redirect legacy.
  <Route
    key="suppliers-credit-terms"
    path="/suppliers/credit-terms"
    element={<CreditTermsPage />}
  />,
  <Route
    key="finance-credit-terms-legacy"
    path="/finance/credit-terms"
    element={<Navigate to="/suppliers/credit-terms" replace />}
  />,
  <Route
    key="finance-receivables"
    path="/finance/receivables"
    element={<AccountsReceivablePage />}
  />,
  <Route
    key="suppliers-credits"
    path="/suppliers/credits"
    element={<SupplierCreditListPage />}
  />,
  <Route
    key="suppliers-credits-detail"
    path="/suppliers/credits/:id"
    element={<SupplierCreditDetailPage />}
  />,
  <Route
    key="finance-supplier-credits-legacy"
    path="/finance/supplier-credits"
    element={<Navigate to="/suppliers/credits" replace />}
  />,
  <Route
    key="finance-supplier-credits-detail-legacy"
    path="/finance/supplier-credits/:id"
    element={<SupplierCreditDetailLegacyRedirect />}
  />,
  // TREASURY-BANK-ACCOUNTS-01
  <Route
    key="treasury-bank-accounts"
    path="/treasury/banks/accounts"
    element={<BankAccountsPage />}
  />,

  // -- Payables (generico: Compras + Gastos) -------------------------------
  <Route key="payables-list" path="/payables" element={<PayablesPage />} />,
  <Route key="payables-detail" path="/payables/:id" element={<PayableDetailPage />} />,

  // -- Supplier Payments (modulo independiente de Pagos a Proveedores) ----
  <Route key="supplier-payments-list" path="/supplier-payments" element={<SupplierPaymentsPage />} />,
  <Route
    key="supplier-payments-new"
    path="/supplier-payments/new"
    element={<SupplierPaymentFormPage />}
  />,
  <Route
    key="supplier-payments-detail"
    path="/supplier-payments/:id"
    element={<SupplierPaymentDetailPage />}
  />,

  // -- Expenses -----------------------------------------------------------
  <Route
    key="expenses-root"
    path="/expenses"
    element={<Navigate to="/expenses/documents" replace />}
  />,
  <Route
    key="expenses-documents"
    path="/expenses/documents"
    element={<ExpenseDocumentsPage />}
  />,
  <Route
    key="expenses-documents-new"
    path="/expenses/documents/new"
    element={<ExpenseDocumentFormPage />}
  />,
  <Route
    key="expenses-documents-detail"
    path="/expenses/documents/:id"
    element={<ExpenseDocumentFormPage />}
  />,
  <Route
    key="expenses-categories"
    path="/expenses/categories"
    element={<ExpenseCategoriesPage />}
  />,

  // -- Suppliers / Payment Terms -------------------------------------------
  // URLS-MENU-ALIGNMENT-01: reubicado bajo /suppliers/payment-terms (antes /master/payment-terms,
  // heredado del módulo "masterdata" ya disuelto) — misma pantalla/endpoints.
  <Route
    key="suppliers-payment-terms"
    path="/suppliers/payment-terms"
    element={<PaymentTermsPage />}
  />,
  <Route
    key="master-payment-terms-legacy"
    path="/master/payment-terms"
    element={<Navigate to="/suppliers/payment-terms" replace />}
  />,

  // -- Products / Catalog ---------------------------------------------------
  // URLS-MENU-ALIGNMENT-01: reubicados bajo /products/* (antes /catalog/*, heredado de cuando
  // este catálogo era un módulo "catalog" separado) — mismas pantallas/endpoints.
  <Route
    key="products-brands"
    path="/products/brands"
    element={<BrandsPage />}
  />,
  <Route
    key="products-attribute-groups"
    path="/products/attribute-groups"
    element={<AttributeGroupsPage />}
  />,
  <Route
    key="products-attribute-definitions"
    path="/products/attribute-definitions"
    element={<AttributeDefinitionsPage />}
  />,
  <Route
    key="products-categories"
    path="/products/categories"
    element={<TreeEditorPage />}
  />,
  <Route
    key="catalog-brands-legacy"
    path="/catalog/brands"
    element={<Navigate to="/products/brands" replace />}
  />,
  <Route
    key="catalog-attribute-groups-legacy"
    path="/catalog/attribute-groups"
    element={<Navigate to="/products/attribute-groups" replace />}
  />,
  <Route
    key="catalog-attribute-definitions-legacy"
    path="/catalog/attribute-definitions"
    element={<Navigate to="/products/attribute-definitions" replace />}
  />,
  <Route
    key="catalog-tree-legacy"
    path="/catalog/tree"
    element={<Navigate to="/products/categories" replace />}
  />,
];
