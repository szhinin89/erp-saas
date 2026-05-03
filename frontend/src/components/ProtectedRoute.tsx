import { Navigate, Outlet } from 'react-router-dom';
import { useAuthStore } from '../store/authStore';
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

  // SuperAdmin global: rutas de panel SaaS + empresas + seguridad (el menú principal enlaza fuera de /superadmin).
  if (
    superAdminPanelEnabled &&
    (user?.role ?? '') === 'SuperAdmin' &&
    (user?.tenantId ?? '') === '00000000-0000-0000-0000-000000000000'
  ) {
    const path = window.location.pathname;
    const allowed =
      path.startsWith('/superadmin') ||
      path === '/companies' ||
      path.startsWith('/companies/') ||
      path === '/security' ||
      path.startsWith('/security/') ||
      path.startsWith('/access') ||
      path.startsWith('/profiles') ||
      path.startsWith('/saas/') ||
      path === '/compras' ||
      path.startsWith('/compras/') ||
      path === '/rrhh' ||
      path.startsWith('/rrhh/');
    return allowed ? <Outlet /> : <Navigate to="/superadmin" replace />;
  }

  return isAuthenticated || token ? <Outlet /> : <Navigate to="/login" replace />;
}
