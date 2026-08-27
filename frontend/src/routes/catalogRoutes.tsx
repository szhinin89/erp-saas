import { Route, Navigate } from "react-router-dom";
import { lazyNamedPage } from "./lazyPage";

const GeographyPage = lazyNamedPage(
  () => import("../modules/settings/geography/pages/GeographyPage"),
  "GeographyPage",
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
const AccountsPayablePage = lazyNamedPage(
  () => import("../modules/finance/pages/AccountsPayablePage"),
  "AccountsPayablePage",
);
const SupplierCreditListPage = lazyNamedPage(
  () => import("../modules/finance/pages/SupplierCreditListPage"),
  "SupplierCreditListPage",
);
const SupplierCreditDetailPage = lazyNamedPage(
  () => import("../modules/finance/pages/SupplierCreditDetailPage"),
  "SupplierCreditDetailPage",
);
const FinancialDestinationsPage = lazyNamedPage(
  () => import("../modules/finance/pages/FinancialDestinationsPage"),
  "FinancialDestinationsPage",
);
const ExpenseCategoriesPage = lazyNamedPage(
  () => import("../modules/expenses/pages/ExpenseCategoriesPage"),
  "ExpenseCategoriesPage",
);
const PaymentTermsPage = lazyNamedPage(
  () => import("../modules/masterData/pages/PaymentTermsPage"),
  "PaymentTermsPage",
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
  <Route key="pricing" path="/pricing" element={<PriceListsPage />} />,

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
  <Route
    key="sales-payment-methods"
    path="/sales/payment-methods"
    element={<PaymentMethodsPage />}
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

  // -- Caja (Cash Management) ---------------------------------------------
  <Route key="cash" path="/cash" element={<CajaPage />} />,
  <Route
    key="cash-registers"
    path="/cash/registers"
    element={<CashRegistersPage />}
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
  <Route
    key="accounting-chart-of-accounts"
    path="/accounting/chart-of-accounts"
    element={<ChartOfAccountsPage />}
  />,
  <Route
    key="accounting-posting-rules"
    path="/accounting/posting-rules"
    element={<PostingRulesPage />}
  />,
  <Route
    key="accounting-reports"
    path="/accounting/reports"
    element={<AccountingReportsPage />}
  />,

  // -- Finance ------------------------------------------------------------
  <Route
    key="finance-credit-terms"
    path="/finance/credit-terms"
    element={<CreditTermsPage />}
  />,
  <Route
    key="finance-receivables"
    path="/finance/receivables"
    element={<AccountsReceivablePage />}
  />,
  <Route
    key="finance-payables"
    path="/finance/payables"
    element={<AccountsPayablePage />}
  />,
  <Route
    key="finance-supplier-credits"
    path="/finance/supplier-credits"
    element={<SupplierCreditListPage />}
  />,
  <Route
    key="finance-supplier-credits-detail"
    path="/finance/supplier-credits/:id"
    element={<SupplierCreditDetailPage />}
  />,
  <Route
    key="settings-financial-destinations"
    path="/settings/financial-destinations"
    element={<FinancialDestinationsPage />}
  />,

  // -- Expenses -----------------------------------------------------------
  <Route
    key="expenses-categories"
    path="/expenses/categories"
    element={<ExpenseCategoriesPage />}
  />,

  // -- MasterData / Payment Terms -----------------------------------------
  <Route
    key="master-payment-terms"
    path="/master/payment-terms"
    element={<PaymentTermsPage />}
  />,

  // -- Catalog ------------------------------------------------------------
  <Route
    key="catalog-brands"
    path="/catalog/brands"
    element={<BrandsPage />}
  />,
  <Route
    key="catalog-attribute-groups"
    path="/catalog/attribute-groups"
    element={<AttributeGroupsPage />}
  />,
  <Route
    key="catalog-attribute-definitions"
    path="/catalog/attribute-definitions"
    element={<AttributeDefinitionsPage />}
  />,
  <Route
    key="catalog-tree"
    path="/catalog/tree"
    element={<TreeEditorPage />}
  />,
];
