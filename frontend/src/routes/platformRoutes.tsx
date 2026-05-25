import { Navigate, Route } from 'react-router-dom';
import { PlatformLayout } from '../layouts/PlatformLayout';
import { lazyNamedPage } from './lazyPage';

const PlatformSubscribersPage = lazyNamedPage(
  () => import('../pages/Platform/PlatformSubscribersPage'),
  'PlatformSubscribersPage',
);

const PlatformSubscriberDetailPage = lazyNamedPage(
  () => import('../modules/platform/pages/PlatformSubscriberDetailPage'),
  'PlatformSubscriberDetailPage',
);

const PlatformOverviewPage = lazyNamedPage(
  () => import('../pages/Platform/PlatformOverviewPage'),
  'PlatformOverviewPage',
);

const PlatformPlansPage = lazyNamedPage(
  () => import('../pages/Platform/PlatformPlansPage'),
  'PlatformPlansPage',
);

const PlatformObservabilityPage = lazyNamedPage(
  () => import('../modules/platform/pages/PlatformObservabilityPage'),
  'PlatformObservabilityPage',
);

const PlatformAuditPage = lazyNamedPage(
  () => import('../modules/platform/pages/PlatformAuditPage'),
  'PlatformAuditPage',
);

const PlatformUsersPage = lazyNamedPage(
  () => import('../modules/platform/pages/PlatformUsersPage'),
  'PlatformUsersPage',
);

const PlatformBillingPage = lazyNamedPage(
  () => import('../modules/platform/pages/PlatformBillingPage'),
  'PlatformBillingPage',
);

/**
 * Platform Control Plane UI — única fuente de verdad de rutas `/platform/*`.
 * Shell fuera de `AppLayout`.
 */
export function platformShellRoutes() {
  return (
    <Route path="/platform" element={<PlatformLayout />}>
      <Route index element={<Navigate to="overview" replace />} />
      <Route path="overview" element={<PlatformOverviewPage />} />
      <Route path="subscribers" element={<PlatformSubscribersPage />} />
      <Route path="subscribers/:subscriberId" element={<PlatformSubscriberDetailPage />} />
      <Route path="plans" element={<PlatformPlansPage />} />
      <Route path="users" element={<PlatformUsersPage />} />
      <Route path="billing" element={<PlatformBillingPage />} />
      <Route path="observability" element={<PlatformObservabilityPage />} />
      <Route path="audit" element={<PlatformAuditPage />} />
    </Route>
  );
}
