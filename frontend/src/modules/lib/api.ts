import axios, { type AxiosRequestConfig } from 'axios';
import { fullLogout } from '../../lib/session/fullLogout';
import { clearAccessToken, getAccessToken } from '../../lib/session/authTokenMemory';
import { shouldAttemptTokenRefresh } from './authRefreshPolicy';
import { refreshSessionToken } from '../../lib/session/refreshSessionToken';
import { getCorrelationId } from '../../lib/observability/requestContext';
import { logDevApiRequest } from '../../lib/observability/devApiLog';
import { useAuthStore } from '../../store/authStore';

/** Subscription-related 403 denial codes returned by SubscriptionAccessMiddleware. */
const SUBSCRIPTION_DENIAL_CODES = new Set([
  'subscription_suspended',
  'subscription_inactive',
  'subscription_cancelled',
  'subscription_trial_expired',
  'subscription_access_denied',
]);

function isSubscriptionDenial(responseData: unknown): boolean {
  if (typeof responseData !== 'object' || responseData === null) return false;
  const code = (responseData as Record<string, unknown>).code;
  return typeof code === 'string' && SUBSCRIPTION_DENIAL_CODES.has(code);
}

function handleSubscriptionDenial(responseData: unknown): void {
  const data   = responseData as Record<string, unknown>;
  const code   = String(data.code   ?? 'subscription_suspended');
  const reason = String(data.reason ?? '');

  // Clear session immediately — no retry, no refresh loop
  fullLogout({ broadcast: true });

  const params = new URLSearchParams({ code });
  if (reason) params.set('reason', reason);

  window.location.href = `/subscription-suspended?${params.toString()}`;
}

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
  return url.includes('/api/master/business-partners') ? 'masterdata' : 'legacy';
}

api.interceptors.request.use((config) => {
  const token = getAccessToken();
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }

  const correlationId = getCorrelationId();
  config.headers['x-correlation-id'] = correlationId;

  const { companySessionVersion } = useAuthStore.getState();
  config.headers['x-company-session-version'] = String(companySessionVersion);

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

    // Handle subscription-level 403 before attempting token refresh.
    // These codes mean the tenant is blocked — logout immediately, no retry.
    if (status === 403 && isSubscriptionDenial(error.response?.data)) {
      handleSubscriptionDenial(error.response?.data);
      return Promise.reject(error);
    }

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
      fullLogout();
      window.location.href = '/login';
      return Promise.reject(refreshError);
    }
  },
);
