import { create } from "zustand";

interface SessionIdleState {
  isLocked: boolean;
  lock: () => void;
  unlock: () => void;
}

/**
 * Estado de bloqueo por inactividad (Fase 3). Deliberadamente sin `persist`: es puramente
 * de la pestaña activa, nunca debe sobrevivir un reload ni guardarse en storage — un
 * "bloqueado" fantasma tras recargar sería peor UX que simplemente perder el bloqueo.
 */
export const useSessionIdleStore = create<SessionIdleState>((set) => ({
  isLocked: false,
  lock: () => set({ isLocked: true }),
  unlock: () => set({ isLocked: false }),
}));
