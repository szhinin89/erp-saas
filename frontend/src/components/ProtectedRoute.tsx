import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { useAuthStore } from '../store/authStore';
import { GLOBAL_SUBSCRIBER_ID } from '../constants/subscriberIds';
import { isJwtPlatformOperatorRole } from '../constants/platformAuth';
import { useDeployment } from '../deployment/DeploymentContext';
import { usePlatformGate } from '../hooks/usePlatformGate';
import { fullLogout } from '../lib/session/fullLogout';
import { getAccessToken } from '../lib/session/authTokenMemory';

function normalizeUuid(uuid: string): string {
  return uuid.replace(/-/g, '').toLowerCase();
}

/** Rutas SaaS de cuenta que no exigen companyId operativo. */
function isSubscriberAccountPath(path: string): boolean {
  return (
    path.startsWith('/saas/') ||
    path === '/select-company' ||
    path === '/select-subscriber'
  );
}

/** Rutas ERP operativas que exigen companyId. */
function requiresCompanyContext(path: string): boolean {
  if (isSubscriberAccountPath(path)) return false;
  if (path === '/dashboard') return true;
  const erpPrefixes = [
    '/sales/',
    '/ventas/',
    '/inventory/',
    '/inventario/',
    '/products',
    '/finance/',
    '/accounting',
    '/contabilidad',
    '/purchases/',
    '/compras',
    '/expenses',
    '/gastos',
    '/settings/',
    '/configuracion/',
    '/catalog/',
    '/reportes/',
    '/rrhh',
    '/admin/',
    '/security',
  ];
  return erpPrefixes.some((p) => path === p || path.startsWith(p));
}

export function ProtectedRoute() {
  const { platformPanelEnabled } = useDeployment();
  const { isPlatformOperator } = usePlatformGate();
  const isAuthenticated = useAuthStore((s) => s.isAuthenticated);
  const hasHydrated = useAuthStore((s) => s.hasHydrated);
  const user = useAuthStore((s) => s.user);
  const location = useLocation();

  if (!hasHydrated) return null;

  const token = getAccessToken();

  if (!platformPanelEnabled && isJwtPlatformOperatorRole(user?.role)) {
    fullLogout();
    return <Navigate to="/login" replace />;
  }

  const path = location.pathname;
  const isGlobalPlatform =
    isPlatformOperator &&
    normalizeUuid(user?.subscriberId ?? '') === normalizeUuid(GLOBAL_SUBSCRIBER_ID);

  if (platformPanelEnabled && isGlobalPlatform) {
    const allowed =
      path.startsWith('/platform') ||
      path === '/companies' ||
      path.startsWith('/companies/');
    return allowed ? <Outlet /> : <Navigate to="/platform/overview" replace />;
  }

  const hasSubscriber =
    !!user?.subscriberId &&
    normalizeUuid(user.subscriberId) !== normalizeUuid(GLOBAL_SUBSCRIBER_ID);

  if (path.startsWith('/platform') && !isGlobalPlatform) {
    return <Navigate to={hasSubscriber ? '/saas/overview' : '/login'} replace />;
  }

  // Sin suscriptor activo (global): /saas/* no aplica; con impersonación sí (hasSubscriber).
  if (path.startsWith('/saas/') && !hasSubscriber) {
    return <Navigate to="/platform/overview" replace />;
  }

  const needsCompany =
    isAuthenticated &&
    user &&
    hasSubscriber &&
    !user.companyId;

  if (needsCompany) {
    if (path === '/dashboard') {
      return <Navigate to="/saas/overview" replace />;
    }
    if (requiresCompanyContext(path)) {
      return <Navigate to="/select-company" replace />;
    }
  }

  if (path === '/dashboard' && isAuthenticated && user && !user.companyId) {
    return <Navigate to="/saas/overview" replace />;
  }

  return isAuthenticated || token ? <Outlet /> : <Navigate to="/login" replace />;
}
