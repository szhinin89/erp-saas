/**
 * UI-only permission helpers. Reflect backend snapshot from GET /api/admin/iam/me/permissions.
 * Never used for security enforcement — API is authoritative.
 */

import { useAuthStore } from '../store/authStore';
import { usePermissionsStore, normalizePolicyPermissionKey } from '../store/permissionsStore';

export { normalizePolicyPermissionKey };

/** Roles that receive wildcard permissions from backend (`*`). */
export const TENANT_ADMIN_ROLES = ['Admin', 'SuperAdmin'] as const;

export function isTenantAdminRole(role?: string | null): boolean {
  if (!role) return false;
  return TENANT_ADMIN_ROLES.includes(role as (typeof TENANT_ADMIN_ROLES)[number]);
}

/** True when backend sent unrestricted UI snapshot (`permissions` includes `*`). */
export function hasUnrestrictedPermissionSnapshot(permissions: readonly string[]): boolean {
  return permissions.includes('*');
}

/**
 * UI rendering check for a permission key (backend snapshot + legacy empty-snapshot parity).
 * Prefer {@link usePermissionsUi} in React components.
 */
export function canShowPermissionKey(
  permissionKey: string,
  snapshot: {
    permissions: readonly string[];
    has: (key: string) => boolean;
    role?: string | null;
    adminFallbackRoles?: readonly string[];
  },
): boolean {
  if (snapshot.has(permissionKey)) return true;
  const fallbackRoles = snapshot.adminFallbackRoles ?? TENANT_ADMIN_ROLES;
  if (snapshot.permissions.length === 0 && snapshot.role && fallbackRoles.includes(snapshot.role)) {
    return true;
  }
  return false;
}

/** Nav-only: tenant admins see full menu groups (legacy UI parity; modules still from backend). */
export function shouldUseNavAdminBypass(role?: string | null): boolean {
  return isTenantAdminRole(role);
}

export function readPermissionUiSnapshot() {
  const { permissions, has, hasHydrated, enabledModules, planCode, planName } =
    usePermissionsStore.getState();
  const role = useAuthStore.getState().user?.role ?? '';
  return {
    permissions,
    has,
    hasHydrated,
    enabledModules,
    planCode,
    planName,
    role,
    hasUnrestrictedAccess: hasUnrestrictedPermissionSnapshot(permissions),
    isTenantAdmin: isTenantAdminRole(role),
  };
}
