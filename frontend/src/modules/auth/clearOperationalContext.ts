import { useActiveBranchStore } from "../../store/activeBranchStore";

/**
 * Limpia contexto operativo dependiente de empresa/sucursal antes de reconstruir
 * la sesión enriquecida. No toca auth, access token ni preferencias globales.
 */
export function clearOperationalContext(): void {
  useActiveBranchStore.getState().clear();
}
