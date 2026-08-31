import axios from "axios";
import { setAccessToken } from "./authTokenMemory";
import { useAuthStore } from "../../store/authStore";
import { readEnvelopePayload } from "../../modules/lib/apiEnvelope";
import { normalizeAuthResponse } from "../../modules/auth/normalizeAuthResponse";
import { readApiErrorMessage } from "../../modules/lib/apiError";
import type { ApiResponse } from "../../types/api";

const viteApiBase =
  (import.meta.env.VITE_API_URL as string | undefined)?.trim() ?? "";

/**
 * Fase 4: reautenticación del mismo usuario tras bloqueo por inactividad (SessionLockOverlay).
 * Usa `axios` crudo (no la instancia `api` de modules/lib/api.ts) — mismo patrón que
 * `postRefresh` en authRefreshManager.ts — así una contraseña incorrecta (401) nunca dispara el
 * interceptor de refresh ni un logout automático.
 *
 * Éxito: guarda el nuevo access token en memoria y actualiza `authStore` exactamente como un
 * refresh normal (mismo `AuthResponse`, mismo `login()`) — ninguna pantalla necesita saber que
 * hubo un bloqueo de por medio. La cookie de refresh la reemplaza el backend (Set-Cookie),
 * transparente para este módulo.
 *
 * Falla: nunca toca el estado local (ni store ni token en memoria) — el caller decide qué
 * mostrar (contraseña incorrecta vs. sesión inválida) según el mensaje del error.
 */
export async function reauthenticate(password: string): Promise<void> {
  try {
    const res = await axios.post<ApiResponse<Record<string, unknown>>>(
      `${viteApiBase}/api/v1/auth/reauthenticate`,
      { password },
      { withCredentials: true },
    );
    const session = normalizeAuthResponse(
      readEnvelopePayload<Record<string, unknown> | null>(res.data),
    );
    if (!session.token) {
      throw new Error("La reautenticación no devolvió un token.");
    }
    setAccessToken(session.token);
    useAuthStore.getState().login(session);
  } catch (err) {
    const message =
      readApiErrorMessage(err) ??
      (err instanceof Error && err.message
        ? err.message
        : "No se pudo reautenticar. Intenta de nuevo.");
    throw new Error(message, { cause: err });
  }
}

/**
 * Distingue "contraseña incorrecta" (el usuario puede reintentar en el mismo modal) de
 * "sesión ya no reautenticable" (refresh vencido/revocado, tenant/membresía inválida, usuario
 * inactivo) — en ese segundo caso el modal debe ofrecer ir al login completo en vez de dejar
 * reintentar la contraseña indefinidamente contra una sesión que nunca va a funcionar.
 */
export function isSessionInvalidError(message: string): boolean {
  const lower = message.toLowerCase();
  return (
    lower.includes("sesión expirada") ||
    lower.includes("sesión no válida") ||
    lower.includes("inicia sesión nuevamente") ||
    lower.includes("tenant") ||
    lower.includes("membresía") ||
    lower.includes("usuario no válido") ||
    lower.includes("selecciona nuevamente tu empresa") ||
    lower.includes("se requiere una sesión activa")
  );
}
