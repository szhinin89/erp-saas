import { create } from 'zustand';
import { createJSONStorage, persist } from 'zustand/middleware';
import { PERMISSIONS_STORAGE_KEY } from '../lib/session/sessionStorageKeys';
import { zustandSessionStorage } from '../lib/session/zustandSessionStorage';

/**
 * UI cache of backend authorization snapshot (GET /api/admin/iam/me/permissions + entitlements).
 * Not a security boundary — API enforces access.
 */

/** Alinea claves del menú/catálogo (`perm:…`) con las del perfil (`inventario.*.view`). */
export function normalizePolicyPermissionKey(key: string): string {
  const k = (key ?? '').trim().toLowerCase();
  if (k.startsWith('perm:')) return k.slice(5);
  return k;
}

interface PermissionsState {
  permissions: string[];
  planCode: string | null;
  planName: string | null;
  enabledModules: string[];
  enabledFeatures: string[];
  limits: Record<string, number | null>;
  hasModuleRestrictions: boolean;
  setPermissionSnapshot: (payload: {
    permissions: string[];
    planCode?: string | null;
    enabledModules?: string[];
  }) => void;
  setEntitlementsSnapshot: (payload: {
    permissions: string[];
    planCode?: string | null;
    planName?: string | null;
    enabledModules: string[];
    enabledFeatures?: string[];
    limits?: Record<string, number | null>;
    hasModuleRestrictions: boolean;
  }) => void;
  clearPermissions: () => void;
  has: (permissionKey: string) => boolean;
  hasHydrated: boolean;
}

export const usePermissionsStore = create<PermissionsState>()(
  persist(
    (set, get) => ({
      permissions: [],
      planCode: null,
      planName: null,
      enabledModules: [],
      enabledFeatures: [],
      limits: {},
      hasModuleRestrictions: true,
      hasHydrated: false,
      setPermissionSnapshot: ({ permissions, planCode = null, enabledModules = [] }) =>
        set({
          permissions: permissions ?? [],
          planCode: planCode ?? null,
          enabledModules: enabledModules ?? [],
        }),
      setEntitlementsSnapshot: ({
        permissions,
        planCode = null,
        planName = null,
        enabledModules,
        enabledFeatures = [],
        limits = {},
        hasModuleRestrictions,
      }) =>
        set({
          permissions: permissions ?? [],
          planCode: planCode ?? null,
          planName: planName ?? null,
          enabledModules: enabledModules ?? [],
          enabledFeatures: enabledFeatures ?? [],
          limits: limits ?? {},
          hasModuleRestrictions,
        }),
      clearPermissions: () =>
        set({
          permissions: [],
          planCode: null,
          planName: null,
          enabledModules: [],
          enabledFeatures: [],
          limits: {},
          hasModuleRestrictions: true,
        }),
      has: (permissionKey) => {
        if (permissionKey.startsWith('session:')) return true;
        const perms = get().permissions;
        // Wildcard from backend (Admin/SuperAdmin UX snapshot — not a client-side security rule).
        if (perms.includes('*')) return true;
        const want = normalizePolicyPermissionKey(permissionKey);
        return perms.some((p) => normalizePolicyPermissionKey(p) === want);
      },
    }),
    {
      name: PERMISSIONS_STORAGE_KEY,
      storage: createJSONStorage(() => zustandSessionStorage),
      onRehydrateStorage: () => (state) => {
        if (!state) return;
        state.hasHydrated = true;
      },
    },
  ),
);
