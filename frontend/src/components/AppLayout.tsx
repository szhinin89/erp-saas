import { NavLink, Outlet, useLocation, useNavigate } from 'react-router-dom';
import { Fragment, useEffect, useMemo, useRef, useState, startTransition } from 'react';
import { createPortal } from 'react-dom';
import { useAuthStore } from '../store/authStore';
import { useI18n } from '../i18n/i18n';
import { LanguageSwitcher } from './LanguageSwitcher';
import { usePermissionsStore } from '../store/permissionsStore';
import { accessService } from '../services/accessService';
import { superAdminService } from '../services/superAdminService';
import { ZHAppTenantHeader } from './zh/ZHAppTenantHeader';
import {
  buildNavGroups,
  ensureSalesNextToInventory,
  flattenAccessIntoSecurity,
  flattenSaaSIntoHome,
  mapSessionMenuToNavGroups,
  mergeMissingStaticNavGroups,
  mergeSuperAdminNavExtrasIntoHome,
  type NavItem,
} from '../nav/navConfig';
import { useDeployment } from '../deployment/DeploymentContext';
import type { SessionMenuGroupDto } from '../types/access';
import './AppLayout.css';

function MainMenuList({
  items,
  depth,
  onClose,
  isFavorite,
  toggleFavorite,
  t,
}: {
  items: NavItem[];
  depth: number;
  onClose: () => void;
  isFavorite: (to: string) => boolean;
  toggleFavorite: (item: NavItem) => void;
  t: (key: string) => string;
}) {
  return (
    <>
      {items.map((it, idx) => (
        <Fragment key={`${depth}-${it.to}-${idx}`}>
          <div className={`app-mainmenu-row${depth > 0 ? ' is-nested' : ''}`}>
            {it.children?.length ? (
              <span className="app-mainmenu-link app-mainmenu-parent">{it.label}</span>
            ) : (
              <NavLink to={it.to} className="app-mainmenu-link" onClick={onClose}>
                {it.label}
              </NavLink>
            )}
            {!it.children?.length ? (
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
            ) : null}
          </div>
          {it.children?.length ? (
            <MainMenuList
              items={it.children}
              depth={depth + 1}
              onClose={onClose}
              isFavorite={isFavorite}
              toggleFavorite={toggleFavorite}
              t={t}
            />
          ) : null}
        </Fragment>
      ))}
    </>
  );
}

function getImpersonationTenantName(): string | null {
  return localStorage.getItem('superadmin-impersonation-tenant-name');
}

export function AppLayout() {
  const { superAdminPanelEnabled } = useDeployment();
  const { user, logout, login } = useAuthStore();
  const { permissions, enabledModules, has: hasPerm, clearPermissions, hasHydrated: permsHydrated } =
    usePermissionsStore();
  const navigate = useNavigate();
  const location = useLocation();
  const { t } = useI18n();
  const [superadminBannerOpen, setSuperadminBannerOpen] = useState(false);
  const [superadminReturningGlobal, setSuperadminReturningGlobal] = useState(false);
  const [sessionMenuDto, setSessionMenuDto] = useState<SessionMenuGroupDto[] | undefined>(undefined);

  useEffect(() => {
    if (!user) {
      startTransition(() => setSessionMenuDto(undefined));
      return;
    }
    let cancelled = false;
    void accessService
      .getSessionMenu()
      .then((rows) => {
        if (!cancelled) setSessionMenuDto(rows.length > 0 ? rows : []);
      })
      .catch(() => {
        if (!cancelled) setSessionMenuDto([]);
      });
    return () => {
      cancelled = true;
    };
  }, [user?.tenantId, user?.userId]);

  const groups = useMemo(() => {
    const opts = { superAdminPanelEnabled };
    const raw = mergeSuperAdminNavExtrasIntoHome(
      sessionMenuDto !== undefined && sessionMenuDto.length > 0
        ? mapSessionMenuToNavGroups(sessionMenuDto, t, opts)
        : buildNavGroups(t, opts),
      t,
      opts,
    );
    return mergeMissingStaticNavGroups(
      flattenAccessIntoSecurity(flattenSaaSIntoHome(ensureSalesNextToInventory(raw, t, opts))),
      t,
      opts,
    );
  }, [sessionMenuDto, t, superAdminPanelEnabled]);

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
        if (!cancelled)
          usePermissionsStore.getState().setPermissionSnapshot({
            permissions: res?.permissions ?? [],
            planCode: res?.planCode ?? null,
            enabledModules: res?.enabledModules ?? [],
          });
      } catch {
        // si falla, el menú seguirá ocultando items hasta re-login/switch tenant
      }
    });

    return () => {
      cancelled = true;
    };
  }, [permissions.length, user]);

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
    if (user?.role === 'SuperAdmin') return byRole;

    const moduleEntitled = (key?: string) => {
      if (!key) return true;
      const mods =
        enabledModules.length > 0 ? enabledModules : (user?.enabledModules ?? []);
      if (mods.length === 0) return true;
      return mods.some((m) => m.toLowerCase() === key.toLowerCase());
    };

    const bySubscription = byRole.filter((g) => moduleEntitled(g.moduleKey));

    if (!permsHydrated) return bySubscription;

    if (permissions.includes('*')) return bySubscription;

    const itemVisible = (it: NavItem) => {
      if (it.roles?.length && (!user?.role || !it.roles.includes(user.role))) return false;
      if (!moduleEntitled(it.moduleKey)) return false;
      if (it.permissionKeysAny?.length) return it.permissionKeysAny.some((k) => hasPerm(k));
      if (it.permissionKey) return hasPerm(it.permissionKey);
      return true;
    };

    return bySubscription
      .map((g) => ({ ...g, items: g.items.filter(itemVisible) }))
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

  /** Menú superior: mismas agrupaciones que antes (SaaS solo en píldoras si eres SuperAdmin). */
  const mainMenuGroups = useMemo(() => {
    const allowed = visibleGroups.filter((g) => g.items.length > 0);
    return allowed.map((g) => ({
      id: g.id,
      label: g.label,
      isActive: activeGroupIds.has(g.id),
      items: g.items,
    }));
  }, [activeGroupIds, t, user?.role, visibleGroups]);

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
      const popEl = mainMenuPopoverRef.current;
      if (anchor && anchor.contains(target)) return;
      if (popEl && popEl.contains(target)) return;
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

  useEffect(() => {
    startTransition(() => {
      setMainMenuOpenId(null);
    });
  }, [location.pathname]);

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
      navigate('/superadmin');
    } finally {
      setSuperadminReturningGlobal(false);
    }
  };

  return (
    <div className="layout">
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
              <span className="superadmin-banner-tenantInline" title={t('superadmin.tenant.title')}>
                {getImpersonationTenantName() ?? t('superadmin.tenant.unknown')}
              </span>
              <span className="superadmin-banner-tenantIdInline mono" title={t('superadmin.tenant.idTitle')}>
                {user.tenantId}
              </span>
              <span className="superadmin-banner-caret" aria-hidden="true">{superadminBannerOpen ? '▾' : '▸'}</span>
            </button>

            {superadminBannerOpen ? (
              <div className="superadmin-banner-details">
                <div className="superadmin-banner-tenant" title={t('superadmin.tenant.title')}>
                  {getImpersonationTenantName() ?? t('superadmin.tenant.unknown')}
                </div>
                <div className="superadmin-banner-tenantId mono" title={t('superadmin.tenant.idTitle')}>
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
            rightExtra={<LanguageSwitcher />}
            bottomLeft={
              mainMenuGroups.length > 0 ? (
                <div className="app-mainmenu" role="navigation" aria-label={t('app.layout.mainNav')}>
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
              ) : null
            }
          />
        </div>

        {mainMenuOpenId && mainMenuPos
          ? createPortal(
              <div
                className="app-mainmenu-popover"
                role="menu"
                aria-label={t('app.layout.groupMenu')}
                ref={mainMenuPopoverRef}
                data-pos="fixed"
                data-top={mainMenuPos.top}
                data-left={mainMenuPos.left}
              >
                <MainMenuList
                  items={mainMenuGroups.find((g) => g.id === mainMenuOpenId)?.items ?? []}
                  depth={0}
                  onClose={() => setMainMenuOpenId(null)}
                  isFavorite={isFavorite}
                  toggleFavorite={toggleFavorite}
                  t={t}
                />
              </div>,
              document.body
            )
          : null}
        <Outlet />
      </main>
    </div>
  );
}
