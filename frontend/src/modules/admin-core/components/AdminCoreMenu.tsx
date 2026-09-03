import { NavLink } from "react-router-dom";
import "./AdminCoreMenu.css";

/**
 * Menú global propio de AdminGlobalCore — deliberadamente estático (no viene de
 * GET /api/v1/me/menu, que exige tenant_id real vía policy "Session" y nunca debe ser llamado
 * en este shell).
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
      <div className="admin-core-menu-group">
        <span className="admin-core-menu-group-label">Configuración global</span>
        <NavLink
          to="/admin-core/system-provider-settings"
          className={({ isActive }) =>
            isActive
              ? "admin-core-menu-link admin-core-menu-link--active"
              : "admin-core-menu-link"
          }
        >
          Proveedor SRI
        </NavLink>
      </div>
      <button type="button" className="admin-core-menu-link admin-core-menu-logout" onClick={onLogout}>
        Cerrar sesión
      </button>
    </nav>
  );
}
