import { NavLink, Navigate, Outlet, useLocation, useNavigate } from 'react-router-dom';
import { useEffect, useMemo, useState } from 'react';
import { useAuthStore } from '../store/authStore';
import { useI18n } from '../i18n/i18n';
import { useSuperAdminGate } from '../hooks/useSuperAdminGate';
import { fullLogout } from '../lib/session/fullLogout';
import { SUPERADMIN_IMPERSONATION_NAME_KEY } from '../lib/session/sessionStorageKeys';
import { RuntimeModeBadge } from '../components/RuntimeModeBadge';
import { LayoutFrame } from '../components/layout/LayoutFrame';
import { SUPERADMIN_UI } from '../modules/superadmin/api/platformApiPaths';
import './SuperAdminLayout.css';

export function SuperAdminLayout() {
  const { t } = useI18n();
  const { isSuperAdmin } = useSuperAdminGate();
  const location = useLocation();
  const navigate = useNavigate();
  const { user } = useAuthStore();

  const [impersonationName, setImpersonationName] = useState(
    () => localStorage.getItem(SUPERADMIN_IMPERSONATION_NAME_KEY) ?? '',
  );

  useEffect(() => {
    if (location.pathname !== '/superadmin') return;
    navigate(SUPERADMIN_UI.overview, { replace: true });
  }, [location.pathname, navigate]);

  const pageTitle = useMemo(() => {
    const p = location.pathname;
    if (p.includes('/subscribers/') && p !== SUPERADMIN_UI.subscribers) return 'Suscriptor';
    if (p.includes('/subscribers')) return 'Suscriptores';
    if (p.includes('/plans')) return t('superadmin.shell.plans');
    if (p.includes('/users')) return 'Platform users';
    if (p.includes('/billing')) return 'Platform billing';
    if (p.includes('/observability')) return 'Observability';
    if (p.includes('/audit')) return 'Audit log';
    if (p.includes('/overview')) return t('superadmin.title');
    return t('superadmin.title');
  }, [location.pathname, t]);

  const handleLogout = () => {
    fullLogout();
    navigate('/login');
  };

  const exitImpersonation = () => {
    localStorage.removeItem(SUPERADMIN_IMPERSONATION_NAME_KEY);
    setImpersonationName('');
    navigate(SUPERADMIN_UI.overview);
  };

  const showBanner = !!impersonationName;
  const userInitial = (user?.fullName ?? user?.email ?? 'SA').charAt(0).toUpperCase();

  if (!isSuperAdmin) {
    return <Navigate to="/login" replace />;
  }

  const navLinkClass = ({ isActive }: { isActive: boolean }) =>
    `sa-nav-link${isActive ? ' is-active' : ''}`;

  return (
    <div className={`sa-shell${showBanner ? ' sa-shell--with-banner' : ''}`}>
      {showBanner && (
        <div className="sa-banner">
          <div className="sa-banner-left">
            <span className="material-symbols-outlined">person_search</span>
            <span>
              Modo Impersonación: visualizando <strong>{impersonationName}</strong> como SuperAdmin
            </span>
          </div>
          <button className="sa-banner-btn" onClick={exitImpersonation}>
            Volver a Global
            <span className="material-symbols-outlined">logout</span>
          </button>
        </div>
      )}

      <aside className="sa-sidebar" aria-label="Navegación SuperAdmin">
        <div className="sa-sidebar-logo">
          <div className="sa-sidebar-logo-icon" aria-hidden="true">
            <span className="material-symbols-outlined">shield_person</span>
          </div>
          <div>
            <p className="sa-sidebar-logo-name">ERP Portal</p>
            <p className="sa-sidebar-logo-sub">Platform Control Plane</p>
          </div>
        </div>

        <nav className="sa-sidebar-nav">
          <NavLink to={SUPERADMIN_UI.overview} className={navLinkClass} end>
            <span className="sa-nav-icon material-symbols-outlined">dashboard</span>
            <span>{t('superadmin.tabOverview')}</span>
          </NavLink>

          <NavLink to={SUPERADMIN_UI.subscribers} className={navLinkClass}>
            <span className="sa-nav-icon material-symbols-outlined">manage_accounts</span>
            <span>Suscriptores</span>
          </NavLink>

          <NavLink to={SUPERADMIN_UI.plans} className={navLinkClass} end={false}>
            <span className="sa-nav-icon material-symbols-outlined">loyalty</span>
            <span>{t('superadmin.shell.plans')}</span>
          </NavLink>

          <NavLink to={SUPERADMIN_UI.users} className={navLinkClass}>
            <span className="sa-nav-icon material-symbols-outlined">group</span>
            <span>Users</span>
          </NavLink>

          <NavLink to={SUPERADMIN_UI.billing} className={navLinkClass}>
            <span className="sa-nav-icon material-symbols-outlined">receipt_long</span>
            <span>Billing</span>
          </NavLink>

          <div className="sa-nav-divider" />

          <NavLink to={SUPERADMIN_UI.observability} className={navLinkClass}>
            <span className="sa-nav-icon material-symbols-outlined">monitoring</span>
            <span>Observability</span>
          </NavLink>

          <NavLink to={SUPERADMIN_UI.audit} className={navLinkClass}>
            <span className="sa-nav-icon material-symbols-outlined">history</span>
            <span>Audit</span>
          </NavLink>
        </nav>

        <div className="sa-sidebar-footer">
          <button
            className="sa-sidebar-new-btn"
            type="button"
            onClick={() => navigate(SUPERADMIN_UI.subscribers)}
          >
            <span className="material-symbols-outlined">add_circle</span>
            Nuevo suscriptor
          </button>

          <button className="sa-sidebar-util-link" type="button" onClick={handleLogout}>
            <span className="material-symbols-outlined">logout</span>
            <span>{t('app.logout')}</span>
          </button>
        </div>
      </aside>

      <header className="sa-topbar">
        <h2 className="sa-topbar-title">{pageTitle}</h2>

        <div className="sa-topbar-right">
          <RuntimeModeBadge />
          <div className="sa-topbar-search">
            <span className="material-symbols-outlined">search</span>
            <input type="text" placeholder="Buscar suscriptores…" />
          </div>

          <div className="sa-topbar-actions">
            <button className="sa-topbar-icon-btn" type="button" aria-label="Notificaciones">
              <span className="material-symbols-outlined">notifications</span>
              <span className="sa-topbar-notif-dot" aria-hidden="true" />
            </button>
            <button className="sa-topbar-icon-btn" type="button" aria-label="Ayuda">
              <span className="material-symbols-outlined">help</span>
            </button>
          </div>

          <div className="sa-topbar-divider" aria-hidden="true" />

          <div className="sa-topbar-user">
            <div className="sa-topbar-user-info">
              <span className="sa-topbar-user-name">Super Admin</span>
              <span className="sa-topbar-user-sub">{user?.email ?? ''}</span>
            </div>
            <div className="sa-topbar-avatar" aria-hidden="true">{userInitial}</div>
          </div>
        </div>
      </header>

      <div className="sa-shell-content">
        <LayoutFrame variant="platform" className="sa-shell__frame">
          <Outlet />
        </LayoutFrame>
      </div>

      <button
        className="sa-fab"
        type="button"
        aria-label="Nuevo suscriptor"
        onClick={() => navigate(SUPERADMIN_UI.subscribers)}
      >
        <span className="material-symbols-outlined">add</span>
      </button>
    </div>
  );
}
