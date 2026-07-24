import { Route } from 'react-router-dom';
import { lazyNamedPage } from './lazyPage';

const ModulePlaceholderPage = lazyNamedPage<{ variant: 'hr' }>(
  () => import('../modules/shared/pages/ModulePlaceholderPage'),
  'ModulePlaceholderPage',
);

const DashboardPage = lazyNamedPage(() => import('../modules/dashboard/pages/DashboardPage'), 'DashboardPage');
const ItemsPage = lazyNamedPage(() => import('../modules/items/pages/ItemsPage'), 'ItemsPage');
const KardexPage = lazyNamedPage(() => import('../modules/inventory/kardex/pages/KardexPage'), 'KardexPage');
const ItemTypesPage = lazyNamedPage(() => import('../modules/items/pages/ItemTypesPage'), 'ItemTypesPage');
const BranchDetailPage = lazyNamedPage(() => import('../modules/branches/pages/BranchDetailPage'), 'BranchDetailPage');
const BranchesPage = lazyNamedPage(() => import('../modules/branches/pages/BranchesPage'), 'BranchesPage');
const EmissionPointsPage = lazyNamedPage(
  () => import('../modules/emissionPoints/pages/EmissionPointsPage'),
  'EmissionPointsPage',
);
const EstablishmentsPage = lazyNamedPage(
  () => import('../modules/establishments/pages/EstablishmentsPage'),
  'EstablishmentsPage',
);
const SalesReportPage = lazyNamedPage(() => import('../modules/reportes/pages/SalesReportPage'), 'SalesReportPage');
const MasterDataCustomersPage = lazyNamedPage(
  () => import('../modules/masterData/pages/MasterDataCustomersPage'),
  'MasterDataCustomersPage',
);
const MasterDataSuppliersPage = lazyNamedPage(
  () => import('../modules/masterData/pages/MasterDataSuppliersPage'),
  'MasterDataSuppliersPage',
);
const MasterDataBusinessPartnerDetailPage = lazyNamedPage(
  () => import('../modules/masterData/pages/MasterDataBusinessPartnerDetailPage'),
  'MasterDataBusinessPartnerDetailPage',
);
const CompanySettingsHubPage = lazyNamedPage(
  () => import('../modules/configuracion/empresa/pages/CompanySettingsHubPage'),
  'CompanySettingsHubPage',
);
const ElectronicInvoicingPage = lazyNamedPage(
  () => import('../modules/configuracion/facturacionElectronica/pages/ElectronicInvoicingPage'),
  'ElectronicInvoicingPage',
);
const ElectronicDocumentsMonitorPage = lazyNamedPage(
  () => import('../modules/electronicDocuments/monitor/pages/ElectronicDocumentsMonitorPage'),
  'ElectronicDocumentsMonitorPage',
);

export const mainRoutes = [
  <Route key="dashboard" path="/dashboard" element={<DashboardPage />} />,

  // -- Inventory / Items -----------------------------------------------------
  <Route key="inventory-Items" path="/inventory/items" element={<ItemsPage />} />,
  <Route key="inventory-item-types" path="/inventory/item-types" element={<ItemTypesPage />} />,
  <Route key="inventory-kardex" path="/inventory/kardex" element={<KardexPage />} />,

  // -- Master data --------------------------------------------------------
  <Route key="masterdata-customers" path="/masterdata/customers" element={<MasterDataCustomersPage />} />,
  <Route key="masterdata-bp-detail" path="/masterdata/business-partners/:id" element={<MasterDataBusinessPartnerDetailPage />} />,
  <Route key="masterdata-suppliers" path="/masterdata/suppliers" element={<MasterDataSuppliersPage />} />,

  // -- Settings / Configuración -----------------------------------------------
  <Route key="settings-company" path="/settings/company" element={<CompanySettingsHubPage />} />,
  <Route key="settings-electronic-invoicing" path="/settings/electronic-invoicing" element={<ElectronicInvoicingPage />} />,
  <Route key="electronic-documents-monitor" path="/electronic-documents/monitor" element={<ElectronicDocumentsMonitorPage />} />,
  <Route key="settings-branches" path="/settings/branches" element={<BranchesPage />} />,
  <Route key="settings-branch-detail" path="/settings/branches/:id" element={<BranchDetailPage />} />,
  <Route key="settings-establishments" path="/settings/establishments" element={<EstablishmentsPage />} />,
  <Route key="settings-emission-points" path="/settings/emission-points" element={<EmissionPointsPage />} />,

  // -- Reportes ---------------------------------------------------------------
  <Route key="sales-report" path="/reportes/ventas" element={<SalesReportPage />} />,

  <Route key="rrhh" path="/rrhh" element={<ModulePlaceholderPage variant="hr" />} />,
];
