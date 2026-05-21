import { Navigate, Route } from 'react-router-dom';
import { SuperAdminLayout } from '../layouts/SuperAdminLayout';
import { lazyNamedPage } from './lazyPage';

const SuperAdminSubscribersPage = lazyNamedPage(
  () => import('../pages/SuperAdmin/SuperAdminSubscribersPage'),
  'SuperAdminSubscribersPage',
);

const SuperAdminOverviewPage = lazyNamedPage(
  () => import('../pages/SuperAdmin/SuperAdminOverviewPage'),
  'SuperAdminOverviewPage',
);
const SuperAdminCompaniesShellPage = lazyNamedPage(
  () => import('../pages/SuperAdmin/SuperAdminCompaniesShellPage'),
  'SuperAdminCompaniesShellPage',
);
const SuperAdminMenuPlansHubPage = lazyNamedPage(
  () => import('../pages/SuperAdmin/SuperAdminMenuPlansHubPage'),
  'SuperAdminMenuPlansHubPage',
);
const SuperAdminPlansPage = lazyNamedPage(
  () => import('../pages/SuperAdmin/SuperAdminPlansPage'),
  'SuperAdminPlansPage',
);

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
      <Route path="subscribers" element={<SuperAdminSubscribersPage />} />
      <Route path="menu-builder" element={<Navigate to="/superadmin/menu-plans?tab=menu" replace />} />
      <Route path="forms" element={<Navigate to="/superadmin/overview" replace />} />
      <Route path="growth" element={<Navigate to="/superadmin/overview" replace />} />
    </Route>
  );
}
