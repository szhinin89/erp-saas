import { Navigate, Route, useLocation, useSearchParams } from 'react-router-dom';
import { PlatformLayout } from '../layouts/PlatformLayout';
import { lazyNamedPage } from './lazyPage';
import {
  readCompaniesDetailSubscriberId,
  clearCompaniesDetailSubscriberId,
} from '../navigation/platformSubscriberDetailNav';
import { PLATFORM_UI } from '../modules/platform/api/platformApiPaths';

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

/** Bookmark `/companies` → rutas canónicas platform (sessionStorage compat). */
function PlatformBookmarkRedirect() {
  const [searchParams] = useSearchParams();
  const detailId = readCompaniesDetailSubscriberId();

  if (detailId) {
    clearCompaniesDetailSubscriberId();
    return <Navigate to={PLATFORM_UI.subscriberDetail(detailId)} replace />;
  }

  const mainNavigation = searchParams.get('mainNavigation');
  if (mainNavigation === '1') {
    return <Navigate to={`${PLATFORM_UI.plans}?tab=menu`} replace />;
  }

  const plan = searchParams.get('plan')?.trim();
  if (plan) {
    return <Navigate to={`${PLATFORM_UI.plans}?plan=${encodeURIComponent(plan)}`} replace />;
  }

  return <Navigate to={PLATFORM_UI.subscribers} replace />;
}

/** Redirect legacy `/superadmin/*` bookmarks to `/platform/*`. */
export function PlatformLegacyUiRedirect() {
  const location = useLocation();
  const target = `${location.pathname.replace(/^\/superadmin(?=\/|$)/, '/platform')}${location.search}${location.hash}`;
  return <Navigate to={target || PLATFORM_UI.overview} replace />;
}

/**
 * Platform Control Plane UI — única fuente de verdad de rutas `/platform/*`.
 * Shell fuera de `AppLayout`; bookmarks `/companies/*` redirigen aquí.
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
      <Route path="navigation-menu" element={<Navigate to={`${PLATFORM_UI.plans}?tab=menu`} replace />} />
      <Route path="companies" element={<Navigate to={PLATFORM_UI.subscribers} replace />} />
      <Route path="menu-plans" element={<Navigate to={`${PLATFORM_UI.plans}?tab=menu`} replace />} />
      <Route path="menu-builder" element={<Navigate to={`${PLATFORM_UI.plans}?tab=menu`} replace />} />
      <Route path="features" element={<Navigate to={`${PLATFORM_UI.plans}?tab=plans`} replace />} />
      <Route path="forms" element={<Navigate to={PLATFORM_UI.overview} replace />} />
      <Route path="growth" element={<Navigate to={PLATFORM_UI.overview} replace />} />
    </Route>
  );
}

/** Redirects de bookmarks externos (sin API legacy). */
export function platformBookmarkRedirectRoutes() {
  return <Route path="/companies/*" element={<PlatformBookmarkRedirect />} />;
}

/** Legacy UI prefix — solo redirect, no enlazar en producto. */
export function platformLegacyUiRedirectRoutes() {
  return (
    <>
      <Route path="/superadmin" element={<PlatformLegacyUiRedirect />} />
      <Route path="/superadmin/*" element={<PlatformLegacyUiRedirect />} />
    </>
  );
}
