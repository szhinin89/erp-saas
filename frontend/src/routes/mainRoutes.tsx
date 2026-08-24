import { Route } from "react-router-dom";
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

  // -- Inventory / Items -----------------------------------------------------
  <Route
    key="inventory-Items"
    path="/inventory/items"
    element={<ItemsPage />}
  />,
  <Route
    key="inventory-item-types"
    path="/inventory/item-types"
    element={<ItemTypesPage />}
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

  // -- Master data --------------------------------------------------------
  <Route
    key="masterdata-customers"
    path="/masterdata/customers"
    element={<MasterDataCustomersPage />}
  />,
  <Route
    key="masterdata-bp-detail"
    path="/masterdata/business-partners/:id"
    element={<MasterDataBusinessPartnerDetailPage />}
  />,
  <Route
    key="masterdata-suppliers"
    path="/masterdata/suppliers"
    element={<MasterDataSuppliersPage />}
  />,

  // -- Settings / Configuración -----------------------------------------------
  <Route
    key="settings-company"
    path="/settings/company"
    element={<CompanySettingsHubPage />}
  />,
  <Route
    key="settings-electronic-invoicing"
    path="/settings/electronic-invoicing"
    element={<ElectronicInvoicingPage />}
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
  <Route
    key="electronic-documents-monitor"
    path="/electronic-documents/monitor"
    element={<ElectronicDocumentsMonitorPage />}
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
