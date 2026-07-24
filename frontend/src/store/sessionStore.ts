import { create } from 'zustand';
import { sessionService } from '../modules/session/api/sessionService';
import { useActiveBranchStore } from './activeBranchStore';
import type { SessionContextDto } from '../types/session';

interface SessionState {
  identity: SessionContextDto['identity'] | null;
  tenant: SessionContextDto['tenant'] | null;
  authorization: SessionContextDto['authorization'] | null;
  preferences: SessionContextDto['preferences'] | null;
  isLoaded: boolean;
  setSession: (dto: SessionContextDto) => void;
  refresh: () => Promise<void>;
  clear: () => void;
}

/**
 * Estado de sesión enriquecido (identidad, empresa activa, roles/permisos,
 * preferencias) proveniente de GET /api/v1/session/context.
 * Solo en memoria — no se persiste en sessionStorage/localStorage.
 */
export const useSessionStore = create<SessionState>()((set) => ({
  identity:      null,
  tenant:        null,
  authorization: null,
  preferences:   null,
  isLoaded:      false,

  setSession: (dto) => {
    set({
      identity:      dto.identity,
      tenant:        dto.tenant,
      authorization: dto.authorization,
      preferences:   dto.preferences,
      isLoaded:      true,
    });
    useActiveBranchStore.getState().setBranch(dto.branch);
  },

  refresh: async () => {
    const dto = await sessionService.getContext();
    set({
      identity:      dto.identity,
      tenant:        dto.tenant,
      authorization: dto.authorization,
      preferences:   dto.preferences,
      isLoaded:      true,
    });
    useActiveBranchStore.getState().setBranch(dto.branch);
  },

  clear: () => {
    set({
      identity:      null,
      tenant:        null,
      authorization: null,
      preferences:   null,
      isLoaded:      false,
    });
    useActiveBranchStore.getState().clear();
  },
}));
