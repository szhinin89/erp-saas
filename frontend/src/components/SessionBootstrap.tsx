import type { ReactNode } from "react";
import { useEffect, useState } from "react";
import { useAuthStore } from "../store/authStore";
import { useSessionStore } from "../store/sessionStore";
import { useElectronicInvoicingStatusStore } from "../store/electronicInvoicingStatusStore";
import { restoreSessionFromCookie } from "../lib/session/restoreSessionFromCookie";
import { getAccessToken } from "../lib/session/authTokenMemory";
import { initializeAuthBroadcastListener } from "../lib/session/authRefreshManager";
import { clearPrecisionPolicy, loadPrecisionPolicy } from "../lib/config/precisionPolicy.config";
import { useOptionalI18n } from "../i18n/i18n";
import { ZHBtn } from "./zh/ZHForm";
import { ZHPageNotice } from "./zh/ZHPageNotice";

type Props = { children: ReactNode };

/**
 * Espera hidratación Zustand y restaura access token vía cookie httpOnly si hace falta.
 * No renderiza rutas hasta bootstrap completo (evita flash login/app).
 */
export function SessionBootstrap({ children }: Props) {
  const hasHydrated = useAuthStore((s) => s.hasHydrated);
  const isAuthenticated = useAuthStore((s) => s.isAuthenticated);
  const companyId = useAuthStore((s) => s.user?.companyId ?? null);
  const { t } = useOptionalI18n();
  const [ready, setReady] = useState(false);
  // La política de precisión de la empresa se exige ANTES de renderizar la app: los módulos la
  // leen de forma síncrona y no existen valores por defecto (fail-closed).
  const [policyResult, setPolicyResult] = useState<{
    companyId: string | null;
    status: "ready" | "error";
  } | null>(null);
  const [policyAttempt, setPolicyAttempt] = useState(0);
  const needsPolicy = ready && isAuthenticated && !!companyId;

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

  // Política de precisión de la empresa (company_precision_policy, único SSOT). Se limpia al salir
  // de sesión para que la política de una empresa nunca sobreviva a otra.
  useEffect(() => {
    if (!ready) return;
    if (!needsPolicy) {
      clearPrecisionPolicy();
      return;
    }
    let cancelled = false;
    loadPrecisionPolicy().then(
      () => {
        if (!cancelled) setPolicyResult({ companyId, status: "ready" });
      },
      () => {
        if (!cancelled) setPolicyResult({ companyId, status: "error" });
      },
    );
    return () => {
      cancelled = true;
    };
  }, [ready, needsPolicy, companyId, policyAttempt]);

  if (!hasHydrated || !ready) return null;

  // La política de OTRA empresa nunca vale: mientras no se resuelva la de la empresa activa, no hay app.
  if (needsPolicy && policyResult?.companyId !== companyId) return null;
  if (needsPolicy && policyResult?.status !== "ready") {
    return (
      <div>
        <ZHPageNotice variant="error" message={t("session.precisionPolicy.loadError")} />
        <ZHBtn
          variant="primary"
          size="md"
          type="button"
          onClick={() => {
            setPolicyResult(null);
            setPolicyAttempt((n) => n + 1);
          }}
        >
          {t("session.precisionPolicy.retry")}
        </ZHBtn>
      </div>
    );
  }

  return <>{children}</>;
}
