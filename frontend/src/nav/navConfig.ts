import type { SessionMenuGroupDto } from '../types/access';

export type NavItem = {
  to: string;
  label: string;
  icon?: string;
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
};

export type TranslateFn = (key: string) => string;

/** Convierte la respuesta del API en el mismo shape que `buildNavGroups` (incl. filtro panel superadmin). */
export function mapSessionMenuToNavGroups(
  dto: SessionMenuGroupDto[],
  t: TranslateFn,
  options?: { superAdminPanelEnabled?: boolean },
): NavGroup[] {
  const superOn = options?.superAdminPanelEnabled ?? true;
  return dto
    .filter((g) => !g.requireSuperAdminPanel || superOn)
    .map((g) => ({
      id: g.code,
      label: t(g.labelKey),
      icon: g.icon,
      moduleKey: g.moduleKey ?? undefined,
      roles: g.roles ?? undefined,
      items: g.items.map((it) => ({
        to: it.routePath,
        label: t(it.labelKey),
        moduleKey: it.moduleKey ?? undefined,
        permissionKey: it.permissionKey ?? undefined,
        permissionKeysAny: it.permissionKeysAny?.length ? it.permissionKeysAny : undefined,
      })),
    }));
}

function collectNavTos(items: NavItem[]): string[] {
  const out: string[] = [];
  for (const it of items) {
    out.push(it.to);
    if (it.children?.length) out.push(...collectNavTos(it.children));
  }
  return out;
}

export function buildNavGroups(
  t: TranslateFn,
  options?: { superAdminPanelEnabled?: boolean },
): NavGroup[] {
  const groups: NavGroup[] = [
    {
      id: 'home',
      label: t('app.nav.group.home'),
      icon: '⊞',
      items: [{ to: '/dashboard', label: t('app.nav.dashboard') }],
    },
    {
      id: 'catalog',
      label: t('app.nav.group.catalog'),
      icon: '📦',
      moduleKey: 'catalog',
      items: [
        { to: '/products', label: t('app.nav.products'), permissionKey: 'catalog.products.view' },
        { to: '/catalog/customers', label: t('app.nav.catalog.customers'), permissionKey: 'catalog.customers.view' },
        { to: '/catalog/brands', label: t('app.nav.catalog.brands'), permissionKey: 'catalog.brands.view' },
        { to: '/catalog/product-types', label: t('app.nav.catalog.productTypes'), permissionKey: 'catalog.productTypes.view' },
        { to: '/catalog/units', label: t('app.nav.catalog.units'), permissionKey: 'catalog.units.view' },
        { to: '/catalog/tax-rates', label: t('app.nav.catalog.taxRates'), permissionKey: 'catalog.taxRates.view' },
        { to: '/catalog/tariffs', label: t('app.nav.catalog.tariffs'), permissionKey: 'catalog.tariffs.view' },
        { to: '/catalog/structure', label: t('app.nav.catalog.structure'), permissionKey: 'catalog.categories.view' },
      ],
    },
    {
      id: 'accounting',
      label: t('app.nav.group.accounting'),
      icon: '📒',
      moduleKey: 'accounting',
      items: [
        {
          to: '/accounting',
          label: t('app.nav.accounting'),
          permissionKeysAny: ['accounting.accounts.view', 'accounting.journal.view'],
        },
      ],
    },
    {
      id: 'access',
      label: t('app.nav.group.access'),
      icon: '🔐',
      roles: ['Admin', 'SuperAdmin'],
      items: [
        { to: '/access', label: t('app.nav.access') },
        { to: '/profiles', label: t('app.nav.profiles') },
        {
          to: '/saas/branches',
          label: t('app.nav.branches'),
          moduleKey: 'saas',
          permissionKey: 'saas.branches.view',
        },
      ],
    },
    {
      id: 'security',
      label: t('app.nav.group.security'),
      icon: '🛡️',
      roles: ['SuperAdmin'],
      items: [{ to: '/security', label: t('app.nav.security') }],
    },
    {
      id: 'saas',
      label: t('app.nav.group.saas'),
      icon: '🏢',
      roles: ['SuperAdmin'],
      items: [
        { to: '/companies', label: t('app.nav.companies') },
        { to: '/superadmin', label: t('app.nav.superadmin') },
        { to: '/superadmin/instance-quota', label: t('app.nav.superadmin.instanceQuota') },
        { to: '/superadmin/forms', label: t('app.nav.superadmin.forms') },
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

  const superAdminOn = options?.superAdminPanelEnabled ?? true;
  if (!superAdminOn) {
    return groups.filter((g) => g.id !== 'security' && g.id !== 'saas');
  }

  return groups;
}

