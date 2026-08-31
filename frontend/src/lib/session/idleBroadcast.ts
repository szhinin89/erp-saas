/**
 * Coordinación entre pestañas del bloqueo por inactividad (Fase 3). Canal propio
 * (`erp.idle`), separado de `erp.auth` (authRefreshManager.ts) para no tocar la lógica de
 * refresh/logout ya validada — bloquear por inactividad no es lo mismo que cerrar sesión.
 */
const IDLE_BROADCAST_CHANNEL = "erp.idle";
const IDLE_LOCK_STORAGE_KEY = "erp.idle.locked";
const IDLE_UNLOCK_STORAGE_KEY = "erp.idle.unlocked";

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

/**
 * Notifica a otras pestañas que esta se reautenticó con éxito (Fase 4): pueden desbloquear su
 * UI sin repetir el login. Es seguro porque la reautenticación ya actualizó la cookie httpOnly
 * de refresh (compartida por el navegador, no por pestaña) — cualquier otra pestaña que necesite
 * renovar su access token usará esa cookie ya válida en su próximo refresh automático. No se
 * envía ningún token por el canal: cada pestaña sigue gestionando el suyo en su propia memoria.
 */
export function broadcastIdleUnlock(): void {
  try {
    getChannel()?.postMessage({ type: "unlocked" });
  } catch {
    /* canal no disponible */
  }
  try {
    localStorage.setItem(
      IDLE_UNLOCK_STORAGE_KEY,
      `${Date.now()}-${Math.random()}`,
    );
  } catch {
    /* storage deshabilitado */
  }
}

/** Suscribe a desbloqueos detectados en otras pestañas. Devuelve función de limpieza. */
export function listenForRemoteIdleUnlock(onUnlocked: () => void): () => void {
  const ch = getChannel();
  const handleMessage = (event: MessageEvent<{ type?: string }>) => {
    if (event.data?.type === "unlocked") onUnlocked();
  };
  ch?.addEventListener("message", handleMessage);

  const handleStorage = (event: StorageEvent) => {
    if (event.key === IDLE_UNLOCK_STORAGE_KEY && event.newValue) onUnlocked();
  };
  window.addEventListener("storage", handleStorage);

  return () => {
    ch?.removeEventListener("message", handleMessage);
    window.removeEventListener("storage", handleStorage);
  };
}
