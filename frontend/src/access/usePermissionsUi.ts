import { useAuthStore } from '../store/authStore';
import { useSessionStore } from '../store/sessionStore';
import { isAdminRole } from './permissionUi';

/**
 * Permission check for UI rendering (button/section visibility).
 * Reads from sessionStore.authorization.permissions (loaded via GET /api/v1/session/context).
 * Admin role gets wildcard access; non-admin users are checked against the effective permissions list.
 * Navigation rendering is server-driven via GET /api/me/menu — this hook controls page-level
 * and action-level visibility only.
 */
export function usePermissionsUi() {
  const role = useAuthStore((s) => s.user?.role);
  const authorization = useSessionStore((s) => s.authorization);
  const isLoaded = useSessionStore((s) => s.isLoaded);
  const admin = isAdminRole(role);

  const permissions = authorization?.permissions ?? [];

  const canShow = (permissionKey: string): boolean => {
    if (admin) return true;
    if (!isLoaded || permissions.length === 0) return true;
    if (permissions.includes('*')) return true;
    return permissions.includes(permissionKey);
  };

  return {
    canShow,
    has: canShow,
    isAdminRole: admin,
  };
}
