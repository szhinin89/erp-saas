import type { SessionMenuGroupDto, SessionMenuItemDto } from '../types/access';

export type NavItem = {
  to: string;
  label: string;
  icon?: string;
  /** Si está definido, el ítem solo se muestra si el rol del usuario está en la lista (p. ej. solo SuperAdmin). */
  roles?: string[];
  /** Clave de módulo contratado (catalog, accounting, saas, access). Si falta, no se filtra por suscripción. */
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

/** Orden fijo de la barra principal: Inventario y Ventas siempre uno al lado del otro. */
/** Orden barra: Inicio → Contabilidad → Inventario → Ventas → Compras → RRHH → Seguridad. */
const MAIN_NAV_GROUP_ORDER = [
  'home',
  'accounting',
  'catalog',
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

function mapSessionMenuItem(it: SessionMenuItemDto, t: TranslateFn): NavItem {
  const children =
    it.children && it.children.length > 0 ? it.children.map((c) => mapSessionMenuItem(c, t)) : undefined;
  return {
    to: it.routePath,
    label: t(it.labelKey),
    moduleKey: it.moduleKey ?? undefined,
    permissionKey: it.permissionKey ?? undefined,
    permissionKeysAny: it.permissionKeysAny?.length ? it.permissionKeysAny : undefined,
    ...(children?.length ? { children } : {}),
  };
}

/** Convierte la respuesta del API en el mismo shape que `buildNavGroups` (incl. filtro panel superadmin). */
export function mapSessionMenuToNavGroups(
  dto: SessionMenuGroupDto[],
  t: TranslateFn,
  options?: { superAdminPanelEnabled?: boolean },
): NavGroup[] {
  const superOn = options?.superAdminPanelEnabled ?? true;
  const mapped = dto
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

function collectNavTos(items: NavItem[]): string[] {
  const out: string[] = [];
  for (const it of items) {
    out.push(it.to);
    if (it.children?.length) out.push(...collectNavTos(it.children));
  }
  return out;
}

/** Enlaces del panel SuperAdmin que no vienen del API de sesión; se fusionan en el grupo Inicio. */
export function getSuperAdminPanelNavExtras(
  t: TranslateFn,
  options?: { superAdminPanelEnabled?: boolean },
): NavItem[] {
  const superAdminOn = options?.superAdminPanelEnabled ?? true;
  if (!superAdminOn) return [];
  return [
    { to: '/superadmin', label: t('app.nav.superadmin'), roles: ['SuperAdmin'] },
    { to: '/superadmin/plans', label: t('app.nav.superadmin.plans'), roles: ['SuperAdmin'] },
    { to: '/companies', label: t('app.nav.companies'), roles: ['SuperAdmin'] },
    { to: '/superadmin/forms', label: t('app.nav.superadmin.forms'), roles: ['SuperAdmin'] },
    { to: '/superadmin/instance-quota', label: t('app.nav.superadmin.instanceQuota'), roles: ['SuperAdmin'] },
    { to: '/superadmin/navigation-menu', label: t('app.nav.superadmin.navigationMenu'), roles: ['SuperAdmin'] },
  ];
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
      id: 'catalog',
      label: t('app.nav.group.catalog'),
      icon: '📦',
      moduleKey: 'catalog',
      sortOrder: defaultBarRank('catalog') * 10,
      items: [
        { to: '/products', label: t('app.nav.products'), permissionKey: 'catalog.products.view' },
        { to: '/catalog/brands', label: t('app.nav.catalog.brands'), permissionKey: 'catalog.brands.view' },
        { to: '/catalog/product-types', label: t('app.nav.catalog.productTypes'), permissionKey: 'catalog.productTypes.view' },
        { to: '/catalog/units', label: t('app.nav.catalog.units'), permissionKey: 'catalog.units.view' },
        { to: '/catalog/tax-rates', label: t('app.nav.catalog.taxRates'), permissionKey: 'catalog.taxRates.view' },
        { to: '/catalog/tariffs', label: t('app.nav.catalog.tariffs'), permissionKey: 'catalog.tariffs.view' },
        { to: '/catalog/structure', label: t('app.nav.catalog.structure'), permissionKey: 'catalog.categories.view' },
      ],
    },
    {
      id: 'sales',
      label: t('app.nav.group.sales'),
      icon: '🛒',
      moduleKey: 'catalog',
      sortOrder: defaultBarRank('sales') * 10,
      items: [{ to: '/catalog/customers', label: t('app.nav.catalog.customers'), permissionKey: 'catalog.customers.view' }],
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
          permissionKeysAny: ['accounting.accounts.view', 'accounting.journal.view'],
        },
      ],
    },
    {
      id: 'purchases',
      label: t('app.nav.group.purchases'),
      icon: '📥',
      sortOrder: defaultBarRank('purchases') * 10,
      items: [{ to: '/compras', label: t('app.nav.group.purchases') }],
    },
    {
      id: 'hr',
      label: t('app.nav.group.hr'),
      icon: '👥',
      sortOrder: defaultBarRank('hr') * 10,
      items: [{ to: '/rrhh', label: t('app.nav.group.hr') }],
    },
    {
      id: 'security',
      label: t('app.nav.group.security'),
      icon: '🛡️',
      sortOrder: defaultBarRank('security') * 10,
      items: [
        { to: '/access', label: t('app.nav.access'), roles: ['Admin', 'SuperAdmin'] },
        { to: '/profiles', label: t('app.nav.profiles'), roles: ['Admin', 'SuperAdmin'] },
        {
          to: '/saas/branches',
          label: t('app.nav.branches'),
          moduleKey: 'saas',
          permissionKey: 'saas.branches.view',
          roles: ['Admin', 'SuperAdmin'],
        },
        { to: '/security', label: t('app.nav.security'), roles: ['SuperAdmin'] },
      ],
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
 * - Si "Clientes" sigue bajo `catalog`, lo movemos en memoria a un grupo Ventas.
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

  const catalog = groups.find((g) => g.id === 'catalog');
  if (catalog) {
    const custIdx = catalog.items.findIndex((it) => it.to === '/catalog/customers');
    if (custIdx >= 0) {
      const customerItem = catalog.items[custIdx]!;
      const newCatalogItems = catalog.items.filter((_, i) => i !== custIdx);
      const newCatalog: NavGroup = { ...catalog, items: newCatalogItems };
      const salesGroup: NavGroup = {
        id: 'sales',
        label: t('app.nav.group.sales'),
        icon: '🛒',
        moduleKey: catalog.moduleKey ?? 'catalog',
        sortOrder: defaultBarRank('sales') * 10,
        items: [customerItem],
      };
      const withoutCatalog = groups.filter((g) => g.id !== 'catalog');
      const next =
        newCatalog.items.length > 0
          ? [...withoutCatalog, newCatalog, salesGroup]
          : [...withoutCatalog, salesGroup];
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
const GROUPS_FILL_FROM_STATIC: readonly string[] = ['purchases', 'hr'];

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

