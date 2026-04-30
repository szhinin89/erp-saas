import { create } from 'zustand';
import { persist } from 'zustand/middleware';

interface PermissionsState {
  permissions: string[];
  setPermissions: (permissions: string[]) => void;
  clearPermissions: () => void;
  has: (permissionKey: string) => boolean;
  hasHydrated: boolean;
}

export const usePermissionsStore = create<PermissionsState>()(
  persist(
    (set, get) => ({
      permissions: [],
      hasHydrated: false,
      setPermissions: (permissions) => set({ permissions: permissions ?? [] }),
      clearPermissions: () => set({ permissions: [] }),
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

