import { useAuthStore } from "../../store/authStore";
import { clearAccessToken, getAccessToken } from "./authTokenMemory";
import { refreshSessionToken } from "./refreshSessionToken";

/**
 * Tras recargar la pestaña: perfil en sessionStorage; access token solo en memoria.
 * Refresh vía cookie httpOnly con retry benigno si otra pestaña rotó primero.
 */
export async function restoreSessionFromCookie(): Promise<boolean> {
  const { user, isAuthenticated } = useAuthStore.getState();
  if (!isAuthenticated || !user) return false;
  if (getAccessToken()) return true;

  try {
    await refreshSessionToken({ bootstrapRetry: true });
    return true;
  } catch {
    if (getAccessToken()) return true;
    clearAccessToken();
    useAuthStore.getState().logout();
    return false;
  }
}
