import { useCallback, useEffect, useRef } from "react";
import { useBlocker } from "react-router-dom";
import { useI18n, useOptionalI18n } from "../i18n/i18n";
import { message } from "../lib/messages";
import { useAuthStore } from "../store/authStore";

/**
 * Protección de cambios sin guardar (BP-NAV-GUARD-01), mecanismo único:
 *
 * - **Fuente única:** cada pantalla declara sus cambios con `useUnsavedChangesGuard(hasUnsaved)`; el
 *   registro de este módulo es lo que consultan el bloqueador, `beforeunload` y las acciones voluntarias.
 * - **Navegación del router:** `useBlocker` (router de datos de App.tsx) con `message.confirm`. Cambiar solo
 *   la query (`?tab=`) no cambia de ruta y nunca bloquea.
 * - **Acciones voluntarias que alteran sesión/empresa** (cambio de empresa, cerrar sesión, volver al
 *   contexto global): consultan antes de ejecutar con `useDiscardConfirmation`. Al confirmar queda una
 *   aprobación que evita la segunda confirmación del router en la navegación que sigue; quien la pidió debe
 *   liberarla con `releaseDiscardApproval()` al terminar (éxito o fallo).
 * - **Eventos automáticos de seguridad** (401 tras refresh fallido, expiración, revocación, logout remoto):
 *   nunca se bloquean. Todos dejan primero la sesión sin autenticar (`fullLogout`), y con la sesión inactiva
 *   ni el bloqueador ni `beforeunload` intervienen; no se restaura ninguna sesión.
 * - Una sola confirmación a la vez, compartida por todas las vías.
 */

const pending = new Set<symbol>();
let discardApproved = false;
let inFlight: Promise<boolean> | null = null;

const hasPending = () => pending.size > 0;
const sessionActive = () => useAuthStore.getState().isAuthenticated;
/** Hay cambios que proteger y nada autoriza prescindir de ellos. */
const guardActive = () => hasPending() && !discardApproved && sessionActive();

type Translate = ReturnType<typeof useI18n>["t"];

function askDiscard(t: Translate): Promise<boolean> {
  inFlight ??= message
    .confirm({
      title: t("common.discardChangesTitle", "¿Descartar cambios?"),
      message: t("common.unsavedChanges", "Tienes cambios sin guardar."),
      variant: "warning",
      confirmLabel: t("common.discard", "Descartar"),
      cancelLabel: t("common.cancel"),
    })
    .finally(() => {
      inFlight = null;
    });
  return inFlight;
}

/** Libera la aprobación de descarte concedida por una acción voluntaria (llamar siempre al terminar). */
export function releaseDiscardApproval(): void {
  discardApproved = false;
}

/**
 * Devuelve `confirmDiscard()`: `true` si no hay cambios pendientes o el usuario confirma descartarlos;
 * `false` si cancela (la acción no debe ejecutarse). Con cambios pendientes y sesión activa muestra la
 * confirmación oficial una sola vez, aunque se invoque varias veces.
 */
export function useDiscardConfirmation(): () => Promise<boolean> {
  const { t } = useOptionalI18n();
  return useCallback(async () => {
    if (!guardActive()) return true;
    const confirmed = await askDiscard(t);
    if (confirmed) discardApproved = true;
    return confirmed;
  }, [t]);
}

export function useUnsavedChangesGuard(hasUnsaved: boolean): void {
  const { t } = useI18n();
  const id = useRef(Symbol("unsaved-changes"));

  useEffect(() => {
    const key = id.current;
    if (hasUnsaved) pending.add(key);
    return () => {
      pending.delete(key);
      if (!hasPending()) discardApproved = false;
    };
  }, [hasUnsaved]);

  const blocker = useBlocker(
    useCallback(
      ({
        currentLocation,
        nextLocation,
      }: {
        currentLocation: { pathname: string };
        nextLocation: { pathname: string };
      }) => guardActive() && currentLocation.pathname !== nextLocation.pathname,
      [],
    ),
  );
  const alive = useRef(true);
  useEffect(() => {
    alive.current = true;
    return () => {
      alive.current = false;
    };
  }, []);

  const handled = useRef(false);
  useEffect(() => {
    if (blocker.state !== "blocked") {
      handled.current = false;
      return;
    }
    if (handled.current) return;
    handled.current = true;
    // La sesión terminó o una acción voluntaria ya fue confirmada mientras esperaba: no preguntar de nuevo.
    if (!guardActive()) {
      blocker.proceed();
      return;
    }
    void askDiscard(t).then((confirmed) => {
      if (!alive.current) return;
      if (confirmed) blocker.proceed();
      else blocker.reset();
    });
  }, [blocker, t]);

  useEffect(() => {
    if (!hasUnsaved) return;
    const onBeforeUnload = (e: BeforeUnloadEvent) => {
      if (guardActive()) e.preventDefault();
    };
    window.addEventListener("beforeunload", onBeforeUnload);
    return () => window.removeEventListener("beforeunload", onBeforeUnload);
  }, [hasUnsaved]);
}
