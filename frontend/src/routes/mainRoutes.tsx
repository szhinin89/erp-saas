import { Route, Navigate, useParams } from 'react-router-dom';
import { lazyNamedPage } from './lazyPage';

function LegacySalesOrderDetailRedirect() {
  const { publicId } = useParams<{ publicId: string }>();
  return <Navigate to={`/sales/orders/${publicId ?? ''}`} replace />;
}

function LegacyQuoteDetailRedirect() {
  const { publicId } = useParams<{ publicId: string }>();
  return <Navigate to={`/sales/quotes/${publicId ?? ''}`} replace />;
}

const ModulePlaceholderPage = lazyNamedPage<{ variant: 'purchases' | 'hr' }>(
  () => import('../modules/shared/pages/ModulePlaceholderPage'),
  'ModulePlaceholderPage',
);

const DashboardPage = lazyNamedPage(() => import('../modules/dashboard/pages/DashboardPage'), 'DashboardPage');
const ProductsPage = lazyNamedPage(() => import('../modules/products/pages/ProductPage'), 'ProductPage');
const AccountingPage = lazyNamedPage(() => import('../modules/accounting/pages/AccountingPage'), 'AccountingPage');
const VentasFacturasPage = lazyNamedPage(() => import('../modules/ventas/pages/VentasFacturasPage'), 'VentasFacturasPage');
const CreateInvoicePage = lazyNamedPage(
  () => import('../modules/ventas/pages/CreateInvoicePage'),
  'CreateInvoicePage',
);
const InvoiceDetailPage = lazyNamedPage(
  () => import('../modules/ventas/pages/InvoiceDetailPage'),
  'InvoiceDetailPage',
);
const CreateSalesOrderPage = lazyNamedPage(
  () => import('../modules/ventas/ordenes/pages/CreateSalesOrderPage'),
  'CreateSalesOrderPage',
);
const SalesOrdersListPage = lazyNamedPage(
  () => import('../modules/ventas/ordenes/pages/SalesOrdersListPage'),
  'SalesOrdersListPage',
);
const SalesOrderDetailPage = lazyNamedPage(
  () => import('../modules/ventas/ordenes/pages/SalesOrderDetailPage'),
  'SalesOrderDetailPage',
);
const CreateQuotePage = lazyNamedPage(
  () => import('../modules/ventas/cotizaciones/pages/CreateQuotePage'),
  'CreateQuotePage',
);
const QuotesListPage = lazyNamedPage(
  () => import('../modules/ventas/cotizaciones/pages/QuotesListPage'),
  'QuotesListPage',
);
const QuoteDetailPage = lazyNamedPage(
  () => import('../modules/ventas/cotizaciones/pages/QuoteDetailPage'),
  'QuoteDetailPage',
);
const BranchesPage = lazyNamedPage(() => import('../modules/branches/pages/BranchesPage'), 'BranchesPage');
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
  <Route key="sales-invoices-detail" path="/sales/invoices/:id" element={<InvoiceDetailPage />} />,
  <Route key="sales-quotes" path="/sales/quotes" element={<QuotesListPage />} />,
  <Route key="sales-quotes-new" path="/sales/quotes/new" element={<CreateQuotePage />} />,
  <Route key="sales-quotes-detail" path="/sales/quotes/:publicId" element={<QuoteDetailPage />} />,
  <Route key="sales-orders" path="/sales/orders" element={<SalesOrdersListPage />} />,
  <Route key="sales-orders-new" path="/sales/orders/new" element={<CreateSalesOrderPage />} />,
  <Route key="sales-orders-detail" path="/sales/orders/:publicId" element={<SalesOrderDetailPage />} />,
  <Route key="masterdata-customers" path="/masterdata/customers" element={<MasterDataCustomersPage />} />,
  <Route key="masterdata-bp-detail" path="/masterdata/business-partners/:id" element={<MasterDataBusinessPartnerDetailPage />} />,
  // Legacy redirects
  <Route key="ventas-facturas" path="/ventas/facturas" element={<Navigate to="/sales/invoices" replace />} />,
  <Route key="ventas-facturas-nueva" path="/ventas/facturas/nueva" element={<Navigate to="/sales/invoices/new" replace />} />,
  <Route key="ventas-cotizaciones" path="/ventas/cotizaciones" element={<Navigate to="/sales/quotes" replace />} />,
  <Route key="ventas-cotizaciones-nueva" path="/ventas/cotizaciones/nueva" element={<Navigate to="/sales/quotes/new" replace />} />,
  <Route key="ventas-cotizaciones-detail" path="/ventas/cotizaciones/:publicId" element={<LegacyQuoteDetailRedirect />} />,
  <Route key="ventas-pedidos" path="/ventas/pedidos" element={<Navigate to="/sales/orders" replace />} />,
  <Route key="ventas-pedidos-nuevo" path="/ventas/pedidos/nuevo" element={<Navigate to="/sales/orders/new" replace />} />,
  <Route key="ventas-pedidos-detail" path="/ventas/pedidos/:publicId" element={<LegacySalesOrderDetailRedirect />} />,
  <Route key="ventas-root" path="/ventas" element={<Navigate to="/sales/invoices" replace />} />,

  // ── Finance / Contabilidad ─────────────────────────────────────────────────
  <Route key="finance-accounts" path="/finance/accounts" element={<AccountingPage />} />,
  // Legacy redirects
  <Route key="finance-config" path="/finance/config" element={<Navigate to="/finance/accounts?tab=config" replace />} />,
  <Route key="accounting-legacy" path="/accounting" element={<Navigate to="/finance/accounts" replace />} />,
  <Route key="contabilidad-legacy" path="/contabilidad" element={<Navigate to="/finance/accounts" replace />} />,
  <Route key="contabilidad-config" path="/contabilidad/configuracion" element={<Navigate to="/finance/accounts?tab=config" replace />} />,

  // ── Purchases / Compras ────────────────────────────────────────────────────
  <Route key="purchases-invoices" path="/purchases/invoices" element={<ComprasListPage />} />,
  <Route key="purchases-invoices-new" path="/purchases/invoices/new" element={<CrearCompraPage />} />,
  <Route key="masterdata-suppliers" path="/masterdata/suppliers" element={<MasterDataSuppliersPage />} />,
  // Legacy redirects
  <Route key="compras-facturas" path="/compras/facturas" element={<Navigate to="/purchases/invoices" replace />} />,
  <Route key="compras-facturas-new" path="/compras/facturas/nueva" element={<Navigate to="/purchases/invoices/new" replace />} />,
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
