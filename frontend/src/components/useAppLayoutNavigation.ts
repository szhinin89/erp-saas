import { useEffect, useMemo, useState, startTransition } from "react";
import { useLocation } from "react-router-dom";
import { useAuthStore } from "../store/authStore";
import { useI18n } from "../i18n/i18n";

import { accessService } from "../modules/auth/api/accessService";
import {
  flattenAccessIntoSecurity,
  flattenSaaSIntoHome,
  isPlanBuilderSessionMenu,
  mapSessionMenuToNavGroups,
  ensureTenantHomeOverview,
  ensureReportsGroup,
  expandPlanCustomRootsToBarGroups,
  sortNavGroupsForMainBar,
  navSubtreeMatchesPath,
  type NavItem,
} from "../nav/navConfig";
import type { NavMenuGroupDto } from "../types/access";

export type MainMenuGroup = {
  id: string;
  label: string;
  icon?: string;
  isActive: boolean;
  items: NavItem[];
};

export function useAppLayoutNavigation() {
  const { user } = useAuthStore();
  const location = useLocation();
  const { t } = useI18n();
  const [sessionMenuDto, setSessionMenuDto] = useState<
    NavMenuGroupDto[] | undefined
  >(undefined);
  const [sessionMenuResolved, setSessionMenuResolved] = useState(false);

  useEffect(() => {
    if (!user) {
      startTransition(() => {
        setSessionMenuDto(undefined);
        setSessionMenuResolved(false);
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
  }, [user, user?.tenantId]);

  const restrictedPlanMenu = useMemo(
    () => isPlanBuilderSessionMenu(sessionMenuDto),
    [sessionMenuDto],
  );

  const planMenuBarLayout = useMemo((): "horizontal" | "vertical" => {
    // Without platform menu-builder, always use horizontal layout
    return "horizontal";
  }, []);

  const groups = useMemo(() => {
    if (!sessionMenuResolved) return [];

    const fromApi =
      sessionMenuDto !== undefined && sessionMenuDto.length > 0
        ? mapSessionMenuToNavGroups(sessionMenuDto, t)
        : [];
    const withHome = ensureTenantHomeOverview(fromApi, t);
    const raw = ensureReportsGroup(withHome, t);

    if (restrictedPlanMenu) {
      let piped = sortNavGroupsForMainBar(
        flattenAccessIntoSecurity(flattenSaaSIntoHome(raw)),
      );
      if (planMenuBarLayout === "horizontal") {
        piped = expandPlanCustomRootsToBarGroups(piped);
      }
      return piped;
    }
    return sortNavGroupsForMainBar(
      flattenAccessIntoSecurity(flattenSaaSIntoHome(raw)),
    );
  }, [
    sessionMenuDto,
    sessionMenuResolved,
    t,
    restrictedPlanMenu,
    planMenuBarLayout,
  ]);

  // `zh-favorites` legacy format: { to: string; label: string }[] (identidad por ruta).
  // Formato actual: string[] de NavItem.id (identidad estable, sobrevive cambios de
  // ruta/idioma/label). Los favoritos legacy se migran a ids una vez resuelto
  // `mainMenuGroups` (ver efecto de migración más abajo).
  const [favoriteIds, setFavoriteIds] = useState<Set<string>>(() => {
    try {
      const raw = localStorage.getItem("zh-favorites");
      if (!raw) return new Set();
      const parsed = JSON.parse(raw) as unknown;
      if (!Array.isArray(parsed)) return new Set();
      return new Set(parsed.filter((x): x is string => typeof x === "string"));
    } catch {
      return new Set();
    }
  });

  const [legacyFavorites] = useState<{ to: string; label: string }[]>(() => {
    try {
      const raw = localStorage.getItem("zh-favorites");
      if (!raw) return [];
      const parsed = JSON.parse(raw) as unknown;
      if (!Array.isArray(parsed)) return [];
      return parsed
        .filter((x) => x && typeof x === "object")
        .map((x) => x as { to?: unknown; label?: unknown })
        .filter((x) => typeof x.to === "string" && typeof x.label === "string")
        .map((x) => ({ to: x.to as string, label: x.label as string }));
    } catch {
      return [];
    }
  });

  useEffect(() => {
    try {
      localStorage.setItem("zh-favorites", JSON.stringify([...favoriteIds]));
    } catch {
      // ignore
    }
  }, [favoriteIds]);

  const toggleFavorite = (item: NavItem) => {
    setFavoriteIds((prev) => {
      const next = new Set(prev);
      if (next.has(item.id)) next.delete(item.id);
      else next.add(item.id);
      return next;
    });
  };

  const isFavorite = (id: string) => favoriteIds.has(id);

  // Backend pre-filtered by permissions — just exclude empty groups.
  const visibleGroups = useMemo(
    () => groups.filter((g) => g.items.length > 0),
    [groups],
  );

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

  // Migración única de favoritos legacy ({ to, label }) a NavItem.id: recorre el árbol
  // ya resuelto buscando, para cada `to` legacy, el ítem actual y agrega su `id`.
  useEffect(() => {
    if (legacyFavorites.length === 0) return;
    if (!sessionMenuResolved) return;

    const idByTo = new Map<string, string>();
    const visit = (items: NavItem[]) => {
      for (const it of items) {
        if (it.to) idByTo.set(it.to, it.id);
        if (it.children?.length) visit(it.children);
      }
    };
    for (const g of mainMenuGroups) visit(g.items);

    setFavoriteIds((prev) => {
      const next = new Set(prev);
      let changed = false;
      for (const f of legacyFavorites) {
        const id = idByTo.get(f.to);
        if (id && !next.has(id)) {
          next.add(id);
          changed = true;
        }
      }
      return changed ? next : prev;
    });
  }, [legacyFavorites, sessionMenuResolved, mainMenuGroups]);

  const showPlanVerticalNav =
    restrictedPlanMenu &&
    planMenuBarLayout === "vertical" &&
    mainMenuGroups.length > 0;

  return {
    user,
    t,
    sessionMenuResolved,
    mainMenuGroups,
    showPlanVerticalNav,
    isFavorite,
    toggleFavorite,
  };
}
