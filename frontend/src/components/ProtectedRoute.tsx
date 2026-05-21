import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { useAuthStore } from '../store/authStore';
import { GLOBAL_SUBSCRIBER_ID } from '../constants/subscriberIds';
import { useDeployment } from '../deployment/DeploymentContext';
import { useSuperAdminGate } from '../hooks/useSuperAdminGate';

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
  const { superAdminPanelEnabled } = useDeployment();
  const { isSuperAdmin } = useSuperAdminGate();
  const isAuthenticated = useAuthStore((s) => s.isAuthenticated);
  const hasHydrated = useAuthStore((s) => s.hasHydrated);
  const user = useAuthStore((s) => s.user);
  const logout = useAuthStore((s) => s.logout);
  const location = useLocation();

  if (!hasHydrated) return null;

  const raw = localStorage.getItem('auth-storage');
  const token = raw ? (JSON.parse(raw)?.state?.token as string | undefined) : undefined;

  if (!superAdminPanelEnabled && (user?.role ?? '') === 'SuperAdmin') {
    logout();
    return <Navigate to="/login" replace />;
  }

  const path = location.pathname;
  const isGlobalPlatform =
    isSuperAdmin &&
    normalizeUuid(user?.subscriberId ?? '') === normalizeUuid(GLOBAL_SUBSCRIBER_ID);

  if (superAdminPanelEnabled && isGlobalPlatform) {
    const allowed =
      path.startsWith('/superadmin') ||
      path === '/companies' ||
      path.startsWith('/companies/');
    return allowed ? <Outlet /> : <Navigate to="/superadmin/overview" replace />;
  }

  const hasSubscriber =
    !!user?.subscriberId &&
    normalizeUuid(user.subscriberId) !== normalizeUuid(GLOBAL_SUBSCRIBER_ID);

  const isPlatformUser = user?.userType === 'Platform' && user?.platformRole === 'SuperAdmin';

  if (path.startsWith('/superadmin') && !isGlobalPlatform) {
    return <Navigate to={hasSubscriber ? '/saas/overview' : '/login'} replace />;
  }

  if (path.startsWith('/saas/') && (isPlatformUser || !hasSubscriber)) {
    return <Navigate to="/superadmin/overview" replace />;
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
