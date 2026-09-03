import { Outlet, useNavigate } from "react-router-dom";
import { LayoutFrame } from "../../../components/layout/LayoutFrame";
import { logoutSession } from "../../../lib/session/logoutSession";
import { AdminCoreMenu } from "./AdminCoreMenu";
import "./AdminCoreLayout.css";

/**
 * Shell propio de AdminGlobalCore — envuelve LayoutFrame (variant="platform") con un menú
 * global estático propio. Nunca comparte AppLayout ni el menú operativo con el ERP de tenant:
 * ver docs/architecture (Fase B) — este layout no debe importar ni renderizar nada de
 * useAppLayoutNavigation/ConfigContext/useBranchGate (que disparan endpoints operativos).
 */
export function AdminCoreLayout() {
  const navigate = useNavigate();

  const handleLogout = () => {
    void logoutSession().finally(() => navigate("/admin-core/login"));
  };

  return (
    <LayoutFrame
      variant="platform"
      topUtilities={
        <div className="admin-core-header">
          <span className="admin-core-header-brand">AdminGlobalCore</span>
          <AdminCoreMenu onLogout={handleLogout} />
        </div>
      }
    >
      <Outlet />
    </LayoutFrame>
  );
}
