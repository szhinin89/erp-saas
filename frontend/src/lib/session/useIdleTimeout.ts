import { useEffect, useRef } from "react";
import { useSessionIdleStore } from "../../store/sessionIdleStore";
import { IDLE_TIMEOUT_MS } from "./sessionIdleConfig";
import {
  broadcastIdleLock,
  listenForRemoteIdleLock,
  listenForRemoteIdleUnlock,
} from "./idleBroadcast";

/** Eventos que cuentan como "actividad real" del usuario. */
const ACTIVITY_EVENTS = [
  "mousemove",
  "mousedown",
  "keydown",
  "scroll",
  "touchstart",
  "wheel",
] as const;

/**
 * Detecta inactividad global y bloquea la UI (Fase 3) tras `timeoutMs` sin actividad.
 * Pensado para un único montaje (AppLayout) que cubre todas las rutas protegidas — no
 * uno por pantalla. Al bloquear, dejar de escuchar actividad local: el candado solo se
 * levanta con una acción explícita del usuario (botón del overlay, Fase 4 reautenticación).
 */
export function useIdleTimeout(timeoutMs: number = IDLE_TIMEOUT_MS): void {
  const isLocked = useSessionIdleStore((s) => s.isLocked);
  const lock = useSessionIdleStore((s) => s.lock);
  const timerRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  // Bloqueo detectado en otra pestaña: siempre activo, incluso si esta pestaña ya está
  // bloqueada (idempotente) — así todas las pestañas quedan coherentes.
  useEffect(() => {
    return listenForRemoteIdleLock(() => {
      useSessionIdleStore.getState().lock();
    });
  }, []);

  // Desbloqueo por reautenticación exitosa en otra pestaña (Fase 4): la cookie de refresh ya
  // se actualizó (compartida por el navegador), así que esta pestaña puede desbloquear su UI
  // con seguridad sin repetir el login — ver idleBroadcast.ts.
  useEffect(() => {
    return listenForRemoteIdleUnlock(() => {
      useSessionIdleStore.getState().unlock();
    });
  }, []);

  useEffect(() => {
    if (isLocked) return;

    const resetTimer = () => {
      if (timerRef.current) clearTimeout(timerRef.current);
      timerRef.current = setTimeout(() => {
        lock();
        broadcastIdleLock();
      }, timeoutMs);
    };

    const handleVisibility = () => {
      if (document.visibilityState === "visible") resetTimer();
    };

    ACTIVITY_EVENTS.forEach((eventName) =>
      window.addEventListener(eventName, resetTimer, { passive: true }),
    );
    document.addEventListener("visibilitychange", handleVisibility);

    resetTimer();

    return () => {
      ACTIVITY_EVENTS.forEach((eventName) =>
        window.removeEventListener(eventName, resetTimer),
      );
      document.removeEventListener("visibilitychange", handleVisibility);
      if (timerRef.current) clearTimeout(timerRef.current);
    };
  }, [isLocked, lock, timeoutMs]);
}
