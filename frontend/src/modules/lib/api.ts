import axios, { type AxiosRequestConfig } from 'axios';
import { fullLogout } from '../../lib/session/fullLogout';
import {
  clearAccessToken,
  getAccessToken,
  setAccessToken,
} from '../../lib/session/authTokenMemory';
import { useAuthStore } from '../../store/authStore';
import { shouldAttemptTokenRefresh } from './authRefreshPolicy';

/**
 * Cliente HTTP centralizado.
 *
 * Tokens:
 * - Access token: memoria (`authTokenMemory`) + espejo en Zustand (no persistido).
 * - Refresh token: cookie httpOnly del backend (`withCredentials: true`).
 */

const viteApiBase = (import.meta.env.VITE_API_URL as string | undefined)?.trim() ?? '';

export const api = axios.create({
  baseURL:         viteApiBase.length > 0 ? viteApiBase : '',
  headers:         { 'Content-Type': 'application/json' },
  withCredentials: true,
});

type QueueItem = { resolve: (token: string) => void; reject: (err: unknown) => void };

let isRefreshing   = false;
let pendingQueue: QueueItem[] = [];

function processQueue(error: unknown, newToken: string | null = null) {
  pendingQueue.forEach((item) =>
    error ? item.reject(error) : item.resolve(newToken!),
  );
  pendingQueue = [];
}

export function resetAuthTransportState() {
  isRefreshing = false;
  pendingQueue = [];
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

    if (isRefreshing) {
      return new Promise<string>((resolve, reject) => {
        pendingQueue.push({ resolve, reject });
      })
        .then((newToken) => {
          originalRequest.headers = {
            ...originalRequest.headers,
            Authorization: `Bearer ${newToken}`,
          };
          return api(originalRequest);
        })
        .catch((e) => Promise.reject(e));
    }

    originalRequest._retry = true;
    isRefreshing            = true;

    try {
      const refreshRes = await axios.post<{
        responseObject: { token: string; refreshToken?: string | null };
      }>(
        `${viteApiBase}/api/auth/refresh`,
        {},
        { withCredentials: true },
      );

      const newAccessToken = refreshRes.data.responseObject.token;
      setAccessToken(newAccessToken);
      useAuthStore.getState().updateTokens(newAccessToken, null);
      processQueue(null, newAccessToken);

      originalRequest.headers = {
        ...originalRequest.headers,
        Authorization: `Bearer ${newAccessToken}`,
      };
      return api(originalRequest);
    } catch (refreshError) {
      processQueue(refreshError, null);
      clearAccessToken();
      fullLogout();
      window.location.href = '/login';
      return Promise.reject(refreshError);
    } finally {
      isRefreshing = false;
    }
  },
);
