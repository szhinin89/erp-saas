import { Route, Navigate } from 'react-router-dom';
import { lazyNamedPage } from './lazyPage';

const TariffsCatalogPage = lazyNamedPage(
  () => import('../modules/catalog/pages/CatalogPages'),
  'TariffsCatalogPage',
);
const CatalogStructurePage = lazyNamedPage(
  () => import('../modules/catalog/pages/CatalogPages'),
  'CatalogStructurePage',
);
const BrandsPage = lazyNamedPage(() => import('../pages/BrandsPage'), 'BrandsPage');
const ProductTypesPage = lazyNamedPage(() => import('../pages/ProductTypesPage'), 'ProductTypesPage');
const UnitsPage = lazyNamedPage(() => import('../pages/UnitsPage'), 'UnitsPage');
const TransferenciasListPage = lazyNamedPage(
  () => import('../modules/inventario/transferencias/pages/TransferenciasListPage'),
  'TransferenciasListPage',
);
const CrearTransferenciaPage = lazyNamedPage(
  () => import('../modules/inventario/transferencias/pages/CrearTransferenciaPage'),
  'CrearTransferenciaPage',
);
const TransferenciaDetailPage = lazyNamedPage(
  () => import('../modules/inventario/transferencias/pages/TransferenciaDetailPage'),
  'TransferenciaDetailPage',
);
const AjustesListPage = lazyNamedPage(
  () => import('../modules/inventario/ajustes/pages/AjustesListPage'),
  'AjustesListPage',
);
const CrearAjustePage = lazyNamedPage(
  () => import('../modules/inventario/ajustes/pages/CrearAjustePage'),
  'CrearAjustePage',
);
const AjusteDetailPage = lazyNamedPage(
  () => import('../modules/inventario/ajustes/pages/AjusteDetailPage'),
  'AjusteDetailPage',
);
const OrdenesCompraListPage = lazyNamedPage(
  () => import('../modules/compras/ordenes/pages/OrdenesCompraListPage'),
  'OrdenesCompraListPage',
);
const CrearOrdenCompraPage = lazyNamedPage(
  () => import('../modules/compras/ordenes/pages/CrearOrdenCompraPage'),
  'CrearOrdenCompraPage',
);
const OrdenCompraDetailPage = lazyNamedPage(
  () => import('../modules/compras/ordenes/pages/OrdenCompraDetailPage'),
  'OrdenCompraDetailPage',
);
const StockPage = lazyNamedPage(
  () => import('../modules/inventario/stock/pages/StockPage'),
  'StockPage',
);
const KardexPage = lazyNamedPage(
  () => import('../modules/inventario/kardex/pages/KardexPage'),
  'KardexPage',
);
const CashBankPage = lazyNamedPage(
  () => import('../modules/cash/bank/pages/CashBankPage'),
  'CashBankPage',
);
const GeographyPage = lazyNamedPage(
  () => import('../modules/settings/geography/pages/GeographyPage'),
  'GeographyPage',
);
const ActivityPage = lazyNamedPage(
  () => import('../modules/admin/activity/pages/ActivityPage'),
  'ActivityPage',
);
const WithholdingReceivedPage = lazyNamedPage(
  () => import('../modules/ventas/pages/WithholdingReceivedPage'),
  'WithholdingReceivedPage',
);
const PurchaseCreditNotesPage = lazyNamedPage(
  () => import('../modules/compras/credit-notes/pages/PurchaseCreditNotesPage'),
  'PurchaseCreditNotesPage',
);
const WithholdingIssuedPage = lazyNamedPage(
  () => import('../modules/compras/withholding-issued/pages/WithholdingIssuedPage'),
  'WithholdingIssuedPage',
);
const BodegasPage = lazyNamedPage(() => import('../pages/BodegasPage'), 'BodegasPage');
const CarriersPage = lazyNamedPage(
  () => import('../modules/logistica/transportistas/pages/CarriersPage'),
  'CarriersPage',
);
const CreditNotesPage = lazyNamedPage(() => import('../modules/ventas/pages/CreditNotesPage'), 'CreditNotesPage');
const CreateCreditNotePage = lazyNamedPage(
  () => import('../modules/ventas/pages/CreateCreditNotePage'),
  'CreateCreditNotePage',
);

export const catalogRoutes = [
  // ── Purchases / Órdenes de Compra ─────────────────────────────────────────
  <Route key="purchases-orders" path="/purchases/orders" element={<OrdenesCompraListPage />} />,
  <Route key="purchases-orders-new" path="/purchases/orders/new" element={<CrearOrdenCompraPage />} />,
  <Route key="purchases-orders-detail" path="/purchases/orders/:id" element={<OrdenCompraDetailPage />} />,
  <Route key="purchases-credit-notes" path="/purchases/credit-notes" element={<PurchaseCreditNotesPage />} />,
  <Route key="purchases-withholding-issued" path="/purchases/withholding-issued" element={<WithholdingIssuedPage />} />,
  // Legacy
  <Route key="compras-ordenes" path="/compras/ordenes" element={<Navigate to="/purchases/orders" replace />} />,
  <Route key="compras-ordenes-nueva" path="/compras/ordenes/nueva" element={<Navigate to="/purchases/orders/new" replace />} />,
  <Route key="compras-ordenes-detail" path="/compras/ordenes/:id" element={<Navigate to="/purchases/orders/:id" replace />} />,
  <Route key="compras-notas-prov" path="/compras/notas-proveedor" element={<Navigate to="/purchases/credit-notes" replace />} />,
  <Route key="compras-ret" path="/compras/retenciones" element={<Navigate to="/purchases/withholding-issued" replace />} />,

  // ── Inventory / Ajustes ───────────────────────────────────────────────────
  <Route key="inventory-adjustments" path="/inventory/adjustments" element={<AjustesListPage />} />,
  <Route key="inventory-adjustments-new" path="/inventory/adjustments/new" element={<CrearAjustePage />} />,
  <Route key="inventory-adjustments-detail" path="/inventory/adjustments/:id" element={<AjusteDetailPage />} />,
  // Legacy
  <Route key="ajustes-legacy" path="/inventario/ajustes" element={<Navigate to="/inventory/adjustments" replace />} />,
  <Route key="ajustes-nuevo-legacy" path="/inventario/ajustes/nuevo" element={<Navigate to="/inventory/adjustments/new" replace />} />,
  <Route key="ajustes-detail-legacy" path="/inventario/ajustes/:id" element={<Navigate to="/inventory/adjustments/:id" replace />} />,

  // ── Inventory / Transferencias ─────────────────────────────────────────────
  <Route key="inventory-transfers" path="/inventory/transfers" element={<TransferenciasListPage />} />,
  <Route key="inventory-transfers-new" path="/inventory/transfers/new" element={<CrearTransferenciaPage />} />,
  <Route key="inventory-transfers-detail" path="/inventory/transfers/:id" element={<TransferenciaDetailPage />} />,
  // Legacy
  <Route key="transferencias-legacy" path="/inventario/transferencias" element={<Navigate to="/inventory/transfers" replace />} />,
  <Route key="transferencias-nueva-legacy" path="/inventario/transferencias/nueva" element={<Navigate to="/inventory/transfers/new" replace />} />,
  <Route key="transferencias-detail-legacy" path="/inventario/transferencias/:id" element={<Navigate to="/inventory/transfers/:id" replace />} />,

  // ── Inventory / Catálogo ───────────────────────────────────────────────────
  <Route key="inventory-brands" path="/inventory/brands" element={<BrandsPage />} />,
  <Route key="inventory-product-types" path="/inventory/product-types" element={<ProductTypesPage />} />,
  <Route key="inventory-units" path="/inventory/units" element={<UnitsPage />} />,
  <Route key="inventory-tariffs" path="/inventory/tariffs" element={<TariffsCatalogPage />} />,
  <Route key="inventory-catalog-structure" path="/inventory/catalog-structure" element={<CatalogStructurePage />} />,
  <Route key="inventory-warehouses" path="/inventory/warehouses" element={<BodegasPage />} />,
  <Route key="inventory-kardex" path="/inventory/kardex" element={<KardexPage />} />,
  <Route key="inventory-stock" path="/inventory/stock" element={<StockPage />} />,
  // Legacy
  <Route key="inventario-brands" path="/inventario/brands" element={<Navigate to="/inventory/brands" replace />} />,
  <Route key="inventario-product-types" path="/inventario/product-types" element={<Navigate to="/inventory/product-types" replace />} />,
  <Route key="inventario-units" path="/inventario/units" element={<Navigate to="/inventory/units" replace />} />,
  <Route key="inventario-tariffs" path="/inventario/tariffs" element={<Navigate to="/inventory/tariffs" replace />} />,
  <Route key="inventario-structure" path="/inventario/structure" element={<Navigate to="/inventory/catalog-structure" replace />} />,
  <Route key="inventario-bodegas" path="/inventario/bodegas" element={<Navigate to="/inventory/warehouses" replace />} />,
  <Route key="inventario-kardex" path="/inventario/kardex" element={<Navigate to="/inventory/kardex" replace />} />,
  <Route key="logistica-bodegas" path="/logistica/bodegas" element={<Navigate to="/inventory/warehouses" replace />} />,
  <Route key="catalog-brands" path="/catalog/brands" element={<Navigate to="/inventory/brands" replace />} />,
  <Route key="catalog-product-types" path="/catalog/product-types" element={<Navigate to="/inventory/product-types" replace />} />,
  <Route key="catalog-units" path="/catalog/units" element={<Navigate to="/inventory/units" replace />} />,
  <Route key="catalog-tariffs" path="/catalog/tariffs" element={<Navigate to="/inventory/tariffs" replace />} />,
  <Route key="catalog-structure" path="/catalog/structure" element={<Navigate to="/inventory/catalog-structure" replace />} />,

  // ── Logistics / Transportistas ─────────────────────────────────────────────
  <Route key="logistics-carriers" path="/logistics/carriers" element={<CarriersPage />} />,
  // Legacy
  <Route key="logistica-transportistas" path="/logistica/transportistas" element={<Navigate to="/logistics/carriers" replace />} />,

  // ── Cash / Caja ────────────────────────────────────────────────────────────
  <Route key="cash-bank" path="/cash/bank" element={<CashBankPage />} />,
  <Route key="caja-root" path="/caja" element={<Navigate to="/cash/bank" replace />} />,

  // ── Sales (rutas secundarias) ──────────────────────────────────────────────
  <Route key="sales-credit-notes" path="/sales/credit-notes" element={<CreditNotesPage />} />,
  <Route key="sales-credit-notes-new" path="/sales/credit-notes/new" element={<CreateCreditNotePage />} />,
  <Route key="sales-withholding-received" path="/sales/withholding-received" element={<WithholdingReceivedPage />} />,
  <Route key="ventas-notas-legacy" path="/ventas/notas" element={<Navigate to="/sales/credit-notes" replace />} />,
  <Route key="ventas-ret-rec-legacy" path="/ventas/retenciones-recibidas" element={<Navigate to="/sales/withholding-received" replace />} />,

  // ── Settings adicionales ───────────────────────────────────────────────────
  <Route key="settings-geography" path="/settings/geography" element={<GeographyPage />} />,
  <Route key="geo-legacy" path="/configuracion/geografia" element={<Navigate to="/settings/geography" replace />} />,

  // ── Admin / Activity ───────────────────────────────────────────────────────
  <Route key="admin-activity" path="/admin/activity" element={<ActivityPage />} />,
  <Route key="actividad-legacy" path="/actividad" element={<Navigate to="/admin/activity" replace />} />,
];
