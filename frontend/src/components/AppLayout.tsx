import { NavLink, Outlet, useLocation, useNavigate } from 'react-router-dom';
import { useEffect, useMemo, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import { useAuthStore } from '../store/authStore';
import { useI18n } from '../i18n/i18n';
import { LanguageSwitcher } from './LanguageSwitcher';
import { usePermissionsStore } from '../store/permissionsStore';
import { accessService } from '../services/accessService';
import { superAdminService } from '../services/superAdminService';
import { ZHAppTenantHeader } from './zh/ZHAppTenantHeader';
import { buildNavGroups, type NavGroup, type NavItem } from '../nav/navConfig';
import './AppLayout.css';

function getImpersonationTenantName(): string | null {
  return localStorage.getItem('superadmin-impersonation-tenant-name');
}

export function AppLayout() {
  const { user, logout, login } = useAuthStore();
  const { permissions, has: hasPerm, clearPermissions, hasHydrated: permsHydrated } = usePermissionsStore();
  const navigate = useNavigate();
  const location = useLocation();
  const { t } = useI18n();
  const [superadminBannerOpen, setSuperadminBannerOpen] = useState(false);
  const [superadminReturningGlobal, setSuperadminReturningGlobal] = useState(false);
  const isSuperAdminArea = location.pathname === '/superadmin' || location.pathname.startsWith('/superadmin/');
  const [sidebarOpen, setSidebarOpen] = useState(false);

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

  const groups = useMemo(() => buildNavGroups(t), [t]);
  const [favorites, setFavorites] = useState<NavItem[]>(() => {
    try {
      const raw = localStorage.getItem('zh-favorites');
      if (!raw) return [];
      const parsed = JSON.parse(raw) as unknown;
      if (!Array.isArray(parsed)) return [];
      return parsed
        .filter((x) => x && typeof x === 'object')
        .map((x) => x as { to?: unknown; label?: unknown })
        .filter((x) => typeof x.to === 'string' && typeof x.label === 'string')
        .map((x) => ({ to: x.to as string, label: x.label as string }));
    } catch {
      return [];
    }
  });

  useEffect(() => {
    try {
      localStorage.setItem('zh-favorites', JSON.stringify(favorites));
    } catch {
      // ignore
    }
  }, [favorites]);

  const toggleFavorite = (item: NavItem) => {
    setFavorites((prev) => {
      const exists = prev.some((f) => f.to === item.to);
      if (exists) return prev.filter((f) => f.to !== item.to);
      return [...prev, { to: item.to, label: item.label }];
    });
  };

  const isFavorite = (to: string) => favorites.some((f) => f.to === to);

  const visibleGroups = (() => {
    const byRole = groups.filter((g) => !g.roles || (user?.role ? g.roles.includes(user.role) : false));
    // SuperAdmin: UI sin restricción (backend igual valida).
    if (user?.role === 'SuperAdmin') return byRole;

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

  const mainMenuGroups = useMemo(() => {
    const allowed = visibleGroups.filter((g) => g.id !== 'saas' && g.items.length > 0);
    return allowed.map((g) => ({
      id: g.id,
      label: g.id === 'home' ? t('app.nav.dashboard') : g.label,
      isActive: activeGroupIds.has(g.id),
      items: g.items,
    }));
  }, [activeGroupIds, t, visibleGroups]);

  const [mainMenuOpenId, setMainMenuOpenId] = useState<string | null>(null);
  const [mainMenuPos, setMainMenuPos] = useState<{ top: number; left: number } | null>(null);
  const mainMenuAnchorRef = useRef<HTMLButtonElement | null>(null);
  const mainMenuPopoverRef = useRef<HTMLDivElement | null>(null);

  useEffect(() => {
    if (!mainMenuOpenId) return;
    const pop = mainMenuPopoverRef.current;
    if (pop && mainMenuPos) {
      pop.style.position = 'fixed';
      pop.style.top = `${mainMenuPos.top}px`;
      pop.style.left = `${mainMenuPos.left}px`;
    }
    const onDown = (e: PointerEvent) => {
      const target = e.target;
      if (!(target instanceof Node)) return;
      const anchor = mainMenuAnchorRef.current;
      const pop = mainMenuPopoverRef.current;
      if (anchor && anchor.contains(target)) return;
      if (pop && pop.contains(target)) return;
      setMainMenuOpenId(null);
    };
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') setMainMenuOpenId(null);
    };
    window.addEventListener('pointerdown', onDown, true);
    window.addEventListener('keydown', onKey);
    return () => {
      window.removeEventListener('pointerdown', onDown, true);
      window.removeEventListener('keydown', onKey);
    };
  }, [mainMenuOpenId, mainMenuPos]);

  const sidebarGroups: NavGroup[] = useMemo(() => {
    const saas = visibleGroups.find((g) => g.id === 'saas');
    const fav: NavGroup = {
      id: 'favorites',
      label: t('app.nav.group.favorites'),
      icon: '★',
      items: favorites,
    };
    return [fav, ...(saas ? [saas] : [])];
  }, [favorites, t, visibleGroups]);

  const handleLogout = () => {
    logout();
    clearPermissions();
    navigate('/login');
  };

  const returnToGlobal = async () => {
    if (superadminReturningGlobal) return;
    setSuperadminReturningGlobal(true);
    try {
      const auth = await superAdminService.switchTenant('00000000-0000-0000-0000-000000000000');
      localStorage.removeItem('superadmin-impersonation-tenant-name');
      login(auth);
      clearPermissions();
      navigate('/superadmin');
    } catch {
      // Si falla, al menos navega al panel; el backend debería impedir acciones no válidas.
      navigate('/superadmin');
    } finally {
      setSuperadminReturningGlobal(false);
    }
  };

  return (
    <div className="layout">
      <aside className={`sidebar${sidebarOpen ? ' sidebar--open' : ''}`}>
        <div className="sidebar-brand">
          <span className="brand-logo">ZH</span>
          <span className="brand-name">ERP</span>
        </div>

        <nav className="sidebar-nav">
          {sidebarGroups.map((g) => (
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
                  {g.items.length === 0 ? (
                    <div className="nav-empty">{t('app.favorites.empty')}</div>
                  ) : (
                    g.items.map((item) => (
                      <div key={item.to} className="nav-itemRow">
                        <NavLink
                          to={item.to}
                          className={({ isActive }) =>
                            `nav-item nav-item--sub${isActive ? ' nav-item--active' : ''}`
                          }
                        >
                          <span>{item.label}</span>
                        </NavLink>
                        {g.id !== 'favorites' ? (
                          <button
                            type="button"
                            className={`nav-favBtn${isFavorite(item.to) ? ' is-on' : ''}`}
                            aria-label={isFavorite(item.to) ? t('app.favorites.remove') : t('app.favorites.add')}
                            onClick={(e) => {
                              e.preventDefault();
                              e.stopPropagation();
                              toggleFavorite(item);
                            }}
                          >
                            {isFavorite(item.to) ? '★' : '☆'}
                          </button>
                        ) : (
                          <button
                            type="button"
                            className="nav-favBtn is-on"
                            aria-label={t('app.favorites.remove')}
                            onClick={(e) => {
                              e.preventDefault();
                              e.stopPropagation();
                              toggleFavorite(item);
                            }}
                          >
                            ✕
                          </button>
                        )}
                      </div>
                    ))
                  )}
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
        </div>
      </aside>

      {sidebarOpen ? (
        <button
          type="button"
          className="sidebar-overlay"
          aria-label={t('common.close')}
          onClick={() => setSidebarOpen(false)}
        />
      ) : null}

      <main className="content">
        {user?.role === 'SuperAdmin' && user.tenantId && user.tenantId !== '00000000-0000-0000-0000-000000000000' && (
          <div className={`superadmin-banner${superadminBannerOpen ? ' is-open' : ''}`}>
            <button
              type="button"
              className="superadmin-banner-toggle"
              onClick={() => setSuperadminBannerOpen((s) => !s)}
              aria-expanded={superadminBannerOpen}
            >
              <span className="superadmin-banner-dot" aria-hidden="true" />
              <strong className="superadmin-banner-title">{t('superadmin.banner')}</strong>
              <span className="superadmin-banner-tenantInline" title="Empresa">
                {getImpersonationTenantName() ?? t('superadmin.tenant.unknown')}
              </span>
              <span className="superadmin-banner-tenantIdInline mono" title="Tenant ID">
                {user.tenantId}
              </span>
              <span className="superadmin-banner-caret" aria-hidden="true">{superadminBannerOpen ? '▾' : '▸'}</span>
            </button>

            {superadminBannerOpen ? (
              <div className="superadmin-banner-details">
                <div className="superadmin-banner-tenant" title="Empresa">
                  {getImpersonationTenantName() ?? t('superadmin.tenant.unknown')}
                </div>
                <div className="superadmin-banner-tenantId mono" title="Tenant ID">
                  {user.tenantId}
                </div>
                <button className="superadmin-banner-btn" onClick={() => void returnToGlobal()} type="button" disabled={superadminReturningGlobal}>
                  {t('superadmin.backToGlobal')}
                </button>
              </div>
            ) : null}
          </div>
        )}
        <div className="app-tenantHeaderWrap">
          <ZHAppTenantHeader
            onLogout={handleLogout}
            leftExtra={
              <button
                type="button"
                className="zh-app-hamburger"
                aria-label="Abrir menú"
                aria-expanded={sidebarOpen}
                onClick={() => setSidebarOpen((s) => !s)}
              >
                ☰
              </button>
            }
            rightExtra={<LanguageSwitcher />}
            bottomLeft={!isSuperAdminArea ? (
              <div className="app-mainmenu" role="navigation" aria-label="Menú principal">
                {mainMenuGroups.map((g) => (
                  <button
                    key={g.id}
                    type="button"
                    className={`app-mainmenu-item${g.isActive ? ' is-active' : ''}`}
                    aria-haspopup="menu"
                    aria-expanded={mainMenuOpenId === g.id}
                    onClick={(e) => {
                      const willOpen = mainMenuOpenId !== g.id;
                      setMainMenuOpenId(willOpen ? g.id : null);
                      mainMenuAnchorRef.current = e.currentTarget;
                      const r = e.currentTarget.getBoundingClientRect();
                      setMainMenuPos({ top: Math.round(r.bottom + 10), left: Math.round(r.left) });
                    }}
                  >
                    {g.label}
                  </button>
                ))}
              </div>
            ) : null}
          />
        </div>

        {!isSuperAdminArea && mainMenuOpenId && mainMenuPos
          ? createPortal(
              <div
                className="app-mainmenu-popover"
                role="menu"
                aria-label="Formularios"
                ref={mainMenuPopoverRef}
                data-pos="fixed"
                data-top={mainMenuPos.top}
                data-left={mainMenuPos.left}
              >
                {(mainMenuGroups.find((g) => g.id === mainMenuOpenId)?.items ?? []).map((it) => (
                  <div key={it.to} className="app-mainmenu-row">
                    <NavLink
                      to={it.to}
                      className="app-mainmenu-link"
                      onClick={() => setMainMenuOpenId(null)}
                    >
                      {it.label}
                    </NavLink>
                    <button
                      type="button"
                      className={`app-mainmenu-fav${isFavorite(it.to) ? ' is-on' : ''}`}
                      aria-label={isFavorite(it.to) ? t('app.favorites.remove') : t('app.favorites.add')}
                      onClick={(e) => {
                        e.preventDefault();
                        e.stopPropagation();
                        toggleFavorite(it);
                      }}
                    >
                      {isFavorite(it.to) ? '★' : '☆'}
                    </button>
                  </div>
                ))}
              </div>,
              document.body
            )
          : null}
        <Outlet />
      </main>
    </div>
  );
}
