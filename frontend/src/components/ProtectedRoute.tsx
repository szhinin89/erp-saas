import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { useAuthStore } from '../store/authStore';
import { GLOBAL_SUBSCRIBER_ID } from '../constants/subscriberIds';
import { isJwtPlatformOperatorRole } from '../constants/platformAuth';
import { useDeployment } from '../deployment/DeploymentContext';
import { usePlatformGate } from '../hooks/usePlatformGate';
import { fullLogout } from '../lib/session/fullLogout';
import { getAccessToken } from '../lib/session/authTokenMemory';
import { CompanyOperationalStatus } from '../types/auth';

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

/** Rutas ERP operativas que exigen companyId y onboarding completado. */
function requiresCompanyContext(path: string): boolean {
  if (isSubscriberAccountPath(path)) return false;
  if (path === '/dashboard') return true;
  const erpPrefixes = [
    '/sales/',
    '/ventas/',
    '/inventory/',
    '/inventario/',
    '/inventory/products',
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
  const hasHydrated     = useAuthStore((s) => s.hasHydrated);
  const user            = useAuthStore((s) => s.user);
  const location        = useLocation();

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
    const allowed = path.startsWith('/platform');
    return allowed ? <Outlet /> : <Navigate to="/platform/overview" replace />;
  }

  const hasSubscriber =
    !!user?.subscriberId &&
    normalizeUuid(user.subscriberId) !== normalizeUuid(GLOBAL_SUBSCRIBER_ID);

  if (path.startsWith('/platform') && !isGlobalPlatform) {
    return <Navigate to={hasSubscriber ? '/saas/overview' : '/login'} replace />;
  }

  if (path.startsWith('/saas/') && !hasSubscriber) {
    return <Navigate to="/platform/overview" replace />;
  }

  // ── ONBOARDING GUARD (proactive) ─────────────────────────────────────────────
  // Single authority for the ERP vs /onboarding/company routing decision.
  // Fires BEFORE any ERP child component mounts — 0 dashboard requests, 0 race conditions.
  //
  // Condition:
  //   - Company is selected (companyId exists in JWT/store)
  //   - Onboarding is NOT complete (onboardingCompleted === false)
  //   - Target path requires ERP company context
  //
  // Does NOT depend on the 403 company_onboarding_required response.
  // The CompanyOnboardingMiddleware remains as backend defense-in-depth.
  const companySelectedButNotOnboarded =
    isAuthenticated &&
    !!user?.companyId &&
    user.onboardingCompleted === false;

  if (companySelectedButNotOnboarded && requiresCompanyContext(path)) {
    return <Navigate to="/onboarding/company" replace />;
  }

  // Suspended company — block ERP access
  if (
    isAuthenticated &&
    !!user?.companyId &&
    user.operationalStatus === CompanyOperationalStatus.Suspended &&
    requiresCompanyContext(path)
  ) {
    return <Navigate to="/saas/overview" replace />;
  }
  // ─────────────────────────────────────────────────────────────────────────────

  const needsCompany =
    isAuthenticated &&
    user &&
    hasSubscriber &&
    !user.companyId;

  if (needsCompany) {
    if (path === '/dashboard' || requiresCompanyContext(path)) {
      return <Navigate to="/select-company" replace />;
    }
  }

  return isAuthenticated || token ? <Outlet /> : <Navigate to="/login" replace />;
}
