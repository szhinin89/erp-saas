import { useMemo } from 'react';
import { useAuthStore } from '../store/authStore';
import { GLOBAL_SUBSCRIBER_ID } from '../constants/subscriberIds';
import { isJwtPlatformOperatorRole, JWT_PLATFORM_OPERATOR_ROLE } from '../constants/platformAuth';

function normalizeUuid(uuid: string): string {
  return uuid.replace(/-/g, '').toLowerCase();
}

/** Modo runtime actual: Platform global, cuenta SaaS (subscriber) u operación ERP (company). */
export function useRuntimeMode(): RuntimeMode {
  const user = useAuthStore((s) => s.user);

  return useMemo(() => {
    if (!user) return 'unknown';

    const isPlatformOperatorSession =
      (user.userType === 'Platform' && user.platformRole === JWT_PLATFORM_OPERATOR_ROLE) ||
      (isJwtPlatformOperatorRole(user.role) &&
        normalizeUuid(user.subscriberId ?? '') === normalizeUuid(GLOBAL_SUBSCRIBER_ID));

    if (isPlatformOperatorSession && !user.companyId) return 'platform';

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

export type RuntimeMode = 'platform' | 'subscriber' | 'company' | 'unknown';
