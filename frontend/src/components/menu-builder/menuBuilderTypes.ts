import type { FuncionalidadArbolDto } from '../../services/superAdminService';
import type { SessionMenuGroupDto, SessionMenuItemDto } from '../../types/access';
import { normalizePolicyPermissionKey } from '../../store/permissionsStore';

/** Ítem de menú en el editor / vista previa (hoja: ruta+permiso; carpeta: ambos vacíos). */
export interface MenuItem {
  id: string;
  nombre: string;
  icono?: string;
  ruta?: string | null;
  permiso?: string | null;
  children?: MenuItem[];
}

/** Nodo interno del constructor con hijos materializados. */
export type EditorMenuItem = {
  uid: string;
  funcionalidadId?: string;
  nombre: string;
  icono?: string;
  ruta: string;
  permiso: string;
  children: EditorMenuItem[];
};

export function isEditorFolder(n: Pick<EditorMenuItem, 'ruta' | 'permiso'>): boolean {
  return !(n.ruta?.trim()) && !(n.permiso?.trim());
}

export function slugPerm(permiso: string): string {
  return permiso.replace(/[^a-zA-Z0-9]+/g, '_').replace(/^_|_$/g, '') || 'item';
}

export function createFolderEditorItem(nombre = 'Nueva carpeta'): EditorMenuItem {
  return {
    uid: crypto.randomUUID(),
    nombre,
    icono: 'fa-folder',
    ruta: '',
    permiso: '',
    children: [],
  };
}

/** Hoja nueva (formulario) en el árbol; permiso y ruta editables. */
export function createFormEditorItem(nombre = 'Nuevo formulario'): EditorMenuItem {
  return {
    uid: crypto.randomUUID(),
    nombre,
    icono: 'fa-file',
    ruta: '/',
    permiso: 'app.placeholder',
    children: [],
  };
}

export function funcionalidadToEditorItem(f: FuncionalidadArbolDto): EditorMenuItem {
  const ruta = (f.ruta ?? '').trim() || '/dashboard';
  const permiso = (f.permiso ?? '').trim();
  return {
    uid: crypto.randomUUID(),
    funcionalidadId: f.id,
    nombre: f.nombre,
    icono: (f.icono ?? undefined) || undefined,
    ruta,
    permiso,
    children: [],
  };
}

/** Lista plana de todas las funcionalidades (DFS) para la biblioteca. */
export function flattenFuncionalidades(arbol: FuncionalidadArbolDto[]): FuncionalidadArbolDto[] {
  const out: FuncionalidadArbolDto[] = [];
  const walk = (nodes: FuncionalidadArbolDto[]) => {
    for (const n of nodes) {
      out.push(n);
      walk(n.hijos ?? []);
    }
  };
  walk(arbol);
  return out;
}

export function buildFuncionalidadMaps(arbol: FuncionalidadArbolDto[]) {
  const byId = new Map<string, FuncionalidadArbolDto>();
  const byPerm = new Map<string, FuncionalidadArbolDto>();
  const walk = (nodes: FuncionalidadArbolDto[]) => {
    for (const n of nodes) {
      byId.set(n.id, n);
      const p = (n.permiso ?? '').trim();
      if (p) byPerm.set(p, n);
      walk(n.hijos ?? []);
    }
  };
  walk(arbol);
  return { byId, byPerm };
}

function parseFolderUidFromLabelKey(labelKey: string | undefined): string | null {
  const m = /^nav\.planFolder\.(.+)$/.exec((labelKey ?? '').trim());
  if (!m?.[1] || m[1].length < 8) return null;
  return m[1];
}

function findFuncionalidadByPerm(
  byPerm: Map<string, FuncionalidadArbolDto>,
  perm: string,
): FuncionalidadArbolDto | undefined {
  const direct = byPerm.get(perm);
  if (direct) return direct;
  const want = normalizePolicyPermissionKey(perm);
  for (const [k, v] of byPerm) {
    if (normalizePolicyPermissionKey(k) === want) return v;
  }
  return undefined;
}

function sessionItemToEditor(s: SessionMenuItemDto, byPerm: Map<string, FuncionalidadArbolDto>): EditorMenuItem {
  const perm = (s.permissionKey ?? '').trim();
  const route = (s.routePath ?? '').trim();
  const folder = !perm && !route;
  const cat = perm ? findFuncionalidadByPerm(byPerm, perm) : undefined;
  const stableUid = folder ? parseFolderUidFromLabelKey(s.labelKey) ?? crypto.randomUUID() : crypto.randomUUID();
  const iconFromSession = s.icon?.trim();

  if (folder) {
    return {
      uid: stableUid,
      nombre: (s.displayLabel ?? s.labelKey ?? '').trim() || 'Carpeta',
      icono: iconFromSession || undefined,
      ruta: '',
      permiso: '',
      children: (s.children ?? []).map((c) => sessionItemToEditor(c, byPerm)),
    };
  }

  return {
    uid: stableUid,
    funcionalidadId: cat?.id,
    nombre: (s.displayLabel ?? s.labelKey ?? '').trim() || 'Item',
    icono: iconFromSession || cat?.icono || undefined,
    ruta: route || '/dashboard',
    permiso: perm || 'session:menu',
    children: [],
  };
}

/** Aplana grupos en una lista de raíces (un solo grupo visual en el editor). */
export function sessionGroupsToEditorTree(
  groups: SessionMenuGroupDto[],
  byPerm: Map<string, FuncionalidadArbolDto>,
): EditorMenuItem[] {
  const items: SessionMenuItemDto[] = [];
  for (const g of groups) {
    for (const it of g.items) items.push(it);
  }
  return items.map((it) => sessionItemToEditor(it, byPerm));
}

function editorNodeToSessionItem(n: EditorMenuItem, sortOrder: number): SessionMenuItemDto {
  if (isEditorFolder(n)) {
    return {
      routePath: '',
      labelKey: `nav.planFolder.${n.uid}`,
      displayLabel: n.nombre,
      sortOrder,
      moduleKey: null,
      permissionKey: null,
      permissionKeysAny: null,
      itemRoles: null,
      children: n.children.length ? n.children.map((c, i) => editorNodeToSessionItem(c, i)) : undefined,
      icon: n.icono?.trim() || null,
    };
  }

  const perm = (n.permiso ?? '').trim();
  const ruta = (n.ruta ?? '').trim() || '/dashboard';
  return {
    routePath: ruta,
    labelKey: `nav.planLeaf.${slugPerm(perm)}`,
    displayLabel: n.nombre,
    sortOrder,
    moduleKey: null,
    permissionKey: perm.startsWith('session:') ? null : perm,
    permissionKeysAny: null,
    itemRoles: null,
    children: undefined,
    icon: n.icono?.trim() || null,
  };
}

export type PlanMenuBarLayout = 'horizontal' | 'vertical';

/** Lee `menuBarLayout` del grupo `plan-custom` tolerando PascalCase en JSON (`MenuBarLayout`). */
export function readPlanCustomMenuBarLayout(groups: SessionMenuGroupDto[]): PlanMenuBarLayout | null {
  for (const g of groups) {
    const o = g as unknown as Record<string, unknown>;
    const code = String(o.code ?? o.Code ?? '')
      .trim()
      .toLowerCase();
    if (code !== 'plan-custom') continue;
    const raw = o.menuBarLayout ?? o.MenuBarLayout;
    const m = typeof raw === 'string' ? raw.trim().toLowerCase() : '';
    if (m === 'horizontal' || m === 'vertical') return m;
  }
  return null;
}

/** Unifica `code` / `menuBarLayout` en camelCase tras `JSON.parse` (API u otros clientes pueden usar PascalCase). */
export function normalizeParsedMenuGroups(groups: SessionMenuGroupDto[]): SessionMenuGroupDto[] {
  return groups.map((g) => {
    const o = g as unknown as Record<string, unknown>;
    const code =
      (typeof o.code === 'string' ? o.code : typeof o.Code === 'string' ? (o.Code as string) : g.code) || '';
    const menuBarLayout =
      (typeof o.menuBarLayout === 'string' ? o.menuBarLayout : null) ??
      (typeof o.MenuBarLayout === 'string' ? (o.MenuBarLayout as string) : null) ??
      g.menuBarLayout ??
      undefined;
    return {
      ...g,
      code,
      ...(menuBarLayout != null ? { menuBarLayout } : {}),
    };
  });
}

export function editorTreeToSessionGroups(
  items: EditorMenuItem[],
  labelKey: string,
  menuBarLayout: PlanMenuBarLayout = 'horizontal',
): SessionMenuGroupDto[] {
  return [
    {
      code: 'plan-custom',
      icon: 'layers',
      labelKey,
      sortOrder: 5,
      moduleKey: null,
      roles: null,
      requireSuperAdminPanel: false,
      items: items.map((n, i) => editorNodeToSessionItem(n, i)),
      menuBarLayout,
    },
  ];
}

export function serializeEditorTreeToMenuJson(
  tree: EditorMenuItem[],
  labelKey: string,
  menuBarLayout: PlanMenuBarLayout = 'horizontal',
): string {
  return JSON.stringify(editorTreeToSessionGroups(tree, labelKey, menuBarLayout), null, 2);
}

export function editorToMenuItems(nodes: EditorMenuItem[]): MenuItem[] {
  return nodes.map((n) => ({
    id: n.uid,
    nombre: n.nombre,
    icono: n.icono,
    ruta: isEditorFolder(n) ? null : n.ruta,
    permiso: isEditorFolder(n) ? null : n.permiso,
    children: n.children.length ? editorToMenuItems(n.children) : undefined,
  }));
}

/** Valida el árbol del editor antes de guardar (mismas reglas que el backend). */
export function validateEditorMenuTree(nodes: EditorMenuItem[], groupLabel = 'menú'): string[] {
  const errs: string[] = [];
  const walk = (items: EditorMenuItem[]) => {
    items.forEach((item, idx) => {
      const name = (item.nombre ?? '').trim() || `posición ${idx + 1}`;
      const route = (item.ruta ?? '').trim();
      const perm = (item.permiso ?? '').trim();
      const leaf = route.length > 0 && perm.length > 0;
      const folder = route.length === 0 && perm.length === 0;
      const ch = item.children ?? [];

      if (leaf && ch.length > 0)
        errs.push(`${groupLabel}: «${name}» es hoja pero tiene subítems.`);
      if (folder && ch.length === 0)
        errs.push(`${groupLabel}: «${name}» es carpeta y debe tener al menos un hijo.`);
      if (!leaf && !folder)
        errs.push(`${groupLabel}: «${name}» tiene ruta y permiso inconsistentes (complete ambos o vacíe ambos para carpeta).`);

      if (ch.length) walk(ch);
    });
  };
  walk(nodes);
  return errs;
}

/** Valida grupos JSON (p. ej. pestaña JSON) con las mismas reglas recursivas. */
export function validateSessionMenuGroups(groups: SessionMenuGroupDto[]): string[] {
  const errs: string[] = [];
  for (const g of groups) {
    const o = g as unknown as Record<string, unknown>;
    const layoutRaw = o.menuBarLayout ?? o.MenuBarLayout;
    const layout = (typeof layoutRaw === 'string' ? layoutRaw : '').trim().toLowerCase();
    if (layout && layout !== 'horizontal' && layout !== 'vertical') {
      const code = String(o.code ?? o.Code ?? g.code ?? '?');
      errs.push(`Grupo ${code}: menuBarLayout debe ser horizontal o vertical.`);
    }
    const walk = (items: SessionMenuItemDto[]) => {
      items.forEach((item, idx) => {
        const name =
          (item.displayLabel ?? '').trim() ||
          (item.labelKey ?? '').trim() ||
          `posición ${idx + 1}`;
        const route = (item.routePath ?? '').trim();
        const perm = (item.permissionKey ?? '').trim();
        const leaf = route.length > 0 && perm.length > 0;
        const folder = route.length === 0 && perm.length === 0;
        const ch = item.children ?? [];

        if (leaf && ch.length > 0)
          errs.push(`Grupo ${g.code}: «${name}» es hoja pero tiene subítems.`);
        if (folder && ch.length === 0)
          errs.push(`Grupo ${g.code}: «${name}» es carpeta y debe tener al menos un hijo.`);
        if (!leaf && !folder)
          errs.push(`Grupo ${g.code}: «${name}» tiene ruta/permiso inconsistentes.`);

        if (ch.length) walk(ch);
      });
    };
    walk(g.items);
  }
  return errs;
}
