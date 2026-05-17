import { Route, Navigate } from 'react-router-dom';
import {
  BrandsCatalogPage,
  ProductTypesCatalogPage,
  UnitsCatalogPage,
  TariffsCatalogPage,
  CatalogStructurePage,
} from '../modules/catalog/pages/CatalogPages';
import { TransferenciasListPage }  from '../modules/inventario/transferencias/pages/TransferenciasListPage';
import { CrearTransferenciaPage }  from '../modules/inventario/transferencias/pages/CrearTransferenciaPage';
import { TransferenciaDetailPage } from '../modules/inventario/transferencias/pages/TransferenciaDetailPage';
import { AjustesListPage }   from '../modules/inventario/ajustes/pages/AjustesListPage';
import { CrearAjustePage }   from '../modules/inventario/ajustes/pages/CrearAjustePage';
import { AjusteDetailPage }  from '../modules/inventario/ajustes/pages/AjusteDetailPage';
import { OrdenesCompraListPage }  from '../modules/compras/ordenes/pages/OrdenesCompraListPage';
import { CrearOrdenCompraPage }   from '../modules/compras/ordenes/pages/CrearOrdenCompraPage';
import { OrdenCompraDetailPage }  from '../modules/compras/ordenes/pages/OrdenCompraDetailPage';
import { TenantFeaturePlaceholderPage } from '../pages/TenantFeaturePlaceholderPage';
import { BodegasPage } from '../pages/BodegasPage';

export const catalogRoutes = [
  // ── Órdenes de Compra ──────────────────────────────────────────────────────
  <Route key="ordenes-compra"        path="/compras/ordenes"        element={<OrdenesCompraListPage />} />,
  <Route key="ordenes-compra-nueva"  path="/compras/ordenes/nueva"  element={<CrearOrdenCompraPage />} />,
  <Route key="ordenes-compra-detail" path="/compras/ordenes/:id"    element={<OrdenCompraDetailPage />} />,


  // ── Ajustes de Inventario ──────────────────────────────────────────────────
  <Route key="ajustes"        path="/inventario/ajustes"        element={<AjustesListPage />} />,
  <Route key="ajustes-nuevo"  path="/inventario/ajustes/nuevo"  element={<CrearAjustePage />} />,
  <Route key="ajustes-detail" path="/inventario/ajustes/:id"    element={<AjusteDetailPage />} />,

  // ── Transferencias ─────────────────────────────────────────────────────────
  <Route key="transferencias"        path="/inventario/transferencias"        element={<TransferenciasListPage />} />,
  <Route key="transferencias-nueva"  path="/inventario/transferencias/nueva"  element={<CrearTransferenciaPage />} />,
  <Route key="transferencias-detail" path="/inventario/transferencias/:id"    element={<TransferenciaDetailPage />} />,

  <Route key="brands" path="/inventario/brands" element={<BrandsCatalogPage />} />,
  <Route key="product-types" path="/inventario/product-types" element={<ProductTypesCatalogPage />} />,
  <Route key="units" path="/inventario/units" element={<UnitsCatalogPage />} />,
  <Route key="tariffs" path="/inventario/tariffs" element={<TariffsCatalogPage />} />,
  <Route key="structure" path="/inventario/structure" element={<CatalogStructurePage />} />,
  // Legacy catalog/* redirects
  <Route key="catalog-brands" path="/catalog/brands" element={<Navigate to="/inventario/brands" replace />} />,
  <Route key="catalog-product-types" path="/catalog/product-types" element={<Navigate to="/inventario/product-types" replace />} />,
  <Route key="catalog-units" path="/catalog/units" element={<Navigate to="/inventario/units" replace />} />,
  <Route key="catalog-tariffs" path="/catalog/tariffs" element={<Navigate to="/inventario/tariffs" replace />} />,
  <Route key="catalog-structure" path="/catalog/structure" element={<Navigate to="/inventario/structure" replace />} />,

  // Rutas referenciadas por el catálogo API sin pantalla dedicada aún (evitan caer en * → /dashboard).
  <Route key="inv-kardex" path="/inventario/kardex" element={<TenantFeaturePlaceholderPage />} />,
  <Route key="inv-bodegas" path="/inventario/bodegas" element={<BodegasPage />} />,
  <Route key="legacy-logistica-bodegas" path="/logistica/bodegas" element={<Navigate to="/inventario/bodegas" replace />} />,
  <Route key="ventas-notas" path="/ventas/notas" element={<TenantFeaturePlaceholderPage />} />,
  <Route key="ventas-ret-rec" path="/ventas/retenciones-recibidas" element={<TenantFeaturePlaceholderPage />} />,
  <Route key="compras-prov" path="/compras/proveedores" element={<TenantFeaturePlaceholderPage />} />,
  <Route key="compras-notas-prov" path="/compras/notas-proveedor" element={<TenantFeaturePlaceholderPage />} />,
  <Route key="compras-ret" path="/compras/retenciones" element={<TenantFeaturePlaceholderPage />} />,
  <Route key="gastos-root" path="/gastos" element={<TenantFeaturePlaceholderPage />} />,
  <Route key="caja-root" path="/caja" element={<TenantFeaturePlaceholderPage />} />,
  <Route key="actividad" path="/actividad" element={<TenantFeaturePlaceholderPage />} />,
  <Route key="sri" path="/configuracion/sri" element={<TenantFeaturePlaceholderPage />} />,
];
