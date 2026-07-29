import { Navigate, Outlet, useLocation } from "react-router-dom";
import { useAuthStore } from "../store/authStore";
import { getAccessToken } from "../lib/session/authTokenMemory";
import { CompanyOperationalStatus } from "../types/auth";

/** Rutas ERP operativas que exigen companyId. */
function requiresCompanyContext(path: string): boolean {
  if (path === "/select-company") return false;
  if (path === "/dashboard") return true;
  const erpPrefixes = [
    "/inventory/",
    "/inventario/",
    "/settings/",
    "/configuracion/",
    "/catalog/",
    "/reportes/",
    "/rrhh",
    "/admin/",
    "/security",
  ];
  return erpPrefixes.some((p) => path === p || path.startsWith(p));
}

export function ProtectedRoute() {
  const isAuthenticated = useAuthStore((s) => s.isAuthenticated);
  const hasHydrated = useAuthStore((s) => s.hasHydrated);
  const user = useAuthStore((s) => s.user);
  const location = useLocation();

  if (!hasHydrated) return null;

  const token = getAccessToken();
  const path = location.pathname;

  // Suspended company — block ERP access
  if (
    isAuthenticated &&
    !!user?.companyId &&
    user.operationalStatus === CompanyOperationalStatus.Suspended &&
    requiresCompanyContext(path)
  ) {
    return <Navigate to="/dashboard" replace />;
  }

  const needsCompany =
    isAuthenticated && user && !!user.tenantId && !user.companyId;

  if (needsCompany && (path === "/dashboard" || requiresCompanyContext(path))) {
    return <Navigate to="/select-company" replace />;
  }

  return isAuthenticated || token ? (
    <Outlet />
  ) : (
    <Navigate to="/login" replace />
  );
}
