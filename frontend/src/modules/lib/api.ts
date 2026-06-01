import axios, { type AxiosRequestConfig } from 'axios';
import { fullLogout } from '../../lib/session/fullLogout';
import { clearAccessToken, getAccessToken } from '../../lib/session/authTokenMemory';
import { shouldAttemptTokenRefresh } from './authRefreshPolicy';
import { refreshSessionToken } from '../../lib/session/refreshSessionToken';
import { getCorrelationId } from '../../lib/observability/requestContext';
import { logDevApiRequest } from '../../lib/observability/devApiLog';
import { useAuthStore } from '../../store/authStore';
import { spaNavigate } from '../../lib/navigation/globalNavigator';

// Throttle: prevent repeated navigations to /onboarding/company within the same session.
let _onboardingNavigationFired = false;

/** Reset after successful onboarding so the gate can trigger again on next login. */
export function resetOnboardingNavigationThrottle(): void {
  _onboardingNavigationFired = false;
}

// Import centralized status constants — single source of truth for denial code handling
import { BLOCKED_DENIAL_CODES, READONLY_DENIAL_CODES } from '../saas/billing/constants/subscriptionStatus';

function isSubscriptionDenial(responseData: unknown): boolean {
  if (typeof responseData !== 'object' || responseData === null) return false;
  const code = (responseData as Record<string, unknown>).code;
  return typeof code === 'string' && BLOCKED_DENIAL_CODES.has(code);
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

    // Handle company onboarding required (403 from CompanyOnboardingMiddleware).
    // Sets a store flag so ProtectedRoute can suppress ERP route rendering immediately,
    // preventing further API calls from background components (dashboard hooks, etc.).
    if (status === 403) {
      const code403 = (error.response?.data as Record<string, unknown>)?.code;
      if (code403 === 'company_onboarding_required') {
        // Mark in store so ProtectedRoute stops rendering ERP children.
        useAuthStore.getState().setCompanyNeedsOnboarding(true);
        // Navigate only once per session to avoid infinite redirects.
        if (!_onboardingNavigationFired) {
          _onboardingNavigationFired = true;
          spaNavigate('/onboarding/company');
        }
        return Promise.reject(error);
      }
    }

    // Handle subscription-level 403 before attempting token refresh.
    if (status === 403) {
      const data = error.response?.data;
      const code = typeof data === 'object' && data !== null ? (data as Record<string,unknown>).code : undefined;

      // ReadOnly mode (GracePeriod/PastDue) — don't logout, let UI show banner
      if (typeof code === 'string' && READONLY_DENIAL_CODES.has(code)) {
        return Promise.reject(error); // propagate for components to handle
      }

      // Blocked tenant (Suspended/Cancelled/TrialExpired) — logout immediately
      if (isSubscriptionDenial(data)) {
        handleSubscriptionDenial(data);
        return Promise.reject(error);
      }
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
