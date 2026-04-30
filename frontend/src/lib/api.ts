import axios from 'axios';

/**
 * Instancia de Axios preconfigurada para llamar al backend.
 *
 * - La baseURL se toma de la variable de entorno VITE_API_URL.
 *   En desarrollo: definida en .env.development (http://localhost:5003).
 *
 * - Interceptor de request: inyecta el Bearer token desde el store
 *   persistido en localStorage, sin necesidad de pasarlo manualmente.
 *
 * - Interceptor de response: ante un 401, limpia el estado de auth
 *   y redirige al login. Esto cubre tokens vencidos o revocados.
 */
export const api = axios.create({
  baseURL: import.meta.env.VITE_API_URL ?? 'http://localhost:5003',
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
      localStorage.removeItem('auth-storage');
      window.location.href = '/login';
    }
    return Promise.reject(err);
  }
);
