import type { FuncionalidadArbolDto, CommercialPlanAdmin } from '../../modules/superadmin/api/superAdminService';
import type { EditorMenuItem } from '../menu-builder/menuBuilderTypes';
import type { MenuPreviewLayout } from '../menu-builder/MenuPreview';

export type CatalogSearchField = 'all' | 'name' | 'perm' | 'route';
export type CatalogNodeType = 'all' | 'folders' | 'forms';

export type CatalogFilterOptions = {
  query: string;
  field: CatalogSearchField;
  nodeType: CatalogNodeType;
  onlyWithRoute: boolean;
};

export function filterFuncionalidadesArbol(rows: FuncionalidadArbolDto[], options: CatalogFilterOptions): FuncionalidadArbolDto[] {
  const needle = options.query.trim().toLowerCase();
  const shouldFilterText = needle.length > 0;
  const shouldFilterRoute = options.onlyWithRoute;
  const shouldFilterType = options.nodeType !== 'all';

  if (!shouldFilterText && !shouldFilterRoute && !shouldFilterType) return rows;

  const textMatches = (n: FuncionalidadArbolDto): boolean => {
    if (!shouldFilterText) return true;
    const name = (n.name ?? '').toLowerCase();
    const perm = (n.permission ?? '').toLowerCase();
    const route = (n.path ?? '').toLowerCase();
    switch (options.field) {
      case 'name':
        return name.includes(needle);
      case 'perm':
        return perm.includes(needle);
      case 'route':
        return route.includes(needle);
      case 'all':
      default:
        return name.includes(needle) || perm.includes(needle) || route.includes(needle);
    }
  };

  const typeMatches = (n: FuncionalidadArbolDto): boolean => {
    if (!shouldFilterType) return true;
    const isFolder = (n.children ?? []).length > 0;
    return options.nodeType === 'folders' ? isFolder : !isFolder;
  };

  const routeMatches = (n: FuncionalidadArbolDto): boolean => {
    if (!shouldFilterRoute) return true;
    return Boolean((n.path ?? '').trim());
  };

  const walk = (nodes: FuncionalidadArbolDto[]): FuncionalidadArbolDto[] => {
    const out: FuncionalidadArbolDto[] = [];
    for (const n of nodes) {
      const children = walk(n.children ?? []);
      const hay = (textMatches(n) && typeMatches(n) && routeMatches(n)) || children.length > 0;
      if (hay) out.push({ ...n, children });
    }
    return out;
  };
  return walk(rows);
}

export function planEmoji(code: string): string {
  const c = (code ?? '').toUpperCase();
  if (c.includes('START')) return '⭐';
  if (c.includes('BUS')) return '💼';
  if (c.includes('PRO')) return '🚀';
  if (c.includes('ENTER')) return '🏢';
  return '📋';
}

export type SubMode = 'plan' | 'subscriber';

export type EditorMainTab = 'json' | 'visual';

export const MENU_BUILDER_SCHEMA_VERSION = 171;
export const CRM_PLAN_ACTIVE_STORAGE_KEY = 'crmPlanActiveNodeIds';
export const CRM_TREE_STORAGE_KEY = 'crmMenuTree';
export const CRM_AUDIT_STORAGE_KEY = 'crmAuditLog';

/* ── Audit helpers ───────────────────────────────────────── */
export function parseAuditLine(line: string): { timestamp: string; user: string; action: string; details: string } {
  const match = /^\[([^\]]+)\]\s+(.+)$/.exec(line);
  const details = match ? match[2] : line;
  const action = deriveAuditAction(details);
  const user = action === 'SYNC' ? 'system_worker' : 'admin_super';
  return { timestamp: match ? match[1] : '—', user, action, details };
}

export function deriveAuditAction(details: string): string {
  const d = details.toLowerCase();
  if (d.includes('guardado') || d.includes('guardó') || d.includes('snapshot')) return 'SAVE';
  if (d.includes('activado')) return 'TOGGLE';
  if (d.includes('desactivado')) return 'TOGGLE';
  if (d.includes('exportad')) return 'EXPORT';
  if (d.includes('importad')) return 'IMPORT';
  if (d.includes('heredó')) return 'INHERIT';
  if (d.includes('reseteado') || d.includes('reset')) return 'RESET';
  if (d.includes('sincroniz') || d.includes('sync')) return 'SYNC';
  if (d.includes('eliminado') || d.includes('borrado')) return 'DELETE';
  return 'UPDATE';
}

export function auditActionBadge(action: string): string {
  switch (action) {
    case 'SAVE':    return 'green';
    case 'TOGGLE':  return 'orange';
    case 'RESET':
    case 'DELETE':  return 'red';
    case 'EXPORT':
    case 'IMPORT':
    case 'SYNC':    return 'gray';
    default:        return 'blue';
  }
}

export type CrmLocalPlan = {
  id: string;
  code: string;
  name: string;
  priceMonthly: number;
  priceYearly: number;
  description: string;
  layout: MenuPreviewLayout;
  highlight: boolean;
  isPubliclyVisible: boolean;
  sortOrder: number;
};

export const DEFAULT_CRM_TREE_SEED: EditorMenuItem[] = [
  {
    uid: 'seed-inventario',
    nombre: 'Inventario',
    icono: '📦',
    ruta: '',
    permiso: '',
    children: [
      { uid: 'seed-inventario-productos', nombre: 'Productos', icono: '📦', ruta: '/inventario/productos', permiso: 'inventory.products.view', children: [] },
      { uid: 'seed-inventario-kardex', nombre: 'Kardex', icono: '📊', ruta: '/inventario/kardex', permiso: 'inventory.kardex.view', children: [] },
    ],
  },
  {
    uid: 'seed-ventas',
    nombre: 'Ventas',
    icono: '🧾',
    ruta: '',
    permiso: '',
    children: [
      { uid: 'seed-ventas-facturas', nombre: 'Facturas', icono: '🧾', ruta: '/ventas/facturas', permiso: 'sales.invoices.view', children: [] },
      { uid: 'seed-ventas-clientes', nombre: 'Clientes', icono: '👥', ruta: '/ventas/clientes', permiso: 'sales.customers.view', children: [] },
    ],
  },
  {
    uid: 'seed-compras',
    nombre: 'Compras',
    icono: '🛒',
    ruta: '',
    permiso: '',
    children: [
      { uid: 'seed-compras-ordenes', nombre: 'Órdenes de compra', icono: '📑', ruta: '/compras/ordenes', permiso: 'purchases.orders.view', children: [] },
      { uid: 'seed-compras-proveedores', nombre: 'Proveedores', icono: '🏬', ruta: '/compras/proveedores', permiso: 'purchases.suppliers.view', children: [] },
    ],
  },
];

export function cloneDefaultCrmTreeSeed(): EditorMenuItem[] {
  return JSON.parse(JSON.stringify(DEFAULT_CRM_TREE_SEED)) as EditorMenuItem[];
}

export function mapCommercialPlanAdminToCrmPlan(plan: CommercialPlanAdmin): CrmLocalPlan {
  const code = (plan.code ?? '').trim().toUpperCase();
  const name = (plan.name ?? '').trim() || code || 'PLAN';
  const cycle = (plan.billingCycle ?? '').trim().toLowerCase();
  const rawAmount = Number(plan.priceAmount ?? 0);
  const safeAmount = Number.isFinite(rawAmount) && rawAmount > 0 ? rawAmount : 0;
  const isYearly = cycle === 'yearly' || cycle === 'annual' || cycle === 'anual';
  const monthly = isYearly ? safeAmount / 12 : safeAmount;
  const yearly = isYearly ? safeAmount : monthly * 12 * 0.8;
  const layout = (plan.menuSidebarLayout ?? '').trim().toLowerCase() === 'vertical' ? 'vertical' : 'horizontal';

  return {
    id: plan.id,
    code,
    name,
    priceMonthly: Math.round(monthly),
    priceYearly: Math.round(yearly),
    description: `${name} (${code || 'N/A'})`,
    layout,
    highlight: !!plan.isRecommended,
    isPubliclyVisible: !!plan.isPubliclyVisible,
    sortOrder: plan.sortOrder ?? 0,
  };
}

export function formatMoney(amount: number, currency: string, locale: string): string {
  try {
    return new Intl.NumberFormat(locale, { style: 'currency', currency: currency || 'USD', maximumFractionDigits: 0 }).format(amount);
  } catch {
    return `${currency} ${amount.toFixed(0)}`;
  }
}

export function makeExportFileName(prefix: string): string {
  const stamp = new Date().toISOString().replace(/[:.]/g, '-');
  return `${prefix}-${stamp}.json`;
}

export function downloadJsonFile(filename: string, payload: unknown): void {
  const blob = new Blob([JSON.stringify(payload, null, 2)], { type: 'application/json;charset=utf-8' });
  const url = window.URL.createObjectURL(blob);
  const a = window.document.createElement('a');
  a.href = url;
  a.download = filename;
  window.document.body.appendChild(a);
  a.click();
  a.remove();
  window.URL.revokeObjectURL(url);
}
