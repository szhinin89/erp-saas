import type { ReactNode } from "react";
import { useEffect, useState } from "react";
import { useAuthStore } from "../store/authStore";
import { useSessionStore } from "../store/sessionStore";
import { useElectronicInvoicingStatusStore } from "../store/electronicInvoicingStatusStore";
import { restoreSessionFromCookie } from "../lib/session/restoreSessionFromCookie";
import { getAccessToken } from "../lib/session/authTokenMemory";
import { loadDecimalConfig } from "../lib/config/decimal.config";

type Props = { children: ReactNode };

/**
 * Espera hidratación Zustand y restaura access token vía cookie httpOnly si hace falta.
 * No renderiza rutas hasta bootstrap completo (evita flash login/app).
 */
export function SessionBootstrap({ children }: Props) {
  const hasHydrated = useAuthStore((s) => s.hasHydrated);
  const isAuthenticated = useAuthStore((s) => s.isAuthenticated);
  const [ready, setReady] = useState(false);

  useEffect(() => {
    if (!hasHydrated) return;

    let cancelled = false;
    (async () => {
      if (isAuthenticated && !getAccessToken()) {
        await restoreSessionFromCookie();
      }
      if (!cancelled) setReady(true);
    })();

    return () => {
      cancelled = true;
    };
  }, [hasHydrated, isAuthenticated]);

  // Carga el contexto de sesión (identidad/empresa/roles/permisos) en segundo plano,
  // sin bloquear el render — el header tiene fallback mientras se resuelve.
  useEffect(() => {
    if (!ready) return;

    if (isAuthenticated) {
      void useSessionStore.getState().refresh();
      // Config de decimales por empresa — debe estar disponible antes de que
      // cualquier módulo (Ventas, Compras, Items) formatee o valide montos.
      void loadDecimalConfig();
      // Estado de facturación electrónica — alimenta ZHElectronicEnvironmentBanner en
      // cualquier pantalla emisora sin que cada una dispare su propia petición.
      void useElectronicInvoicingStatusStore.getState().refresh();
    } else {
      useSessionStore.getState().clear();
      useElectronicInvoicingStatusStore.getState().clear();
    }
  }, [ready, isAuthenticated]);

  if (!hasHydrated || !ready) return null;

  return <>{children}</>;
}
