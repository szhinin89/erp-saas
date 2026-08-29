import type { NavMenuGroupDto, NavMenuItemDto } from "../types/access";

export type NavItem = {
  to: string;
  label: string;
  /** Descripción breve de uso, mostrada en el menú principal. */
  description?: string;
  icon?: string;
  children?: NavItem[];
  /**
   * Identificador estable del backend (`NavMenuItemDto.id`), obligatorio para todo ítem
   * proveniente del árbol de navegación. Es la única clave válida para identidad persistida
   * (favoritos, expand/collapse): no cambia con el idioma, el label ni la ruta.
   * Los pocos `NavItem` sintéticos creados en el frontend (ver `ensureTenantHomeOverview`)
   * deben declarar un `id` fijo y único (prefijo no colisionable con ids del backend).
   */
  id: string;
};

/** True si la ruta actual coincide exactamente con `to` o con un descendiente directo de esa ruta. */
export function isActivePath(to: string, pathname: string): boolean {
  return pathname === to || (to.length > 1 && pathname.startsWith(`${to}/`));
}

/** True si la ruta actual coincide con este ítem o con algún descendiente (menú anidado). */
export function navSubtreeMatchesPath(it: NavItem, pathname: string): boolean {
  if (it.to && isActivePath(it.to, pathname)) return true;
  return it.children?.some((c) => navSubtreeMatchesPath(c, pathname)) ?? false;
}

/** Clave estable para persistir estado de UI (expand/collapse) de un NavItem. */
export function navItemStorageKey(it: NavItem): string {
  return it.id;
}

export type NavGroup = {
  id: string;
  label: string;
  icon: string;
  items: NavItem[];
  sortOrder?: number;
};

export type TranslateFn = (key: string) => string;

/**
 * Descripciones de navegación centralizadas por ruta. Las rutas siguen viniendo
 * del menú filtrado por permisos del backend; este catálogo solo aporta contexto
 * visual al App Launcher.
 */
const MENU_DESCRIPTION_BY_ROUTE_PREFIX: ReadonlyArray<readonly [string, string]> = [
  ["/dashboard", "Indicadores y actividad reciente"],
  ["/masterdata/customers", "Gestión de clientes y saldos"],
  ["/masterdata/suppliers", "Gestión de proveedores y compras"],
  ["/masterdata", "Catálogos base del sistema"],
  ["/inventory/items", "Catálogo, precios e impuestos"],
  ["/inventory/adjustments", "Ingresos y egresos manuales de stock"],
  ["/inventory/adjustment-reasons", "Motivos configurables para ajustes de inventario"],
  ["/inventory", "Stock, kardex y movimientos"],
  ["/purchases", "Facturas, recepción y proveedores"],
  ["/payables", "Cuentas por pagar de Compras y Gastos"],
  ["/supplier-payments", "Pagos registrados a proveedores"],
  ["/expenses/documents", "Registro de gastos por proveedor"],
  ["/expenses/categories", "Catalogo jerarquico de gastos"],
  ["/expenses", "Documentos y catalogo de gastos"],
  ["/sales", "Facturación, clientes y cobranzas"],
  ["/finance", "Caja, pagos y cuentas por cobrar"],
  ["/accounting", "Asientos, plan de cuentas y reportes contables"],
  ["/logistica", "Sucursales, bodegas y transporte"],
  ["/settings", "Parámetros de empresa y sistema"],
  ["/configuracion", "Parámetros de empresa y sistema"],
  ["/access", "Usuarios, roles y permisos"],
  ["/security", "Usuarios, roles y permisos"],
  ["/admin", "Administración y control operativo"],
  ["/reportes", "Consultas operativas y análisis"],
];

function menuItemDescription(path: string): string {
  const match = MENU_DESCRIPTION_BY_ROUTE_PREFIX.find(
    ([prefix]) => path === prefix || path.startsWith(`${prefix}/`),
  );
  return match?.[1] ?? "Gestión y consulta operativa";
}

/** Returns true when backend sends a plan-custom menu (no static groups injected). */
export function isPlanBuilderSessionMenu(
  rows: NavMenuGroupDto[] | undefined,
): boolean {
  return !!rows?.some((g) => g.code === "plan-custom");
}

/**
 * Fixed bar order: fallback when backend doesn't provide sortOrder.
 *
 * Deuda legacy: esta lista (junto con `flattenAccessIntoSecurity`, `flattenSaaSIntoHome`,
 * `expandPlanCustomRootsToBarGroups` y el branch `home` de `ensureTenantHomeOverview`)
 * fija el orden/posicionamiento de **grupos existentes** (`home`, `access`, `security`,
 * `saas`, `plan-custom`, etc.) por compatibilidad. Un módulo nuevo (p. ej. `crm`, `hr`,
 * `production`) NO necesita registrarse aquí: cae a `defaultBarRank` (orden 1000,
 * desempate alfabético por `id`) y se renderiza sin pasos adicionales.
 */
const MAIN_NAV_GROUP_ORDER = [
  "home",
  "customers",
  "suppliers",
  "products",
  "inventory",
  "sales",
  "accounting",
  "settings",
  "admin",
  "inventario",
  "configuracion",
  "hr",
  "security",
] as const;

function defaultBarRank(id: string): number {
  const i = (MAIN_NAV_GROUP_ORDER as readonly string[]).indexOf(id);
  return i >= 0 ? i : 1000;
}

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

/** Legacy BD route aliases → defined routes in App.tsx. */
const MENU_ROUTE_ALIASES: Record<string, string> = {
  "/logistica/bodegas": "/inventory/warehouses",
};

export function normalizeMenuRoutePath(path: string): string {
  const raw = path.trim();
  if (!raw || raw === "#" || raw.toLowerCase().startsWith("javascript:"))
    return "";
  if (/^[a-z][a-z0-9+.-]*:/i.test(raw)) return raw;

  const internal = raw.startsWith("/") ? raw : `/${raw}`;
  try {
    const u = new URL(`http://dummy.local${internal}`);
    let pathname = u.pathname;
    if (pathname.length > 1 && pathname.endsWith("/"))
      pathname = pathname.slice(0, -1);
    pathname = MENU_ROUTE_ALIASES[pathname] ?? pathname;
    return `${pathname}${u.search}${u.hash}`;
  } catch {
    return internal.startsWith("/") ? internal : `/${internal}`;
  }
}

function mapSessionMenuItem(it: NavMenuItemDto, t: TranslateFn): NavItem {
  const normalized = normalizeMenuRoutePath((it.routePath ?? "").trim());
  const dl = it.displayLabel?.trim();
  const children =
    it.children && it.children.length > 0
      ? it.children.map((c) => mapSessionMenuItem(c, t))
      : undefined;

  return {
    to: normalized,
    label: dl && dl.length > 0 ? dl : t(it.labelKey),
    description: menuItemDescription(normalized),
    id: it.id,
    ...(children?.length ? { children } : {}),
  };
}

/**
 * Converts backend NavMenuGroupDto[] to NavGroup[]. Backend already filtered by permissions.
 *
 * NAV-HIERARCHY-UNIFY-01: la categorización (Módulo > Categoría > Pantalla) vive 100% en
 * backend (KernelRegistry / `[Module]`+`[NavItem]`, ver `backend/src/ERP.Domain/Kernel/Modules/`)
 * — el árbol de `it.children` que llega en `g.items` ya trae las categorías anidadas. Este
 * mapeo es un pass-through directo, sin ninguna regla de agrupación en frontend.
 */
export function mapSessionMenuToNavGroups(
  dto: NavMenuGroupDto[],
  t: TranslateFn,
): NavGroup[] {
  const mapped = dto.map((g) => ({
    id: g.code,
    label: t(g.labelKey),
    icon: g.icon,
    items: g.items.map((it) => mapSessionMenuItem(it, t)),
  }));
  return sortNavGroupsForMainBar(mapped);
}

/**
 * plan-custom horizontal bar: each root item becomes its own bar pill.
 * Legacy shim ligado al grupo `plan-custom` — irrelevante para módulos nuevos.
 */
export function expandPlanCustomRootsToBarGroups(
  groups: NavGroup[],
): NavGroup[] {
  const idx = groups.findIndex((g) => g.id === "plan-custom");
  if (idx < 0) return groups;

  const plan = groups[idx]!;
  const roots = plan.items;
  if (roots.length <= 1) return groups;

  const rest = groups.filter((_, i) => i !== idx);
  const baseOrder = plan.sortOrder ?? 0;
  const slug = (label: string, i: number) => {
    const s = label
      .normalize("NFKD")
      .replace(/[̀-ͯ]/g, "")
      .replace(/\s+/g, "-")
      .replace(/[^a-zA-Z0-9_-]/g, "")
      .slice(0, 24)
      .toLowerCase();
    return s || `r${i}`;
  };

  const expanded: NavGroup[] = roots.map((root, i) => ({
    id: `plan-custom__${i}-${slug(root.label, i)}`,
    label: root.label,
    icon: root.icon ?? plan.icon ?? "•",
    sortOrder: baseOrder * 100 + i,
    items: root.children?.length ? root.children : [root],
  }));

  return sortNavGroupsForMainBar([...rest, ...expanded]);
}

/**
 * Legacy: merges `access` group items into `security` group, removes access pill.
 * Shim ligado a los grupos `access`/`security` — irrelevante para módulos nuevos.
 */
export function flattenAccessIntoSecurity(groups: NavGroup[]): NavGroup[] {
  const access = groups.find((g) => g.id === "access");
  if (!access) return groups;

  const security = groups.find((g) => g.id === "security");
  const rest = groups.filter((g) => g.id !== "access");
  if (!security) return rest;

  const existingInSecurity = new Set(security.items.map((it) => it.to));
  const liftedDeduped = access.items.filter(
    (it) => !existingInSecurity.has(it.to),
  );
  const newSecurity: NavGroup = {
    ...security,
    items: [...liftedDeduped, ...security.items],
  };
  return rest.map((g) => (g.id === "security" ? newSecurity : g));
}

/** Ensures ERP dashboard is the first item in the home group. */
export function ensureTenantHomeOverview(
  groups: NavGroup[],
  t: TranslateFn,
): NavGroup[] {
  // Id sintético fijo (no proviene del backend): prefijo `synthetic-` evita colisión con ids reales.
  const erpItem: NavItem = {
    id: "synthetic-home-erp-dashboard",
    to: "/dashboard",
    label: t("app.nav.erpDashboard"),
    description: menuItemDescription("/dashboard"),
  };

  const homeIdx = groups.findIndex((g) => g.id === "home");
  if (homeIdx < 0) {
    return sortNavGroupsForMainBar([
      {
        id: "home",
        label: t("app.nav.group.home"),
        icon: "⊞",
        sortOrder: defaultBarRank("home") * 10,
        items: [erpItem],
      },
      ...groups,
    ]);
  }

  const home = groups[homeIdx]!;
  const withoutSaas = home.items.filter((it) => it.to !== "/saas/overview");
  const hasErp = withoutSaas.some((it) => it.to === "/dashboard");
  const nextItems = hasErp ? withoutSaas : [erpItem, ...withoutSaas];
  const next = [...groups];
  next[homeIdx] = { ...home, items: nextItems };
  return next;
}

/**
 * Legacy: moves `saas` group items into home group, removes saas pill.
 * Shim ligado a los grupos `saas`/`home` — irrelevante para módulos nuevos.
 */
export function flattenSaaSIntoHome(groups: NavGroup[]): NavGroup[] {
  const saas = groups.find((g) => g.id === "saas");
  if (!saas) return groups;

  const rest = groups.filter((g) => g.id !== "saas");
  const home = rest.find((g) => g.id === "home");
  if (!home) return rest;

  const existingTo = new Set(home.items.map((it) => it.to));
  const liftedDeduped = saas.items.filter((it) => !existingTo.has(it.to));
  const newHome: NavGroup = {
    ...home,
    items: [...home.items, ...liftedDeduped],
  };
  return rest.map((g) => (g.id === "home" ? newHome : g));
}
