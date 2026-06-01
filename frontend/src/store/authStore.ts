import { create } from 'zustand';
import { createJSONStorage, persist } from 'zustand/middleware';
import { logDevSessionContext } from '../lib/session/devSessionLog';
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
  /** Incrementa en cada switch-company / login operativo para invalidar caches UI. */
  companySessionVersion: number;
  login: (response: AuthResponse) => void;
  updateTokens: (accessToken: string, refreshToken: string | null) => void;
  incrementCompanySession: () => void;
  /**
   * Called by CompanyOnboardingPage after a successful onboarding completion.
   * Updates the local store so ProtectedRoute allows ERP access immediately,
   * without requiring a full token refresh roundtrip.
   */
  setOnboardingCompleted: (completed: boolean) => void;
  logout: () => void;
}

/**
 * Perfil de sesión en sessionStorage (pestaña).
 * Access token en memoria; refresh vía cookie httpOnly en el backend.
 *
 * onboardingCompleted is stored as part of `user` (comes from AuthResponse),
 * and is the SINGLE source of truth for the ProtectedRoute onboarding guard.
 * The previous reactive `companyNeedsOnboarding` flag has been removed —
 * ProtectedRoute now reads `user.onboardingCompleted` proactively at render time.
 */
export const useAuthStore = create<AuthState>()(
  persist(
    (set, get) => ({
      user:            null,
      token:           null,
      isAuthenticated: false,
      hasHydrated:     false,
      companySessionVersion: 0,

      login: (response: AuthResponse) => {
        const { token, ...user } = response;
        const prevCompany = get().user?.companyId ?? null;
        const nextCompany = user.companyId ?? null;
        setAccessToken(token);
        set((state) => ({
          user,
          token,
          isAuthenticated: true,
          companySessionVersion:
            nextCompany && nextCompany !== prevCompany
              ? state.companySessionVersion + 1
              : state.companySessionVersion,
        }));
        logDevSessionContext('login');
      },

      updateTokens: (accessToken) => {
        setAccessToken(accessToken);
        set({ token: accessToken });
      },

      incrementCompanySession: () => {
        set((state) => ({ companySessionVersion: state.companySessionVersion + 1 }));
      },

      setOnboardingCompleted: (completed: boolean) => {
        set((state) => ({
          user: state.user ? { ...state.user, onboardingCompleted: completed } : null,
        }));
      },

      logout: () => {
        clearAccessToken();
        set({ user: null, token: null, isAuthenticated: false, companySessionVersion: 0 });
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
