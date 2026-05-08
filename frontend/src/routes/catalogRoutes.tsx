import { Route, Navigate } from 'react-router-dom';
import {
  BrandsCatalogPage,
  ProductTypesCatalogPage,
  UnitsCatalogPage,
  TaxRatesCatalogPage,
  TariffsCatalogPage,
  CatalogStructurePage,
} from '../modules/catalog/pages/CatalogPages';

export const catalogRoutes = [
  <Route key="brands" path="/inventario/brands" element={<BrandsCatalogPage />} />,
  <Route key="product-types" path="/inventario/product-types" element={<ProductTypesCatalogPage />} />,
  <Route key="units" path="/inventario/units" element={<UnitsCatalogPage />} />,
  <Route key="tax-rates" path="/inventario/tax-rates" element={<TaxRatesCatalogPage />} />,
  <Route key="tariffs" path="/inventario/tariffs" element={<TariffsCatalogPage />} />,
  <Route key="structure" path="/inventario/structure" element={<CatalogStructurePage />} />,
  // Legacy catalog/* redirects
  <Route key="catalog-brands" path="/catalog/brands" element={<Navigate to="/inventario/brands" replace />} />,
  <Route key="catalog-product-types" path="/catalog/product-types" element={<Navigate to="/inventario/product-types" replace />} />,
  <Route key="catalog-units" path="/catalog/units" element={<Navigate to="/inventario/units" replace />} />,
  <Route key="catalog-tax-rates" path="/catalog/tax-rates" element={<Navigate to="/inventario/tax-rates" replace />} />,
  <Route key="catalog-tariffs" path="/catalog/tariffs" element={<Navigate to="/inventario/tariffs" replace />} />,
  <Route key="catalog-structure" path="/catalog/structure" element={<Navigate to="/inventario/structure" replace />} />,
];
