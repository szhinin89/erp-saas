import { create } from "zustand";
import { createJSONStorage, persist } from "zustand/middleware";
import { SAAS_SESSION_STORAGE_PREFIX } from "../lib/session/sessionStorageKeys";
import { zustandSessionStorage } from "../lib/session/zustandSessionStorage";

/** Bajo el prefijo erp.saas.* — fullLogout la limpia vía el barrido genérico por prefijo. */
export const SESSION_IDLE_STORAGE_KEY = `${SAAS_SESSION_STORAGE_PREFIX}session.idle`;

/** No persistir en cada mousemove — como mucho una escritura cada tantos ms. */
const ACTIVITY_PERSIST_THROTTLE_MS = 5_000;

interface SessionIdleState {
  isLocked: boolean;
  /** Epoch ms de la última actividad real registrada. */
  lastActivityAt: number;
  lock: () => void;
  unlock: () => void;
  recordActivity: () => void;
  reset: () => void;
}

/**
 * Estado de bloqueo por inactividad (Fase 3) + reautenticación (Fase 4).
 *
 * Persistido en sessionStorage (pestaña) a propósito — a diferencia del diseño original de
 * Fase 3, un F5 durante una sesión bloqueada NO debe revertir el bloqueo: sin esto, recargar
 * la página perdía por completo la barrera de inactividad y permitía seguir trabajando sin
 * contraseña en una sesión abandonada (ZH-AUTH-SESSION-PERSISTENCE-QA-11). `useIdleTimeout`
 * usa `lastActivityAt` al montar para retomar el conteo (o bloquear de inmediato si ya venció)
 * en vez de reiniciar siempre el reloj completo.
 */
export const useSessionIdleStore = create<SessionIdleState>()(
  persist(
    (set, get) => ({
      isLocked: false,
      lastActivityAt: Date.now(),

      lock: () => set({ isLocked: true }),

      unlock: () => set({ isLocked: false, lastActivityAt: Date.now() }),

      recordActivity: () => {
        const { isLocked, lastActivityAt } = get();
        if (isLocked) return;
        const now = Date.now();
        if (now - lastActivityAt < ACTIVITY_PERSIST_THROTTLE_MS) return;
        set({ lastActivityAt: now });
      },

      reset: () => set({ isLocked: false, lastActivityAt: Date.now() }),
    }),
    {
      name: SESSION_IDLE_STORAGE_KEY,
      storage: createJSONStorage(() => zustandSessionStorage),
    },
  ),
);
