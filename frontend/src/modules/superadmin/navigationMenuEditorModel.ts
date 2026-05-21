import type { AdminNavigationMenu, AdminNavGroupRow, AdminNavItemRow } from './api/superAdminService';
import type { NavItemSiblingOrderLevel } from './api/superAdminService';

export function cloneMenu(m: AdminNavigationMenu): AdminNavigationMenu {
  return typeof structuredClone === 'function'
    ? structuredClone(m)
    : (JSON.parse(JSON.stringify(m)) as AdminNavigationMenu);
}

export function moveInPlace<T>(arr: T[], index: number, delta: number): void {
  const j = index + delta;
  if (j < 0 || j >= arr.length) return;
  const t = arr[index]!;
  arr[index] = arr[j]!;
  arr[j] = t;
}

export function findSiblingArray(items: AdminNavItemRow[], itemId: string): AdminNavItemRow[] | null {
  if (items.some((i) => i.id === itemId)) return items;
  for (const it of items) {
    if (!it.children) it.children = [];
    const inner = findSiblingArray(it.children, itemId);
    if (inner) return inner;
  }
  return null;
}

export function findItemLocation(
  roots: AdminNavItemRow[],
  itemId: string,
  parent: AdminNavItemRow | null = null,
): { siblings: AdminNavItemRow[]; index: number; parent: AdminNavItemRow | null } | null {
  const siblings = parent ? (parent.children ??= []) : roots;
  const idx = siblings.findIndex((x) => x.id === itemId);
  if (idx >= 0) return { siblings, index: idx, parent };
  for (const n of siblings) {
    if (!n.children) n.children = [];
    const hit = findItemLocation(n.children, itemId, n);
    if (hit) return hit;
  }
  return null;
}

export function collectItemLevels(
  groupId: string,
  items: AdminNavItemRow[],
  parentItemId: string | null,
  out: NavItemSiblingOrderLevel[],
): void {
  if (items.length === 0) return;
  out.push({ groupId, parentItemId, orderedItemIds: items.map((i) => i.id) });
  for (const it of items) {
    const ch = it.children ?? [];
    if (ch.length) collectItemLevels(groupId, ch, it.id, out);
  }
}

/** Cadena de ids desde la raíz hasta `targetId` (inclusive), para expandir submenús al crear un hijo. */
export function findAncestorPathIncludingSelf(
  items: AdminNavItemRow[],
  targetId: string,
  pathPrefix: string[] = [],
): string[] | null {
  for (const it of items) {
    const chain = [...pathPrefix, it.id];
    if (it.id === targetId) return chain;
    const ch = it.children ?? [];
    const hit = findAncestorPathIncludingSelf(ch, targetId, chain);
    if (hit) return hit;
  }
  return null;
}

export function findNavItemInMenu(
  menu: AdminNavigationMenu,
  groupId: string,
  itemId: string,
): { group: AdminNavGroupRow; item: AdminNavItemRow } | null {
  const group = menu.groups.find((g) => g.id === groupId);
  if (!group) return null;
  const walk = (items: AdminNavItemRow[]): AdminNavItemRow | null => {
    for (const it of items) {
      if (it.id === itemId) return it;
      const ch = it.children ?? [];
      const hit = walk(ch);
      if (hit) return hit;
    }
    return null;
  };
  const item = walk(group.rootItems);
  if (!item) return null;
  return { group, item };
}

export function countNavSubtreeNodes(item: AdminNavItemRow): number {
  let n = 1;
  for (const c of item.children ?? []) n += countNavSubtreeNodes(c);
  return n;
}
