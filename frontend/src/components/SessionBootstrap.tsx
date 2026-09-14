import type { ReactNode } from "react";
import { useEffect, useState } from "react";
import { useAuthStore } from "../store/authStore";
import { useSessionStore } from "../store/sessionStore";
import { useElectronicInvoicingStatusStore } from "../store/electronicInvoicingStatusStore";
import { restoreSessionFromCookie } from "../lib/session/restoreSessionFromCookie";
import { getAccessToken } from "../lib/session/authTokenMemory";
import { initializeAuthBroadcastListener } from "../lib/session/authRefreshManager";
import { loadPrecisionPolicy } from "../lib/config/precisionPolicy.config";

type Props = { children: ReactNode };

/**
 * Espera hidratación Zustand y restaura access token vía cookie httpOnly si hace falta.
 * No renderiza rutas hasta bootstrap completo (evita flash login/app).
 */
export function SessionBootstrap({ children }: Props) {
  const hasHydrated = useAuthStore((s) => s.hasHydrated);
  const isAuthenticated = useAuthStore((s) => s.isAuthenticated);
  const [ready, setReady] = useState(false);

  // Cada pestaña debe escuchar el logout remoto aun si no necesitó refresh.
  useEffect(() => {
    initializeAuthBroadcastListener();
  }, []);

  useEffect(() => {
    if (!hasHydrated) return;

    let cancelled = false;
    (async () => {
      // Una pestaña abierta con `noopener` no hereda sessionStorage. Intentar
      // restaurar desde la cookie permite reconstruir su perfil y sucursal activa.
      if (!getAccessToken()) {
        await restoreSessionFromCookie();
      }
      if (cancelled) return;
      // Marca sessionStore como "cargando" ANTES de desbloquear el render de rutas
      // protegidas: para cuando AppLayout monte useBranchGate en este mismo commit,
      // isLoading ya está en true, evitando que decida "sin sucursales" con el
      // GET /session/context todavía sin resolver (ZH-AUTH-SESSION-HYDRATION-BRANCH-MODAL-10).
      if (useAuthStore.getState().isAuthenticated) {
        useSessionStore.setState({ isLoading: true });
      }
      setReady(true);
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
      // Config de precisión operativa por empresa — debe estar disponible antes de que
      // cualquier módulo (Ventas, Compras, Items, Inventory, Expenses, Payables) formatee
      // o valide montos. company_precision_policy / precisionPolicy.config.ts es la única
      // SSOT frontend desde COMPANY-PRECISION-POLICY-FRONTEND-CONSUMERS-MIGRATION-07
      // (2026-09-13): decimal.config.ts (legacy) fue eliminado del frontend tras confirmar
      // cero consumidores productivos (ZhCurrencyInput pasó a fallback fijo de 2 decimales;
      // la pantalla legacy DecimalSettingsSection.tsx fue eliminada por estar huérfana, ya
      // reemplazada por PrecisionPolicySettingsSection).
      void loadPrecisionPolicy();
      // Estado LOCAL de facturación electrónica (certificado/ambiente/URL) — alimenta
      // ZHElectronicEnvironmentBanner en cualquier pantalla emisora sin que cada una dispare su
      // propia petición. ELECTRONIC-INVOICING-SRI-CONNECTIVITY-CHECK-SCOPE-01: refresh() nunca
      // hace ping externo al SRI (por eso no genera el ruido/latencia de SocketException 10054
      // en cada bootstrap) — la conectividad real solo se verifica en Ventas
      // (electronicInvoicingStatusStore.refreshConnectivity, ver useSalesPage.ts).
      void useElectronicInvoicingStatusStore.getState().refresh();
    } else {
      useSessionStore.getState().clear();
      useElectronicInvoicingStatusStore.getState().clear();
    }
  }, [ready, isAuthenticated]);

  if (!hasHydrated || !ready) return null;

  return <>{children}</>;
}
