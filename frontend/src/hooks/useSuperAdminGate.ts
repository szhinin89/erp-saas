import { useMemo } from 'react';
import { useAuthStore } from '../store/authStore';
import { GLOBAL_SUBSCRIBER_ID } from '../constants/subscriberIds';

/** Estado de acceso a rutas / funciones solo SuperAdmin. */
export function useSuperAdminGate() {
  const user = useAuthStore((s) => s.user);

  return useMemo(() => {
    const isSuperAdmin = user?.role === 'SuperAdmin';
    const subscriberId = user?.subscriberId ?? '';
    const hasSelectedSubscriber = Boolean(subscriberId && subscriberId !== GLOBAL_SUBSCRIBER_ID);
    return { user, isSuperAdmin, hasSelectedSubscriber };
  }, [user]);
}
