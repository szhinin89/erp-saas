import { useMemo } from 'react';
import { useAuthStore } from '../store/authStore';
import { GLOBAL_SUBSCRIBER_ID } from '../constants/subscriberIds';

export type RuntimeMode = 'platform' | 'subscriber' | 'company' | 'unknown';

function normalizeUuid(uuid: string): string {
  return uuid.replace(/-/g, '').toLowerCase();
}

/** Modo runtime actual: Platform global, cuenta SaaS (subscriber) u operación ERP (company). */
export function useRuntimeMode(): RuntimeMode {
  const user = useAuthStore((s) => s.user);

  return useMemo(() => {
    if (!user) return 'unknown';

    const isPlatformSuperAdmin =
      (user.userType === 'Platform' && user.platformRole === 'SuperAdmin') ||
      (user.role === 'SuperAdmin' &&
        normalizeUuid(user.subscriberId ?? '') === normalizeUuid(GLOBAL_SUBSCRIBER_ID));

    if (isPlatformSuperAdmin && !user.companyId) return 'platform';

    if (user.companyId) return 'company';

    if (
      user.subscriberId &&
      normalizeUuid(user.subscriberId) !== normalizeUuid(GLOBAL_SUBSCRIBER_ID)
    ) {
      return 'subscriber';
    }

    return 'unknown';
  }, [user]);
}
