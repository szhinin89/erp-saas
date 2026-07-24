import { create } from 'zustand';
import { createJSONStorage, persist } from 'zustand/middleware';
import { ACCESS_BOOTSTRAP_STORAGE_KEY } from '../lib/session/sessionStorageKeys';
import { zustandSessionStorage } from '../lib/session/zustandSessionStorage';
import type { AccessibleTenant, BootstrapLoginResponse } from '../types/access';

interface AccessState {
  bootstrapToken: string | null;
  bootstrapUser: { userId: string; fullName: string; email: string } | null;
  tenants: AccessibleTenant[];
  setBootstrap: (r: BootstrapLoginResponse) => void;
  clearBootstrap: () => void;
}

export const useAccessStore = create<AccessState>()(
  persist(
    (set) => ({
      bootstrapToken: null,
      bootstrapUser: null,
      tenants: [],
      setBootstrap: (r) =>
        set({
          bootstrapToken: r.bootstrapToken,
          bootstrapUser: { userId: r.userId, fullName: r.fullName, email: r.email },
          tenants: r.tenants ?? [],
        }),
      clearBootstrap: () =>
        set({
          bootstrapToken: null,
          bootstrapUser: null,
          tenants: [],
        }),
    }),
    {
      name: ACCESS_BOOTSTRAP_STORAGE_KEY,
      storage: createJSONStorage(() => zustandSessionStorage),
    },
  ),
);
