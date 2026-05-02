import { create } from 'zustand';
import { persist } from 'zustand/middleware';

interface PermissionsState {
  permissions: string[];
  planCode: string | null;
  enabledModules: string[];
  setPermissionSnapshot: (payload: {
    permissions: string[];
    planCode?: string | null;
    enabledModules?: string[];
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
      enabledModules: [],
      hasHydrated: false,
      setPermissionSnapshot: ({ permissions, planCode = null, enabledModules = [] }) =>
        set({
          permissions: permissions ?? [],
          planCode: planCode ?? null,
          enabledModules: enabledModules ?? [],
        }),
      clearPermissions: () => set({ permissions: [], planCode: null, enabledModules: [] }),
      has: (permissionKey) => {
        const perms = get().permissions;
        if (perms.includes('*')) return true;
        return perms.some((p) => p.toLowerCase() === permissionKey.toLowerCase());
      },
    }),
    {
      name: 'permissions-storage',
      onRehydrateStorage: () => (state) => {
        if (!state) return;
        state.hasHydrated = true;
      },
    }
  )
);

