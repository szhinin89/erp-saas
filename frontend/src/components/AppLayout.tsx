import { Outlet, useNavigate } from "react-router-dom";
import { ZHAppTenantHeader } from "./zh/ZHAppTenantHeader";
import { ZHToast } from "./zh/ZHToast";
import { ZHGlobalDialogs } from "./zh/ZHGlobalDialogs";
import { SessionLockOverlay } from "./zh/SessionLockOverlay";
import { CompanySwitcher } from "./CompanySwitcher";
import { LanguageSwitcher } from "./LanguageSwitcher";
import { logoutSession } from "../lib/session/logoutSession";
import { useIdleTimeout } from "../lib/session/useIdleTimeout";
import { LayoutFrame } from "./layout/LayoutFrame";
import { useAppLayoutNavigation } from "./useAppLayoutNavigation";
import { RouteAccessGuard } from "./RouteAccessGuard";
import { useBranchGate } from "./useBranchGate";
import { BranchSelectorModal } from "./BranchSelectorModal";
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

  // Fase 3: barrera de inactividad — un único temporizador para todas las rutas
  // protegidas (AppLayout se monta una sola vez, no por pantalla).
  useIdleTimeout();

  const handleLogout = () => {
    void logoutSession().finally(() => navigate("/login"));
  };

  return (
    <LayoutFrame
      variant="tenant"
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
