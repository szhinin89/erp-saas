import { create } from 'zustand';
import { createJSONStorage, persist } from 'zustand/middleware';
import { ACCESS_BOOTSTRAP_STORAGE_KEY } from '../lib/session/sessionStorageKeys';
import { zustandSessionStorage } from '../lib/session/zustandSessionStorage';
import type { AccessibleSubscriber, BootstrapLoginResponse } from '../types/access';

interface AccessState {
  bootstrapToken: string | null;
  bootstrapUser: { userId: string; fullName: string; email: string } | null;
  subscribers: AccessibleSubscriber[];
  setBootstrap: (r: BootstrapLoginResponse) => void;
  clearBootstrap: () => void;
}

export const useAccessStore = create<AccessState>()(
  persist(
    (set) => ({
      bootstrapToken: null,
      bootstrapUser: null,
      subscribers: [],
      setBootstrap: (r) =>
        set({
          bootstrapToken: r.bootstrapToken,
          bootstrapUser: { userId: r.userId, fullName: r.fullName, email: r.email },
          subscribers: r.subscribers ?? [],
        }),
      clearBootstrap: () =>
        set({
          bootstrapToken: null,
          bootstrapUser: null,
          subscribers: [],
        }),
    }),
    {
      name: ACCESS_BOOTSTRAP_STORAGE_KEY,
      storage: createJSONStorage(() => zustandSessionStorage),
    },
  ),
);
