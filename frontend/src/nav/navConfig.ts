import type { SessionMenuGroupDto, SessionMenuItemDto } from '../types/access';
import { NAV_PLATFORM_OPERATOR_ROLE, readsRequirePlatformPanel, SUBSCRIBER_ADMIN_JWT_ROLES } from '../constants/platformAuth';
import { normalizePolicyPermissionKey } from '../store/permissionsStore';

export type NavItem = {
  to: string;
  label: string;
  icon?: string;
  /** Si está definido, el ítem solo se muestra si el rol del usuario está en la lista (p. ej. solo operador platform). */
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
  /** Si está definido, el grupo solo se muestra si el subscriber tiene ese módulo contratado. */
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

/** Orden fijo de respaldo cuando un grupo no trae <c>sortOrder</c> desde la API. */
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
  '/dashboard/products': '/inventory/products',
  '/catalog/products': '/inventory/products',
  '/product': '/inventory/products',
  '/products': '/inventory/products',
  '/finance/accounts': '/finance/accounts',
  '/finance/config': '/finance/accounts?tab=config',
  '/contabilidad/configuracion': '/finance/accounts?tab=config',
  '/settings/branches': '/settings/branches',
  '/logistica/bodegas': '/inventory/warehouses',
  '/inventory/stock': '/inventory/stock',
  '/ventas': '/sales/invoices',
  '/catalog/units': '/inventory/units',
  '/catalog/types': '/inventory/product-types',
  '/catalog/subcategories': '/inventory/catalog-structure',
  '/catalog/lines': '/inventory/catalog-structure',
  '/catalog/categories': '/inventory/catalog-structure',
  '/catalog/brands': '/inventory/brands',
  '/catalog/tariffs': '/inventory/tariffs',
  '/catalog/structure': '/inventory/catalog-structure',
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

/** Rutas de administración de plataforma: no deben venir del menú en BD (solo enlaces estáticos para operador platform). */
function isPlatformAdminMenuRoute(routePath: string): boolean {
  return routePath.startsWith('/platform');
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

/** Convierte la respuesta del API en el mismo shape que `buildNavGroups` (incl. filtro panel platform). */
export function mapSessionMenuToNavGroups(
  dto: SessionMenuGroupDto[],
  t: TranslateFn,
  options?: { platformPanelEnabled?: boolean },
): NavGroup[] {
  const superOn = options?.platformPanelEnabled ?? true;
  const cleaned = filterPlatformAdminRoutesFromSessionMenuDto(dto);
  const mapped = cleaned
    .filter((g) => !readsRequirePlatformPanel(g) || superOn)
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
    // Si la raíz es una carpeta (tiene hijos), exponer los hijos directamente como
    // ítems del dropdown en lugar de mostrar la carpeta como branch toggle.
    items: root.children?.length ? root.children : [root],
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
 * Menú único para operador platform en contexto global (sin subscriber operativo).
 * Solo rutas de plataforma; no incluye inventario, contabilidad, etc.
 */
export function buildGlobalPlatformNavGroups(
  t: TranslateFn,
  options?: { platformPanelEnabled?: boolean },
): NavGroup[] {
  const items = getPlatformPanelNavExtras(t, options);
  if (!items.length) return [];
  return sortNavGroupsForMainBar([
    {
      id: 'platform',
      label: t('app.nav.group.platform'),
      icon: '◆',
      sortOrder: 0,
      roles: [NAV_PLATFORM_OPERATOR_ROLE],
      items,
    },
  ]);
}

/**
 * Enlaces del panel platform: siempre estáticos (navConfig), no filas de <c>ui_nav_items</c>,
 * para no mezclar la plataforma con el menú por-suscriptor/empresa en BD.
 */
export function getPlatformPanelNavExtras(
  t: TranslateFn,
  options?: { platformPanelEnabled?: boolean },
): NavItem[] {
  const platformPanelOn = options?.platformPanelEnabled ?? true;
  if (!platformPanelOn) return [];
  return [
    { to: '/platform/overview', label: t('app.nav.platform'), roles: [NAV_PLATFORM_OPERATOR_ROLE] },
  ];
}

/** Cuando el menú viene de BD, añade en Inicio los enlaces estáticos de platform que falten (misma ruta = no duplicar). */
export function mergePlatformNavExtrasIntoHome(
  groups: NavGroup[],
  t: TranslateFn,
  options?: { platformPanelEnabled?: boolean },
): NavGroup[] {
  const extras = getPlatformPanelNavExtras(t, options);
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

/** API/BD antigua: fusiona grupo `access` dentro de `security` y quita la píldora Accesos. */
export function flattenAccessIntoSecurity(groups: NavGroup[]): NavGroup[] {
  const access = groups.find((g) => g.id === 'access');
  if (!access) return groups;

  const security = groups.find((g) => g.id === 'security');
  const liftRoles = access.roles?.filter(Boolean).length
    ? [...(access.roles ?? [])]
    : [...SUBSCRIBER_ADMIN_JWT_ROLES];
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

/** Asegura que el dashboard SaaS del suscriptor sea el primer ítem en Inicio. */
export function ensureSubscriberHomeOverview(groups: NavGroup[], t: TranslateFn): NavGroup[] {
  const saasItem: NavItem = { to: '/saas/overview', label: t('app.nav.saasOverview') };
  const erpItem: NavItem = { to: '/dashboard', label: t('app.nav.erpDashboard') };

  const homeIdx = groups.findIndex((g) => g.id === 'home');
  if (homeIdx < 0) {
    return sortNavGroupsForMainBar([
      {
        id: 'home',
        label: t('app.nav.group.home'),
        icon: '⊞',
        sortOrder: defaultBarRank('home') * 10,
        items: [saasItem, erpItem],
      },
      ...groups,
    ]);
  }

  const home = groups[homeIdx]!;
  const withoutSaas = home.items.filter((it) => it.to !== '/saas/overview');
  const hasErp = withoutSaas.some((it) => it.to === '/dashboard');
  const nextItems = hasErp ? [saasItem, ...withoutSaas] : [saasItem, erpItem, ...withoutSaas];
  const next = [...groups];
  next[homeIdx] = { ...home, items: nextItems };
  return next;
}

/** Mueve el grupo `saas` (API/BD antigua) bajo Inicio y elimina la píldora SaaS. */
export function flattenSaaSIntoHome(groups: NavGroup[]): NavGroup[] {
  const saas = groups.find((g) => g.id === 'saas');
  if (!saas) return groups;

  const liftRoles = saas.roles?.filter(Boolean).length
    ? [...(saas.roles ?? [])]
    : [NAV_PLATFORM_OPERATOR_ROLE];
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
 * Si el menú de sesión (BD) aún no tiene el grupo `sales`, intenta derivarlo del catálogo:
 * - Si "Clientes" está bajo `inventario`/`catalog`, lo mueve en memoria a un grupo Ventas.
 * - Si no aplica, devuelve los grupos tal cual (sin inyectar menú estático).
 */
export function ensureSalesNextToInventory(
  groups: NavGroup[],
  _t: TranslateFn,
  _options?: { platformPanelEnabled?: boolean },
): NavGroup[] {
  void _t;
  void _options;
  if (groups.some((g) => g.id === 'sales')) {
    return sortNavGroupsForMainBar(groups);
  }

  return sortNavGroupsForMainBar(groups);
}

