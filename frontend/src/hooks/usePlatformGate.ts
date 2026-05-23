import { useMemo } from 'react';
import { useAuthStore } from '../store/authStore';
import { GLOBAL_SUBSCRIBER_ID } from '../constants/subscriberIds';

function normalizeUuid(uuid: string): string {
  return uuid.replace(/-/g, '').toLowerCase();
}

/** Estado de acceso a rutas / funciones solo SuperAdmin. */
export function usePlatformGate() {
  const user = useAuthStore((s) => s.user);

  return useMemo(() => {
    const isPlatformUser = user?.userType === 'Platform' && user?.platformRole === 'SuperAdmin';
    const isLegacySuperAdmin =
      user?.role === 'SuperAdmin' &&
      normalizeUuid(user?.subscriberId ?? '') === normalizeUuid(GLOBAL_SUBSCRIBER_ID);
    const isSuperAdmin = isPlatformUser || isLegacySuperAdmin;
    const subscriberId = user?.subscriberId ?? '';
    const hasSelectedSubscriber = Boolean(subscriberId && subscriberId !== GLOBAL_SUBSCRIBER_ID);
    return { user, isSuperAdmin, hasSelectedSubscriber };
  }, [user]);
}
