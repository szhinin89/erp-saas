import { Navigate, Outlet } from 'react-router-dom';
import { useAuthStore } from '../store/authStore';
import { GLOBAL_TENANT_ID } from '../constants/tenantIds';
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
  if (superAdminPanelEnabled && (user?.role ?? '') === 'SuperAdmin' && (user?.tenantId ?? '') === GLOBAL_TENANT_ID) {
    const path = window.location.pathname;
    const allowed = path.startsWith('/superadmin') || path === '/companies' || path.startsWith('/companies/');
    return allowed ? <Outlet /> : <Navigate to="/superadmin" replace />;
  }

  return isAuthenticated || token ? <Outlet /> : <Navigate to="/login" replace />;
}
