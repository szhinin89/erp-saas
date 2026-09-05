import { create } from "zustand";
import { sessionService } from "../modules/session/api/sessionService";
import { useActiveBranchStore } from "./activeBranchStore";
import type { SessionContextDto } from "../types/session";

interface SessionState {
  identity: SessionContextDto["identity"] | null;
  tenant: SessionContextDto["tenant"] | null;
  authorization: SessionContextDto["authorization"] | null;
  preferences: SessionContextDto["preferences"] | null;
  isLoaded: boolean;
  /** true mientras GET /session/context está en vuelo (bootstrap o refresh post switch-company). */
  isLoading: boolean;
  setSession: (dto: SessionContextDto) => void;
  refresh: () => Promise<void>;
  clear: () => void;
}

/**
 * Estado de sesión enriquecido (identidad, empresa activa, roles/permisos,
 * preferencias) proveniente de GET /api/v1/session/context.
 * Solo en memoria — no se persiste en sessionStorage/localStorage.
 *
 * `isLoading` es consumido por `useBranchGate` para no decidir "sin sucursales" ni abrir el
 * selector mientras este fetch está en curso (ZH-AUTH-SESSION-HYDRATION-BRANCH-MODAL-10) —
 * evita la carrera contra GET /session/available-branches durante el bootstrap.
 */
export const useSessionStore = create<SessionState>()((set) => ({
  identity: null,
  tenant: null,
  authorization: null,
  preferences: null,
  isLoaded: false,
  isLoading: false,

  setSession: (dto) => {
    set({
      identity: dto.identity,
      tenant: dto.tenant,
      authorization: dto.authorization,
      preferences: dto.preferences,
      isLoaded: true,
      isLoading: false,
    });
    useActiveBranchStore.getState().setBranch(dto.branch);
  },

  refresh: async () => {
    set({ isLoading: true });
    try {
      const dto = await sessionService.getContext();
      set({
        identity: dto.identity,
        tenant: dto.tenant,
        authorization: dto.authorization,
        preferences: dto.preferences,
        isLoaded: true,
        isLoading: false,
      });
      useActiveBranchStore.getState().setBranch(dto.branch);
    } catch (err) {
      // No dejamos `isLoading` colgado: si el fetch falla, useBranchGate debe poder
      // seguir con su fallback (GET /session/available-branches) en vez de bloquear
      // el selector de sucursal indefinidamente.
      set({ isLoading: false });
      throw err;
    }
  },

  clear: () => {
    set({
      identity: null,
      tenant: null,
      authorization: null,
      preferences: null,
      isLoaded: false,
      isLoading: false,
    });
    useActiveBranchStore.getState().clear();
  },
}));
