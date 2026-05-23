import { Navigate, Route } from 'react-router-dom';
import { SuperAdminLayout } from '../layouts/SuperAdminLayout';
import { lazyNamedPage } from './lazyPage';

const SuperAdminSubscribersPage = lazyNamedPage(
  () => import('../pages/SuperAdmin/SuperAdminSubscribersPage'),
  'SuperAdminSubscribersPage',
);

const SuperAdminSubscriberDetailPage = lazyNamedPage(
  () => import('../modules/superadmin/pages/SuperAdminSubscriberDetailPage'),
  'SuperAdminSubscriberDetailPage',
);

const SuperAdminOverviewPage = lazyNamedPage(
  () => import('../pages/SuperAdmin/SuperAdminOverviewPage'),
  'SuperAdminOverviewPage',
);
const SuperAdminPlansPage = lazyNamedPage(
  () => import('../pages/SuperAdmin/SuperAdminPlansPage'),
  'SuperAdminPlansPage',
);
const SuperAdminObservabilityPage = lazyNamedPage(
  () => import('../modules/superadmin/pages/SuperAdminObservabilityPage'),
  'SuperAdminObservabilityPage',
);
const SuperAdminAuditPage = lazyNamedPage(
  () => import('../modules/superadmin/pages/SuperAdminAuditPage'),
  'SuperAdminAuditPage',
);
const SuperAdminUsersPage = lazyNamedPage(
  () => import('../modules/superadmin/pages/SuperAdminUsersPage'),
  'SuperAdminUsersPage',
);
const SuperAdminBillingPage = lazyNamedPage(
  () => import('../modules/superadmin/pages/SuperAdminBillingPage'),
  'SuperAdminBillingPage',
);

/** Rutas del shell SuperAdmin (fuera de `AppLayout`). */
export function superAdminShellRoutes() {
  return (
    <Route path="/superadmin" element={<SuperAdminLayout />}>
      <Route index element={<Navigate to="overview" replace />} />
      <Route path="overview" element={<SuperAdminOverviewPage />} />
      <Route path="subscribers" element={<SuperAdminSubscribersPage />} />
      <Route path="subscribers/:subscriberId" element={<SuperAdminSubscriberDetailPage />} />
      <Route path="plans" element={<SuperAdminPlansPage />} />
      <Route path="users" element={<SuperAdminUsersPage />} />
      <Route path="billing" element={<SuperAdminBillingPage />} />
      <Route path="observability" element={<SuperAdminObservabilityPage />} />
      <Route path="audit" element={<SuperAdminAuditPage />} />
      {/* Legacy redirects */}
      <Route path="companies" element={<Navigate to="/superadmin/subscribers" replace />} />
      <Route path="menu-plans" element={<Navigate to="/superadmin/plans?tab=menu" replace />} />
      <Route path="menu-builder" element={<Navigate to="/superadmin/plans?tab=menu" replace />} />
      <Route path="features" element={<Navigate to="/superadmin/plans?tab=plans" replace />} />
      <Route path="forms" element={<Navigate to="/superadmin/overview" replace />} />
      <Route path="growth" element={<Navigate to="/superadmin/overview" replace />} />
    </Route>
  );
}
