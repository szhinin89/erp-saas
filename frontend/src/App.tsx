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
import CompaniesPage from './pages/CompaniesPage';
import { TenantAccessPage } from './pages/TenantAccessPage';
import { ProfilesPage } from './pages/ProfilesPage';
import { BranchesPage } from './pages/BranchesPage';
import { CustomersPage } from './pages/CustomersPage';
import {
  BrandsCatalogPage,
  ProductTypesCatalogPage,
  UnitsCatalogPage,
  TaxRatesCatalogPage,
  TariffsCatalogPage,
  CatalogStructurePage,
} from './modules/catalog/pages/CatalogPages';
import { useDeployment } from './deployment/DeploymentContext';
import { ModulePlaceholderPage } from './pages/ModulePlaceholderPage';
import { ConfigProvider } from './modules/config';

function AppRoutes() {
  const { superAdminPanelEnabled } = useDeployment();

  return (
    <BrowserRouter>
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route path="/password-reset" element={<PasswordResetPage />} />
        <Route path="/select-tenant" element={<TenantSelectPage />} />

        <Route element={<ProtectedRoute />}>
          <Route element={<AppLayout />}>
            {superAdminPanelEnabled ? (
              <>
                <Route path="/superadmin" element={<SuperAdminPanelPage />} />
                <Route path="/superadmin/forms" element={<Navigate to="/superadmin" replace />} />
                <Route path="/superadmin/plans" element={<Navigate to="/superadmin?tab=plans" replace />} />
                <Route path="/superadmin/growth" element={<Navigate to="/superadmin" replace />} />
                <Route path="/superadmin/navigation-menu" element={<Navigate to="/companies?mainNavigation=1" replace />} />
              </>
            ) : null}
            <Route path="/dashboard" element={<DashboardPage />} />
            <Route path="/products" element={<ProductsPage />} />
            <Route path="/dashboard/products" element={<Navigate to="/products" replace />} />
            <Route path="/inventario/products" element={<Navigate to="/products" replace />} />
            <Route path="/catalog/products" element={<Navigate to="/products" replace />} />
            <Route path="/product" element={<Navigate to="/products" replace />} />
            <Route path="/ventas/customers" element={<CustomersPage />} />
            <Route path="/inventario/brands" element={<BrandsCatalogPage />} />
            <Route path="/inventario/product-types" element={<ProductTypesCatalogPage />} />
            <Route path="/inventario/units" element={<UnitsCatalogPage />} />
            <Route path="/inventario/tax-rates" element={<TaxRatesCatalogPage />} />
            <Route path="/inventario/tariffs" element={<TariffsCatalogPage />} />
            <Route path="/inventario/structure" element={<CatalogStructurePage />} />
            <Route path="/catalog/customers" element={<Navigate to="/ventas/customers" replace />} />
            <Route path="/inventario/customers" element={<Navigate to="/ventas/customers" replace />} />
            <Route path="/catalog/brands" element={<Navigate to="/inventario/brands" replace />} />
            <Route path="/catalog/product-types" element={<Navigate to="/inventario/product-types" replace />} />
            <Route path="/catalog/units" element={<Navigate to="/inventario/units" replace />} />
            <Route path="/catalog/tax-rates" element={<Navigate to="/inventario/tax-rates" replace />} />
            <Route path="/catalog/tariffs" element={<Navigate to="/inventario/tariffs" replace />} />
            <Route path="/catalog/structure" element={<Navigate to="/inventario/structure" replace />} />
            <Route path="/accounting" element={<AccountingPage />} />
            <Route path="/compras" element={<ModulePlaceholderPage variant="purchases" />} />
            <Route path="/rrhh" element={<ModulePlaceholderPage variant="hr" />} />
            <Route path="/security" element={<SecuritySettingsPage />} />
            <Route path="/companies" element={<CompaniesPage />} />
            <Route path="/saas/branches" element={<BranchesPage />} />
            <Route path="/access" element={<TenantAccessPage />} />
            <Route path="/profiles" element={<ProfilesPage />} />
          </Route>
        </Route>

        <Route path="*" element={<Navigate to="/dashboard" replace />} />
      </Routes>
    </BrowserRouter>
  );
}

export default function App() {
  return (
    <ConfigProvider>
      <AppRoutes />
    </ConfigProvider>
  );
}
