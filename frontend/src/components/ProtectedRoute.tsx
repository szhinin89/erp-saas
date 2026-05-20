import { Navigate, Outlet } from 'react-router-dom';
import { useAuthStore } from '../store/authStore';
import { GLOBAL_SUBSCRIBER_ID } from '../constants/subscriberIds';
import { useDeployment } from '../deployment/DeploymentContext';

export function ProtectedRoute() {
  const { superAdminPanelEnabled } = useDeployment();
  const isAuthenticated = useAuthStore((s) => s.isAuthenticated);
  const hasHydrated = useAuthStore((s) => s.hasHydrated);
  const user = useAuthStore((s) => s.user);
  const logout = useAuthStore((s) => s.logout);

  // Avoid redirecting before Zustand persist rehydrates.
  if (!hasHydrated) return null;

  // Fallback: if token exists but state isn't set yet, allow rendering.
  const raw = localStorage.getItem('auth-storage');
  const token = raw ? (JSON.parse(raw)?.state?.token as string | undefined) : undefined;

  if (!superAdminPanelEnabled && (user?.role ?? '') === 'SuperAdmin') {
    logout();
    return <Navigate to="/login" replace />;
  }

  // SuperAdmin global: solo panel SuperAdmin y administración de empresas (sin rutas operativas de tenant).
  if (superAdminPanelEnabled && (user?.role ?? '') === 'SuperAdmin' && (user?.subscriberId ?? '') === GLOBAL_SUBSCRIBER_ID) {
    const path = window.location.pathname;
    const allowed = path.startsWith('/superadmin') || path === '/companies' || path.startsWith('/companies/');
    return allowed ? <Outlet /> : <Navigate to="/superadmin/overview" replace />;
  }

  const needsCompany =
    isAuthenticated &&
    user &&
    user.role !== 'SuperAdmin' &&
    user.subscriberId &&
    !user.companyId;

  if (needsCompany) {
    const path = window.location.pathname;
    if (path !== '/select-company') {
      return <Navigate to="/select-company" replace />;
    }
  }

  return isAuthenticated || token ? <Outlet /> : <Navigate to="/login" replace />;
}
