import { Route, Navigate } from "react-router-dom";
import { lazyNamedPage } from "./lazyPage";

const DashboardPage = lazyNamedPage(
  () => import("../modules/dashboard/pages/DashboardPage"),
  "DashboardPage",
);
const ItemsPage = lazyNamedPage(
  () => import("../modules/items/pages/ItemsPage"),
  "ItemsPage",
);
const KardexPage = lazyNamedPage(
  () => import("../modules/inventory/kardex/pages/KardexPage"),
  "KardexPage",
);
const StockTransferPage = lazyNamedPage(
  () => import("../modules/inventory/transfers/pages/StockTransferPage"),
  "StockTransferPage",
);
const StockAdjustmentsPage = lazyNamedPage(
  () => import("../modules/inventory/adjustments/pages/StockAdjustmentsPage"),
  "StockAdjustmentsPage",
);
const StockAdjustmentFormPage = lazyNamedPage(
  () => import("../modules/inventory/adjustments/pages/StockAdjustmentFormPage"),
  "StockAdjustmentFormPage",
);
const ItemTypesPage = lazyNamedPage(
  () => import("../modules/items/pages/ItemTypesPage"),
  "ItemTypesPage",
);
const BranchDetailPage = lazyNamedPage(
  () => import("../modules/branches/pages/BranchDetailPage"),
  "BranchDetailPage",
);
const BranchesPage = lazyNamedPage(
  () => import("../modules/branches/pages/BranchesPage"),
  "BranchesPage",
);
const EmissionPointsPage = lazyNamedPage(
  () => import("../modules/emissionPoints/pages/EmissionPointsPage"),
  "EmissionPointsPage",
);
const EstablishmentsPage = lazyNamedPage(
  () => import("../modules/establishments/pages/EstablishmentsPage"),
  "EstablishmentsPage",
);
const DocumentSequencesPage = lazyNamedPage(
  () => import("../modules/documentSequences/pages/DocumentSequencesPage"),
  "DocumentSequencesPage",
);
const SalesReportPage = lazyNamedPage(
  () => import("../modules/reportes/pages/SalesReportPage"),
  "SalesReportPage",
);
const StockReportPage = lazyNamedPage(
  () => import("../modules/reportes/pages/StockReportPage"),
  "StockReportPage",
);
const PurchasesReportPage = lazyNamedPage(
  () => import("../modules/reportes/pages/PurchasesReportPage"),
  "PurchasesReportPage",
);
const MasterDataCustomersPage = lazyNamedPage(
  () => import("../modules/masterData/pages/MasterDataCustomersPage"),
  "MasterDataCustomersPage",
);
const MasterDataSuppliersPage = lazyNamedPage(
  () => import("../modules/masterData/pages/MasterDataSuppliersPage"),
  "MasterDataSuppliersPage",
);
const MasterDataBusinessPartnerDetailPage = lazyNamedPage(
  () =>
    import("../modules/masterData/pages/MasterDataBusinessPartnerDetailPage"),
  "MasterDataBusinessPartnerDetailPage",
);
const CompanySettingsHubPage = lazyNamedPage(
  () => import("../modules/configuracion/empresa/pages/CompanySettingsHubPage"),
  "CompanySettingsHubPage",
);
const ElectronicInvoicingPage = lazyNamedPage(
  () =>
    import("../modules/configuracion/facturacionElectronica/pages/ElectronicInvoicingPage"),
  "ElectronicInvoicingPage",
);
const CommunicationsEmailSettingsPage = lazyNamedPage(
  () =>
    import("../modules/configuracion/comunicaciones/pages/CommunicationsEmailSettingsPage"),
  "CommunicationsEmailSettingsPage",
);
const ElectronicDocumentsMonitorPage = lazyNamedPage(
  () =>
    import("../modules/electronicDocuments/monitor/pages/ElectronicDocumentsMonitorPage"),
  "ElectronicDocumentsMonitorPage",
);
const OperationalPreferencesPage = lazyNamedPage(
  () =>
    import("../modules/configuracion/operaciones/pages/OperationalPreferencesPage"),
  "OperationalPreferencesPage",
);
const DocumentFlowPoliciesPage = lazyNamedPage(
  () =>
    import("../modules/configuracion/documentFlows/pages/DocumentFlowPoliciesPage"),
  "DocumentFlowPoliciesPage",
);
const InitialLoadHubPage = lazyNamedPage(
  () => import("../modules/initialLoad/pages/InitialLoadHubPage"),
  "InitialLoadHubPage",
);
const InitialLoadCustomersPage = lazyNamedPage(
  () => import("../modules/initialLoad/pages/InitialLoadCustomersPage"),
  "InitialLoadCustomersPage",
);
const InitialLoadSuppliersPage = lazyNamedPage(
  () => import("../modules/initialLoad/pages/InitialLoadSuppliersPage"),
  "InitialLoadSuppliersPage",
);
const InitialLoadProductCatalogPage = lazyNamedPage(
  () => import("../modules/initialLoad/pages/InitialLoadProductCatalogPage"),
  "InitialLoadProductCatalogPage",
);
const InitialLoadInitialStockPage = lazyNamedPage(
  () => import("../modules/initialLoad/pages/InitialLoadInitialStockPage"),
  "InitialLoadInitialStockPage",
);

export const mainRoutes = [
  <Route key="dashboard" path="/dashboard" element={<DashboardPage />} />,

  // -- Products / Items -----------------------------------------------------
  // URLS-MENU-ALIGNMENT-01: reubicado bajo /products (antes /inventory/*, heredado de cuando
  // este catálogo vivía dentro de Inventario) — misma pantalla/endpoints; ruta anterior queda
  // como redirect legacy más abajo.
  <Route
    key="products-items"
    path="/products/items"
    element={<ItemsPage />}
  />,
  <Route
    key="products-item-types"
    path="/products/item-types"
    element={<ItemTypesPage />}
  />,
  <Route
    key="inventory-items-legacy"
    path="/inventory/items"
    element={<Navigate to="/products/items" replace />}
  />,
  <Route
    key="inventory-item-types-legacy"
    path="/inventory/item-types"
    element={<Navigate to="/products/item-types" replace />}
  />,
  <Route
    key="inventory-kardex"
    path="/inventory/kardex"
    element={<KardexPage />}
  />,
  <Route
    key="inventory-transfers"
    path="/inventory/transfers"
    element={<StockTransferPage />}
  />,
  // INVENTORY-ADJUSTMENTS-03 — Inventario / Operación (el menú es 100% server-driven desde
  // [AppFeature] en los controladores; aquí solo se registra la ruta que ese menú espera).
  <Route
    key="inventory-adjustments"
    path="/inventory/adjustments"
    element={<StockAdjustmentsPage />}
  />,
  <Route
    key="inventory-adjustments-new"
    path="/inventory/adjustments/new"
    element={<StockAdjustmentFormPage />}
  />,
  <Route
    key="inventory-adjustment-detail"
    path="/inventory/adjustments/:id"
    element={<StockAdjustmentFormPage />}
  />,

  // -- Customers / Suppliers -----------------------------------------------
  // URLS-MENU-ALIGNMENT-01: reubicados a /customers y /suppliers (antes /masterdata/*, heredado
  // del módulo "masterdata" ya disuelto) — mismas pantallas/endpoints; rutas anteriores quedan
  // como redirect legacy más abajo.
  <Route
    key="customers"
    path="/customers"
    element={<MasterDataCustomersPage />}
  />,
  <Route
    key="masterdata-bp-detail"
    path="/masterdata/business-partners/:id"
    element={<MasterDataBusinessPartnerDetailPage />}
  />,
  <Route
    key="suppliers"
    path="/suppliers"
    element={<MasterDataSuppliersPage />}
  />,
  <Route
    key="masterdata-customers-legacy"
    path="/masterdata/customers"
    element={<Navigate to="/customers" replace />}
  />,
  <Route
    key="masterdata-suppliers-legacy"
    path="/masterdata/suppliers"
    element={<Navigate to="/suppliers" replace />}
  />,

  // -- Settings / Configuración -----------------------------------------------
  <Route
    key="settings-company"
    path="/settings/company"
    element={<CompanySettingsHubPage />}
  />,
  // MAPA-MENU-ERP-SSOT-01: reubicado bajo SRI > Configuración (antes bajo Configuración >
  // Facturación electrónica) — misma pantalla/endpoints; /settings/electronic-invoicing queda
  // como redirect legacy.
  <Route
    key="sri-electronic-invoicing"
    path="/sri/configuration/electronic-invoicing"
    element={<ElectronicInvoicingPage />}
  />,
  <Route
    key="settings-electronic-invoicing-legacy"
    path="/settings/electronic-invoicing"
    element={<Navigate to="/sri/configuration/electronic-invoicing" replace />}
  />,
  <Route
    key="settings-communications-email"
    path="/settings/communications/email"
    element={<CommunicationsEmailSettingsPage />}
  />,
  <Route
    key="settings-operations"
    path="/settings/operations"
    element={<OperationalPreferencesPage />}
  />,
  <Route
    key="settings-document-flows"
    path="/settings/document-flows"
    element={<DocumentFlowPoliciesPage />}
  />,

  // -- Carga Inicial (INITIAL-LOAD-ARCH-01) ------------------------------
  <Route key="initial-load-hub" path="/initial-load" element={<InitialLoadHubPage />} />,
  <Route
    key="initial-load-customers"
    path="/initial-load/customers"
    element={<InitialLoadCustomersPage />}
  />,
  <Route
    key="initial-load-suppliers"
    path="/initial-load/suppliers"
    element={<InitialLoadSuppliersPage />}
  />,
  <Route
    key="initial-load-products"
    path="/initial-load/products"
    element={<InitialLoadProductCatalogPage />}
  />,
  <Route
    key="initial-load-initial-stock"
    path="/initial-load/initial-stock"
    element={<InitialLoadInitialStockPage />}
  />,
  // MAPA-MENU-ERP-SSOT-01: reubicado bajo SRI > Documentos electrónicos (antes bajo Ventas) —
  // misma pantalla/endpoints; /electronic-documents/monitor queda como redirect legacy.
  <Route
    key="sri-electronic-documents-monitor"
    path="/sri/electronic-documents/monitor"
    element={<ElectronicDocumentsMonitorPage />}
  />,
  <Route
    key="electronic-documents-monitor-legacy"
    path="/electronic-documents/monitor"
    element={<Navigate to="/sri/electronic-documents/monitor" replace />}
  />,
  <Route
    key="settings-branches"
    path="/settings/branches"
    element={<BranchesPage />}
  />,
  <Route
    key="settings-branch-detail"
    path="/settings/branches/:id"
    element={<BranchDetailPage />}
  />,
  <Route
    key="settings-establishments"
    path="/settings/establishments"
    element={<EstablishmentsPage />}
  />,
  <Route
    key="settings-emission-points"
    path="/settings/emission-points"
    element={<EmissionPointsPage />}
  />,
  <Route
    key="settings-document-sequences"
    path="/settings/document-sequences"
    element={<DocumentSequencesPage />}
  />,

  // -- Reportes ---------------------------------------------------------------
  <Route
    key="sales-report"
    path="/reportes/ventas"
    element={<SalesReportPage />}
  />,
  <Route
    key="stock-report"
    path="/reportes/stock"
    element={<StockReportPage />}
  />,
  <Route
    key="purchases-report"
    path="/reportes/compras"
    element={<PurchasesReportPage />}
  />,
];
