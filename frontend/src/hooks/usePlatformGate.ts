import { useMemo } from 'react';
import { useAuthStore } from '../store/authStore';
import { GLOBAL_SUBSCRIBER_ID } from '../constants/subscriberIds';
import { isJwtPlatformOperatorRole, JWT_PLATFORM_OPERATOR_ROLE } from '../constants/platformAuth';

function normalizeUuid(uuid: string): string {
  return uuid.replace(/-/g, '').toLowerCase();
}

/** Estado de acceso al Platform Control Plane (operador global). */
export function usePlatformGate() {
  const user = useAuthStore((s) => s.user);

  return useMemo(() => {
    const isPlatformUser =
      user?.userType === 'Platform' && user?.platformRole === JWT_PLATFORM_OPERATOR_ROLE;
    const isLegacyPlatformOperator =
      isJwtPlatformOperatorRole(user?.role) &&
      normalizeUuid(user?.subscriberId ?? '') === normalizeUuid(GLOBAL_SUBSCRIBER_ID);
    const isPlatformOperator = isPlatformUser || isLegacyPlatformOperator;
    const subscriberId = user?.subscriberId ?? '';
    const hasSelectedSubscriber = Boolean(subscriberId && subscriberId !== GLOBAL_SUBSCRIBER_ID);
    return {
      user,
      isPlatformOperator,
      hasSelectedSubscriber,
    };
  }, [user]);
}
