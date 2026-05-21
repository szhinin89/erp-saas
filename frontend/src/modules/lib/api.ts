import axios, { type AxiosRequestConfig } from 'axios';
import { fullLogout } from '../../lib/session/fullLogout';
import { clearAccessToken, getAccessToken } from '../../lib/session/authTokenMemory';
import { shouldAttemptTokenRefresh } from './authRefreshPolicy';
import { refreshSessionToken } from '../../lib/session/refreshSessionToken';

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

api.interceptors.request.use((config) => {
  const token = getAccessToken();
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
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
      fullLogout();
      window.location.href = '/login';
      return Promise.reject(refreshError);
    }
  },
);
