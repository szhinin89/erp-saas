import type { SessionMenuGroupDto, SessionMenuItemDto } from '../../types/access';
import { normalizePolicyPermissionKey } from '../../store/permissionsStore';

/** Rutas de administración de identidad: no se asignan como permisos de negocio desde el árbol. */
const IamRoutePrefixes = ['/profiles', '/access'];

const SubscriberIamGroupCode = 'subscriber-company-iam';

export type MenuPermLeaf = {
  id: string;
  routePath: string;
  labelKey: string;
  displayLabel: string | null;
  keys: string[];
};

export type MenuPermSection = {
  groupCode: string;
  labelKey: string;
  leaves: MenuPermLeaf[];
};

function isIamRoute(route: string): boolean {
  const r = route.trim();
  return IamRoutePrefixes.some((p) => r === p || r.startsWith(`${p}/`));
}

function collectLeavesFromItems(
  items: SessionMenuItemDto[],
  groupCode: string,
  acc: MenuPermLeaf[],
  seenLeaf: Set<string>,
): void {
  for (const it of items) {
    if (it.children?.length) {
      collectLeavesFromItems(it.children, groupCode, acc, seenLeaf);
      continue;
    }

    const route = (it.routePath ?? '').trim();
    if (!route || isIamRoute(route)) continue;

    const keys: string[] = [];
    const pk = (it.permissionKey ?? '').trim();
    if (pk) {
      const n = normalizePolicyPermissionKey(pk);
      if (n && !keys.includes(n)) keys.push(n);
    }
    for (const raw of it.permissionKeysAny ?? []) {
      const n = normalizePolicyPermissionKey(raw);
      if (n && !keys.includes(n)) keys.push(n);
    }
    if (keys.length === 0) continue;

    const id = `${groupCode}::${route}::${keys.join('|')}`;
    if (seenLeaf.has(id)) continue;
    seenLeaf.add(id);

    acc.push({
      id,
      routePath: route,
      labelKey: (it.labelKey ?? '').trim() || 'nav.unknown',
      displayLabel: it.displayLabel?.trim() ? it.displayLabel.trim() : null,
      keys,
    });
  }
}

/** Secciones = grupos del menú de sesión; hojas = entradas con al menos un permiso (excluye IAM inyectado). */
export function buildMenuPermissionSections(groups: SessionMenuGroupDto[] | undefined): MenuPermSection[] {
  if (!groups?.length) return [];
  const out: MenuPermSection[] = [];
  const seenLeaf = new Set<string>();

  for (const g of groups) {
    if (g.code === SubscriberIamGroupCode) continue;
    const leaves: MenuPermLeaf[] = [];
    collectLeavesFromItems(g.items, g.code, leaves, seenLeaf);
    if (leaves.length === 0) continue;
    out.push({ groupCode: g.code, labelKey: g.labelKey, leaves });
  }
  return out;
}

export function sectionTitle(t: (k: string) => string, section: MenuPermSection): string {
  const lk = section.labelKey?.trim();
  if (lk && lk.includes('.')) return t(lk);
  return section.groupCode;
}

export function leafTitle(t: (k: string) => string, leaf: MenuPermLeaf): string {
  const d = leaf.displayLabel?.trim();
  if (d) return d;
  const lk = leaf.labelKey?.trim();
  if (lk && lk.includes('.')) return t(lk);
  return leaf.routePath;
}
