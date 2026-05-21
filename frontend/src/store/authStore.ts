import { create } from 'zustand';
import { createJSONStorage, persist } from 'zustand/middleware';
import { AUTH_PROFILE_STORAGE_KEY } from '../lib/session/sessionStorageKeys';
import { clearAccessToken, getAccessToken, setAccessToken } from '../lib/session/authTokenMemory';
import type { AuthResponse } from '../types/auth';
import { zustandSessionStorage } from '../lib/session/zustandSessionStorage';

interface AuthState {
  user: Omit<AuthResponse, 'token' | 'refreshToken' | 'refreshTokenExpiry'> | null;
  /** Espejo en memoria; no se persiste. */
  token: string | null;
  isAuthenticated: boolean;
  hasHydrated: boolean;
  login: (response: AuthResponse) => void;
  updateTokens: (accessToken: string, refreshToken: string | null) => void;
  logout: () => void;
}

/**
 * Perfil de sesión en sessionStorage (pestaña).
 * Access token en memoria; refresh vía cookie httpOnly en el backend.
 */
export const useAuthStore = create<AuthState>()(
  persist(
    (set) => ({
      user:            null,
      token:           null,
      isAuthenticated: false,
      hasHydrated:     false,

      login: ({ token, refreshToken: _rt, refreshTokenExpiry: _exp, ...user }) => {
        setAccessToken(token);
        set({ user, token, isAuthenticated: true });
      },

      updateTokens: (accessToken) => {
        setAccessToken(accessToken);
        set({ token: accessToken });
      },

      logout: () => {
        clearAccessToken();
        set({ user: null, token: null, isAuthenticated: false });
      },
    }),
    {
      name: AUTH_PROFILE_STORAGE_KEY,
      storage: createJSONStorage(() => zustandSessionStorage),
      partialize: (state) => ({
        user: state.user,
        isAuthenticated: state.isAuthenticated,
      }),
      onRehydrateStorage: () => (state) => {
        if (!state) return;
        state.hasHydrated = true;
        state.token = getAccessToken();
      },
    },
  ),
);
