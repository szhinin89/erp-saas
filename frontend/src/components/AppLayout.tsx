import { Outlet, useNavigate } from "react-router-dom";
import { ZHAppTenantHeader } from "./zh/ZHAppTenantHeader";
import { ZHToast } from "./zh/ZHToast";
import { ZHGlobalDialogs } from "./zh/ZHGlobalDialogs";
import { SessionLockOverlay } from "./zh/SessionLockOverlay";
import { ZHPageNotice } from "./zh/ZHPageNotice";
import { ZHBtn } from "./zh/ZHForm";
import { CompanySwitcher } from "./CompanySwitcher";
import { LanguageSwitcher } from "./LanguageSwitcher";
import { logoutSession } from "../lib/session/logoutSession";
import { useIdleTimeout } from "../lib/session/useIdleTimeout";
import { LayoutFrame } from "./layout/LayoutFrame";
import { useAppLayoutNavigation } from "./useAppLayoutNavigation";
import { RouteAccessGuard } from "./RouteAccessGuard";
import { useBranchGate } from "./useBranchGate";
import { BranchSelectorModal } from "./BranchSelectorModal";
import { authService } from "../modules/auth/api/authService";
import { useAuthStore } from "../store/authStore";
import "./AppLayout.css";

export function AppLayout() {
  const navigate = useNavigate();

  const {
    user,
    sessionMenuResolved,
    mainMenuGroups,
    isFavorite,
    toggleFavorite,
  } = useAppLayoutNavigation();

  const branchGate = useBranchGate();
  const login = useAuthStore((s) => s.login);

  // Fase 3: barrera de inactividad — un único temporizador para todas las rutas
  // protegidas (AppLayout se monta una sola vez, no por pantalla).
  useIdleTimeout();

  const handleLogout = () => {
    void logoutSession().finally(() => navigate("/login"));
  };

  const handleReturnToGlobal = async () => {
    try {
      const payload = await authService.returnToGlobal();
      login(payload);
      navigate("/admin-core/dashboard", { replace: true });
    } catch {
      // Fallback documentado (Fase E): si /return falla, cerrar sesión y volver al login global.
      void logoutSession().finally(() => navigate("/admin-core/login"));
    }
  };

  return (
    <LayoutFrame
      variant="tenant"
      banner={
        user?.operatorMode ? (
          <div className="app-operatorBanner">
            <ZHPageNotice
              variant="attention"
              message="AdminGlobalCore operando empresa"
              icon="admin_panel_settings"
            />
            <ZHBtn
              variant="ghost"
              size="sm"
              type="button"
              onClick={() => void handleReturnToGlobal()}
            >
              Volver al Admin Core
            </ZHBtn>
          </div>
        ) : undefined
      }
      topUtilities={
        <div className="app-tenantHeaderWrap">
          <ZHAppTenantHeader
            onLogout={handleLogout}
            rightExtra={
              <>
                <LanguageSwitcher />
                {user ? <CompanySwitcher /> : null}
              </>
            }
            navigation={
              user
                ? {
                    mainMenuGroups,
                    sessionMenuResolved,
                    isFavorite,
                    toggleFavorite,
                  }
                : undefined
            }
          />
        </div>
      }
    >
      {branchGate.gateOpen ? (
        <BranchSelectorModal
          open
          loading={branchGate.loading}
          options={branchGate.options}
          error={branchGate.error}
          switching={branchGate.switching}
          onSelect={(branchId) => {
            void branchGate.selectBranch(branchId);
          }}
          onRetry={() => {
            void branchGate.retry();
          }}
        />
      ) : (
        <RouteAccessGuard
          mainMenuGroups={mainMenuGroups}
          sessionMenuResolved={sessionMenuResolved}
        >
          <Outlet />
        </RouteAccessGuard>
      )}
      <ZHToast />
      <ZHGlobalDialogs />
      <SessionLockOverlay />
    </LayoutFrame>
  );
}
