import type { NavigateFunction } from 'react-router-dom';
import { GLOBAL_SUBSCRIBER_ID } from '../constants/subscriberIds';
import { getAccessToken } from '../lib/session/authTokenMemory';
import { restoreSessionFromCookie } from '../lib/session/restoreSessionFromCookie';
import { clearPlatformImpersonationName } from '../lib/session/sessionStorageKeys';
import { syncSessionEntitlements } from '../lib/syncSessionEntitlements';
import { PLATFORM_UI } from '../modules/platform/api/platformApiPaths';
import { platformService } from '../modules/platform/api/platformService';
import { storeImpersonationSubscriberName } from '../modules/platform/platformPanelUtils';
import type { AuthResponse } from '../types/auth';
import { persistCompaniesDetailSubscriberId } from './platformSubscriberDetailNav';

/** Ruta platform a restaurar al salir de impersonación (sessionStorage). */
export const PLATFORM_IMPERSONATION_RETURN_PATH_KEY = 'erp.platform.impersonationReturnPath';

export type SaasImpersonationTarget = '/saas/overview' | '/saas/companies' | '/saas/billing';

export type ExitPlatformImpersonationDestination = 'return-path' | 'global-overview' | 'global-subscribers';

export function persistPlatformImpersonationReturnPath(path: string): void {
  try {
    sessionStorage.setItem(PLATFORM_IMPERSONATION_RETURN_PATH_KEY, path.trim());
  } catch {
    /* storage disabled */
  }
}

export function readPlatformImpersonationReturnPath(): string | null {
  try {
    const value = sessionStorage.getItem(PLATFORM_IMPERSONATION_RETURN_PATH_KEY)?.trim();
    return value || null;
  } catch {
    return null;
  }
}

export function clearPlatformImpersonationReturnPath(): void {
  try {
    sessionStorage.removeItem(PLATFORM_IMPERSONATION_RETURN_PATH_KEY);
  } catch {
    /* */
  }
}

export function hasPlatformImpersonationReturnPath(): boolean {
  return Boolean(readPlatformImpersonationReturnPath());
}

export function buildSubscriberDetailReturnPath(subscriberId: string, tab?: string | null): string {
  const base = PLATFORM_UI.subscriberDetail(subscriberId);
  const normalizedTab = (tab ?? 'overview').trim().toLowerCase();
  if (!normalizedTab || normalizedTab === 'overview') return base;
  return `${base}?tab=${encodeURIComponent(normalizedTab)}`;
}

async function ensureAccessToken(): Promise<void> {
  if (getAccessToken()) return;
  const restored = await restoreSessionFromCookie();
  if (!restored || !getAccessToken()) {
    throw new Error('Sesión expirada. Vuelve a iniciar sesión.');
  }
}

type SessionHandlers = {
  navigate: NavigateFunction;
  login: (session: AuthResponse) => void;
  clearPermissions: () => void;
};

/** Impersona un suscriptor y guarda la ruta platform para volver a la ficha. */
export async function startPlatformImpersonation(
  options: SessionHandlers & {
    subscriberId: string;
    subscriberName: string;
    target: SaasImpersonationTarget;
    returnPath: string;
  },
): Promise<void> {
  const subscriberId = options.subscriberId.trim();
  if (!subscriberId) throw new Error('Identificador de suscriptor inválido.');

  await ensureAccessToken();
  persistPlatformImpersonationReturnPath(options.returnPath);
  persistCompaniesDetailSubscriberId(subscriberId);

  const auth = await platformService.switchSubscriber(subscriberId);
  storeImpersonationSubscriberName(options.subscriberName);
  options.clearPermissions();
  options.login(auth);
  try {
    await syncSessionEntitlements();
  } catch {
    /* AppLayout reintenta permisos/menú si hace falta. */
  }
  options.navigate(options.target);
}

/** Restaura sesión platform global y navega al destino elegido. */
export async function exitPlatformImpersonation(
  options: SessionHandlers & {
    destination: ExitPlatformImpersonationDestination;
  },
): Promise<void> {
  const storedReturnPath = readPlatformImpersonationReturnPath();
  clearPlatformImpersonationReturnPath();

  await ensureAccessToken();
  const auth = await platformService.switchSubscriber(GLOBAL_SUBSCRIBER_ID);
  clearPlatformImpersonationName();
  options.login(auth);
  options.clearPermissions();

  let target: string;
  if (options.destination === 'return-path' && storedReturnPath) {
    target = storedReturnPath;
  } else if (options.destination === 'global-subscribers') {
    target = PLATFORM_UI.subscribers;
  } else {
    target = PLATFORM_UI.overview;
  }
  options.navigate(target);
}
