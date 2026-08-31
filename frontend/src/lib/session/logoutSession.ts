import axios from "axios";
import { fullLogout } from "./fullLogout";

const viteApiBase =
  (import.meta.env.VITE_API_URL as string | undefined)?.trim() ?? "";

/**
 * Llama a POST /api/v1/auth/logout con `axios` crudo (no la instancia `api` de
 * modules/lib/api.ts) para que el interceptor de refresh no intercepte esta
 * llamada — mismo patrón que `postRefresh` en authRefreshManager.ts. Nunca
 * rechaza: si el backend no responde (red/401/500), el logout local debe
 * poder continuar igual.
 */
async function requestServerLogout(): Promise<void> {
  try {
    await axios.post(
      `${viteApiBase}/api/v1/auth/logout`,
      {},
      { withCredentials: true },
    );
  } catch {
    /* la limpieza local debe continuar aunque el logout remoto falle */
  }
}

/**
 * Logout explícito de UI: revoca la sesión en el servidor (refresh token +
 * cookie) y solo después limpia el estado local. `fullLogout()` sigue siendo
 * limpieza puramente local — este es el punto de entrada de orden superior
 * para el botón "Cerrar sesión".
 */
export async function logoutSession(): Promise<void> {
  await requestServerLogout();
  fullLogout();
}
