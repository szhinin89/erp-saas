/**
 * Coordinación entre pestañas del bloqueo por inactividad (Fase 3). Canal propio
 * (`erp.idle`), separado de `erp.auth` (authRefreshManager.ts) para no tocar la lógica de
 * refresh/logout ya validada — bloquear por inactividad no es lo mismo que cerrar sesión.
 */
const IDLE_BROADCAST_CHANNEL = "erp.idle";
const IDLE_LOCK_STORAGE_KEY = "erp.idle.locked";

let channel: BroadcastChannel | null = null;

function getChannel(): BroadcastChannel | null {
  if (typeof BroadcastChannel === "undefined") return null;
  if (!channel) channel = new BroadcastChannel(IDLE_BROADCAST_CHANNEL);
  return channel;
}

/** Notifica a otras pestañas que esta detectó inactividad y bloqueó su UI. */
export function broadcastIdleLock(): void {
  try {
    getChannel()?.postMessage({ type: "locked" });
  } catch {
    /* canal no disponible */
  }
  try {
    // Fallback para pestañas que no comparten BroadcastChannel (mismo patrón que
    // broadcastAuthLogout en authRefreshManager.ts). El valor es solo un marcador de
    // cambio, no contiene datos de sesión.
    localStorage.setItem(
      IDLE_LOCK_STORAGE_KEY,
      `${Date.now()}-${Math.random()}`,
    );
  } catch {
    /* storage deshabilitado */
  }
}

/** Suscribe a bloqueos de inactividad detectados en otras pestañas. Devuelve función de limpieza. */
export function listenForRemoteIdleLock(onLocked: () => void): () => void {
  const ch = getChannel();
  const handleMessage = (event: MessageEvent<{ type?: string }>) => {
    if (event.data?.type === "locked") onLocked();
  };
  ch?.addEventListener("message", handleMessage);

  const handleStorage = (event: StorageEvent) => {
    if (event.key === IDLE_LOCK_STORAGE_KEY && event.newValue) onLocked();
  };
  window.addEventListener("storage", handleStorage);

  return () => {
    ch?.removeEventListener("message", handleMessage);
    window.removeEventListener("storage", handleStorage);
  };
}
