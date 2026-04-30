import { create } from 'zustand';
import { persist } from 'zustand/middleware';
import type { AuthResponse } from '../types/auth';

interface AuthState {
  user: Omit<AuthResponse, 'token'> | null;
  token: string | null;
  isAuthenticated: boolean;
  /** Guarda el usuario y el token tras un login o register exitoso. */
  login: (response: AuthResponse) => void;
  /** Limpia el estado; el interceptor de Axios redirige al login ante 401. */
  logout: () => void;
}

/**
 * Estado global de autenticación.
 *
 * Persiste en localStorage bajo la clave "auth-storage" para sobrevivir
 * recargas de página. El interceptor de `api.ts` lee el token directamente
 * desde localStorage para no depender de la hidratación del store de Zustand.
 */
export const useAuthStore = create<AuthState>()(
  persist(
    (set) => ({
      user: null,
      token: null,
      isAuthenticated: false,
      login: ({ token, ...user }) =>
        set({ user, token, isAuthenticated: true }),
      logout: () =>
        set({ user: null, token: null, isAuthenticated: false }),
    }),
    { name: 'auth-storage' }
  )
);
