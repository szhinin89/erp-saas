import { NavLink, Outlet, useLocation, useNavigate, useSearchParams } from 'react-router-dom';
import { useEffect, useMemo } from 'react';
import { useAuthStore } from '../store/authStore';
import { useI18n } from '../i18n/i18n';
import './SuperAdminLayout.css';

const tabToPath: Record<string, string> = {
  overview: 'overview',
  companies: 'companies',
  plans: 'menu-plans',
  menus: 'menu-plans',
};

/**
 * Layout exclusivo del panel global SuperAdmin (sin chrome de empresa / AppLayout).
 */
export function SuperAdminLayout() {
  const { t } = useI18n();
  const location = useLocation();
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const { user, logout } = useAuthStore();

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

  const title = useMemo(() => {
    const p = location.pathname;
    if (p.includes('/menu-plans')) return t('superadmin.shell.menuAndPlans');
    if (p.includes('/menu-builder')) return t('superadmin.shell.menuAndPlans');
    if (p.includes('/plans')) return t('superadmin.shell.menuAndPlans');
    if (p.includes('/companies')) return t('superadmin.tabCompanies');
    if (p.includes('/overview')) return t('superadmin.tabOverview');
    return t('superadmin.title');
  }, [location.pathname, t]);

  const handleLogout = () => {
    logout();
    navigate('/login');
  };

  return (
    <div className="sa-shell">
      <div className="sa-shell-main">
        <header className="sa-shell-header">
          <h1>{title}</h1>
          <div className="sa-shell-header-actions">
            {user?.email ? <span className="subtle" style={{ fontSize: 12 }}>{user.email}</span> : null}
            <button type="button" onClick={() => void handleLogout()}>
              {t('app.logout')}
            </button>
          </div>
        </header>
        <nav className="sa-shell-tabs" aria-label={t('superadmin.shell.nav')}>
          <NavLink to="/superadmin/overview" className={({ isActive }) => (isActive ? 'active' : '')}>
            {t('superadmin.tabOverview')}
          </NavLink>
          <NavLink to="/superadmin/companies" className={({ isActive }) => (isActive ? 'active' : '')}>
            {t('superadmin.tabCompanies')}
          </NavLink>
          <NavLink to="/superadmin/menu-plans" className={({ isActive }) => (isActive ? 'active' : '')}>
            {t('superadmin.shell.menuAndPlans')}
          </NavLink>
        </nav>
        <div className="sa-shell-outlet">
          <Outlet />
        </div>
      </div>
    </div>
  );
}
