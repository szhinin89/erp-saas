import { Navigate, Route } from 'react-router-dom';
import { SuperAdminLayout } from '../layouts/SuperAdminLayout';
import { SuperAdminOverviewPage } from '../pages/SuperAdmin/SuperAdminOverviewPage';
import { SuperAdminCompaniesShellPage } from '../pages/SuperAdmin/SuperAdminCompaniesShellPage';
import { SuperAdminMenuPlansHubPage } from '../pages/SuperAdmin/SuperAdminMenuPlansHubPage';
import { SuperAdminPlansPage } from '../pages/SuperAdmin/SuperAdminPlansPage';

/** Rutas del shell SuperAdmin (fuera de `AppLayout`). */
export function superAdminShellRoutes() {
  return (
    <Route path="/superadmin" element={<SuperAdminLayout />}>
      <Route index element={<Navigate to="overview" replace />} />
      <Route path="overview" element={<SuperAdminOverviewPage />} />
      <Route path="companies" element={<SuperAdminCompaniesShellPage />} />
      <Route path="features" element={<Navigate to="/superadmin/menu-plans?tab=plans" replace />} />
      <Route path="menu-plans" element={<SuperAdminMenuPlansHubPage />} />
      <Route path="plans" element={<SuperAdminPlansPage />} />
      <Route path="menu-builder" element={<Navigate to="/superadmin/menu-plans?tab=menu" replace />} />
      <Route path="forms" element={<Navigate to="/superadmin/overview" replace />} />
      <Route path="growth" element={<Navigate to="/superadmin/overview" replace />} />
    </Route>
  );
}
