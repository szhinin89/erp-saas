import { create } from 'zustand';
import { persist } from 'zustand/middleware';

/** Alinea claves del menú/catálogo (`perm:…`) con las del perfil (`inventario.*.view`). */
export function normalizePolicyPermissionKey(key: string): string {
  const k = (key ?? '').trim().toLowerCase();
  if (k.startsWith('perm:')) return k.slice(5);
  return k;
}

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
        if (permissionKey.startsWith('session:')) return true;
        const perms = get().permissions;
        if (perms.includes('*')) return true;
        const want = normalizePolicyPermissionKey(permissionKey);
        return perms.some((p) => normalizePolicyPermissionKey(p) === want);
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

