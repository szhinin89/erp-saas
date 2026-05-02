export type NavItem = { to: string; label: string; icon?: string; permissionKey?: string; children?: NavItem[] };
export type NavGroup = { id: string; label: string; icon: string; items: NavItem[]; roles?: string[] };

export type TranslateFn = (key: string) => string;

function collectNavTos(items: NavItem[]): string[] {
  const out: string[] = [];
  for (const it of items) {
    out.push(it.to);
    if (it.children?.length) out.push(...collectNavTos(it.children));
  }
  return out;
}

export function buildNavGroups(t: TranslateFn): NavGroup[] {
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
      id: 'accounting',
      label: t('app.nav.group.accounting'),
      icon: '📒',
      items: [{ to: '/accounting', label: t('app.nav.accounting') }],
    },
    {
      id: 'access',
      label: t('app.nav.group.access'),
      icon: '🔐',
      roles: ['Admin', 'SuperAdmin'],
      items: [
        { to: '/access', label: t('app.nav.access') },
        { to: '/profiles', label: t('app.nav.profiles') },
        { to: '/saas/branches', label: t('app.nav.branches'), permissionKey: 'saas.branches.view' },
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

  return groups;
}

