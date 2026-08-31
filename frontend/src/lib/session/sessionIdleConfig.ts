/**
 * Minutos de inactividad global antes de bloquear la UI (Fase 3 — ver SessionLockOverlay).
 * Independiente de la ventana absoluta de sesión del backend (Auth:SessionAbsoluteLifetimeMinutes,
 * 8h): esto es una barrera de UI, no de autenticación — el backend puede seguir aceptando el
 * refresh token mientras el usuario esté ausente, pero la pantalla queda bloqueada igual.
 */
const DEFAULT_IDLE_TIMEOUT_MINUTES = 30;

function readConfiguredMinutes(): number {
  const raw = (
    import.meta.env.VITE_IDLE_TIMEOUT_MINUTES as string | undefined
  )?.trim();
  const parsed = raw ? Number(raw) : NaN;
  return Number.isFinite(parsed) && parsed > 0
    ? parsed
    : DEFAULT_IDLE_TIMEOUT_MINUTES;
}

export const IDLE_TIMEOUT_MINUTES = readConfiguredMinutes();

export const IDLE_TIMEOUT_MS = IDLE_TIMEOUT_MINUTES * 60_000;
