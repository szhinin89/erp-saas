import { useMemo } from 'react';
import { useAuthStore } from '../store/authStore';
import { GLOBAL_TENANT_ID } from '../constants/tenantIds';

/** Estado de acceso a rutas / funciones solo SuperAdmin. */
export function useSuperAdminGate() {
  const user = useAuthStore((s) => s.user);

  return useMemo(() => {
    const isSuperAdmin = user?.role === 'SuperAdmin';
    const tenantId = user?.tenantId ?? '';
    const hasSelectedTenant = Boolean(tenantId && tenantId !== GLOBAL_TENANT_ID);
    return { user, isSuperAdmin, hasSelectedTenant };
  }, [user]);
}
