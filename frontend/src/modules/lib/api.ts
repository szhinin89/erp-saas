import axios, { type AxiosRequestConfig } from 'axios';

/**
 * Cliente HTTP centralizado.
 *
 * Estrategia de tokens:
 * - Access token (corto plazo, 60 min): en Authorization: Bearer, persistido en localStorage.
 * - Refresh token (largo plazo, 30 días): preferentemente en httpOnly cookie (el backend la
 *   establece automáticamente). Como fallback, también se persiste en localStorage para
 *   entornos no-browser (Postman, apps nativas) o cuando las cookies están bloqueadas.
 *
 * Flujo ante 401:
 * 1. El interceptor intercepta el 401.
 * 2. Si NO es un endpoint público de auth, intenta refrescar vía POST /api/auth/refresh.
 *    - La cookie httpOnly se envía automáticamente (withCredentials: true).
 *    - Si no hay cookie, el body incluye el refreshToken desde localStorage.
 * 3. Si el refresh tiene éxito: actualiza los tokens y reintenta la petición original.
 * 4. Si el refresh falla: limpia la sesión y redirige al login.
 * 5. Peticiones concurrentes que reciben 401 simultáneamente: se encolan y se resuelven
 *    con el nuevo token una vez que el refresh termina (no se hacen múltiples refreshes).
 */

const viteApiBase = (import.meta.env.VITE_API_URL as string | undefined)?.trim() ?? '';

export const api = axios.create({
  baseURL:         viteApiBase.length > 0 ? viteApiBase : '',
  headers:         { 'Content-Type': 'application/json' },
  withCredentials: true, // necesario para que el navegador envíe/reciba la cookie httpOnly
});

// ── Cola de reintentos (previene múltiples refreshes simultáneos) ─────────

type QueueItem = { resolve: (token: string) => void; reject: (err: unknown) => void };

let isRefreshing   = false;
let pendingQueue: QueueItem[] = [];

function processQueue(error: unknown, newToken: string | null = null) {
  pendingQueue.forEach((item) =>
    error ? item.reject(error) : item.resolve(newToken!)
  );
  pendingQueue = [];
}

// ── Helpers para leer el store sin importar el hook (evita dependencias cíclicas) ──

function getStoredToken(): string | null {
  try {
    const raw = localStorage.getItem('auth-storage');
    return raw ? (JSON.parse(raw)?.state?.token ?? null) : null;
  } catch {
    return null;
  }
}

function getStoredRefreshToken(): string | null {
  try {
    const raw = localStorage.getItem('auth-storage');
    return raw ? (JSON.parse(raw)?.state?.refreshToken ?? null) : null;
  } catch {
    return null;
  }
}

function updateStoredTokens(accessToken: string, refreshToken: string | null) {
  try {
    const raw = localStorage.getItem('auth-storage');
    if (!raw) return;
    const parsed = JSON.parse(raw);
    parsed.state.token        = accessToken;
    parsed.state.refreshToken = refreshToken ?? parsed.state.refreshToken;
    localStorage.setItem('auth-storage', JSON.stringify(parsed));
  } catch {
    // no-op
  }
}

function clearSession() {
  localStorage.removeItem('auth-storage');
}

// ── Interceptor de REQUEST: inyecta Bearer token ──────────────────────────

api.interceptors.request.use((config) => {
  const token = getStoredToken();
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

// ── Interceptor de RESPONSE: maneja 401 con refresh automático ───────────

const PUBLIC_AUTH_PATHS = [
  '/api/auth/login',
  '/api/platform/auth/login',
  '/api/auth/superadmin-login',
  '/api/auth/register',
  '/api/auth/password-reset',
  '/api/auth/forgot-password',
  '/api/auth/reset-password',
  '/api/auth/refresh',          // evitar loop infinito
  '/api/admin/iam/bootstrap-login',
  '/api/admin/iam/switch-subscriber',
  '/api/admin/iam/subscriber/company_user_memberships',
  '/api/admin/iam/register-tenant',
  '/api/admin/iam/register-subscriber',
];

api.interceptors.response.use(
  (res) => res,
  async (error) => {
    const originalRequest = error.config as AxiosRequestConfig & { _retry?: boolean };
    const status          = error.response?.status as number | undefined;
    const url             = (originalRequest?.url ?? '') as string;

    // Solo intentar refresh para 401 en endpoints protegidos (y solo una vez)
    const isPublicAuth = PUBLIC_AUTH_PATHS.some((p) => url.includes(p));
    if (status !== 401 || isPublicAuth || originalRequest._retry) {
      return Promise.reject(error);
    }

    // Si ya hay un refresh en curso, encolar esta petición
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
      // Intentar refresh: la cookie httpOnly se envía automáticamente.
      // Si no hay cookie, el fallback es el refreshToken de localStorage.
      const storedRefreshToken = getStoredRefreshToken();

      const refreshRes = await axios.post<{
        responseObject: { token: string; refreshToken?: string | null };
      }>(
        `${viteApiBase}/api/auth/refresh`,
        storedRefreshToken ? { refreshToken: storedRefreshToken } : {},
        { withCredentials: true }
      );

      const newAccessToken   = refreshRes.data.responseObject.token;
      const newRefreshToken  = refreshRes.data.responseObject.refreshToken ?? null;

      updateStoredTokens(newAccessToken, newRefreshToken);
      processQueue(null, newAccessToken);

      originalRequest.headers = {
        ...originalRequest.headers,
        Authorization: `Bearer ${newAccessToken}`,
      };
      return api(originalRequest);
    } catch (refreshError) {
      processQueue(refreshError, null);
      clearSession();
      window.location.href = '/login';
      return Promise.reject(refreshError);
    } finally {
      isRefreshing = false;
    }
  }
);
