import {
  clearCompaniesDetailSubscriberId,
  clearCompaniesSubscriptionSubscriberId,
} from '../../navigation/companiesSubscriberDetailNav';
import { resetAuthTransportState } from '../../modules/lib/api';
import { useAccessStore } from '../../store/accessStore';
import { useAuthStore } from '../../store/authStore';
import { usePermissionsStore } from '../../store/permissionsStore';
import { clearAccessToken } from './authTokenMemory';
import {
  ACCESS_BOOTSTRAP_STORAGE_KEY,
  AUTH_PROFILE_STORAGE_KEY,
  AUTH_STORAGE_KEY,
  PERMISSIONS_STORAGE_KEY,
  SAAS_SESSION_STORAGE_PREFIX,
  SUPERADMIN_IMPERSONATION_NAME_KEY,
} from './sessionStorageKeys';

export type FullLogoutOptions = {
  resetStores?: boolean;
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
    localStorage.removeItem(SUPERADMIN_IMPERSONATION_NAME_KEY);
  } catch {
    /* storage disabled */
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

export function fullLogout(options: FullLogoutOptions = {}): void {
  const { resetStores = true } = options;

  resetAuthTransportState();
  clearAccessToken();

  if (resetStores) {
    useAuthStore.getState().logout();
    usePermissionsStore.getState().clearPermissions();
    useAccessStore.getState().clearBootstrap();
  }

  clearPersistedSessionArtifacts();
}
