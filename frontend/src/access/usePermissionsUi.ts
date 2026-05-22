import { useCallback } from 'react';
import { useAuthStore } from '../store/authStore';
import { usePermissionsStore } from '../store/permissionsStore';
import {
  canShowPermissionKey,
  hasUnrestrictedPermissionSnapshot,
  isTenantAdminRole,
  TENANT_ADMIN_ROLES,
} from './permissionUi';

type UsePermissionsUiOptions = {
  /** Restrict empty-snapshot fallback to Admin only (not SuperAdmin). */
  adminOnlyFallback?: boolean;
};

/**
 * Central hook for permission-driven UI rendering.
 * Data source: permissions store (synced from backend `/api/admin/iam/me/permissions`).
 */
export function usePermissionsUi(options?: UsePermissionsUiOptions) {
  const permissions = usePermissionsStore((s) => s.permissions);
  const has = usePermissionsStore((s) => s.has);
  const hasHydrated = usePermissionsStore((s) => s.hasHydrated);
  const enabledModules = usePermissionsStore((s) => s.enabledModules);
  const planCode = usePermissionsStore((s) => s.planCode);
  const planName = usePermissionsStore((s) => s.planName);
  const role = useAuthStore((s) => s.user?.role ?? '');

  const adminFallbackRoles = options?.adminOnlyFallback ? (['Admin'] as const) : TENANT_ADMIN_ROLES;

  const canShow = useCallback(
    (permissionKey: string) =>
      canShowPermissionKey(permissionKey, {
        permissions,
        has,
        role,
        adminFallbackRoles,
      }),
    [permissions, has, role, adminFallbackRoles],
  );

  const hasUnrestrictedAccess = hasUnrestrictedPermissionSnapshot(permissions);
  const isTenantAdmin = isTenantAdminRole(role);

  /** Skip permission hydration loading gate (legacy Admin/SuperAdmin UX). */
  const skipPermissionHydrationWait = hasUnrestrictedAccess || isTenantAdmin;

  return {
    canShow,
    has,
    permissions,
    hasHydrated,
    enabledModules,
    planCode,
    planName,
    role,
    hasUnrestrictedAccess,
    isTenantAdmin,
    skipPermissionHydrationWait,
  };
}
