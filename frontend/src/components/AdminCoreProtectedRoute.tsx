import { Navigate, Outlet } from "react-router-dom";
import { useAuthStore } from "../store/authStore";
import { getAccessToken } from "../lib/session/authTokenMemory";
import { GLOBAL_TENANT_ID } from "../access/permissionUi";

/**
 * Guarda simétrica a ProtectedRoute: solo deja pasar sesiones de AdminGlobalCore genuinas
 * (tenant_id == GLOBAL_TENANT_ID, sin companyId, rol Admin).
 *
 * BUGFIX (AdminGlobalCore → "Ingresar a esta empresa"): operate-company reemplaza la sesión
 * global por una operativa (tenant_id/companyId reales) y navega a /dashboard, pero
 * AdminCoreProtectedRoute puede re-renderizar con el usuario YA operativo mientras la ruta
 * activa todavía es /admin-core/dashboard (el cambio de location del navigate() explícito no es
 * atómico con la actualización del store). Antes, esa combinación (sesión operativa real,
 * todavía montado aquí) caía al mismo fallback que "no autenticado" → `/admin-core/login`,
 * pisando la navegación a /dashboard en plena carrera. Se distingue explícitamente ese caso: una
 * sesión operativa real nunca es un fallo de auth de AdminCore, es la transición exitosa fuera de
 * él — debe ir a /dashboard (que ProtectedRoute ya sabe manejar), no a /admin-core/login. Esto
 * hace que el destino final no dependa de qué navegación "gane" la carrera: ambas apuntan a
 * /dashboard. Un AdminEmpresa (tenant real) que navega manualmente a /admin-core/* toma la misma
 * rama — se lo manda a su propio dashboard operativo en vez de a un login que no necesita.
 */
export function AdminCoreProtectedRoute() {
  const isAuthenticated = useAuthStore((s) => s.isAuthenticated);
  const hasHydrated = useAuthStore((s) => s.hasHydrated);
  const user = useAuthStore((s) => s.user);

  if (!hasHydrated) return null;

  const token = getAccessToken();
  const hasSession = (isAuthenticated || !!token) && !!user;

  const isGlobalAdminSession =
    hasSession &&
    user.tenantId === GLOBAL_TENANT_ID &&
    !user.companyId &&
    user.role === "Admin";

  if (isGlobalAdminSession) {
    return <Outlet />;
  }

  if (hasSession && user.tenantId !== GLOBAL_TENANT_ID) {
    return <Navigate to="/dashboard" replace />;
  }

  return <Navigate to="/admin-core/login" replace />;
}
