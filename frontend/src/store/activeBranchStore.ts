import { create } from 'zustand';
import { createJSONStorage, persist } from 'zustand/middleware';
import { ACTIVE_BRANCH_STORAGE_KEY } from '../lib/session/sessionStorageKeys';
import { zustandSessionStorage } from '../lib/session/zustandSessionStorage';
import type { SessionContextDto } from '../types/session';

type ActiveBranch = SessionContextDto['branch'];

interface ActiveBranchState {
  branch: ActiveBranch;
  setBranch: (branch: ActiveBranch) => void;
  clear: () => void;
}

/**
 * Sucursal activa de la sesión (Fase I-2 — transporte). Independiente de authStore (identidad)
 * y de sessionStore (snapshot en memoria del bootstrap): esta store es la única fuente de la
 * sucursal que el cliente adjunta como X-Branch-Id en cada petición, persistida en
 * sessionStorage (pestaña) igual que authStore.
 */
export const useActiveBranchStore = create<ActiveBranchState>()(
  persist(
    (set) => ({
      branch: null,
      setBranch: (branch) => set({ branch }),
      clear: () => set({ branch: null }),
    }),
    {
      name: ACTIVE_BRANCH_STORAGE_KEY,
      storage: createJSONStorage(() => zustandSessionStorage),
    },
  ),
);
