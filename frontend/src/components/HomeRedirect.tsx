import { Navigate } from "react-router-dom";
import { useAuthStore } from "../store/authStore";
import { GLOBAL_TENANT_ID } from "../access/permissionUi";

export function HomeRedirect() {
  const user = useAuthStore((s) => s.user);

  if (user?.tenantId === GLOBAL_TENANT_ID && !user.companyId) {
    return <Navigate to="/admin-core/dashboard" replace />;
  }

  if (user?.tenantId) {
    return <Navigate to="/dashboard" replace />;
  }

  return <Navigate to="/login" replace />;
}
