import { Route } from 'react-router-dom';
import {
  BrandsCatalogPage,
  ProductTypesCatalogPage,
  UnitsCatalogPage,
  TaxRatesCatalogPage,
  TariffsCatalogPage,
  CatalogStructurePage,
} from '../modules/catalog/pages/CatalogPages';

/**
 * Rutas del módulo Catalog (Inventario).
 * Todas están bajo <ProtectedRoute /> y <AppLayout />.
 * 
 * Paths:
 * - /inventario/brands
 * - /inventario/product-types
 * - /inventario/units
 * - /inventario/tax-rates
 * - /inventario/tariffs
 * - /inventario/structure
 */
export const catalogRoutes = [
  <Route key="brands" path="/inventario/brands" element={<BrandsCatalogPage />} />,
  <Route key="product-types" path="/inventario/product-types" element={<ProductTypesCatalogPage />} />,
  <Route key="units" path="/inventario/units" element={<UnitsCatalogPage />} />,
  <Route key="tax-rates" path="/inventario/tax-rates" element={<TaxRatesCatalogPage />} />,
  <Route key="tariffs" path="/inventario/tariffs" element={<TariffsCatalogPage />} />,
  <Route key="structure" path="/inventario/structure" element={<CatalogStructurePage />} />,
];
