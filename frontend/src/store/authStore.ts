import { create } from 'zustand';
import { persist } from 'zustand/middleware';
import { AUTH_STORAGE_KEY } from '../lib/session/sessionStorageKeys';
import type { AuthResponse } from '../types/auth';

interface AuthState {
  user: Omit<AuthResponse, 'token' | 'refreshToken' | 'refreshTokenExpiry'> | null;
  token: string | null;
  refreshToken: string | null;
  isAuthenticated: boolean;
  hasHydrated: boolean;
  /** Guarda el usuario y los tokens tras login o register exitoso. */
  login: (response: AuthResponse) => void;
  /**
   * Actualiza solo los tokens (sin cambiar el perfil de usuario).
   * Llamado por el interceptor de Axios tras un refresh exitoso.
   */
  updateTokens: (accessToken: string, refreshToken: string | null) => void;
  /** Limpia todo el estado de sesión. */
  logout: () => void;
}

/**
 * Estado global de autenticación.
 *
 * Persiste en localStorage bajo "auth-storage".
 * El interceptor de api.ts lee el token directamente desde localStorage
 * para no depender de la hidratación de Zustand.
 *
 * Refresh token también se persiste en localStorage como fallback cuando
 * el backend no puede establecer la cookie httpOnly (ej. Postman, apps nativas).
 * Si el backend establece la cookie, el refresh se hace automáticamente sin leer
 * localStorage — la cookie tiene precedencia en el servidor.
 */
export const useAuthStore = create<AuthState>()(
  persist(
    (set) => ({
      user:            null,
      token:           null,
      refreshToken:    null,
      isAuthenticated: false,
      hasHydrated:     false,

      login: ({ token, refreshToken, refreshTokenExpiry, ...user }) =>
        set({ user, token, refreshToken: refreshToken ?? null, isAuthenticated: true }),

      updateTokens: (accessToken, newRefreshToken) =>
        set((state) => ({
          token:        accessToken,
          refreshToken: newRefreshToken ?? state.refreshToken,
        })),

      logout: () =>
        set({ user: null, token: null, refreshToken: null, isAuthenticated: false }),
    }),
    {
      name: AUTH_STORAGE_KEY,
      onRehydrateStorage: () => (state) => {
        if (!state) return;
        state.hasHydrated = true;
      },
    }
  )
);
