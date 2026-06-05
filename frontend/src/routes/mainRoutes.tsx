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
const SalesInvoicesPage = lazyNamedPage(() => import('../modules/sales/pages/SalesInvoicesPage'), 'SalesInvoicesPage');
const CreateInvoicePage = lazyNamedPage(
  () => import('../modules/sales/pages/CreateInvoicePage'),
  'CreateInvoicePage',
);
const InvoiceDetailPage = lazyNamedPage(
  () => import('../modules/sales/pages/InvoiceDetailPage'),
  'InvoiceDetailPage',
);
const CreateSalesOrderPage = lazyNamedPage(
  () => import('../modules/sales/ordenes/pages/CreateSalesOrderPage'),
  'CreateSalesOrderPage',
);
const SalesOrdersListPage = lazyNamedPage(
  () => import('../modules/sales/ordenes/pages/SalesOrdersListPage'),
  'SalesOrdersListPage',
);
const SalesOrderDetailPage = lazyNamedPage(
  () => import('../modules/sales/ordenes/pages/SalesOrderDetailPage'),
  'SalesOrderDetailPage',
);
const CreateQuotePage = lazyNamedPage(
  () => import('../modules/sales/cotizaciones/pages/CreateQuotePage'),
  'CreateQuotePage',
);
const QuotesListPage = lazyNamedPage(
  () => import('../modules/sales/cotizaciones/pages/QuotesListPage'),
  'QuotesListPage',
);
const QuoteDetailPage = lazyNamedPage(
  () => import('../modules/sales/cotizaciones/pages/QuoteDetailPage'),
  'QuoteDetailPage',
);
const BranchDetailPage = lazyNamedPage(() => import('../modules/branches/pages/BranchDetailPage'), 'BranchDetailPage');
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
const BillingSettingsPage = lazyNamedPage(
  () => import('../modules/configuracion/facturacion/pages/BillingSettingsPage'),
  'BillingSettingsPage',
);
const PurchaseInvoicesListPage = lazyNamedPage(
  () => import('../modules/purchasing/facturas/pages/PurchaseInvoicesListPage'),
  'PurchaseInvoicesListPage',
);
const CreatePurchaseInvoicePage = lazyNamedPage(
  () => import('../modules/purchasing/facturas/pages/CreatePurchaseInvoicePage'),
  'CreatePurchaseInvoicePage',
);
const PurchaseOrdersListPage = lazyNamedPage(
  () => import('../modules/purchasing/ordenes/pages/PurchaseOrdersListPage'),
  'PurchaseOrdersListPage',
);
const CreatePurchaseOrderPage = lazyNamedPage(
  () => import('../modules/purchasing/ordenes/pages/CreatePurchaseOrderPage'),
  'CreatePurchaseOrderPage',
);
const PurchaseOrderDetailPage = lazyNamedPage(
  () => import('../modules/purchasing/ordenes/pages/PurchaseOrderDetailPage'),
  'PurchaseOrderDetailPage',
);
const CashBankPage = lazyNamedPage(
  () => import('../modules/cash/bank/pages/CashBankPage'),
  'CashBankPage',
);
const CreditNotesPage = lazyNamedPage(
  () => import('../modules/sales/pages/CreditNotesPage'),
  'CreditNotesPage',
);
const CreateCreditNotePage = lazyNamedPage(
  () => import('../modules/sales/pages/CreateCreditNotePage'),
  'CreateCreditNotePage',
);
const ExpensesListPage = lazyNamedPage(() => import('../modules/expenses/pages/ExpensesListPage'), 'ExpensesListPage');
const CreateExpensePage = lazyNamedPage(() => import('../modules/expenses/pages/CreateExpensePage'), 'CreateExpensePage');

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
  <Route key="sales-invoices" path="/sales/invoices" element={<SalesInvoicesPage />} />,
  <Route key="sales-invoices-new" path="/sales/invoices/new" element={<CreateInvoicePage />} />,
  <Route key="sales-invoices-detail" path="/sales/invoices/:id" element={<InvoiceDetailPage />} />,
  <Route key="sales-credit-notes" path="/sales/credit-notes" element={<CreditNotesPage />} />,
  <Route key="sales-credit-notes-new" path="/sales/credit-notes/new" element={<CreateCreditNotePage />} />,
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
  <Route key="purchases-invoices" path="/purchases/invoices" element={<PurchaseInvoicesListPage />} />,
  <Route key="purchases-invoices-new" path="/purchases/invoices/new" element={<CreatePurchaseInvoicePage />} />,
  <Route key="purchases-orders" path="/purchases/orders" element={<PurchaseOrdersListPage />} />,
  <Route key="purchases-orders-new" path="/purchases/orders/new" element={<CreatePurchaseOrderPage />} />,
  <Route key="purchases-orders-detail" path="/purchases/orders/:id" element={<PurchaseOrderDetailPage />} />,
  <Route key="masterdata-suppliers" path="/masterdata/suppliers" element={<MasterDataSuppliersPage />} />,
  // Legacy redirects
  <Route key="compras-facturas" path="/compras/facturas" element={<Navigate to="/purchases/invoices" replace />} />,
  <Route key="compras-facturas-new" path="/compras/facturas/nueva" element={<Navigate to="/purchases/invoices/new" replace />} />,
  <Route key="compras-ordenes" path="/compras/ordenes" element={<Navigate to="/purchases/orders" replace />} />,
  <Route key="compras-ordenes-nueva" path="/compras/ordenes/nueva" element={<Navigate to="/purchases/orders/new" replace />} />,
  <Route key="compras-root" path="/compras" element={<Navigate to="/purchases/invoices" replace />} />,

  // ── Cash & Banks / Caja y Bancos ──────────────────────────────────────────
  <Route key="cash-bank" path="/cash/bank" element={<CashBankPage />} />,
  // Legacy redirects
  <Route key="caja-root" path="/caja" element={<Navigate to="/cash/bank" replace />} />,
  <Route key="bancos-root" path="/bancos" element={<Navigate to="/cash/bank" replace />} />,

  // ── Expenses / Gastos ──────────────────────────────────────────────────────
  <Route key="expenses" path="/expenses" element={<ExpensesListPage />} />,
  <Route key="expenses-new" path="/expenses/new" element={<CreateExpensePage />} />,
  // Legacy redirects
  <Route key="gastos-legacy" path="/gastos" element={<Navigate to="/expenses" replace />} />,
  <Route key="gastos-nuevo-legacy" path="/gastos/nuevo" element={<Navigate to="/expenses/new" replace />} />,

  // ── Settings / Configuración ───────────────────────────────────────────────
  <Route key="settings-company" path="/settings/company" element={<CompanySettingsHubPage />} />,
  <Route key="settings-sri" path="/settings/sri" element={<Navigate to="/settings/company?tab=sri" replace />} />,
  <Route key="settings-ride" path="/settings/ride" element={<BillingSettingsPage />} />,
  <Route key="settings-branches" path="/settings/branches" element={<Navigate to="/settings/company?tab=branches" replace />} />,
  <Route key="settings-branch-detail" path="/settings/branches/:id" element={<BranchDetailPage />} />,
  // Legacy redirects
  <Route key="config-empresa" path="/configuracion/empresa" element={<Navigate to="/settings/company" replace />} />,
  <Route key="config-sri" path="/configuracion/sri" element={<Navigate to="/settings/company?tab=sri" replace />} />,
  <Route key="config-facturacion" path="/configuracion/facturacion" element={<Navigate to="/settings/ride" replace />} />,
  <Route key="config-sucursales" path="/configuracion/sucursales" element={<Navigate to="/settings/company?tab=branches" replace />} />,
  <Route key="saas-branches" path="/saas/branches" element={<Navigate to="/settings/company?tab=branches" replace />} />,

  // ── Reportes ───────────────────────────────────────────────────────────────
  <Route key="sales-report" path="/reportes/ventas" element={<SalesReportPage />} />,

  <Route key="rrhh" path="/rrhh" element={<ModulePlaceholderPage variant="hr" />} />,
];
