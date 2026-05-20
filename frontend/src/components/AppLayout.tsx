import { Outlet, useLocation, useNavigate } from 'react-router-dom';
import { Fragment, useEffect, useMemo, useRef, useState, startTransition } from 'react';
import { createPortal } from 'react-dom';
import { useAuthStore } from '../store/authStore';
import { useI18n } from '../i18n/i18n';
import { LanguageSwitcher } from './LanguageSwitcher';
import { usePermissionsStore } from '../store/permissionsStore';
import { accessService } from '../services/accessService';
import { superAdminService } from '../services/superAdminService';
import { LoadingState } from './PageShell';
import { ZHAppTenantHeader } from './zh/ZHAppTenantHeader';
import {
  buildGlobalSuperAdminNavGroups,
  ensureSalesNextToInventory,
  flattenAccessIntoSecurity,
  flattenSaaSIntoHome,
  isPlanBuilderSessionMenu,
  mapSessionMenuToNavGroups,
  mergeSuperAdminNavExtrasIntoHome,
  expandPlanCustomRootsToBarGroups,
  sortNavGroupsForMainBar,
  type NavItem,
} from '../nav/navConfig';
import { GLOBAL_TENANT_ID } from '../constants/tenantIds';
import { useDeployment } from '../deployment/DeploymentContext';
import type { SessionMenuGroupDto } from '../types/access';
import { readPlanCustomMenuBarLayout } from './menu-builder/menuBuilderTypes';
import './AppLayout.css';

/** True si la ruta actual coincide con este ítem o con algún descendiente (menú anidado). */
function navSubtreeMatchesPath(it: NavItem, pathname: string): boolean {
  if (it.to) {
    const ok = pathname === it.to || (it.to.length > 1 && pathname.startsWith(`${it.to}/`));
    if (ok) return true;
  }
  return it.children?.some((c) => navSubtreeMatchesPath(c, pathname)) ?? false;
}

/** Ramas con hijos (p. ej. Administración de ítems): cerradas al abrir el menú; clic en la fila despliega Nuevo, Marca, … */
function MainMenuBranchRow({
  it,
  depth,
  onClose,
  isFavorite,
  toggleFavorite,
  t,
  showFavoriteStars,
}: {
  it: NavItem;
  depth: number;
  onClose: () => void;
  isFavorite: (to: string) => boolean;
  toggleFavorite: (item: NavItem) => void;
  t: (key: string) => string;
  showFavoriteStars: boolean;
}) {
  const [expanded, setExpanded] = useState(false);

  return (
    <div className="app-mainmenu-branchRoot">
      <div className={`app-mainmenu-row${depth > 0 ? ' is-nested' : ''}`}>
        <button
          type="button"
          className="app-mainmenu-link app-mainmenu-navBtn app-mainmenu-branchToggle"
          aria-expanded={expanded}
          onClick={() => setExpanded((v) => !v)}
        >
          {it.icon ? <span className="app-mainmenu-itemIcon" aria-hidden="true">{it.icon}</span> : null}
          <span className="app-mainmenu-branchLabel">{it.label}</span>
          <span className="app-mainmenu-branchCaret" aria-hidden>
            {expanded ? '▾' : '▸'}
          </span>
        </button>
        {it.to && showFavoriteStars ? (
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
      {expanded ? (
        <div className="app-mainmenu-branchChildren">
          <MainMenuList
            items={it.children!}
            depth={depth + 1}
            onClose={onClose}
            isFavorite={isFavorite}
            toggleFavorite={toggleFavorite}
            t={t}
            showFavoriteStars={showFavoriteStars}
          />
        </div>
      ) : null}
    </div>
  );
}

function MainMenuList({
  items,
  depth,
  onClose,
  isFavorite,
  toggleFavorite,
  t,
  showFavoriteStars,
}: {
  items: NavItem[];
  depth: number;
  onClose: () => void;
  isFavorite: (to: string) => boolean;
  toggleFavorite: (item: NavItem) => void;
  t: (key: string) => string;
  showFavoriteStars: boolean;
}) {
  const navigate = useNavigate();

  return (
    <>
      {items.map((it, idx) =>
        it.children?.length ? (
          <MainMenuBranchRow
            key={`${depth}-b-${idx}-${it.label}`}
            it={it}
            depth={depth}
            onClose={onClose}
            isFavorite={isFavorite}
            toggleFavorite={toggleFavorite}
            t={t}
            showFavoriteStars={showFavoriteStars}
          />
        ) : (
          <Fragment key={`${depth}-${it.label}-${it.to}-${idx}`}>
            <div className={`app-mainmenu-row${depth > 0 ? ' is-nested' : ''}`}>
              {it.to ? (
                <button
                  type="button"
                  className="app-mainmenu-link app-mainmenu-navBtn"
                  onClick={() => {
                    navigate(it.to);
                    onClose();
                  }}
                >
                  {it.icon ? <span className="app-mainmenu-itemIcon" aria-hidden="true">{it.icon}</span> : null}
                  <span>{it.label}</span>
                </button>
              ) : (
                <span className="app-mainmenu-link app-mainmenu-parent" title={t('app.layout.menuMissingRoute')}>
                  {it.icon ? <span className="app-mainmenu-itemIcon" aria-hidden="true">{it.icon}</span> : null}
                  <span>{it.label}</span>
                </span>
              )}
              {it.to && showFavoriteStars ? (
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
          </Fragment>
        ),
      )}
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
  const [sessionMenuResolved, setSessionMenuResolved] = useState(false);

  const isGlobalSuperAdmin = useMemo(
    () => (user?.role ?? '') === 'SuperAdmin' && (user?.tenantId ?? '') === GLOBAL_TENANT_ID,
    [user?.role, user?.tenantId],
  );

  useEffect(() => {
    if (!user) {
      startTransition(() => {
        setSessionMenuDto(undefined);
        setSessionMenuResolved(false);
      });
      return;
    }
    if (isGlobalSuperAdmin) {
      startTransition(() => {
        setSessionMenuDto(undefined);
        setSessionMenuResolved(true);
      });
      return;
    }
    let cancelled = false;
    setSessionMenuResolved(false);
    void accessService
      .getSessionMenu()
      .then((rows) => {
        if (!cancelled) {
          setSessionMenuDto(rows.length > 0 ? rows : []);
          setSessionMenuResolved(true);
        }
      })
      .catch(() => {
        if (!cancelled) {
          setSessionMenuDto([]);
          setSessionMenuResolved(true);
        }
      });
    return () => {
      cancelled = true;
    };
  }, [user, isGlobalSuperAdmin, user?.tenantId]);

  const restrictedPlanMenu = useMemo(
    () => isPlanBuilderSessionMenu(sessionMenuDto),
    [sessionMenuDto],
  );

  const planMenuBarLayout = useMemo((): 'horizontal' | 'vertical' => {
    const m = sessionMenuDto?.length ? readPlanCustomMenuBarLayout(sessionMenuDto) : null;
    return m ?? 'horizontal';
  }, [sessionMenuDto]);

  const groups = useMemo(() => {
    const opts = { superAdminPanelEnabled };
    if (isGlobalSuperAdmin) {
      if (!superAdminPanelEnabled) return [];
      return buildGlobalSuperAdminNavGroups(t, opts);
    }
    if (!sessionMenuResolved) {
      return [];
    }
    const fromApi =
      sessionMenuDto !== undefined && sessionMenuDto.length > 0
        ? mapSessionMenuToNavGroups(sessionMenuDto, t, opts)
        : [];
    const raw = mergeSuperAdminNavExtrasIntoHome(fromApi, t, opts);
    if (restrictedPlanMenu) {
      let piped = sortNavGroupsForMainBar(flattenAccessIntoSecurity(flattenSaaSIntoHome(raw)));
      if (planMenuBarLayout === 'horizontal') {
        piped = expandPlanCustomRootsToBarGroups(piped);
      }
      return piped;
    }
    return sortNavGroupsForMainBar(
      flattenAccessIntoSecurity(flattenSaaSIntoHome(ensureSalesNextToInventory(raw, t, opts))),
    );
  }, [
    sessionMenuDto,
    sessionMenuResolved,
    t,
    superAdminPanelEnabled,
    isGlobalSuperAdmin,
    restrictedPlanMenu,
    planMenuBarLayout,
  ]);

  // Auto-recupera permisos después de refresh/hidratación.
  useEffect(() => {
    let cancelled = false;
    const tenantId = user?.tenantId ?? '';
    const globalSa = (user?.role ?? '') === 'SuperAdmin' && tenantId === GLOBAL_TENANT_ID;

    if (!user || globalSa) return;
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

  const visibleGroups = useMemo(() => {
    const byRole = groups.filter((g) => !g.roles || (user?.role ? g.roles.includes(user.role) : false));

    if (isGlobalSuperAdmin) {
      return byRole.filter((g) => g.items.length > 0);
    }

    const moduleEntitled = (key?: string) => {
      if (!key) return true;
      const mods =
        enabledModules.length > 0 ? enabledModules : (user?.enabledModules ?? []);
      if (mods.length === 0) return false;
      return mods.some((m) => m.toLowerCase() === key.toLowerCase());
    };

    const bySubscription = byRole.filter((g) => moduleEntitled(g.moduleKey));

    // SuperAdmin (impersonando) y Admin de empresa ven todos los grupos del plan
    // sin filtro de módulos ni de permisos por ítem.
    // La autorización real ocurre en cada endpoint del backend.
    if (user?.role === 'SuperAdmin' || user?.role === 'Admin') {
      return byRole.filter((g) => g.items.length > 0);
    }

    if (!permsHydrated) return bySubscription;

    if (permissions.includes('*')) return bySubscription;

    const itemVisible = (it: NavItem) => {
      if (it.roles?.length && (!user?.role || !it.roles.includes(user.role))) return false;
      if (!moduleEntitled(it.moduleKey)) return false;
      if (it.permissionKeysAny?.length) return it.permissionKeysAny.some((k) => hasPerm(k));
      if (it.permissionKey) return hasPerm(it.permissionKey);
      return true;
    };

    const filterNavItemsDeep = (items: NavItem[]): NavItem[] => {
      const out: NavItem[] = [];
      for (const it of items) {
        const rawKids = it.children;
        const kidsFiltered = rawKids?.length ? filterNavItemsDeep(rawKids) : undefined;
        if (kidsFiltered?.length) {
          out.push({ ...it, children: kidsFiltered });
          continue;
        }
        if (rawKids?.length && !kidsFiltered?.length) {
          continue;
        }
        if (itemVisible(it)) out.push({ ...it, children: undefined });
      }
      return out;
    };

    return bySubscription
      .map((g) => ({ ...g, items: filterNavItemsDeep(g.items) }))
      .filter((g) => g.items.length > 0);
  }, [groups, user, isGlobalSuperAdmin, enabledModules, permissions, permsHydrated, hasPerm]);

  const activeGroupIds = useMemo(() => {
    const path = location.pathname;
    const ids = new Set<string>();
    for (const g of visibleGroups) {
      if (g.items.some((it) => navSubtreeMatchesPath(it, path))) ids.add(g.id);
    }
    return ids;
  }, [location.pathname, visibleGroups]);

  /** Menú superior: mismas agrupaciones que antes (SaaS solo en píldoras si eres SuperAdmin). */
  const mainMenuGroups = useMemo(() => {
    const allowed = visibleGroups.filter((g) => g.items.length > 0);
    return allowed.map((g) => ({
      id: g.id,
      label: g.label,
      icon: g.icon,
      isActive: activeGroupIds.has(g.id),
      items: g.items,
    }));
  }, [activeGroupIds, visibleGroups]);

  const showPlanVerticalNav =
    restrictedPlanMenu && planMenuBarLayout === 'vertical' && mainMenuGroups.length > 0;

  const [mainMenuOpenId, setMainMenuOpenId] = useState<string | null>(null);
  const [mainMenuPos, setMainMenuPos] = useState<{ top: number; left: number } | null>(null);
  const mainMenuBarRef = useRef<HTMLDivElement | null>(null);
  const mainMenuPopoverRef = useRef<HTMLDivElement | null>(null);
  const closeTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  const openGroup = (id: string, rect: DOMRect) => {
    if (closeTimerRef.current) clearTimeout(closeTimerRef.current);
    setMainMenuOpenId(id);
    setMainMenuPos({ top: Math.round(rect.bottom + 4), left: Math.round(rect.left) });
  };

  const scheduleClose = () => {
    closeTimerRef.current = setTimeout(() => setMainMenuOpenId(null), 150);
  };

  const cancelClose = () => {
    if (closeTimerRef.current) clearTimeout(closeTimerRef.current);
  };

  useEffect(() => {
    return () => {
      if (closeTimerRef.current) clearTimeout(closeTimerRef.current);
    };
  }, []);

  useEffect(() => {
    if (!mainMenuOpenId) return;
    const pop = mainMenuPopoverRef.current;
    if (pop && mainMenuPos) {
      pop.style.position = 'fixed';
      pop.style.top = `${mainMenuPos.top}px`;
      pop.style.left = `${mainMenuPos.left}px`;
    }
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') setMainMenuOpenId(null);
    };
    window.addEventListener('keydown', onKey);
    return () => {
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
      const auth = await superAdminService.switchTenant(GLOBAL_TENANT_ID);
      localStorage.removeItem('superadmin-impersonation-tenant-name');
      login(auth);
      clearPermissions();
      navigate('/superadmin/overview');
    } catch {
      navigate('/superadmin/overview');
    } finally {
      setSuperadminReturningGlobal(false);
    }
  };

  return (
    <div className="layout">
      <main className="content">
        {user?.role === 'SuperAdmin' && user.tenantId && user.tenantId !== GLOBAL_TENANT_ID && (
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
              !isGlobalSuperAdmin && user && !sessionMenuResolved ? (
                <div className="app-mainmenu app-mainmenu--loading" role="status" aria-live="polite" aria-busy="true">
                  <LoadingState />
                </div>
              ) : mainMenuGroups.length > 0 ? (
                showPlanVerticalNav ? (
                  <div
                    ref={mainMenuBarRef}
                    className="app-mainmenu app-mainmenu--planVertical"
                    role="navigation"
                    aria-label={t('app.layout.mainNav')}
                  >
                    {mainMenuGroups.map((g) => (
                      <div key={g.id} className="app-mainmenu-verticalSection">
                        {mainMenuGroups.length > 1 ? (
                          <div className="app-mainmenu-verticalSectionLabel">{g.label}</div>
                        ) : null}
                        <MainMenuList
                          items={g.items}
                          depth={0}
                          onClose={() => {}}
                          isFavorite={isFavorite}
                          toggleFavorite={toggleFavorite}
                          t={t}
                          showFavoriteStars={!isGlobalSuperAdmin}
                        />
                      </div>
                    ))}
                  </div>
                ) : (
                  <div ref={mainMenuBarRef} className="app-mainmenu" role="navigation" aria-label={t('app.layout.mainNav')}>
                    {mainMenuGroups.map((g) => (
                      <button
                        key={g.id}
                        type="button"
                        className={`app-mainmenu-item${g.isActive ? ' is-active' : ''}${mainMenuOpenId === g.id ? ' is-open' : ''}`}
                        aria-haspopup="menu"
                        aria-expanded={mainMenuOpenId === g.id}
                        onMouseEnter={(e) => openGroup(g.id, e.currentTarget.getBoundingClientRect())}
                        onMouseLeave={scheduleClose}
                        onClick={(e) => {
                          if (mainMenuOpenId === g.id) {
                            setMainMenuOpenId(null);
                          } else {
                            openGroup(g.id, e.currentTarget.getBoundingClientRect());
                          }
                        }}
                      >
                        {g.icon ? <span className="app-mainmenu-groupIcon" aria-hidden="true">{g.icon}</span> : null}
                        <span className="app-mainmenu-groupLabel">{g.label}</span>
                        <span className="app-mainmenu-caret" aria-hidden="true">▾</span>
                      </button>
                    ))}
                  </div>
                )
              ) : null
            }
          />
        </div>

        {mainMenuOpenId && mainMenuPos && !showPlanVerticalNav
          ? createPortal(
              <div
                ref={mainMenuPopoverRef}
                className="app-mainmenu-popover"
                role="menu"
                aria-label={t('app.layout.groupMenu')}
                data-top={mainMenuPos.top}
                data-left={mainMenuPos.left}
                onMouseEnter={cancelClose}
                onMouseLeave={scheduleClose}
              >
                <MainMenuList
                  items={mainMenuGroups.find((g) => g.id === mainMenuOpenId)?.items ?? []}
                  depth={0}
                  onClose={() => setMainMenuOpenId(null)}
                  isFavorite={isFavorite}
                  toggleFavorite={toggleFavorite}
                  t={t}
                  showFavoriteStars={!isGlobalSuperAdmin}
                />
              </div>,
              document.body,
            )
          : null}
        <Outlet />
      </main>
    </div>
  );
}
