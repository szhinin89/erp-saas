import { NavLink } from "react-router-dom";
import "./AdminCoreMenu.css";

/**
 * Menú global propio de AdminGlobalCore — deliberadamente estático (no viene de
 * GET /api/v1/me/menu, que exige tenant_id real vía policy "Session" y nunca debe ser llamado
 * en este shell). "Configuración global / proveedor SRI" queda pendiente: existe el endpoint
 * backend (SystemProviderSettingsController) pero aún no tiene página frontend.
 */
export function AdminCoreMenu({ onLogout }: { onLogout: () => void }) {
  return (
    <nav className="admin-core-menu" aria-label="Menú Admin Core">
      <NavLink
        to="/admin-core/dashboard"
        className={({ isActive }) =>
          isActive ? "admin-core-menu-link admin-core-menu-link--active" : "admin-core-menu-link"
        }
      >
        Dashboard global
      </NavLink>
      <NavLink
        to="/admin-core/companies/new"
        className={({ isActive }) =>
          isActive ? "admin-core-menu-link admin-core-menu-link--active" : "admin-core-menu-link"
        }
      >
        Nueva empresa
      </NavLink>
      <button type="button" className="admin-core-menu-link admin-core-menu-logout" onClick={onLogout}>
        Cerrar sesión
      </button>
    </nav>
  );
}
