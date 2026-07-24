import axios, { type AxiosRequestConfig } from 'axios';
import { fullLogout } from '../../lib/session/fullLogout';
import { clearAccessToken, getAccessToken } from '../../lib/session/authTokenMemory';
import { shouldAttemptTokenRefresh } from './authRefreshPolicy';
import { refreshSessionToken } from '../../lib/session/refreshSessionToken';
import { getCorrelationId } from '../../lib/observability/requestContext';
import { logDevApiRequest } from '../../lib/observability/devApiLog';
import { useAuthStore } from '../../store/authStore';
import { useActiveBranchStore } from '../../store/activeBranchStore';

/**
 * Cliente HTTP centralizado.
 *
 * Tokens:
 * - Access token: memoria (`authTokenMemory`) + espejo en Zustand (no persistido).
 * - Refresh token: cookie httpOnly del backend (`withCredentials: true`).
 * - Refresh coordinado: `authRefreshManager` (Web Locks + BroadcastChannel + single-flight).
 */

const viteApiBase = (import.meta.env.VITE_API_URL as string | undefined)?.trim() ?? '';

export const api = axios.create({
  baseURL:         viteApiBase.length > 0 ? viteApiBase : '',
  headers:         { 'Content-Type': 'application/json' },
  withCredentials: true,
});

/** @deprecated Usar resetRefreshSessionFlight vía fullLogout. Mantener por compatibilidad de tests. */
export function resetAuthTransportState() {
  /* cola duplicada eliminada — refreshSessionToken dedupe en authRefreshManager */
}

function inferRequestMode(url: string): 'masterdata' | 'legacy' {
  return url.includes('/api/v1/master/business-partners') ? 'masterdata' : 'legacy';
}

api.interceptors.request.use((config) => {
  const token = getAccessToken();
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }

  const correlationId = getCorrelationId();
  config.headers['x-correlation-id'] = correlationId;

  const { user, companySessionVersion } = useAuthStore.getState();
  if (user?.companyId) {
    config.headers['X-Company-Id'] = user.companyId;
  }
  config.headers['x-company-session-version'] = String(companySessionVersion);

  const { branch } = useActiveBranchStore.getState();
  if (branch?.id) {
    config.headers['X-Branch-Id'] = branch.id;
  }

  if (import.meta.env.DEV) {
    const url = String(config.url ?? '');
    logDevApiRequest({
      endpoint: url,
      mode: inferRequestMode(url),
      method: (config.method ?? 'get').toUpperCase(),
    });
  }

  return config;
});

api.interceptors.response.use(
  (res) => res,
  async (error) => {
    const originalRequest = error.config as AxiosRequestConfig & { _retry?: boolean };
    const status          = error.response?.status as number | undefined;
    const url             = (originalRequest?.url ?? '') as string;

    if (!shouldAttemptTokenRefresh(status, url, originalRequest._retry ?? false)) {
      return Promise.reject(error);
    }

    originalRequest._retry = true;

    try {
      const newAccessToken = await refreshSessionToken();
      originalRequest.headers = {
        ...originalRequest.headers,
        Authorization: `Bearer ${newAccessToken}`,
      };
      return api(originalRequest);
    } catch (refreshError) {
      clearAccessToken();
      // 429 means the refresh endpoint is rate-limiting this user (chain depth or burst limit).
      // Don't log out — let the user stay on the current page; the next navigation or retry
      // will re-attempt once the rate window resets. Logout only on definitive auth failures.
      const refreshStatus = (refreshError as { response?: { status?: number } })?.response?.status;
      if (refreshStatus === 429) {
        return Promise.reject(refreshError);
      }
      fullLogout();
      window.location.href = '/login';
      return Promise.reject(refreshError);
    }
  },
);
