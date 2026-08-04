import { useAuthStore } from "../../store/authStore";
import { clearAccessToken, getAccessToken } from "./authTokenMemory";
import { refreshSessionToken } from "./refreshSessionToken";

/**
 * Tras recargar o abrir una nueva pestaña: el access token solo vive en memoria y
 * sessionStorage puede estar vacío. El refresh con cookie httpOnly reconstruye el
 * perfil de Zustand mediante `login` dentro de `refreshSessionToken`.
 */
export async function restoreSessionFromCookie(): Promise<boolean> {
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
