import {
  clearCompaniesDetailSubscriberId,
  clearCompaniesSubscriptionSubscriberId,
} from '../../navigation/companiesSubscriberDetailNav';
import { resetAuthTransportState } from '../../modules/lib/api';
import { useAccessStore } from '../../store/accessStore';
import { useAuthStore } from '../../store/authStore';
import { usePermissionsStore } from '../../store/permissionsStore';
import {
  ACCESS_BOOTSTRAP_STORAGE_KEY,
  AUTH_STORAGE_KEY,
  PERMISSIONS_STORAGE_KEY,
  SAAS_SESSION_STORAGE_PREFIX,
  SUPERADMIN_IMPERSONATION_NAME_KEY,
} from './sessionStorageKeys';

export type FullLogoutOptions = {
  /** Reinicia stores Zustand en memoria (default: true). */
  resetStores?: boolean;
};

/**
 * Elimina artefactos persistidos de sesión (localStorage + sessionStorage erp.saas.*).
 * Idempotente; no navega ni llama API.
 */
export function clearPersistedSessionArtifacts(): void {
  try {
    localStorage.removeItem(AUTH_STORAGE_KEY);
    localStorage.removeItem(PERMISSIONS_STORAGE_KEY);
    localStorage.removeItem(ACCESS_BOOTSTRAP_STORAGE_KEY);
    localStorage.removeItem(SUPERADMIN_IMPERSONATION_NAME_KEY);
  } catch {
    /* storage disabled / private mode */
  }

  clearCompaniesDetailSubscriberId();
  clearCompaniesSubscriptionSubscriberId();

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

/**
 * Cierre de sesión centralizado: stores + persistencia + cola de refresh Axios.
 * Mantiene i18n, favoritos del menú y preferencias no sensibles.
 */
export function fullLogout(options: FullLogoutOptions = {}): void {
  const { resetStores = true } = options;

  resetAuthTransportState();

  if (resetStores) {
    useAuthStore.getState().logout();
    usePermissionsStore.getState().clearPermissions();
    useAccessStore.getState().clearBootstrap();
  }

  clearPersistedSessionArtifacts();
}
