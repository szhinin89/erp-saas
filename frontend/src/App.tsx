import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { ProtectedRoute } from './components/ProtectedRoute';
import { AppLayout } from './components/AppLayout';
import { LoginPage } from './pages/LoginPage';
import { PasswordResetPage } from './pages/PasswordResetPage';
import { TenantSelectPage } from './pages/TenantSelectPage';
import { SuperAdminPanelPage } from './pages/SuperAdminPanelPage';
import { SuperAdminFormsPage } from './pages/SuperAdminFormsPage';
import { SuperAdminInstanceQuotaPage } from './pages/SuperAdminInstanceQuotaPage';
import { SuperAdminNavMenuPage } from './pages/SuperAdminNavMenuPage';
import { SuperAdminPlansPage } from './pages/SuperAdminPlansPage';
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
                <Route path="/superadmin/forms" element={<SuperAdminFormsPage />} />
                <Route path="/superadmin/plans" element={<SuperAdminPlansPage />} />
                <Route path="/superadmin/instance-quota" element={<SuperAdminInstanceQuotaPage />} />
                <Route path="/superadmin/navigation-menu" element={<SuperAdminNavMenuPage />} />
              </>
            ) : null}
            <Route path="/dashboard" element={<DashboardPage />} />
            <Route path="/products" element={<ProductsPage />} />
            <Route path="/catalog/customers" element={<CustomersPage />} />
            <Route path="/catalog/brands" element={<BrandsCatalogPage />} />
            <Route path="/catalog/product-types" element={<ProductTypesCatalogPage />} />
            <Route path="/catalog/units" element={<UnitsCatalogPage />} />
            <Route path="/catalog/tax-rates" element={<TaxRatesCatalogPage />} />
            <Route path="/catalog/tariffs" element={<TariffsCatalogPage />} />
            <Route path="/catalog/structure" element={<CatalogStructurePage />} />
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
  return <AppRoutes />;
}
