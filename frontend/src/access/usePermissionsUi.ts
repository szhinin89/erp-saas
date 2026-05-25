import { useCallback } from 'react';
import { useAuthStore } from '../store/authStore';
import { usePermissionsStore } from '../store/permissionsStore';
import {
  canShowPermissionKey,
  hasUnrestrictedPermissionSnapshot,
  isSubscriberAdminRole,
} from './permissionUi';

/**
 * Central hook for permission-driven UI rendering.
 * Data source: permissions store (synced from backend `/api/admin/iam/me/permissions`).
 */
export function usePermissionsUi() {
  const permissions = usePermissionsStore((s) => s.permissions);
  const has = usePermissionsStore((s) => s.has);
  const hasHydrated = usePermissionsStore((s) => s.hasHydrated);
  const permissionsSyncing = usePermissionsStore((s) => s.permissionsSyncing);
  const enabledModules = usePermissionsStore((s) => s.enabledModules);
  const planCode = usePermissionsStore((s) => s.planCode);
  const planName = usePermissionsStore((s) => s.planName);
  const role = useAuthStore((s) => s.user?.role ?? '');

  const canShow = useCallback(
    (permissionKey: string) =>
      canShowPermissionKey(permissionKey, {
        permissions,
        has,
        permissionsSyncing,
      }),
    [permissions, has, permissionsSyncing],
  );

  const hasUnrestrictedAccess = hasUnrestrictedPermissionSnapshot(permissions);
  const isSubscriberAdmin = isSubscriberAdminRole(role);

  /** Muestra loading gate hasta hidratar permisos o terminar sync (sin fallback Admin). */
  const skipPermissionHydrationWait = hasUnrestrictedAccess;

  const isPermissionsReady = hasHydrated && !permissionsSyncing;

  return {
    canShow,
    has,
    permissions,
    hasHydrated,
    permissionsSyncing,
    isPermissionsReady,
    enabledModules,
    planCode,
    planName,
    role,
    hasUnrestrictedAccess,
    isSubscriberAdmin,
    skipPermissionHydrationWait,
  };
}
