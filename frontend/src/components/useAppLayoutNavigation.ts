import { useEffect, useMemo, useState, startTransition } from 'react';
import { useLocation } from 'react-router-dom';
import { useAuthStore } from '../store/authStore';
import { useI18n } from '../i18n/i18n';
import { usePermissionsStore } from '../store/permissionsStore';
import { normalizeModuleKey } from '../constants/subscriptionModules';
import { syncSessionEntitlements } from '../lib/syncSessionEntitlements';
import { accessService } from '../modules/auth/api/accessService';
import { useDeployment } from '../deployment/DeploymentContext';
import {
  buildGlobalSuperAdminNavGroups,
  buildNavGroups,
  ensureSalesNextToInventory,
  flattenAccessIntoSecurity,
  flattenSaaSIntoHome,
  isPlanBuilderSessionMenu,
  mapSessionMenuToNavGroups,
  mergeSuperAdminNavExtrasIntoHome,
  ensureSubscriberHomeOverview,
  expandPlanCustomRootsToBarGroups,
  sortNavGroupsForMainBar,
  type NavItem,
} from '../nav/navConfig';
import { GLOBAL_SUBSCRIBER_ID } from '../constants/subscriberIds';
import type { SessionMenuGroupDto } from '../types/access';
import { readPlanCustomMenuBarLayout } from './menu-builder/menuBuilderTypes';
import { navSubtreeMatchesPath } from './AppLayoutMainMenu';

export type MainMenuGroup = {
  id: string;
  label: string;
  icon?: string;
  isActive: boolean;
  items: NavItem[];
};

export function useAppLayoutNavigation() {
  const { superAdminPanelEnabled } = useDeployment();
  const { user } = useAuthStore();
  const { permissions, enabledModules, has: hasPerm, hasHydrated: permsHydrated } = usePermissionsStore();
  const location = useLocation();
  const { t } = useI18n();
  const [sessionMenuDto, setSessionMenuDto] = useState<SessionMenuGroupDto[] | undefined>(undefined);
  const [sessionMenuResolved, setSessionMenuResolved] = useState(false);

  const isGlobalSuperAdmin = useMemo(
    () => (user?.role ?? '') === 'SuperAdmin' && (user?.subscriberId ?? '') === GLOBAL_SUBSCRIBER_ID,
    [user?.role, user?.subscriberId],
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
  }, [user, isGlobalSuperAdmin, user?.subscriberId]);

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
        : buildNavGroups(t, opts);
    const raw = ensureSubscriberHomeOverview(
      mergeSuperAdminNavExtrasIntoHome(fromApi, t, opts),
      t,
    );
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

  useEffect(() => {
    let cancelled = false;
    const subscriberId = user?.subscriberId ?? '';
    const globalSa = (user?.role ?? '') === 'SuperAdmin' && subscriberId === GLOBAL_SUBSCRIBER_ID;

    if (!user || globalSa) return;
    if (permissions.length > 0 && enabledModules.length > 0) return;

    void Promise.resolve().then(async () => {
      try {
        if (!cancelled) await syncSessionEntitlements();
      } catch {
        // si falla, el menú seguirá ocultando items hasta re-login/switch tenant
      }
    });

    return () => {
      cancelled = true;
    };
  }, [permissions.length, enabledModules.length, user]);

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
      if (enabledModules.length === 0) return false;
      const want = normalizeModuleKey(key);
      return enabledModules.some((m) => normalizeModuleKey(m) === want);
    };

    const bySubscription = byRole.filter((g) => moduleEntitled(g.moduleKey));

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

  const mainMenuGroups = useMemo((): MainMenuGroup[] => {
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

  return {
    user,
    t,
    isGlobalSuperAdmin,
    sessionMenuResolved,
    mainMenuGroups,
    showPlanVerticalNav,
    isFavorite,
    toggleFavorite,
  };
}
