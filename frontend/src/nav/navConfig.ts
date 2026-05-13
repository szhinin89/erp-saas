import type { SessionMenuGroupDto, SessionMenuItemDto } from '../types/access';
import { normalizePolicyPermissionKey } from '../store/permissionsStore';

export type NavItem = {
  to: string;
  label: string;
  icon?: string;
  /** Si está definido, el ítem solo se muestra si el rol del usuario está en la lista (p. ej. solo SuperAdmin). */
  roles?: string[];
  /** Clave de módulo contratado (inventario, accounting, saas, access). Si falta, no se filtra por suscripción. */
  moduleKey?: string;
  /** Si está definido, el ítem solo se muestra si el usuario tiene este permiso. */
  permissionKey?: string;
  /** Si está definido, basta con tener uno de estos permisos (OR). Tiene prioridad sobre `permissionKey`. */
  permissionKeysAny?: string[];
  children?: NavItem[];
};
export type NavGroup = {
  id: string;
  label: string;
  icon: string;
  items: NavItem[];
  roles?: string[];
  /** Si está definido, el grupo solo se muestra si el tenant tiene ese módulo contratado. */
  moduleKey?: string;
  /** Orden desde API/BD (<c>ui_nav_groups.sort_order</c>); si falta, se usa el orden fijo de barra. */
  sortOrder?: number;
};

export type TranslateFn = (key: string) => string;

/** Menú del constructor por plan/empresa: no inyectar grupos estáticos ni Ventas por defecto. */
export function isPlanBuilderSessionMenu(rows: SessionMenuGroupDto[] | undefined): boolean {
  return !!rows?.some((g) => g.code === 'plan-custom');
}

/** Orden fijo de la barra principal: Configuración junto a Inventario; Ventas detrás. */
/** Orden barra: Inicio → Contabilidad → Inventario → Configuración → Ventas → Compras → RRHH → Seguridad. */
const MAIN_NAV_GROUP_ORDER = [
  'home',
  'accounting',
  'inventario',
  'configuracion',
  'sales',
  'purchases',
  'hr',
  'security',
] as const;

/** Orden fijo de respaldo cuando un grupo no trae <c>sortOrder</c> (menú estático). */
function defaultBarRank(id: string): number {
  const i = (MAIN_NAV_GROUP_ORDER as readonly string[]).indexOf(id);
  return i >= 0 ? i : 1000;
}

/** Reordena grupos para la barra superior (sesión API o fallback local). */
export function sortNavGroupsForMainBar(groups: NavGroup[]): NavGroup[] {
  return [...groups].sort((a, b) => {
    const sa = a.sortOrder;
    const sb = b.sortOrder;
    if (sa != null && sb != null && sa !== sb) return sa - sb;
    if (sa != null && sb == null) return -1;
    if (sa == null && sb != null) return 1;
    const d = defaultBarRank(a.id) - defaultBarRank(b.id);
    if (d !== 0) return d;
    return a.id.localeCompare(b.id);
  });
}

/** Alias BD/legacy → rutas definidas en `App.tsx` (<Routes>). Sin coincidencia, `*` envía al dashboard. */
const MENU_ROUTE_ALIASES: Record<string, string> = {
  '/dashboard/products': '/products',
  '/inventario/products': '/products',
  '/catalog/products': '/products',
  '/product': '/products',
  '/contabilidad': '/accounting',
  '/contabilidad/configuracion': '/accounting',
  '/configuracion/sucursales': '/saas/branches',
  '/logistica/bodegas': '/inventario/transferencias',
  '/inventario/stock': '/products',
  '/ventas': '/ventas/facturas',
  '/ventas/clientes': '/ventas/customers',
  '/catalog/units': '/inventario/units',
  '/catalog/types': '/inventario/product-types',
  '/catalog/subcategories': '/inventario/structure',
  '/catalog/lines': '/inventario/structure',
  '/catalog/categories': '/inventario/structure',
  '/catalog/brands': '/inventario/brands',
  '/catalog/tax-rates': '/inventario/tax-rates',
  '/catalog/tariffs': '/inventario/tariffs',
  '/catalog/structure': '/inventario/structure',
};

/**
 * Normaliza rutas del menú (BD): slash inicial, quita slash final, conserva ?query y #hash,
 * y aplica alias conocidos. Sin "/" inicial, enlaces `<a>`/`NavLink` se resolvían relativos al
 * pathname actual (p. ej. `products` en `/dashboard` → `/dashboard/products` → 404 → dashboard).
 */
export function normalizeMenuRoutePath(path: string): string {
  const raw = path.trim();
  if (!raw || raw === '#' || raw.toLowerCase().startsWith('javascript:')) return '';
  if (/^[a-z][a-z0-9+.-]*:/i.test(raw)) return raw;

  const internal = raw.startsWith('/') ? raw : `/${raw}`;
  try {
    const u = new URL(`http://dummy.local${internal}`);
    let pathname = u.pathname;
    if (pathname.length > 1 && pathname.endsWith('/')) pathname = pathname.slice(0, -1);
    pathname = MENU_ROUTE_ALIASES[pathname] ?? pathname;
    return `${pathname}${u.search}${u.hash}`;
  } catch {
    return internal.startsWith('/') ? internal : `/${internal}`;
  }
}

function mapSessionMenuItem(it: SessionMenuItemDto, t: TranslateFn, inheritedRoute?: string): NavItem {
  const routeRaw = (it.routePath ?? '').trim();
  const permRaw = (it.permissionKey ?? '').trim();
  const folder = !permRaw && !routeRaw && (it.children?.length ?? 0) > 0;

  const normalized = normalizeMenuRoutePath(routeRaw);
  const fallback = inheritedRoute ? normalizeMenuRoutePath(inheritedRoute) : '';
  const to = folder ? '' : normalized || fallback;

  const chainFallback = to || fallback;
  const children =
    it.children && it.children.length > 0
      ? it.children.map((c) => mapSessionMenuItem(c, t, chainFallback || inheritedRoute))
      : undefined;

  const dl = it.displayLabel?.trim();
  const icon = it.icon?.trim();
  const pkNorm = permRaw ? normalizePolicyPermissionKey(permRaw) : '';
  const keysAnyNorm = it.permissionKeysAny?.length
    ? [...new Set(it.permissionKeysAny.map((k) => normalizePolicyPermissionKey(k)))]
    : undefined;

  return {
    to,
    label: dl && dl.length > 0 ? dl : t(it.labelKey),
    ...(icon ? { icon } : {}),
    moduleKey: it.moduleKey ?? undefined,
    permissionKey: pkNorm || undefined,
    permissionKeysAny: keysAnyNorm?.length ? keysAnyNorm : undefined,
    roles: it.itemRoles?.length ? [...it.itemRoles] : undefined,
    ...(children?.length ? { children } : {}),
  };
}

/** Rutas de administración de plataforma: no deben venir del menú en BD (solo enlaces estáticos para SuperAdmin). */
function isPlatformAdminMenuRoute(routePath: string): boolean {
  if (routePath === '/companies') return true;
  return routePath.startsWith('/superadmin');
}

function filterPlatformAdminRoutesFromSessionItems(items: SessionMenuItemDto[]): SessionMenuItemDto[] {
  const out: SessionMenuItemDto[] = [];
  for (const it of items) {
    if (isPlatformAdminMenuRoute(it.routePath)) continue;
    const ch = it.children?.length ? filterPlatformAdminRoutesFromSessionItems(it.children) : undefined;
    out.push({
      ...it,
      children: ch?.length ? ch : undefined,
    });
  }
  return out;
}

function filterPlatformAdminRoutesFromSessionMenuDto(dto: SessionMenuGroupDto[]): SessionMenuGroupDto[] {
  return dto
    .map((g) => ({ ...g, items: filterPlatformAdminRoutesFromSessionItems(g.items) }))
    .filter((g) => g.items.length > 0);
}

/** Convierte la respuesta del API en el mismo shape que `buildNavGroups` (incl. filtro panel superadmin). */
export function mapSessionMenuToNavGroups(
  dto: SessionMenuGroupDto[],
  t: TranslateFn,
  options?: { superAdminPanelEnabled?: boolean },
): NavGroup[] {
  const superOn = options?.superAdminPanelEnabled ?? true;
  const cleaned = filterPlatformAdminRoutesFromSessionMenuDto(dto);
  const mapped = cleaned
    .filter((g) => !g.requireSuperAdminPanel || superOn)
    .map((g) => ({
      id: g.code,
      label: t(g.labelKey),
      icon: g.icon,
      moduleKey: g.moduleKey ?? undefined,
      roles: g.roles ?? undefined,
      sortOrder: g.sortOrder,
      items: g.items.map((it) => mapSessionMenuItem(it, t)),
    }));
  return sortNavGroupsForMainBar(mapped);
}

/**
 * Menú plan `plan-custom` en barra horizontal: el preview coloca cada raíz del árbol como sección en fila;
 * aquí se refleja creando una píldora por cada ítem raíz (carpeta con hijos o hoja), en lugar de una sola
 * píldora con todo el árbol en un desplegable vertical.
 */
export function expandPlanCustomRootsToBarGroups(groups: NavGroup[]): NavGroup[] {
  const idx = groups.findIndex((g) => g.id === 'plan-custom');
  if (idx < 0) return groups;

  const plan = groups[idx]!;
  const roots = plan.items;
  if (roots.length <= 1) return groups;

  const rest = groups.filter((_, i) => i !== idx);
  const baseOrder = plan.sortOrder ?? 0;
  const slug = (label: string, i: number) => {
    const s = label
      .normalize('NFKD')
      .replace(/[\u0300-\u036f]/g, '')
      .replace(/\s+/g, '-')
      .replace(/[^a-zA-Z0-9_-]/g, '')
      .slice(0, 24)
      .toLowerCase();
    return s || `r${i}`;
  };

  const expanded: NavGroup[] = roots.map((root, i) => ({
    id: `plan-custom__${i}-${slug(root.label, i)}`,
    label: root.label,
    icon: root.icon ?? plan.icon ?? '•',
    moduleKey: root.moduleKey ?? plan.moduleKey,
    roles: plan.roles,
    sortOrder: baseOrder * 100 + i,
    items: [root],
  }));

  return sortNavGroupsForMainBar([...rest, ...expanded]);
}

function collectNavTos(items: NavItem[]): string[] {
  const out: string[] = [];
  for (const it of items) {
    out.push(it.to);
    if (it.children?.length) out.push(...collectNavTos(it.children));
  }
  return out;
}

/**
 * Menú único para SuperAdmin en contexto global (sin tenant operativo).
 * Solo rutas de plataforma; no incluye inventario, contabilidad, etc.
 */
export function buildGlobalSuperAdminNavGroups(
  t: TranslateFn,
  options?: { superAdminPanelEnabled?: boolean },
): NavGroup[] {
  const items = getSuperAdminPanelNavExtras(t, options);
  if (!items.length) return [];
  return sortNavGroupsForMainBar([
    {
      id: 'superadmin',
      label: t('app.nav.group.superadmin'),
      icon: '◆',
      sortOrder: 0,
      roles: ['SuperAdmin'],
      items,
    },
  ]);
}

/**
 * Enlaces del panel SuperAdmin: siempre estáticos (navConfig), no filas de <c>ui_nav_items</c>,
 * para no mezclar la plataforma con el menú por-tenant/empresa en BD.
 */
export function getSuperAdminPanelNavExtras(
  t: TranslateFn,
  options?: { superAdminPanelEnabled?: boolean },
): NavItem[] {
  const superAdminOn = options?.superAdminPanelEnabled ?? true;
  if (!superAdminOn) return [];
  return [{ to: '/superadmin', label: t('app.nav.superadmin'), roles: ['SuperAdmin'] }];
}

/** Cuando el menú viene de BD, añade en Inicio los enlaces estáticos de SuperAdmin que falten (misma ruta = no duplicar). */
export function mergeSuperAdminNavExtrasIntoHome(
  groups: NavGroup[],
  t: TranslateFn,
  options?: { superAdminPanelEnabled?: boolean },
): NavGroup[] {
  const extras = getSuperAdminPanelNavExtras(t, options);
  if (!extras.length) return groups;

  const homeIdx = groups.findIndex((g) => g.id === 'home');
  if (homeIdx < 0) return groups;

  const home = groups[homeIdx]!;
  const existing = new Set(collectNavTos(home.items));
  const toAdd = extras.filter((it) => !existing.has(it.to));
  if (!toAdd.length) return groups;

  const newHome: NavGroup = { ...home, items: [...home.items, ...toAdd] };
  const next = [...groups];
  next[homeIdx] = newHome;
  return next;
}

export function buildNavGroups(
  t: TranslateFn,
  options?: { superAdminPanelEnabled?: boolean },
): NavGroup[] {
  const superAdminOn = options?.superAdminPanelEnabled ?? true;
  const groups: NavGroup[] = [
    {
      id: 'home',
      label: t('app.nav.group.home'),
      icon: '⊞',
      sortOrder: defaultBarRank('home') * 10,
      items: [{ to: '/dashboard', label: t('app.nav.dashboard') }, ...getSuperAdminPanelNavExtras(t, options)],
    },
    {
      id: 'inventario',
      label: t('app.nav.group.inventario'),
      icon: '📦',
      moduleKey: 'inventario',
      sortOrder: defaultBarRank('inventario') * 10,
      items: [
        { to: '/products', label: t('app.nav.products'), permissionKey: 'inventario.products.view' },
        { to: '/inventario/ajustes',         label: t('app.nav.catalog.ajustes'),        permissionKey: 'inventario.ajustes.view' },
        { to: '/inventario/transferencias', label: t('app.nav.catalog.transferencias'), permissionKey: 'inventario.transferencias.view' },
        { to: '/inventario/brands', label: t('app.nav.catalog.brands'), permissionKey: 'inventario.brands.view' },
        { to: '/inventario/product-types', label: t('app.nav.catalog.productTypes'), permissionKey: 'inventario.productTypes.view' },
        { to: '/inventario/units', label: t('app.nav.catalog.units'), permissionKey: 'inventario.units.view' },
        { to: '/inventario/tax-rates', label: t('app.nav.catalog.taxRates'), permissionKey: 'inventario.taxRates.view' },
        { to: '/inventario/tariffs', label: t('app.nav.catalog.tariffs'), permissionKey: 'inventario.tariffs.view' },
        { to: '/inventario/structure', label: t('app.nav.catalog.structure'), permissionKey: 'inventario.categories.view' },
      ],
    },
    {
      id: 'sales',
      label: t('app.nav.group.sales'),
      icon: '🛒',
      moduleKey: 'ventas',
      sortOrder: defaultBarRank('sales') * 10,
      items: [
        { to: '/ventas/customers', label: t('app.nav.catalog.customers'), permissionKey: 'ventas.customers.view' },
        { to: '/ventas/facturas', label: t('app.nav.sales.invoices'), permissionKey: 'ventas.facturas.view' },
      ],
    },
    {
      id: 'accounting',
      label: t('app.nav.group.accounting'),
      icon: '📒',
      moduleKey: 'accounting',
      sortOrder: defaultBarRank('accounting') * 10,
      items: [
        {
          to: '/accounting',
          label: t('app.nav.accounting'),
          permissionKeysAny: ['accounting.accounts.view', 'accounting.journal.view', 'accounting.config.view'],
        },
      ],
    },
    {
      id: 'purchases',
      label: t('app.nav.group.purchases'),
      icon: '📥',
      moduleKey: 'compras',
      sortOrder: defaultBarRank('purchases') * 10,
      items: [{ to: '/compras', label: t('app.nav.group.purchases'), moduleKey: 'compras' }],
    },
    {
      id: 'hr',
      label: t('app.nav.group.hr'),
      icon: '👥',
      moduleKey: 'rrhh',
      sortOrder: defaultBarRank('hr') * 10,
      items: [{ to: '/rrhh', label: t('app.nav.group.hr'), moduleKey: 'rrhh' }],
    },
    {
      id: 'configuracion',
      label: t('app.nav.group.configuracion'),
      icon: '⚙',
      sortOrder: defaultBarRank('configuracion') * 10,
      items: [
        {
          to: '/access',
          label: t('app.nav.access'),
          moduleKey: 'access',
          permissionKey: 'access.memberships.view',
        },
        {
          to: '/profiles',
          label: t('app.nav.profiles'),
          moduleKey: 'access',
          permissionKey: 'access.profiles.view',
        },
        {
          to: '/saas/branches',
          label: t('app.nav.branches'),
          moduleKey: 'saas',
          permissionKey: 'saas.branches.view',
          roles: ['Admin', 'SuperAdmin'],
        },
      ],
    },
    {
      id: 'security',
      label: t('app.nav.group.security'),
      icon: '🛡️',
      sortOrder: defaultBarRank('security') * 10,
      items: [{ to: '/security', label: t('app.nav.security'), roles: ['SuperAdmin'] }],
    },
  ];

  if (import.meta.env.DEV) {
    const tos = groups.flatMap((g) => collectNavTos(g.items));
    const seen = new Set<string>();
    for (const path of tos) {
      if (seen.has(path)) {
        console.warn(`[navConfig] Ruta de menú duplicada (mismo "to"): ${path}. Cada pantalla de formulario debe aparecer una sola vez en grupos; Favoritos es la excepción (copias en sesión).`);
      }
      seen.add(path);
    }
  }

  if (!superAdminOn) {
    return sortNavGroupsForMainBar(
      groups.map((g) => {
        if (g.id !== 'security') return g;
        return {
          ...g,
          items: g.items.filter(
            (it) => !(it.roles?.includes('SuperAdmin') && !it.roles?.includes('Admin')),
          ),
        };
      }),
    );
  }

  return sortNavGroupsForMainBar(groups);
}

/** API/BD antigua: fusiona grupo `access` dentro de `security` y quita la píldora Accesos. */
export function flattenAccessIntoSecurity(groups: NavGroup[]): NavGroup[] {
  const access = groups.find((g) => g.id === 'access');
  if (!access) return groups;

  const security = groups.find((g) => g.id === 'security');
  const liftRoles = access.roles?.filter(Boolean).length ? [...(access.roles ?? [])] : ['Admin', 'SuperAdmin'];
  const lifted: NavItem[] = access.items.map((it) => ({
    ...it,
    roles: it.roles?.length ? [...new Set([...it.roles, ...liftRoles])] : [...liftRoles],
  }));

  const rest = groups.filter((g) => g.id !== 'access');
  if (!security) {
    return rest;
  }

  const existingInSecurity = new Set(security.items.map((it) => it.to));
  const liftedDeduped = lifted.filter((it) => !existingInSecurity.has(it.to));
  const mergedItems = [...liftedDeduped, ...security.items];
  const newSecurity: NavGroup = {
    ...security,
    roles: undefined,
    items: mergedItems,
  };
  return rest.map((g) => (g.id === 'security' ? newSecurity : g));
}

/** Mueve el grupo `saas` (API/BD antigua) bajo Inicio y elimina la píldora SaaS. */
export function flattenSaaSIntoHome(groups: NavGroup[]): NavGroup[] {
  const saas = groups.find((g) => g.id === 'saas');
  if (!saas) return groups;

  const liftRoles = saas.roles?.filter(Boolean).length ? [...(saas.roles ?? [])] : ['SuperAdmin'];
  const lifted: NavItem[] = saas.items.map((it) => ({
    ...it,
    roles: it.roles?.length ? [...new Set([...it.roles, ...liftRoles])] : [...liftRoles],
  }));

  const rest = groups.filter((g) => g.id !== 'saas');
  const home = rest.find((g) => g.id === 'home');
  if (!home) {
    return rest;
  }
  const existingTo = new Set(home.items.map((it) => it.to));
  const liftedDeduped = lifted.filter((it) => !existingTo.has(it.to));
  const newHome: NavGroup = { ...home, items: [...home.items, ...liftedDeduped] };
  return rest.map((g) => (g.id === 'home' ? newHome : g));
}

/**
 * Si el menú de sesión (BD) aún no tiene el grupo `sales`, lo añadimos junto a Inventario:
 * - Si "Clientes" sigue bajo `inventario`, lo movemos en memoria a un grupo Ventas.
 * - Si no hay clientes en catálogo, se reutiliza el ítem del menú estático.
 */
export function ensureSalesNextToInventory(
  groups: NavGroup[],
  t: TranslateFn,
  options?: { superAdminPanelEnabled?: boolean },
): NavGroup[] {
  if (groups.some((g) => g.id === 'sales')) {
    return sortNavGroupsForMainBar(groups);
  }

  const inventory = groups.find((g) => g.id === 'inventario' || g.id === 'catalog');
  if (inventory) {
    const custIdx = inventory.items.findIndex(
      (it) => it.to === '/ventas/customers' || it.to === '/inventario/customers' || it.to === '/catalog/customers',
    );
    if (custIdx >= 0) {
      const customerItem = inventory.items[custIdx]!;
      const newInventoryItems = inventory.items.filter((_, i) => i !== custIdx);
      const newInventory: NavGroup = { ...inventory, items: newInventoryItems };
      const salesGroup: NavGroup = {
        id: 'sales',
        label: t('app.nav.group.sales'),
        icon: '🛒',
        moduleKey: 'ventas',
        sortOrder: defaultBarRank('sales') * 10,
        items: [
          { ...customerItem, moduleKey: 'ventas' },
          { to: '/ventas/facturas', label: t('app.nav.sales.invoices'), permissionKey: 'ventas.facturas.view' },
        ],
      };
      const withoutInventory = groups.filter((g) => g.id !== inventory.id);
      const next =
        newInventory.items.length > 0
          ? [...withoutInventory, newInventory, salesGroup]
          : [...withoutInventory, salesGroup];
      return sortNavGroupsForMainBar(next);
    }
  }

  const staticFull = buildNavGroups(t, options);
  const salesOnly = staticFull.find((g) => g.id === 'sales');
  if (salesOnly) {
    return sortNavGroupsForMainBar([...groups, salesOnly]);
  }
  return sortNavGroupsForMainBar(groups);
}

/** Grupos solo en menú estático (p. ej. Compras / RRHH) que la API de sesión aún no define. */
const GROUPS_FILL_FROM_STATIC: readonly string[] = ['purchases', 'hr', 'configuracion'];

export function mergeMissingStaticNavGroups(
  groups: NavGroup[],
  t: TranslateFn,
  options?: { superAdminPanelEnabled?: boolean },
): NavGroup[] {
  const staticFull = buildNavGroups(t, options);
  let out = [...groups];
  for (const id of GROUPS_FILL_FROM_STATIC) {
    if (!out.some((g) => g.id === id)) {
      const add = staticFull.find((g) => g.id === id);
      if (add) out = [...out, add];
    }
  }
  return sortNavGroupsForMainBar(out);
}

