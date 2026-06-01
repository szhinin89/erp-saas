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
  /**
   * True when the backend returned company_onboarding_required (403).
   * ProtectedRoute uses this to suppress ERP component rendering while the
   * onboarding wizard is active, preventing background API calls that would
   * also return 403 and pollute the console.
   * Cleared automatically when onboarding completes (new JWT issued).
   */
  companyNeedsOnboarding: boolean;
  login: (response: AuthResponse) => void;
  updateTokens: (accessToken: string, refreshToken: string | null) => void;
  incrementCompanySession: () => void;
  setCompanyNeedsOnboarding: (value: boolean) => void;
  logout: () => void;
}

/**
 * Perfil de sesión en sessionStorage (pestaña).
 * Access token en memoria; refresh vía cookie httpOnly en el backend.
 */
export const useAuthStore = create<AuthState>()(
  persist(
    (set, get) => ({
      user:                   null,
      token:                  null,
      isAuthenticated:        false,
      hasHydrated:            false,
      companySessionVersion:  0,
      // Initialized from sessionStorage so it survives hot-reload and StrictMode re-mounts.
      companyNeedsOnboarding: sessionStorage.getItem('erp.onboarding.needed') === '1',

      login: (response: AuthResponse) => {
        const { token, ...user } = response;
        const prevCompany = get().user?.companyId ?? null;
        const nextCompany = user.companyId ?? null;
        setAccessToken(token);
        sessionStorage.removeItem('erp.onboarding.needed');
        set((state) => ({
          user,
          token,
          isAuthenticated: true,
          // Clear onboarding flag when a new JWT with companyId is issued.
          companyNeedsOnboarding: false,
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

      setCompanyNeedsOnboarding: (value: boolean) => {
        // Also persist in sessionStorage so StrictMode re-mounts / hot-reloads pick it up.
        if (value) {
          sessionStorage.setItem('erp.onboarding.needed', '1');
        } else {
          sessionStorage.removeItem('erp.onboarding.needed');
        }
        set({ companyNeedsOnboarding: value });
      },

      logout: () => {
        clearAccessToken();
        set({ user: null, token: null, isAuthenticated: false, companySessionVersion: 0, companyNeedsOnboarding: false });
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
