import { NavLink, Outlet, useLocation, useNavigate, useSearchParams } from 'react-router-dom';
import { useEffect, useMemo, useState } from 'react';
import { useAuthStore } from '../store/authStore';
import { useI18n } from '../i18n/i18n';
import './SuperAdminLayout.css';

const tabToPath: Record<string, string> = {
  overview: 'overview',
  companies: 'companies',
  plans: 'menu-plans',
  menus: 'menu-plans',
};

export function SuperAdminLayout() {
  const { t } = useI18n();
  const location = useLocation();
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const { user, logout } = useAuthStore();

  const [impersonationName, setImpersonationName] = useState(
    () => localStorage.getItem('superadmin-impersonation-tenant-name') ?? '',
  );

  /* ── Existing redirect logic (unchanged) ── */
  useEffect(() => {
    if (location.pathname !== '/superadmin') return;
    const tab = searchParams.get('tab')?.trim().toLowerCase();
    if (!tab) return;
    if (tab === 'plans') {
      navigate('/superadmin/menu-plans?tab=plans', { replace: true });
      return;
    }
    if (tab === 'features') {
      navigate('/superadmin/menu-plans?tab=plans', { replace: true });
      return;
    }
    if (tab === 'menus') {
      navigate('/superadmin/menu-plans?tab=menu', { replace: true });
      return;
    }
    const seg = tabToPath[tab];
    if (!seg) return;
    navigate(`/superadmin/${seg}`, { replace: true });
  }, [location.pathname, navigate, searchParams]);

  const pageTitle = useMemo(() => {
    const p = location.pathname;
    if (p.includes('/menu-plans')) return t('superadmin.shell.menuAndPlans');
    if (p.includes('/plans')) return t('superadmin.shell.plans');
    if (p.includes('/companies')) return t('superadmin.tabCompanies');
    if (p.includes('/overview')) return t('superadmin.title');
    return t('superadmin.title');
  }, [location.pathname, t]);

  const handleLogout = () => {
    logout();
    navigate('/login');
  };

  const exitImpersonation = () => {
    localStorage.removeItem('superadmin-impersonation-tenant-name');
    setImpersonationName('');
    navigate('/dashboard');
  };

  const showBanner = !!impersonationName;
  const userInitial = (user?.fullName ?? user?.email ?? 'SA').charAt(0).toUpperCase();

  return (
    <div className={`sa-shell${showBanner ? ' sa-shell--with-banner' : ''}`}>

      {/* ── Impersonation banner ── */}
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

      {/* ── Sidebar ── */}
      <aside className="sa-sidebar" aria-label="Navegación SuperAdmin">
        {/* Logo */}
        <div className="sa-sidebar-logo">
          <div className="sa-sidebar-logo-icon" aria-hidden="true">
            <span className="material-symbols-outlined">shield_person</span>
          </div>
          <div>
            <p className="sa-sidebar-logo-name">ERP Portal</p>
            <p className="sa-sidebar-logo-sub">Administrador</p>
          </div>
        </div>

        {/* Nav */}
        <nav className="sa-sidebar-nav">
          <NavLink
            to="/superadmin/overview"
            className={({ isActive }) => `sa-nav-link${isActive ? ' is-active' : ''}`}
          >
            <span className="sa-nav-icon material-symbols-outlined">dashboard</span>
            <span>{t('superadmin.tabOverview')}</span>
          </NavLink>

          <NavLink
            to="/superadmin/companies"
            className={({ isActive }) => `sa-nav-link${isActive ? ' is-active' : ''}`}
          >
            <span className="sa-nav-icon material-symbols-outlined">corporate_fare</span>
            <span>{t('superadmin.tabCompanies')}</span>
          </NavLink>

          <NavLink
            to="/superadmin/plans"
            className={({ isActive }) => `sa-nav-link${isActive ? ' is-active' : ''}`}
            end={false}
          >
            <span className="sa-nav-icon material-symbols-outlined">loyalty</span>
            <span>{t('superadmin.shell.plans')}</span>
          </NavLink>

          <NavLink
            to="/superadmin/menu-plans"
            className={({ isActive }) => `sa-nav-link${isActive ? ' is-active' : ''}`}
            end={false}
          >
            <span className="sa-nav-icon material-symbols-outlined">menu_book</span>
            <span>{t('superadmin.shell.menuAndPlans')}</span>
          </NavLink>
        </nav>

        {/* Footer */}
        <div className="sa-sidebar-footer">
          <button
            className="sa-sidebar-new-btn"
            type="button"
            onClick={() => navigate('/superadmin/companies')}
          >
            <span className="material-symbols-outlined">add_circle</span>
            Nuevo Registro
          </button>

          <button
            className="sa-sidebar-util-link"
            type="button"
            onClick={handleLogout}
          >
            <span className="material-symbols-outlined">logout</span>
            <span>{t('app.logout')}</span>
          </button>
        </div>
      </aside>

      {/* ── Top app bar ── */}
      <header className="sa-topbar">
        <h2 className="sa-topbar-title">{pageTitle}</h2>

        <div className="sa-topbar-right">
          {/* Search */}
          <div className="sa-topbar-search">
            <span className="material-symbols-outlined">search</span>
            <input type="text" placeholder="Buscar empresas..." />
          </div>

          {/* Icon actions */}
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

          {/* User */}
          <div className="sa-topbar-user">
            <div className="sa-topbar-user-info">
              <span className="sa-topbar-user-name">Super Admin</span>
              <span className="sa-topbar-user-sub">{user?.email ?? ''}</span>
            </div>
            <div className="sa-topbar-avatar" aria-hidden="true">{userInitial}</div>
          </div>
        </div>
      </header>

      {/* ── Main content ── */}
      <main className="sa-shell-content">
        <div className="sa-shell-outlet">
          <Outlet />
        </div>
      </main>

      {/* ── FAB ── */}
      <button
        className="sa-fab"
        type="button"
        aria-label="Nuevo registro"
        onClick={() => navigate('/superadmin/companies')}
      >
        <span className="material-symbols-outlined">add</span>
      </button>
    </div>
  );
}
