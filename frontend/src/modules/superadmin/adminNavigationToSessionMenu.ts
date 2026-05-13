import type { AdminNavigationMenu, AdminNavItemRow } from '../../services/superAdminService';
import type { SessionMenuGroupDto, SessionMenuItemDto } from '../../types/access';

function isPlatformRoute(path: string): boolean {
  const p = (path ?? '').trim();
  return p === '/companies' || p.startsWith('/superadmin');
}

function mapAdminItems(items: AdminNavItemRow[] | null | undefined): SessionMenuItemDto[] {
  if (!items?.length) return [];
  const out: SessionMenuItemDto[] = [];
  for (const i of items) {
    if (!i.isActive) continue;
    if (isPlatformRoute(i.routePath)) continue;
    const children = mapAdminItems(i.children ?? undefined);
    out.push({
      routePath: i.routePath,
      labelKey: i.labelKey,
      displayLabel: i.displayLabel ?? null,
      sortOrder: i.sortOrder,
      moduleKey: i.moduleKey,
      permissionKey: i.permissionKey,
      permissionKeysAny: i.permissionKeysAny,
      itemRoles: null,
      icon: null,
      children: children.length ? children : undefined,
    });
  }
  return out;
}

/** Convierte el árbol administrable `ui_nav_*` al formato de sesión (como GET /api/me/menu). */
export function adminNavigationToSessionMenu(menu: AdminNavigationMenu): SessionMenuGroupDto[] {
  return menu.groups
    .filter((g) => g.isActive && !g.requireSuperAdminPanel)
    .map((g) => ({
      code: g.code,
      icon: g.icon,
      labelKey: g.labelKey,
      sortOrder: g.sortOrder,
      moduleKey: g.moduleKey,
      roles: g.roles,
      requireSuperAdminPanel: false,
      items: mapAdminItems(g.rootItems),
    }))
    .filter((g) => g.items.length > 0);
}
