import { Route, Navigate } from 'react-router-dom';
import { lazyNamedPage } from './lazyPage';

const ModulePlaceholderPage = lazyNamedPage<{ variant: 'purchases' | 'hr' }>(
  () => import('../modules/shared/pages/ModulePlaceholderPage'),
  'ModulePlaceholderPage',
);

const DashboardPage = lazyNamedPage(() => import('../pages/DashboardPage'), 'DashboardPage');
const ProductsPage = lazyNamedPage(() => import('../pages/ProductsPage'), 'ProductsPage');
const AccountingPage = lazyNamedPage(() => import('../pages/AccountingPage'), 'AccountingPage');
const CustomersPage = lazyNamedPage(() => import('../pages/CustomersPage'), 'CustomersPage');
const VentasFacturasPage = lazyNamedPage(() => import('../pages/VentasFacturasPage'), 'VentasFacturasPage');
const CreateInvoicePage = lazyNamedPage(
  () => import('../modules/ventas/pages/CreateInvoicePage'),
  'CreateInvoicePage',
);
const BranchesPage = lazyNamedPage(() => import('../pages/BranchesPage'), 'BranchesPage');
const SalesReportPage = lazyNamedPage(() => import('../pages/SalesReportPage'), 'SalesReportPage');
const SuppliersPage = lazyNamedPage(
  () => import('../modules/compras/suppliers/pages/SuppliersPage'),
  'SuppliersPage',
);
const MasterDataCustomersPage = lazyNamedPage(
  () => import('../modules/masterData/pages/MasterDataCustomersPage'),
  'MasterDataCustomersPage',
);
const MasterDataSuppliersPage = lazyNamedPage(
  () => import('../modules/masterData/pages/MasterDataSuppliersPage'),
  'MasterDataSuppliersPage',
);
const CompanyConfigPage = lazyNamedPage(
  () => import('../modules/configuracion/empresa/pages/CompanyConfigPage'),
  'CompanyConfigPage',
);
const SriConfigPage = lazyNamedPage(
  () => import('../modules/configuracion/sri/pages/SriConfigPage'),
  'SriConfigPage',
);
const BillingSettingsPage = lazyNamedPage(
  () => import('../modules/configuracion/facturacion/pages/BillingSettingsPage'),
  'BillingSettingsPage',
);
const ComprasListPage = lazyNamedPage(
  () => import('../modules/compras/facturas/pages/ComprasListPage'),
  'ComprasListPage',
);
const CrearCompraPage = lazyNamedPage(
  () => import('../modules/compras/facturas/pages/CrearCompraPage'),
  'CrearCompraPage',
);
const GastosListPage = lazyNamedPage(() => import('../modules/gastos/pages/GastosListPage'), 'GastosListPage');
const CrearGastoPage = lazyNamedPage(() => import('../modules/gastos/pages/CrearGastoPage'), 'CrearGastoPage');

export const mainRoutes = [
  <Route key="dashboard" path="/dashboard" element={<DashboardPage />} />,

  // ── Inventory / Productos ──────────────────────────────────────────────────
  <Route key="inventory-products" path="/inventory/products" element={<ProductsPage />} />,
  // Legacy redirects
  <Route key="products-legacy" path="/products" element={<Navigate to="/inventory/products" replace />} />,
  <Route key="inventario-products" path="/inventario/products" element={<Navigate to="/inventory/products" replace />} />,
  <Route key="catalog-products" path="/catalog/products" element={<Navigate to="/inventory/products" replace />} />,
  <Route key="product-legacy" path="/product" element={<Navigate to="/inventory/products" replace />} />,

  // ── Sales / Ventas ─────────────────────────────────────────────────────────
  <Route key="sales-invoices" path="/sales/invoices" element={<VentasFacturasPage />} />,
  <Route key="sales-invoices-new" path="/sales/invoices/new" element={<CreateInvoicePage />} />,
  <Route key="sales-customers" path="/sales/customers" element={<CustomersPage />} />,
  <Route key="masterdata-customers" path="/masterdata/customers" element={<MasterDataCustomersPage />} />,
  // Legacy redirects
  <Route key="ventas-facturas" path="/ventas/facturas" element={<Navigate to="/sales/invoices" replace />} />,
  <Route key="ventas-facturas-nueva" path="/ventas/facturas/nueva" element={<Navigate to="/sales/invoices/new" replace />} />,
  <Route key="ventas-customers" path="/ventas/customers" element={<Navigate to="/sales/customers" replace />} />,
  <Route key="ventas-clientes" path="/ventas/clientes" element={<Navigate to="/sales/customers" replace />} />,
  <Route key="ventas-root" path="/ventas" element={<Navigate to="/sales/invoices" replace />} />,
  <Route key="catalog-customers" path="/catalog/customers" element={<Navigate to="/sales/customers" replace />} />,

  // ── Finance / Contabilidad ─────────────────────────────────────────────────
  <Route key="finance-accounts" path="/finance/accounts" element={<AccountingPage />} />,
  <Route key="finance-config" path="/finance/config" element={<AccountingPage />} />,
  // Legacy redirects
  <Route key="accounting-legacy" path="/accounting" element={<Navigate to="/finance/accounts" replace />} />,
  <Route key="contabilidad-legacy" path="/contabilidad" element={<Navigate to="/finance/accounts" replace />} />,
  <Route key="contabilidad-config" path="/contabilidad/configuracion" element={<Navigate to="/finance/config" replace />} />,

  // ── Purchases / Compras ────────────────────────────────────────────────────
  <Route key="purchases-invoices" path="/purchases/invoices" element={<ComprasListPage />} />,
  <Route key="purchases-invoices-new" path="/purchases/invoices/new" element={<CrearCompraPage />} />,
  <Route key="purchases-suppliers" path="/purchases/suppliers" element={<SuppliersPage />} />,
  <Route key="masterdata-suppliers" path="/masterdata/suppliers" element={<MasterDataSuppliersPage />} />,
  // Legacy redirects
  <Route key="compras-facturas" path="/compras/facturas" element={<Navigate to="/purchases/invoices" replace />} />,
  <Route key="compras-facturas-new" path="/compras/facturas/nueva" element={<Navigate to="/purchases/invoices/new" replace />} />,
  <Route key="compras-proveedores" path="/compras/proveedores" element={<Navigate to="/purchases/suppliers" replace />} />,
  <Route key="compras-root" path="/compras" element={<Navigate to="/purchases/invoices" replace />} />,

  // ── Expenses / Gastos ──────────────────────────────────────────────────────
  <Route key="expenses" path="/expenses" element={<GastosListPage />} />,
  <Route key="expenses-new" path="/expenses/new" element={<CrearGastoPage />} />,
  // Legacy redirects
  <Route key="gastos-legacy" path="/gastos" element={<Navigate to="/expenses" replace />} />,
  <Route key="gastos-nuevo-legacy" path="/gastos/nuevo" element={<Navigate to="/expenses/new" replace />} />,

  // ── Settings / Configuración ───────────────────────────────────────────────
  <Route key="settings-company" path="/settings/company" element={<CompanyConfigPage />} />,
  <Route key="settings-sri" path="/settings/sri" element={<SriConfigPage />} />,
  <Route key="settings-ride" path="/settings/ride" element={<BillingSettingsPage />} />,
  <Route key="settings-branches" path="/settings/branches" element={<BranchesPage />} />,
  // Legacy redirects
  <Route key="config-empresa" path="/configuracion/empresa" element={<Navigate to="/settings/company" replace />} />,
  <Route key="config-sri" path="/configuracion/sri" element={<Navigate to="/settings/sri" replace />} />,
  <Route key="config-facturacion" path="/configuracion/facturacion" element={<Navigate to="/settings/ride" replace />} />,
  <Route key="config-sucursales" path="/configuracion/sucursales" element={<Navigate to="/settings/branches" replace />} />,
  <Route key="saas-branches" path="/saas/branches" element={<Navigate to="/settings/branches" replace />} />,

  // ── Reportes ───────────────────────────────────────────────────────────────
  <Route key="sales-report" path="/reportes/ventas" element={<SalesReportPage />} />,

  <Route key="rrhh" path="/rrhh" element={<ModulePlaceholderPage variant="hr" />} />,
];
