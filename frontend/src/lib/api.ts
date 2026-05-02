import axios from 'axios';

/**
 * Instancia de Axios preconfigurada para llamar al backend.
 *
 * - La baseURL es VITE_API_URL si está definida (no vacía).
 *   En desarrollo, si está vacía o no existe: baseURL '' → las rutas /api/*
 *   van al mismo host que Vite y el proxy (vite.config) reenvía a la API (sin CORS).
 *
 * - Interceptor de request: inyecta el Bearer token desde el store
 *   persistido en localStorage, sin necesidad de pasarlo manualmente.
 *
 * - Interceptor de response: ante un 401, limpia el estado de auth
 *   y redirige al login. Esto cubre tokens vencidos o revocados.
 */
const viteApiBase = (import.meta.env.VITE_API_URL as string | undefined)?.trim() ?? '';
/**
 * Vacío = rutas relativas /api/* al mismo host (Vite dev o preview con proxy en vite.config).
 * Solo usar URL absoluta si VITE_API_URL está definida (otro dominio/puerto sin proxy).
 */
const resolvedBaseUrl = viteApiBase.length > 0 ? viteApiBase : '';

export const api = axios.create({
  baseURL: resolvedBaseUrl,
  headers: { 'Content-Type': 'application/json' },
});

api.interceptors.request.use((config) => {
  const raw = localStorage.getItem('auth-storage');
  if (raw) {
    const { state } = JSON.parse(raw);
    if (state?.token) {
      config.headers.Authorization = `Bearer ${state.token}`;
    }
  }
  return config;
});

api.interceptors.response.use(
  (res) => res,
  (err) => {
    if (err.response?.status === 401) {
      // No forzar redirect en endpoints públicos de autenticación,
      // porque un 401 ahí es parte normal del flujo (credenciales inválidas)
      // y debe manejarse en la UI (sin recargar la página).
      const url = (err.config?.url as string | undefined) ?? '';
      const isPublicAuth =
        url.includes('/api/auth/login') ||
        url.includes('/api/auth/register') ||
        url.includes('/api/auth/password-reset') ||
        url.includes('/api/access/bootstrap-login') ||
        url.includes('/api/access/switch-tenant') ||
        url.includes('/api/access/register-tenant');

      if (!isPublicAuth) {
        localStorage.removeItem('auth-storage');
        window.location.href = '/login';
      }
    }
    return Promise.reject(err);
  }
);
