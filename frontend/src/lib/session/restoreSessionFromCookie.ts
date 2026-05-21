import axios from 'axios';
import type { AuthResponse } from '../../types/auth';
import { useAuthStore } from '../../store/authStore';
import { clearAccessToken, getAccessToken, setAccessToken } from './authTokenMemory';

const viteApiBase = (import.meta.env.VITE_API_URL as string | undefined)?.trim() ?? '';

type RefreshPayload = {
  responseObject: Pick<AuthResponse, 'token' | 'refreshToken' | 'refreshTokenExpiry'>;
};

/**
 * Tras recargar la pestaña: perfil en sessionStorage; access token solo en memoria.
 * Refresh vía cookie httpOnly (sin refreshToken en storage).
 */
export async function restoreSessionFromCookie(): Promise<boolean> {
  const { user, isAuthenticated } = useAuthStore.getState();
  if (!isAuthenticated || !user) return false;
  if (getAccessToken()) return true;

  try {
    const res = await axios.post<RefreshPayload>(
      `${viteApiBase}/api/auth/refresh`,
      {},
      { withCredentials: true },
    );
    const access = res.data.responseObject.token;
    setAccessToken(access);
    useAuthStore.getState().updateTokens(access, res.data.responseObject.refreshToken ?? null);
    return true;
  } catch {
    clearAccessToken();
    useAuthStore.getState().logout();
    return false;
  }
}
