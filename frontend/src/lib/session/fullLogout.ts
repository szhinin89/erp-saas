import {
  resetRefreshSessionFlight,
  broadcastAuthLogout,
} from "../../lib/session/authRefreshManager";
import { useAccessStore } from "../../store/accessStore";
import { useAuthStore } from "../../store/authStore";
import { useSessionIdleStore } from "../../store/sessionIdleStore";
import { clearAccessToken } from "./authTokenMemory";
import {
  ACCESS_BOOTSTRAP_STORAGE_KEY,
  AUTH_PROFILE_STORAGE_KEY,
  AUTH_STORAGE_KEY,
  PERMISSIONS_STORAGE_KEY,
  SAAS_SESSION_STORAGE_PREFIX,
} from "./sessionStorageKeys";

export type FullLogoutOptions = {
  resetStores?: boolean;
  /** Publicar logout a otras pestañas (default true). */
  broadcast?: boolean;
};

/** Elimina artefactos de sesión (sessionStorage + legacy localStorage). */
export function clearPersistedSessionArtifacts(): void {
  try {
    sessionStorage.removeItem(AUTH_PROFILE_STORAGE_KEY);
    sessionStorage.removeItem(PERMISSIONS_STORAGE_KEY);
    sessionStorage.removeItem(ACCESS_BOOTSTRAP_STORAGE_KEY);
    localStorage.removeItem(AUTH_STORAGE_KEY);
    localStorage.removeItem(PERMISSIONS_STORAGE_KEY);
    localStorage.removeItem(ACCESS_BOOTSTRAP_STORAGE_KEY);
  } catch {
    /* storage disabled */
  }

  try {
    for (let i = sessionStorage.length - 1; i >= 0; i -= 1) {
      const key = sessionStorage.key(i);
      if (key?.startsWith(SAAS_SESSION_STORAGE_PREFIX)) {
        sessionStorage.removeItem(key);
      }
    }
  } catch {
    /* */
  }
}

export function fullLogout(options: FullLogoutOptions = {}): void {
  const { resetStores = true, broadcast = true } = options;

  if (broadcast) {
    broadcastAuthLogout();
  }

  resetRefreshSessionFlight();
  clearAccessToken();

  if (resetStores) {
    useAuthStore.getState().logout();
    useAccessStore.getState().clearBootstrap();
    // Sin esto, un cambio de usuario en la misma pestaña (sin recargar) conservaría el
    // candado/lastActivityAt en memoria del usuario anterior (ZH-AUTH-SESSION-PERSISTENCE-QA-11).
    useSessionIdleStore.getState().reset();
  }

  clearPersistedSessionArtifacts();
}
