import { NavLink, Outlet, useLocation, useNavigate } from 'react-router-dom';
import { useEffect, useMemo, useState } from 'react';
import { useAuthStore } from '../store/authStore';
import { useI18n } from '../i18n/i18n';
import { LanguageSwitcher } from './LanguageSwitcher';
import { usePermissionsStore } from '../store/permissionsStore';
import { accessService } from '../services/accessService';
import './AppLayout.css';

function getImpersonationTenantName(): string | null {
  return localStorage.getItem('superadmin-impersonation-tenant-name');
}

export function AppLayout() {
  const { user, logout } = useAuthStore();
  const { permissions, has: hasPerm, clearPermissions, hasHydrated: permsHydrated } = usePermissionsStore();
  const navigate = useNavigate();
  const location = useLocation();
  const { t } = useI18n();

  // Auto-recupera permisos después de refresh/hidratación.
  useEffect(() => {
    let cancelled = false;
    const tenantId = user?.tenantId ?? '';
    const isGlobalSuperAdmin =
      (user?.role ?? '') === 'SuperAdmin' &&
      tenantId === '00000000-0000-0000-0000-000000000000';

    if (!user || isGlobalSuperAdmin) return;
    if (permissions.length > 0) return;

    void Promise.resolve().then(async () => {
      try {
        const res = await accessService.getMyPermissions();
        if (!cancelled) usePermissionsStore.getState().setPermissions(res?.permissions ?? []);
      } catch {
        // si falla, el menú seguirá ocultando items hasta re-login/switch tenant
      }
    });

    return () => {
      cancelled = true;
    };
  }, [permissions.length, user]);

  type NavItem = { to: string; label: string; icon?: string; permissionKey?: string };
  type NavGroup = { id: string; label: string; icon: string; items: NavItem[]; roles?: string[] };

  const groups: NavGroup[] = useMemo(() => ([
    {
      id: 'home',
      label: t('app.nav.group.home'),
      icon: '⊞',
      items: [{ to: '/dashboard', label: t('app.nav.dashboard') }],
    },
    {
      id: 'catalog',
      label: t('app.nav.group.catalog'),
      icon: '📦',
      items: [
        { to: '/products', label: t('app.nav.products'), permissionKey: 'catalog.products.view' },
        { to: '/catalog/brands', label: t('app.nav.catalog.brands'), permissionKey: 'catalog.brands.view' },
        { to: '/catalog/product-types', label: t('app.nav.catalog.productTypes'), permissionKey: 'catalog.productTypes.view' },
        { to: '/catalog/units', label: t('app.nav.catalog.units'), permissionKey: 'catalog.units.view' },
        { to: '/catalog/tax-rates', label: t('app.nav.catalog.taxRates'), permissionKey: 'catalog.taxRates.view' },
        { to: '/catalog/tariffs', label: t('app.nav.catalog.tariffs'), permissionKey: 'catalog.tariffs.view' },
        { to: '/catalog/structure', label: t('app.nav.catalog.structure'), permissionKey: 'catalog.categories.view' },
      ],
    },
    {
      id: 'accounting',
      label: t('app.nav.group.accounting'),
      icon: '📒',
      items: [{ to: '/accounting', label: t('app.nav.accounting') }],
    },
    {
      id: 'access',
      label: t('app.nav.group.access'),
      icon: '🔐',
      roles: ['Admin', 'SuperAdmin'],
      items: [
        { to: '/access', label: t('app.nav.access') },
        { to: '/profiles', label: t('app.nav.profiles') },
        { to: '/saas/branches', label: t('app.nav.branches'), permissionKey: 'saas.branches.view' },
      ],
    },
    {
      id: 'security',
      label: t('app.nav.group.security'),
      icon: '🛡️',
      roles: ['SuperAdmin'],
      items: [
        { to: '/security', label: t('app.nav.security') },
      ],
    },
    {
      id: 'saas',
      label: t('app.nav.group.saas'),
      icon: '🏢',
      roles: ['SuperAdmin'],
      items: [
        { to: '/companies', label: t('app.nav.companies') },
        { to: '/superadmin', label: t('app.nav.superadmin') },
        { to: '/saas/branches', label: t('app.nav.branches'), permissionKey: 'saas.branches.view' },
      ],
    },
  ]), [t]);

  const visibleGroups = (() => {
    const byRole = groups.filter((g) => !g.roles || (user?.role ? g.roles.includes(user.role) : false));
    // SuperAdmin/Admin: UI sin restricción (backend igual valida).
    if (user?.role === 'SuperAdmin' || user?.role === 'Admin') return byRole;

    // Mientras hidrata/recupera permisos (después de refresh), no ocultar el menú por flicker.
    if (!permsHydrated) return byRole;

    if (permissions.includes('*')) return byRole;
    return byRole
      .map((g) => ({ ...g, items: g.items.filter((it) => !it.permissionKey || hasPerm(it.permissionKey)) }))
      .filter((g) => g.items.length > 0);
  })();

  const activeGroupIds = useMemo(() => {
    const path = location.pathname;
    const ids = new Set<string>();
    for (const g of visibleGroups) {
      if (g.items.some((it) => path === it.to || path.startsWith(it.to + '/'))) ids.add(g.id);
    }
    return ids;
  }, [location.pathname, visibleGroups]);

  const [openGroups, setOpenGroups] = useState<Record<string, boolean>>({});
  const isOpen = (id: string) => openGroups[id] ?? activeGroupIds.has(id);
  const toggleGroup = (id: string) => setOpenGroups((s) => ({ ...s, [id]: !isOpen(id) }));

  const handleLogout = () => {
    logout();
    clearPermissions();
    navigate('/login');
  };

  return (
    <div className="layout">
      <aside className="sidebar">
        <div className="sidebar-brand">
          <span className="brand-logo">ZH</span>
          <span className="brand-name">ERP</span>
        </div>

        <nav className="sidebar-nav">
          {visibleGroups.map((g) => (
            <div key={g.id} className="nav-group">
              <button
                type="button"
                className={`nav-group-btn${activeGroupIds.has(g.id) ? ' nav-group-btn--active' : ''}`}
                onClick={() => toggleGroup(g.id)}
                aria-expanded={isOpen(g.id)}
              >
                <span className="nav-icon">{g.icon}</span>
                <span className="nav-group-label">{g.label}</span>
                <span className={`nav-caret${isOpen(g.id) ? ' nav-caret--open' : ''}`}>▸</span>
              </button>

              {isOpen(g.id) && (
                <div className="nav-submenu">
                  {g.items.map((item) => (
                    <NavLink
                      key={item.to}
                      to={item.to}
                      className={({ isActive }) =>
                        `nav-item nav-item--sub${isActive ? ' nav-item--active' : ''}`
                      }
                    >
                      <span>{item.label}</span>
                    </NavLink>
                  ))}
                </div>
              )}
            </div>
          ))}
        </nav>

        <div className="sidebar-footer">
          <div className="user-info">
            <div className="user-avatar">
              {user?.fullName?.[0]?.toUpperCase() ?? '?'}
            </div>
            <div className="user-details">
              <span className="user-name">{user?.fullName}</span>
              <span className="user-role">{user?.role}</span>
            </div>
          </div>
          <LanguageSwitcher />
          <button className="logout-btn" onClick={handleLogout} title={t('app.logout')}>
            ⏻
          </button>
        </div>
      </aside>

      <main className="content">
        {user?.role === 'SuperAdmin' && user.tenantId && user.tenantId !== '00000000-0000-0000-0000-000000000000' && (
          <div className="superadmin-banner">
            <div className="superadmin-banner-left">
              <strong>{t('superadmin.banner')}</strong>
              <span className="superadmin-banner-tenant">
                {getImpersonationTenantName() ?? user.tenantId}
              </span>
            </div>
            <button className="superadmin-banner-btn" onClick={() => navigate('/superadmin')}>
              {t('superadmin.backToGlobal')}
            </button>
          </div>
        )}
        <Outlet />
      </main>
    </div>
  );
}
