import { Navigate } from 'react-router-dom';
import { useAuthStore } from '../store/authStore';
import { GLOBAL_SUBSCRIBER_ID } from '../constants/subscriberIds';
import { useSuperAdminGate } from '../hooks/useSuperAdminGate';

function normalizeUuid(uuid: string): string {
  return uuid.replace(/-/g, '').toLowerCase();
}

/** Ruta inicial según modo: Platform → superadmin; Subscriber → cuenta SaaS. */
export function HomeRedirect() {
  const user = useAuthStore((s) => s.user);
  const { isSuperAdmin } = useSuperAdminGate();

  const isGlobalPlatform =
    isSuperAdmin &&
    normalizeUuid(user?.subscriberId ?? '') === normalizeUuid(GLOBAL_SUBSCRIBER_ID);

  if (isGlobalPlatform) {
    return <Navigate to="/superadmin/overview" replace />;
  }

  const hasSubscriber =
    !!user?.subscriberId &&
    normalizeUuid(user.subscriberId) !== normalizeUuid(GLOBAL_SUBSCRIBER_ID);

  if (hasSubscriber) {
    return <Navigate to="/saas/overview" replace />;
  }

  return <Navigate to="/login" replace />;
}
