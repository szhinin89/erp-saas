import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { ProtectedRoute } from './components/ProtectedRoute';
import { AppLayout } from './components/AppLayout';
import { LoginPage } from './pages/LoginPage';
import { PasswordResetPage } from './pages/PasswordResetPage';
import { TenantSelectPage } from './pages/TenantSelectPage';
import { SuperAdminPanelPage } from './pages/SuperAdminPanelPage';
import { DashboardPage } from './pages/DashboardPage';
import { ProductsPage } from './pages/ProductsPage';
import { AccountingPage } from './pages/AccountingPage';
import { SecuritySettingsPage } from './pages/SecuritySettingsPage';
import { CompaniesPage } from './pages/CompaniesPage';
import { TenantAccessPage } from './pages/TenantAccessPage';
import { ProfilesPage } from './pages/ProfilesPage';
import { BranchesPage } from './pages/BranchesPage';
import {
  BrandsCatalogPage,
  ProductTypesCatalogPage,
  UnitsCatalogPage,
  TaxRatesCatalogPage,
  TariffsCatalogPage,
  CatalogStructurePage,
} from './pages/CatalogPages';

export default function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route path="/password-reset" element={<PasswordResetPage />} />
        <Route path="/select-tenant" element={<TenantSelectPage />} />

        <Route element={<ProtectedRoute />}>
          <Route path="/superadmin" element={<SuperAdminPanelPage />} />
          <Route element={<AppLayout />}>
            <Route path="/dashboard"   element={<DashboardPage />} />
            <Route path="/products"    element={<ProductsPage />} />
            <Route path="/catalog/brands" element={<BrandsCatalogPage />} />
            <Route path="/catalog/product-types" element={<ProductTypesCatalogPage />} />
            <Route path="/catalog/units" element={<UnitsCatalogPage />} />
            <Route path="/catalog/tax-rates" element={<TaxRatesCatalogPage />} />
            <Route path="/catalog/tariffs" element={<TariffsCatalogPage />} />
            <Route path="/catalog/structure" element={<CatalogStructurePage />} />
            <Route path="/accounting"  element={<AccountingPage />} />
            <Route path="/security"    element={<SecuritySettingsPage />} />
            <Route path="/companies"   element={<CompaniesPage />} />
            <Route path="/saas/branches" element={<BranchesPage />} />
            <Route path="/access"      element={<TenantAccessPage />} />
            <Route path="/profiles"    element={<ProfilesPage />} />
          </Route>
        </Route>

        <Route path="*" element={<Navigate to="/dashboard" replace />} />
      </Routes>
    </BrowserRouter>
  );
}
