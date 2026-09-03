import { Navigate, Outlet } from "react-router-dom";
import { useAuthStore } from "../store/authStore";
import { getAccessToken } from "../lib/session/authTokenMemory";
import { GLOBAL_TENANT_ID } from "../access/permissionUi";

/**
 * Guarda simétrica a ProtectedRoute: solo deja pasar sesiones de AdminGlobalCore genuinas
 * (tenant_id == GLOBAL_TENANT_ID, sin companyId, rol Admin). Un AdminEmpresa (tenant real)
 * nunca entra a /admin-core/* — se lo redirige a /admin-core/login, igual que un usuario
 * global no autenticado.
 */
export function AdminCoreProtectedRoute() {
  const isAuthenticated = useAuthStore((s) => s.isAuthenticated);
  const hasHydrated = useAuthStore((s) => s.hasHydrated);
  const user = useAuthStore((s) => s.user);

  if (!hasHydrated) return null;

  const token = getAccessToken();
  const isGlobalAdminSession =
    (isAuthenticated || !!token) &&
    user?.tenantId === GLOBAL_TENANT_ID &&
    !user.companyId &&
    user.role === "Admin";

  return isGlobalAdminSession ? (
    <Outlet />
  ) : (
    <Navigate to="/admin-core/login" replace />
  );
}
