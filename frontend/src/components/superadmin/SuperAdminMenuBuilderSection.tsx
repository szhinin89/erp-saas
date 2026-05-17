import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import { Card } from '../ui';
import { ZHBtn, ZHField } from '../zh/ZHForm';
import { ZHConfirmModal } from '../zh/ZHConfirmModal';
import { ZHCardSection, ZHGridRow, ZHInlineRowRight } from '../zh/ZHLayout';
import { ZHPageNotice } from '../zh/ZHPageNotice';
import { useI18n } from '../../i18n/i18n';
import { MenuBuilder, type MenuBuilderViewMode } from '../menu-builder/MenuBuilder';
import type { MenuPreviewLayout } from '../menu-builder/MenuPreview';
import {
  buildFuncionalidadMaps,
  editorToMenuItems,
  normalizeParsedMenuGroups,
  readPlanCustomMenuBarLayout,
  serializeEditorTreeToMenuJson,
  sessionGroupsToEditorTree,
  validateSessionMenuGroups,
  type EditorMenuItem,
  type MenuItem,
} from '../menu-builder/menuBuilderTypes';
import { useEditorTreeHistory } from '../menu-builder/withHistory';
import {
  superAdminService,
  type FuncionalidadArbolDto,
  type SaasPlanAdmin,
  type SuperAdminTenant,
} from '../../services/superAdminService';
import type { SessionMenuGroupDto } from '../../types/access';
import { formatApiRequestError } from '../../modules/lib/apiError';
import { adminNavigationToSessionMenu } from '../../modules/superadmin/adminNavigationToSessionMenu';
import {
  parseImportedCrmWorkspace,
  normalizePlanActiveById,
  resolveInheritedActiveNodeIds,
  type CrmWorkspaceExportPayload,
} from './crmPlanIntegrity';
import '../menu-builder/menu-builder.css';
import './menu-plan-composer.css';

type CatalogSearchField = 'all' | 'name' | 'perm' | 'route';
type CatalogNodeType = 'all' | 'folders' | 'forms';

type CatalogFilterOptions = {
  query: string;
  field: CatalogSearchField;
  nodeType: CatalogNodeType;
  onlyWithRoute: boolean;
};

function filterFuncionalidadesArbol(rows: FuncionalidadArbolDto[], options: CatalogFilterOptions): FuncionalidadArbolDto[] {
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

function planEmoji(code: string): string {
  const c = (code ?? '').toUpperCase();
  if (c.includes('START')) return '⭐';
  if (c.includes('BUS')) return '💼';
  if (c.includes('PRO')) return '🚀';
  if (c.includes('ENTER')) return '🏢';
  return '📋';
}

type SubMode = 'plan' | 'tenant';

type EditorMainTab = 'json' | 'visual';

const MENU_BUILDER_SCHEMA_VERSION = 171;
const CRM_PLAN_ACTIVE_STORAGE_KEY = 'crmPlanActiveNodeIds';
const CRM_TREE_STORAGE_KEY = 'crmMenuTree';
const CRM_AUDIT_STORAGE_KEY = 'crmAuditLog';

/* ── Audit helpers ───────────────────────────────────────── */
function parseAuditLine(line: string): { timestamp: string; action: string; details: string } {
  const match = /^\[([^\]]+)\]\s+(.+)$/.exec(line);
  const details = match ? match[2] : line;
  return { timestamp: match ? match[1] : '—', action: deriveAuditAction(details), details };
}

function deriveAuditAction(details: string): string {
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

function auditActionBadge(action: string): string {
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

type CrmLocalPlan = {
  id: string;
  code: string;
  name: string;
  priceMonthly: number;
  priceYearly: number;
  description: string;
  layout: MenuPreviewLayout;
  highlight: boolean;
};

const DEFAULT_CRM_TREE_SEED: EditorMenuItem[] = [
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

function cloneDefaultCrmTreeSeed(): EditorMenuItem[] {
  return JSON.parse(JSON.stringify(DEFAULT_CRM_TREE_SEED)) as EditorMenuItem[];
}

function mapSaasPlanAdminToCrmPlan(plan: SaasPlanAdmin): CrmLocalPlan {
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
  };
}

function formatMoney(amount: number, currency: string, locale: string): string {
  try {
    return new Intl.NumberFormat(locale, { style: 'currency', currency: currency || 'USD', maximumFractionDigits: 0 }).format(amount);
  } catch {
    return `${currency} ${amount.toFixed(0)}`;
  }
}

function makeExportFileName(prefix: string): string {
  const stamp = new Date().toISOString().replace(/[:.]/g, '-');
  return `${prefix}-${stamp}.json`;
}

function downloadJsonFile(filename: string, payload: unknown): void {
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

export type SuperAdminMenuBuilderSectionProps = {
  /** Vista única 3 columnas (árbol | empresa | formularios) para el hub menú/planes. */
  crmWorkspace?: boolean;
};

export { adminNavigationToSessionMenu } from '../../modules/superadmin/adminNavigationToSessionMenu';

export function SuperAdminMenuBuilderSection({ crmWorkspace = false }: SuperAdminMenuBuilderSectionProps = {}) {
  const { t, locale } = useI18n();
  const [sub, setSub] = useState<SubMode>('plan');
  const [plans, setPlans] = useState<SaasPlanAdmin[]>([]);
  const [tenants, setTenants] = useState<SuperAdminTenant[]>([]);
  const [planId, setPlanId] = useState('');
  const [tenantId, setTenantId] = useState('');
  const [catalogSearch, setCatalogSearch] = useState('');
  const [catalogSearchField, setCatalogSearchField] = useState<CatalogSearchField>('all');
  const [catalogNodeType, setCatalogNodeType] = useState<CatalogNodeType>('all');
  const [catalogOnlyWithRoute, setCatalogOnlyWithRoute] = useState(false);
  const [wizardOpen, setWizardOpen] = useState(false);
  const [wizardStep, setWizardStep] = useState(0);
  const [auditLines, setAuditLines] = useState<string[]>(() => {
    if (typeof window === 'undefined') return [];
    try {
      const raw = window.localStorage.getItem(CRM_AUDIT_STORAGE_KEY);
      if (!raw) return [];
      const parsed = JSON.parse(raw) as unknown;
      return Array.isArray(parsed) ? parsed.filter((line): line is string => typeof line === 'string').slice(0, 100) : [];
    } catch {
      return [];
    }
  });
  const [json, setJson] = useState('[]');
  const [busy, setBusy] = useState(false);
  const [err, setErr] = useState('');
  const [tenantFlags, setTenantFlags] = useState<{
    hasCustomMenu: boolean;
    usedPlanMenu: boolean;
    usedGlobalFallback: boolean;
  } | null>(null);

  const [arbol, setArbol] = useState<FuncionalidadArbolDto[]>([]);
  const { byPerm } = useMemo(() => buildFuncionalidadMaps(arbol), [arbol]);
  const filteredArbol = useMemo(
    () =>
      filterFuncionalidadesArbol(arbol, {
        query: catalogSearch,
        field: catalogSearchField,
        nodeType: catalogNodeType,
        onlyWithRoute: catalogOnlyWithRoute,
      }),
    [arbol, catalogSearch, catalogSearchField, catalogNodeType, catalogOnlyWithRoute],
  );

  const [editorMainTab, setEditorMainTab] = useState<EditorMainTab>('visual');
  const {
    present: visualTree,
    reset: resetEditorTree,
    commit: commitEditorTree,
    undo: undoEditorTree,
    redo: redoEditorTree,
    canUndo: canUndoEditorTree,
    canRedo: canRedoEditorTree,
  } = useEditorTreeHistory();
  const [menuViewMode, setMenuViewMode] = useState<MenuBuilderViewMode>('split');
  const [previewLayout, setPreviewLayout] = useState<MenuPreviewLayout>('vertical');

  const [copySourcePlanId, setCopySourcePlanId] = useState('');
  const [copyMenu, setCopyMenu] = useState(true);
  const [savingAuto, setSavingAuto] = useState(false);
  const [resetPlanConfirmOpen, setResetPlanConfirmOpen] = useState(false);
  const [inheritPlanConfirmOpen, setInheritPlanConfirmOpen] = useState(false);
  const [showAnnual, setShowAnnual] = useState(false);
  const [newPlanModalOpen, setNewPlanModalOpen] = useState(false);
  const [newPlanName, setNewPlanName] = useState('');
  const [newPlanMonthly, setNewPlanMonthly] = useState('199');
  const [newPlanYearly, setNewPlanYearly] = useState('');
  const [newPlanDescription, setNewPlanDescription] = useState('');
  const [newPlanInheritOnCreate, setNewPlanInheritOnCreate] = useState(false);
  const [newPlanInheritSourcePlanId, setNewPlanInheritSourcePlanId] = useState('');
  const importWorkspaceInputRef = useRef<HTMLInputElement | null>(null);
  const crmPlans = useMemo(
    () => plans.map(mapSaasPlanAdminToCrmPlan),
    [plans],
  );
  const [planActiveById, setPlanActiveById] = useState<Record<string, string[]>>(() => {
    if (typeof window === 'undefined') return {};
    try {
      const raw = window.localStorage.getItem(CRM_PLAN_ACTIVE_STORAGE_KEY);
      if (!raw) return {};
      const parsed = JSON.parse(raw) as Record<string, string[]>;
      return parsed && typeof parsed === 'object' ? parsed : {};
    } catch {
      return {};
    }
  });
  const hydratingPlanRef = useRef(false);
  const planSwitchClock = useRef(0);

  const visualLabelKey = t('superadmin.menuBuilder.visualGroupLabel');
  const visualTreeIds = useMemo(() => {
    const all: string[] = [];
    const walk = (nodes: EditorMenuItem[]) => {
      for (const n of nodes) {
        all.push(n.uid);
        walk(n.children);
      }
    };
    walk(visualTree);
    return all;
  }, [visualTree]);
  const visualTreeIdSet = useMemo(() => new Set(visualTreeIds), [visualTreeIds]);

  const buildPersistableMenuJson = useCallback(() => {
    if (!crmWorkspace) return json.trim();
    if (!planId) return '';

    const activeSet = new Set(planActiveById[planId] ?? []);
    const filterTreeByActive = (nodes: EditorMenuItem[]): EditorMenuItem[] => {
      const out: EditorMenuItem[] = [];
      for (const node of nodes) {
        const children = filterTreeByActive(node.children);
        const route = (node.ruta ?? '').trim();
        const perm = (node.permiso ?? '').trim();
        const hasRoute = route.length > 0;
        const hasPerm = perm.length > 0;
        const isStrictFolder = !hasRoute && !hasPerm;
        const isStrictLeaf = hasRoute && hasPerm;
        const isChecked = activeSet.has(node.uid);

        if (isStrictFolder) {
          // Keep active folders even when empty so admins can persist
          // in-progress menu scaffolding (folder/subfolder structure).
          if (children.length > 0 || isChecked) out.push({ ...node, children });
          continue;
        }

        if (isStrictLeaf) {
          if (!isChecked) continue;
          out.push({ ...node, ruta: route, permiso: perm, children: [] });
          continue;
        }

        // Inconsistent node (only route or only permission): normalize safely.
        // If it still has visible descendants, keep it as folder; otherwise skip it.
        if (children.length > 0) {
          out.push({ ...node, ruta: '', permiso: '', children });
        }
      }
      return out;
    };

    const filteredTree = filterTreeByActive(visualTree);
    return serializeEditorTreeToMenuJson(filteredTree, visualLabelKey, previewLayout).trim();
  }, [crmWorkspace, json, planId, planActiveById, previewLayout, visualLabelKey, visualTree]);

  useEffect(() => {
    if (crmWorkspace) return;
    if (!planId) return;
    const p = plans.find((x) => x.id === planId);
    const l = p?.menuSidebarLayout?.trim().toLowerCase();
    if (l === 'horizontal' || l === 'vertical') setPreviewLayout(l);
  }, [crmWorkspace, planId, plans]);

  const applyMenuJsonString = useCallback(
    (raw: string) => {
      try {
        const parsed = JSON.parse(raw.trim() || '[]') as SessionMenuGroupDto[];
        const groups = Array.isArray(parsed) ? normalizeParsedMenuGroups(parsed) : [];
        setJson(JSON.stringify(groups, null, 2));
        const layout = readPlanCustomMenuBarLayout(groups);
        if (layout) setPreviewLayout(layout);
        resetEditorTree(sessionGroupsToEditorTree(groups, byPerm));
      } catch {
        setJson(raw);
        resetEditorTree([]);
      }
    },
    [byPerm, resetEditorTree],
  );

  const appendAudit = useCallback(
    (line: string) => {
      if (!crmWorkspace) return;
      const stamp = new Date().toLocaleTimeString();
      setAuditLines((prev) => [`[${stamp}] ${line}`, ...prev].slice(0, 100));
    },
    [crmWorkspace],
  );

  useEffect(() => {
    if (!crmWorkspace) return;
    setSub('plan');
    setEditorMainTab('visual');
    setMenuViewMode('split');
    setWizardOpen(false);
    setWizardStep(0);
  }, [crmWorkspace]);

  useEffect(() => {
    if (!crmWorkspace) return;
    if (planId) return;
    if (crmPlans[0]) {
      setPlanId(crmPlans[0].id);
      setPreviewLayout(crmPlans[0].layout);
    }
  }, [crmWorkspace, planId, crmPlans]);

  useEffect(() => {
    if (!crmWorkspace || !planId) return;
    if (typeof window === 'undefined') return;
    let cancelled = false;
    hydratingPlanRef.current = true;
    planSwitchClock.current = Date.now();
    void (async () => {
      try {
        const bundle = await superAdminService.getPlanMenu(planId);
        if (cancelled) return;
        if (bundle.menuConfigJson?.trim()) {
          applyMenuJsonString(JSON.stringify(JSON.parse(bundle.menuConfigJson), null, 2));
          const l = bundle.menuSidebarLayout?.trim().toLowerCase();
          if (l === 'horizontal' || l === 'vertical') setPreviewLayout(l);
          return;
        }
      } catch {
        // fallback to global menu below.
      }

      const currentPlanIndex = crmPlans.findIndex((p) => p.id === planId);
      const previousPlan = currentPlanIndex > 0 ? crmPlans[currentPlanIndex - 1] : null;
      if (previousPlan) {
        try {
          const inherited = await superAdminService.getPlanMenu(previousPlan.id);
          if (cancelled) return;
          if (inherited.menuConfigJson?.trim()) {
            const inheritedPretty = JSON.stringify(JSON.parse(inherited.menuConfigJson), null, 2);
            applyMenuJsonString(inheritedPretty);
            const inheritedLayout = inherited.menuSidebarLayout?.trim().toLowerCase();
            const nextLayout = inheritedLayout === 'horizontal' || inheritedLayout === 'vertical' ? inheritedLayout : previewLayout;
            setPreviewLayout(nextLayout);
            setPlanActiveById((prev) => {
              if (prev[planId]) return prev;
              const inheritedNodeIds = prev[previousPlan.id] ?? [];
              return { ...prev, [planId]: [...inheritedNodeIds] };
            });
            await superAdminService.setPlanMenuJson(planId, inheritedPretty, nextLayout);
            appendAudit(`Plan ${planId} heredó menú desde ${previousPlan.code}`);
            return;
          }
        } catch {
          // fallback to global menu below.
        }
      }

      try {
        const menu = await superAdminService.getNavigationMenu();
        if (cancelled) return;
        const sess = normalizeParsedMenuGroups(adminNavigationToSessionMenu(menu));
        const layout = readPlanCustomMenuBarLayout(sess);
        const tree = sessionGroupsToEditorTree(sess, byPerm);
        if (tree.length > 0) {
          setJson(JSON.stringify(sess, null, 2));
          if (layout) setPreviewLayout(layout);
          resetEditorTree(tree);
          return;
        }
      } catch {
        // fallback to local seed below.
      }

      const seed = cloneDefaultCrmTreeSeed();
      setJson(serializeEditorTreeToMenuJson(seed, visualLabelKey, 'horizontal'));
      setPreviewLayout('horizontal');
      resetEditorTree(seed);
    })()
      .finally(() => {
        if (!cancelled) {
          queueMicrotask(() => {
            hydratingPlanRef.current = false;
          });
        }
      });
    return () => {
      cancelled = true;
      hydratingPlanRef.current = false;
    };
  }, [appendAudit, applyMenuJsonString, byPerm, crmPlans, crmWorkspace, planId, previewLayout, resetEditorTree, visualLabelKey]);

  useEffect(() => {
    if (!crmWorkspace) return;
    if (typeof window === 'undefined') return;
    window.localStorage.setItem(CRM_TREE_STORAGE_KEY, JSON.stringify(visualTree));
  }, [crmWorkspace, visualTree]);

  useEffect(() => {
    if (!crmWorkspace) return;
    if (typeof window === 'undefined') return;
    window.localStorage.setItem(CRM_AUDIT_STORAGE_KEY, JSON.stringify(auditLines));
  }, [crmWorkspace, auditLines]);

  useEffect(() => {
    if (crmWorkspace || !planId) return;
    let cancelled = false;
    hydratingPlanRef.current = true;
    planSwitchClock.current = Date.now();
    void (async () => {
      try {
        const bundle = await superAdminService.getPlanMenu(planId);
        if (cancelled) return;
        const l = bundle.menuSidebarLayout?.trim().toLowerCase();
        if (l === 'horizontal' || l === 'vertical') setPreviewLayout(l);
        if (bundle.menuConfigJson?.trim()) {
          applyMenuJsonString(JSON.stringify(JSON.parse(bundle.menuConfigJson), null, 2));
        } else {
          const menu = await superAdminService.getNavigationMenu();
          const sess = adminNavigationToSessionMenu(menu);
          applyMenuJsonString(JSON.stringify(sess, null, 2));
        }
      } catch {
        if (!cancelled) setErr(t('common.errorGeneric'));
      } finally {
        if (!cancelled) {
          queueMicrotask(() => {
            hydratingPlanRef.current = false;
          });
        }
      }
    })();
    return () => {
      cancelled = true;
      hydratingPlanRef.current = false;
    };
  }, [crmWorkspace, planId, applyMenuJsonString, t]);

  useEffect(() => {
    if (!crmWorkspace) return;
    if (!planId) return;
    if (!visualTreeIds.length) return;
    setPlanActiveById((prev) => {
      const exists = prev[planId];
      if (exists) return prev;
      return { ...prev, [planId]: [...visualTreeIds] };
    });
  }, [crmWorkspace, planId, visualTreeIds]);

  useEffect(() => {
    if (!crmWorkspace) return;
    if (typeof window === 'undefined') return;
    window.localStorage.setItem(CRM_PLAN_ACTIVE_STORAGE_KEY, JSON.stringify(planActiveById));
  }, [crmWorkspace, planActiveById]);

  useEffect(() => {
    if (!crmWorkspace) return;
    if (visualTreeIdSet.size === 0) return;
    const validPlanIds = new Set(crmPlans.map((p) => p.id));
    setPlanActiveById((prev) => {
      const next = normalizePlanActiveById(prev, validPlanIds, visualTreeIdSet);
      if (JSON.stringify(next) === JSON.stringify(prev)) return prev;
      return next;
    });
  }, [crmWorkspace, crmPlans, visualTreeIdSet]);

  const reloadArbol = useCallback(async () => {
    try {
      const rows = await superAdminService.getFuncionalidadesArbol();
      setArbol(rows);
    } catch {
      setArbol([]);
    }
  }, []);

  useEffect(() => {
    void superAdminService
      .listSaasPlansAdmin()
      .then(setPlans)
      .catch(() => setPlans([]));
    void superAdminService
      .getTenants()
      .then(setTenants)
      .catch(() => setTenants([]));
    void reloadArbol();
  }, [reloadArbol]);

  const syncCatalog = async () => {
    setBusy(true);
    setErr('');
    try {
      await superAdminService.syncFuncionalidadesCatalogo();
      await reloadArbol();
      if (crmWorkspace) appendAudit('Catálogo de funcionalidades sincronizado');
    } catch (e) {
      setErr(
        formatApiRequestError(e, {
          offline: t('common.apiUnreachable'),
          generic: t('common.errorGeneric'),
        }),
      );
    } finally {
      setBusy(false);
    }
  };

  const handleVisualTreeChange = useCallback(
    (next: EditorMenuItem[]) => {
      if (crmWorkspace && planId) {
        const prevIds = new Set<string>();
        const nextIds = new Set<string>();
        const walk = (nodes: EditorMenuItem[], out: Set<string>) => {
          for (const node of nodes) {
            out.add(node.uid);
            walk(node.children, out);
          }
        };
        walk(visualTree, prevIds);
        walk(next, nextIds);
        const addedIds: string[] = [];
        for (const id of nextIds) {
          if (!prevIds.has(id)) addedIds.push(id);
        }
        if (addedIds.length > 0) {
          setPlanActiveById((prev) => {
            const curr = new Set(prev[planId] ?? []);
            for (const id of addedIds) curr.add(id);
            return { ...prev, [planId]: Array.from(curr) };
          });
        }
      }
      commitEditorTree(next, crmWorkspace);
    },
    [commitEditorTree, crmWorkspace, planId, visualTree],
  );

  useEffect(() => {
    if (!crmWorkspace && editorMainTab !== 'visual') return;
    setJson(serializeEditorTreeToMenuJson(visualTree, visualLabelKey, previewLayout));
  }, [visualTree, visualLabelKey, previewLayout, crmWorkspace, editorMainTab]);

  const layoutPatchSkipMount = useRef(true);
  useEffect(() => {
    if (layoutPatchSkipMount.current) {
      layoutPatchSkipMount.current = false;
      return;
    }
    if (editorMainTab !== 'visual') return;
    setJson((curr) => {
      try {
        const groups = JSON.parse(curr.trim() || '[]') as SessionMenuGroupDto[];
        if (!groups.length) return curr;
        return JSON.stringify(
          groups.map((g) => {
            const o = g as unknown as Record<string, unknown>;
            const code = String(o.code ?? o.Code ?? '')
              .trim()
              .toLowerCase();
            return code === 'plan-custom' ? { ...g, code: 'plan-custom', menuBarLayout: previewLayout } : g;
          }),
          null,
          2,
        );
      } catch {
        return curr;
      }
    });
  }, [previewLayout, editorMainTab]);

  const fillFromGlobal = async () => {
    setErr('');
    try {
      const menu = await superAdminService.getNavigationMenu();
      const sess = adminNavigationToSessionMenu(menu);
      applyMenuJsonString(JSON.stringify(sess, null, 2));
      if (crmWorkspace) appendAudit('Editor rellenado desde el catálogo global');
    } catch (e) {
      setErr(
        formatApiRequestError(e, {
          offline: t('common.apiUnreachable'),
          generic: t('common.errorGeneric'),
        }),
      );
    }
  };

  const loadPlanSaved = async () => {
    if (!planId) return;
    setErr('');
    try {
      const bundle = await superAdminService.getPlanMenu(planId);
      const l = bundle.menuSidebarLayout?.trim().toLowerCase();
      if (l === 'horizontal' || l === 'vertical') setPreviewLayout(l);
      if (bundle.menuConfigJson?.trim()) {
        applyMenuJsonString(JSON.stringify(JSON.parse(bundle.menuConfigJson), null, 2));
      } else {
        await fillFromGlobal();
      }
      if (crmWorkspace) appendAudit('Menú recargado desde el plan');
    } catch {
      setErr(t('common.errorGeneric'));
    }
  };

  const savePlan = async () => {
    if (!planId) return;
    setBusy(true);
    setErr('');
    try {
      const trimmed = buildPersistableMenuJson();
      if (trimmed.length > 0) {
        const groups = JSON.parse(trimmed) as SessionMenuGroupDto[];
        const v = validateSessionMenuGroups(groups);
        if (v.length) {
          setErr(v.join(' '));
          return;
        }
      }
      await superAdminService.setPlanMenuJson(
        planId,
        trimmed.length === 0 ? null : trimmed,
        previewLayout,
      );
      const next = await superAdminService.listSaasPlansAdmin();
      setPlans(next);
      if (crmWorkspace) appendAudit('Menú guardado en el plan');
    } catch (e) {
      setErr(
        formatApiRequestError(e, {
          offline: t('common.apiUnreachable'),
          generic: t('common.errorGeneric'),
        }),
      );
    } finally {
      setBusy(false);
    }
  };

  const clearPlan = async () => {
    if (!planId) return;
    setBusy(true);
    setErr('');
    try {
      await superAdminService.setPlanMenuJson(planId, null, 'horizontal');
      applyMenuJsonString('[]');
      const next = await superAdminService.listSaasPlansAdmin();
      setPlans(next);
      if (crmWorkspace) appendAudit('Menú del plan limpiado');
    } catch (e) {
      setErr(
        formatApiRequestError(e, {
          offline: t('common.apiUnreachable'),
          generic: t('common.errorGeneric'),
        }),
      );
    } finally {
      setBusy(false);
    }
  };

  const loadTenantResolved = async () => {
    if (!tenantId) return;
    setErr('');
    try {
      const r = await superAdminService.getTenantResolvedMenu(tenantId);
      applyMenuJsonString(JSON.stringify(r.menu, null, 2));
      setTenantFlags({
        hasCustomMenu: r.hasCustomMenu,
        usedPlanMenu: r.usedPlanMenu,
        usedGlobalFallback: r.usedGlobalFallback,
      });
    } catch (e) {
      setErr(
        formatApiRequestError(e, {
          offline: t('common.apiUnreachable'),
          generic: t('common.errorGeneric'),
        }),
      );
    }
  };

  const saveTenant = async () => {
    if (!tenantId) return;
    setBusy(true);
    setErr('');
    try {
      const trimmed = json.trim();
      if (trimmed.length > 0) {
        const groups = JSON.parse(trimmed) as SessionMenuGroupDto[];
        const v = validateSessionMenuGroups(groups);
        if (v.length) {
          setErr(v.join(' '));
          return;
        }
      }
      await superAdminService.putTenantCustomMenu(tenantId, json);
      await loadTenantResolved();
    } catch (e) {
      setErr(
        formatApiRequestError(e, {
          offline: t('common.apiUnreachable'),
          generic: t('common.errorGeneric'),
        }),
      );
    } finally {
      setBusy(false);
    }
  };

  const resetTenant = async () => {
    if (!tenantId) return;
    setBusy(true);
    setErr('');
    try {
      await superAdminService.deleteTenantCustomMenu(tenantId);
      await loadTenantResolved();
    } catch (e) {
      setErr(
        formatApiRequestError(e, {
          offline: t('common.apiUnreachable'),
          generic: t('common.errorGeneric'),
        }),
      );
    } finally {
      setBusy(false);
    }
  };

  const runCopyFromPlan = async () => {
    if (!planId || !copySourcePlanId || copySourcePlanId === planId) {
      setErr(t('superadmin.menuBuilder.copyPickPlans'));
      return;
    }
    if (!copyMenu) {
      setErr(t('superadmin.menuBuilder.copyPickOptions'));
      return;
    }
    setBusy(true);
    setErr('');
    try {
      await superAdminService.copyPlanFrom(planId, copySourcePlanId, {
        copyMenu,
      });
      const next = await superAdminService.listSaasPlansAdmin();
      setPlans(next);
      await loadPlanSaved();
      if (crmWorkspace) appendAudit('Configuración copiada desde otro plan');
    } catch (e) {
      setErr(
        formatApiRequestError(e, {
          offline: t('common.apiUnreachable'),
          generic: t('common.errorGeneric'),
        }),
      );
    } finally {
      setBusy(false);
    }
  };

  const persistCrmPlan = useCallback(async () => {
    if (!planId || hydratingPlanRef.current) return;
    if (Date.now() - planSwitchClock.current < 900) return;
    const trimmed = buildPersistableMenuJson();
    setErr('');
    try {
      if (trimmed.length > 0) {
        const groups = JSON.parse(trimmed) as SessionMenuGroupDto[];
        const v = validateSessionMenuGroups(groups);
        if (v.length) {
          setErr(v.join(' '));
          return;
        }
      }
      setSavingAuto(true);
      await superAdminService.setPlanMenuJson(
        planId,
        trimmed.length === 0 ? null : trimmed,
        previewLayout,
      );
      appendAudit('Guardado automático');
    } catch (e) {
      setErr(
        formatApiRequestError(e, {
          offline: t('common.apiUnreachable'),
          generic: t('common.errorGeneric'),
        }),
      );
    } finally {
      setSavingAuto(false);
    }
  }, [buildPersistableMenuJson, planId, previewLayout, appendAudit, t]);

  useEffect(() => {
    if (!planId) return;
    if (hydratingPlanRef.current) return;
    if (Date.now() - planSwitchClock.current < 900) return;
    const tid = window.setTimeout(() => {
      void persistCrmPlan();
    }, 900);
    return () => window.clearTimeout(tid);
  }, [planId, json, previewLayout, persistCrmPlan]);

  if (crmWorkspace) {
    const activePlan = crmPlans.find((p) => p.id === planId);
    const activePlanIndex = crmPlans.findIndex((p) => p.id === planId);
    const previousPlanForInheritance = activePlanIndex > 0 ? crmPlans[activePlanIndex - 1] : null;
    const wizardSteps = [
      {
        title: '1) Selecciona un plan',
        body: 'Usa las pills superiores para elegir el plan comercial que quieres editar.',
      },
      {
        title: '2) Construye el menú',
        body: 'Arrastra formularios del catálogo y ordénalos en el árbol maestro.',
      },
      {
        title: '3) Activa por plan',
        body: 'Marca checkboxes para definir qué nodos quedan activos en el plan seleccionado.',
      },
      {
        title: '4) Valida en vista empresa',
        body: 'Revisa layout horizontal/vertical y usa la simulación para comprobar el resultado final.',
      },
    ] as const;
    const wizardCurrentStep = wizardSteps[wizardStep] ?? wizardSteps[0];
    const locTag = locale === 'en' ? 'en-US' : 'es-ES';
    const priceLabel = activePlan ? formatMoney(showAnnual ? activePlan.priceYearly : activePlan.priceMonthly, 'USD', locTag) : '—';
    const cycleLabel = showAnnual ? '/año' : '/mes';
    const currentActiveSet = new Set(planActiveById[planId] ?? []);
    const planCardFeatures = visualTree
      .filter((x) => currentActiveSet.has(x.uid))
      .map((x) => x.nombre)
      .slice(0, 14);
    const onToggleNodeActive = (uid: string, checked: boolean) => {
      const findByUid = (nodes: EditorMenuItem[]): EditorMenuItem | null => {
        for (const n of nodes) {
          if (n.uid === uid) return n;
          const child = findByUid(n.children);
          if (child) return child;
        }
        return null;
      };
      const node = findByUid(visualTree);
      if (!node) return;
      const collectDescendantIds = (root: EditorMenuItem): string[] => {
        const ids: string[] = [];
        const walk = (curr: EditorMenuItem) => {
          ids.push(curr.uid);
          for (const child of curr.children) walk(child);
        };
        walk(root);
        return ids;
      };
      const impactedIds = collectDescendantIds(node);
      setPlanActiveById((prev) => {
        const curr = new Set(prev[planId] ?? []);
        if (checked) {
          for (const id of impactedIds) curr.add(id);
        } else {
          for (const id of impactedIds) curr.delete(id);
        }
        return { ...prev, [planId]: Array.from(curr) };
      });
      appendAudit(`${checked ? 'Activado' : 'Desactivado'}: ${node.nombre}`);
    };
    const createNewPlan = async () => {
      const normalizedName = newPlanName.trim();
      const monthly = Number.parseFloat(newPlanMonthly.trim());
      if (!normalizedName || !Number.isFinite(monthly) || monthly <= 0) {
        setErr('Completa nombre y precio mensual válidos');
        return;
      }

      const inheritanceSourcePlanId = newPlanInheritSourcePlanId || planId;
      const inheritedNodeIds = newPlanInheritOnCreate
        ? resolveInheritedActiveNodeIds(planActiveById, inheritanceSourcePlanId, visualTreeIdSet)
        : [];

      const code = normalizedName
        .toLowerCase()
        .replace(/[^a-z0-9]+/g, '-')
        .replace(/(^-|-$)/g, '')
        .slice(0, 40) || `plan-${Date.now().toString().slice(-6)}`;

      const shortLabel = normalizedName
        .trim()
        .toUpperCase()
        .replace(/[^A-Z0-9]+/g, '_')
        .replace(/^_+|_+$/g, '')
        .slice(0, 32) || 'PLAN';

      setBusy(true);
      setErr('');
      try {
        const newPlanId = await superAdminService.createSaasPlan({
          code,
          name: normalizedName,
          shortLabel,
          isActive: true,
          priceAmount: monthly,
          currency: 'USD',
          billingCycle: 'monthly',
          isPubliclyVisible: true,
          isRecommended: false,
          sortOrder: plans.length,
          externalBillingRef: null,
        });

        const nextPlans = await superAdminService.listSaasPlansAdmin();
        setPlans(nextPlans);
        const created = nextPlans.find((p) => p.id === newPlanId);
        const createdLabel = created?.name ?? normalizedName;
        setPlanId(newPlanId);
        setPlanActiveById((prev) => ({ ...prev, [newPlanId]: inheritedNodeIds }));
        setNewPlanModalOpen(false);
        setNewPlanName('');
        setNewPlanMonthly('199');
        setNewPlanYearly('');
        setNewPlanDescription('');
        setNewPlanInheritOnCreate(false);
        setNewPlanInheritSourcePlanId('');
        if (newPlanInheritOnCreate && inheritedNodeIds.length > 0) {
          const sourceLabel = crmPlans.find((p) => p.id === inheritanceSourcePlanId)?.name ?? inheritanceSourcePlanId;
          appendAudit(`Plan creado: ${createdLabel} (heredó ${inheritedNodeIds.length} nodos desde ${sourceLabel})`);
        } else {
          appendAudit(`Plan creado: ${createdLabel}`);
        }
      } catch (e) {
        setErr(
          formatApiRequestError(e, {
            offline: t('common.apiUnreachable'),
            generic: t('common.errorGeneric'),
          }),
        );
      } finally {
        setBusy(false);
      }
    };
    const inheritFromPreviousPlanNow = async () => {
      const order = crmPlans.map((p) => p.id);
      const idx = order.indexOf(planId);
      if (idx <= 0) {
        setErr('No hay plan anterior para heredar.');
        appendAudit('No hay plan inferior para heredar');
        return;
      }
      const previousPlanId = order[idx - 1]!;
      const previousPlan = crmPlans.find((p) => p.id === previousPlanId);
      if (!previousPlan) {
        setErr('No se encontró el plan anterior para heredar.');
        return;
      }

      setBusy(true);
      setErr('');
      try {
        const bundle = await superAdminService.getPlanMenu(previousPlanId);
        if (!bundle.menuConfigJson?.trim()) {
          setErr(`El plan anterior (${previousPlan.code}) no tiene menú guardado.`);
          appendAudit(`No se pudo heredar: ${previousPlan.code} no tiene menú persistido`);
          return;
        }

        const inheritedPretty = JSON.stringify(JSON.parse(bundle.menuConfigJson), null, 2);
        applyMenuJsonString(inheritedPretty);
        const inheritedLayout = bundle.menuSidebarLayout?.trim().toLowerCase();
        const nextLayout = inheritedLayout === 'horizontal' || inheritedLayout === 'vertical' ? inheritedLayout : previewLayout;
        setPreviewLayout(nextLayout);
        setPlanActiveById((prev) => ({ ...prev, [planId]: [...(prev[previousPlanId] ?? [])] }));
        await superAdminService.setPlanMenuJson(planId, inheritedPretty, nextLayout);
        const next = await superAdminService.listSaasPlansAdmin();
        setPlans(next);
        appendAudit(`Plan ${activePlan?.name ?? planId} heredó y guardó menú desde ${previousPlan.code}`);
      } catch (e) {
        setErr(
          formatApiRequestError(e, {
            offline: t('common.apiUnreachable'),
            generic: t('common.errorGeneric'),
          }),
        );
      } finally {
        setBusy(false);
      }
    };
    const resetPlanLocal = () => {
      setResetPlanConfirmOpen(true);
    };
    const exportWorkspaceSnapshot = () => {
      const payload: CrmWorkspaceExportPayload<EditorMenuItem> = {
        version: MENU_BUILDER_SCHEMA_VERSION,
        exportedAt: new Date().toISOString(),
        plans: crmPlans,
        planActiveById,
        tree: visualTree,
        auditLines,
      };
      downloadJsonFile(makeExportFileName('superadmin-crm-workspace'), payload);
      appendAudit('Snapshot exportado (workspace completo)');
    };
    const exportAuditSnapshot = () => {
      const payload = {
        version: MENU_BUILDER_SCHEMA_VERSION,
        exportedAt: new Date().toISOString(),
        auditLines,
      };
      downloadJsonFile(makeExportFileName('superadmin-crm-audit'), payload);
      appendAudit('Auditoría exportada');
    };
    const triggerImportWorkspace = () => {
      importWorkspaceInputRef.current?.click();
    };
    const importWorkspaceSnapshot = async (file: File) => {
      try {
        const text = await file.text();
        const parsed = JSON.parse(text) as unknown;
        const imported = parseImportedCrmWorkspace(parsed, crmPlans);
        setPlanActiveById(imported.planActiveById);
        resetEditorTree(Array.isArray(imported.tree) ? (imported.tree as EditorMenuItem[]) : []);
        setAuditLines(imported.auditLines);
        const effectivePlanId = crmPlans.some((p) => p.id === planId) ? planId : crmPlans[0]?.id;
        if (effectivePlanId) {
          setPlanId(effectivePlanId);
          const selected = crmPlans.find((p) => p.id === effectivePlanId);
          if (selected) setPreviewLayout(selected.layout);
        }
        appendAudit(`Snapshot importado (v${imported.version})`);
      } catch {
        setErr('No se pudo importar el archivo JSON');
      }
    };

    const crmToolbar: ReactNode = (
      <>
        <button
          type="button"
          className="zh-btn zh-btn--ghost zh-btn--sm"
          onClick={() => undoEditorTree()}
          disabled={!canUndoEditorTree}
          aria-label="Deshacer último cambio del árbol"
          title="Deshacer"
        >
          ↺
        </button>
        <button
          type="button"
          className="zh-btn zh-btn--ghost zh-btn--sm"
          onClick={() => redoEditorTree()}
          disabled={!canRedoEditorTree}
          aria-label="Rehacer cambio deshecho del árbol"
          title="Rehacer"
        >
          ↻
        </button>
        <span className="subtle mono" style={{ fontSize: 11 }} title="Versión del esquema de menú">
          v{MENU_BUILDER_SCHEMA_VERSION}
        </span>
      </>
    );

    const crmMasterStack: ReactNode = (
      <div className="zh-form-tabs" role="tablist" aria-label="Planes comerciales" style={{ padding: 'var(--space-4) var(--space-4) 0' }}>
        {crmPlans.map((p) => (
          <button
            key={p.id}
            type="button"
            role="tab"
            aria-selected={planId === p.id}
            className={planId === p.id ? 'is-active' : ''}
            onClick={() => {
              setErr('');
              setPlanId(p.id);
              setPreviewLayout(p.layout);
            }}
            disabled={busy || savingAuto}
          >
            <span aria-hidden style={{ fontSize: 14 }}>{planEmoji(p.code)}</span>
            {p.code}
          </button>
        ))}
      </div>
    );
    const crmLibraryStack: ReactNode = (
      <div className="menu-plan-composer__masterStack">
        <div className="pg-table-controls" style={{ flexWrap: 'wrap' }}>
          <div className="pg-search" style={{ flex: 1, minWidth: 140 }}>
            <span className="material-symbols-outlined">search</span>
          <input
            className="zh-input"
            type="search"
            autoComplete="off"
            placeholder="Buscar nodo…"
            value={catalogSearch}
            onChange={(e) => setCatalogSearch(e.target.value)}
            disabled={busy || savingAuto}
            aria-label="Buscar en el catálogo de formularios"
          />
          </div>
          <div style={{ display: 'flex', gap: 'var(--space-2)', flexShrink: 0 }}>
            <ZHBtn
              variant="ghost"
              size="sm"
              type="button"
              onClick={() => {
                setCatalogSearch('');
                setCatalogSearchField('all');
                setCatalogNodeType('all');
                setCatalogOnlyWithRoute(false);
              }}
              disabled={
                busy ||
                savingAuto ||
                (!catalogSearch.trim() && catalogSearchField === 'all' && catalogNodeType === 'all' && !catalogOnlyWithRoute)
              }
              aria-label="Limpiar búsqueda del catálogo"
            >
              Limpiar
            </ZHBtn>
            <ZHBtn variant="ghost" size="sm" type="button" onClick={() => void syncCatalog()} disabled={busy || savingAuto} aria-label="Sincronizar catálogo desde controladores">
              Sync API
            </ZHBtn>
            <ZHBtn variant="ghost" size="sm" type="button" onClick={() => void reloadArbol()} disabled={busy || savingAuto} aria-label="Refrescar lista de formularios">
              <span className="material-symbols-outlined" style={{ fontSize: 16 }}>refresh</span>
            </ZHBtn>
          </div>
        </div>
        <div className="menu-plan-composer__advancedFilters" aria-label="Filtros avanzados del catálogo">
          <label className="menu-plan-composer__advancedFilterItem">
            <span>Campo</span>
            <select
              className="zh-input"
              value={catalogSearchField}
              onChange={(e) => setCatalogSearchField(e.target.value as CatalogSearchField)}
              disabled={busy || savingAuto}
              title="Limita la búsqueda por nombre, permiso o ruta."
            >
              <option value="all">Todo</option>
              <option value="name">Nombre</option>
              <option value="perm">Permiso</option>
              <option value="route">Ruta</option>
            </select>
          </label>
          <label className="menu-plan-composer__advancedFilterItem">
            <span>Tipo</span>
            <select
              className="zh-input"
              value={catalogNodeType}
              onChange={(e) => setCatalogNodeType(e.target.value as CatalogNodeType)}
              disabled={busy || savingAuto}
              title="Filtra por carpetas o formularios."
            >
              <option value="all">Todos</option>
              <option value="folders">Solo carpetas</option>
              <option value="forms">Solo formularios</option>
            </select>
          </label>
          <label className="zh-inline-check" title="Muestra solo nodos que tengan ruta configurada.">
            <input
              type="checkbox"
              checked={catalogOnlyWithRoute}
              onChange={(e) => setCatalogOnlyWithRoute(e.target.checked)}
              disabled={busy || savingAuto}
            />
            <span>Solo con ruta</span>
          </label>
          <span className="subtle" style={{ fontSize: 11, marginLeft: 'auto' }} title="Tip: combina búsqueda + tipo para encontrar nodos más rápido.">
            💡 Filtro avanzado
          </span>
        </div>
      </div>
    );

    const crmMasterFooter: ReactNode = (
      <div className="pg-actions-bar">
        <div className="pg-actions-buttons">
          <ZHBtn
            variant="primary"
            size="md"
            type="button"
            onClick={() => void savePlan()}
            disabled={busy || savingAuto || !planId}
            aria-label="Guardar armado del menú en el plan actual"
          >
            💾 Guardar menú
          </ZHBtn>
          <ZHBtn
            variant="ghost"
            size="md"
            type="button"
            onClick={() => setInheritPlanConfirmOpen(true)}
            disabled={busy || savingAuto}
            aria-label="Heredar y guardar menú desde plan anterior"
          >
            📄 Heredar ahora
          </ZHBtn>
          <ZHBtn variant="ghost" size="md" type="button" onClick={resetPlanLocal} disabled={busy || savingAuto || !planId} aria-label="Resetear plan actual">
            🔄 Resetear plan
          </ZHBtn>
        </div>
        <div className="menu-plan-composer__callout menu-plan-composer__callout--info" role="note">
          Arrastra nodos para reordenar o cambiar de carpeta. Usa las flechas del bloque de acciones para subir, bajar o cambiar de nivel. Deshacer y rehacer solo afectan esta sesión; los
          cambios válidos se guardan solos en el plan.
        </div>
      </div>
    );

    const previewControls: ReactNode = (
      <div
        role="radiogroup"
        aria-label="Orientación del menú"
        style={{ display: 'flex', gap: 2, background: 'rgba(0,0,0,0.08)', borderRadius: 6, padding: 2 }}
      >
        {(['horizontal', 'vertical'] as const).map((layout) => {
          const active = previewLayout === layout;
          return (
            <button
              key={layout}
              type="button"
              role="radio"
              aria-checked={active}
              disabled={busy || savingAuto}
              onClick={() => setPreviewLayout(layout)}
              style={{
                display: 'flex', alignItems: 'center', gap: 3,
                padding: '2px 7px', borderRadius: 4,
                border: 'none', cursor: 'pointer', fontSize: 10,
                fontWeight: active ? 700 : 400,
                background: active ? '#fff' : 'transparent',
                color: active ? '#3a5f84' : '#64748b',
                boxShadow: active ? '0 1px 3px rgba(0,0,0,0.12)' : 'none',
                transition: 'all 0.15s',
              }}
            >
              <span className="material-symbols-outlined" style={{ fontSize: 11 }}>
                {layout === 'horizontal' ? 'dock_to_right' : 'view_sidebar'}
              </span>
              {layout === 'horizontal' ? 'Horizontal' : 'Vertical'}
            </button>
          );
        })}
      </div>
    );

    const crmPreviewExtrasBottom: ReactNode = (
      <div className="menu-plan-composer__planCard">
        {/* Card header: plan badge + price + billing toggle */}
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', flexWrap: 'wrap', gap: 8, marginBottom: 8 }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <span aria-hidden style={{ fontSize: 16 }}>{planEmoji(activePlan?.code ?? '')}</span>
            <span className="badge badge--blue badge--md" style={{ textTransform: 'uppercase' }}>
              {activePlan?.code ?? 'PLAN'}
            </span>
            <span style={{ fontSize: 13, fontWeight: 700, color: 'var(--color-text)' }}>
              {priceLabel}<span style={{ fontSize: 11, fontWeight: 400, color: 'var(--color-text-secondary)', marginLeft: 2 }}>{cycleLabel}</span>
            </span>
          </div>
          <div
            role="radiogroup"
            aria-label="Ciclo de facturación"
            style={{ display: 'flex', gap: 1, background: 'var(--color-surface-container)', borderRadius: 'var(--radius-md)', padding: 2 }}
          >
            {(['Mensual', 'Anual'] as const).map((label) => {
              const isAnnual = label === 'Anual';
              const active = showAnnual === isAnnual;
              return (
                <button
                  key={label}
                  type="button"
                  role="radio"
                  aria-checked={active}
                  onClick={() => setShowAnnual(isAnnual)}
                  style={{
                    padding: '2px 8px', borderRadius: 'var(--radius-sm)',
                    border: 'none', cursor: 'pointer', fontSize: 10,
                    fontWeight: active ? 700 : 400,
                    background: active ? 'var(--color-surface)' : 'transparent',
                    color: active ? 'var(--color-primary)' : 'var(--color-text-secondary)',
                    boxShadow: active ? 'var(--shadow-sm)' : 'none',
                    transition: 'all 0.15s',
                  }}
                >
                  {label}
                </button>
              );
            })}
          </div>
        </div>
        <p className="menu-plan-composer__planCardSub">{activePlan?.description || 'Ideal para equipos en crecimiento'}</p>
        <ul className="menu-plan-composer__planCardList">
          {planCardFeatures.length ? (
            planCardFeatures.map((name, idx) => (
              <li key={`${idx}-${name}`}>
                <span aria-hidden>✓</span> {name}
              </li>
            ))
          ) : (
            <li className="subtle">Añade carpetas o formularios al árbol para listarlos aquí.</li>
          )}
        </ul>
        <button type="button" className="zh-btn zh-btn--primary zh-btn--md menu-plan-composer__planCardCta" disabled aria-label="Seleccionar plan (solo demostración visual)">
          Seleccionar plan →
        </button>
      </div>
    );

    return (
      <div className="pg-page">
        <div className="pg-section">
          <div className="pg-section-header">
            <div className="pg-section-header-left">
              <span className="material-symbols-outlined pg-section-icon">settings_accessibility</span>
              <h2 className="pg-section-label">Panel SuperAdmin</h2>
              <p className="subtle" style={{ margin: 0, marginLeft: 'var(--space-2)' }}>
                Gestión completa de menús, planes comerciales y configuración global
              </p>
            </div>
            <div style={{ display: 'flex', alignItems: 'center', gap: 'var(--space-3)' }}>
              <ZHBtn
                variant="ghost"
                size="md"
                type="button"
                onClick={() => {
                  setWizardStep(0);
                  setWizardOpen(true);
                }}
                disabled={busy || savingAuto}
                title="Abrir guía rápida del flujo de configuración"
              >
                <span className="material-symbols-outlined" style={{ fontSize: 16 }}>help</span>
                Guía rápida
              </ZHBtn>
              {savingAuto ? (
                <span className="menu-plan-composer__saving" aria-live="polite">
                  Guardando…
                </span>
              ) : null}
            </div>
          </div>
        </div>

        {err ? <ZHPageNotice variant="error" message="Error" detail={err} /> : null}

        <div className="pg-section menu-plan-composer__workspace">
          <MenuBuilder
            workspaceVariant="crm"
            hideWorkspaceToolbar
            catalogArbol={filteredArbol}
            tree={visualTree}
            onTreeChange={handleVisualTreeChange}
            viewMode={menuViewMode}
            onViewModeChange={setMenuViewMode}
            previewLayout={previewLayout}
            onPreviewLayoutChange={setPreviewLayout}
            onBuilderMessage={(msg) => setErr(msg)}
            crmToolbar={crmToolbar}
            crmMasterStack={crmMasterStack}
            crmLibraryStack={crmLibraryStack}
            crmMasterFooter={crmMasterFooter}
            crmPreviewExtrasBottom={crmPreviewExtrasBottom}
            previewControls={previewControls}
            activeNodeIds={currentActiveSet}
            onToggleNodeActive={onToggleNodeActive}
            panelTitles={{
              canvas: '📁 Árbol maestro',
              preview: '🖥 Vista empresa (previsualización)',
              library: '📋 Formularios disponibles (arrastra al árbol)',
            }}
          />
        </div>

        <section className="pg-section" aria-labelledby="menu-plan-audit-heading">
          <div className="pg-section-header">
            <div className="pg-section-header-left">
              <span className="material-symbols-outlined pg-section-icon">history_edu</span>
              <h3 id="menu-plan-audit-heading" className="pg-section-label">Auditoría</h3>
            </div>
            <div style={{ display: 'flex', gap: 'var(--space-2)', flexWrap: 'wrap' }}>
              <ZHBtn variant="ghost" size="sm" type="button" onClick={exportAuditSnapshot} disabled={!auditLines.length} aria-label="Exportar auditoría">
                Exportar auditoría
              </ZHBtn>
              <ZHBtn variant="ghost" size="sm" type="button" onClick={exportWorkspaceSnapshot} aria-label="Exportar workspace completo">
                Exportar workspace
              </ZHBtn>
              <ZHBtn variant="ghost" size="sm" type="button" onClick={triggerImportWorkspace} aria-label="Importar workspace completo">
                Importar workspace
              </ZHBtn>
              <ZHBtn variant="ghost" size="sm" type="button" onClick={() => setAuditLines([])} disabled={!auditLines.length} aria-label="Limpiar historial de auditoría">
                Limpiar
              </ZHBtn>
            </div>
          </div>
          <input
            ref={importWorkspaceInputRef}
            type="file"
            accept="application/json,.json"
            className="menu-plan-composer__hiddenFileInput"
            onChange={(e) => {
              const file = e.target.files?.[0];
              if (file) void importWorkspaceSnapshot(file);
              e.currentTarget.value = '';
            }}
          />
          {auditLines.length === 0 ? (
            <p className="subtle" style={{ padding: 'var(--space-6)', textAlign: 'center' }}>
              Las acciones (guardado automático, recargas, simulación…) aparecerán aquí.
            </p>
          ) : (
            <div style={{ overflowX: 'auto', maxHeight: 200, overflowY: 'auto' }}>
              <table className="table">
                <thead>
                  <tr>
                    <th style={{ width: 110 }}>Timestamp</th>
                    <th style={{ width: 96 }}>Acción</th>
                    <th>Detalles</th>
                  </tr>
                </thead>
                <tbody>
                  {auditLines.map((line, i) => {
                    const { timestamp, action, details } = parseAuditLine(line);
                    return (
                      <tr key={`${i}-${line.slice(0, 16)}`}>
                        <td>
                          <span className="subtle mono" style={{ fontSize: 11 }}>{timestamp}</span>
                        </td>
                        <td>
                          <span className={`badge badge--${auditActionBadge(action)} badge--upper`} style={{ fontSize: 10 }}>
                            {action}
                          </span>
                        </td>
                        <td style={{ fontSize: 12 }}>{details}</td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          )}
        </section>

        {resetPlanConfirmOpen ? (
          <ZHConfirmModal
            title="Resetear plan"
            message={
              <>
                ¿Seguro que deseas resetear <strong>{activePlan?.name ?? activePlan?.code ?? planId}</strong>?
                <br />
                Se desactivarán todos los nodos de este plan.
              </>
            }
            confirmLabel="Resetear"
            cancelLabel="Cancelar"
            variant="destructive"
            onCancel={() => setResetPlanConfirmOpen(false)}
            onConfirm={() => {
              setPlanActiveById((prev) => ({ ...prev, [planId]: [] }));
              appendAudit(`Plan ${activePlan?.name ?? planId} reseteado`);
              setResetPlanConfirmOpen(false);
            }}
          />
        ) : null}
        {inheritPlanConfirmOpen ? (
          <ZHConfirmModal
            title="Heredar menú desde plan anterior"
            message={
              <>
                ¿Confirmas heredar y guardar el menú de{' '}
                <strong>{previousPlanForInheritance?.name ?? previousPlanForInheritance?.code ?? '—'}</strong> en{' '}
                <strong>{activePlan?.name ?? activePlan?.code ?? planId}</strong>?
                <br />
                Esta acción reemplazará el menú actual del plan seleccionado.
              </>
            }
            confirmLabel="Heredar ahora"
            cancelLabel="Cancelar"
            variant="primary"
            onCancel={() => setInheritPlanConfirmOpen(false)}
            onConfirm={() => {
              setInheritPlanConfirmOpen(false);
              void inheritFromPreviousPlanNow();
            }}
          />
        ) : null}

        {newPlanModalOpen ? (
          <div className="zh-modal-overlay" role="dialog" aria-modal="true" aria-label="Crear nuevo plan">
            <div className="zh-modal" style={{ maxWidth: 480 }}>
              <div className="zh-modal-header">
                <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                  <span className="material-symbols-outlined" style={{ fontSize: 20, color: 'var(--color-primary)' }}>add_circle</span>
                  <h3 style={{ margin: 0, fontSize: 16, fontWeight: 600 }}>Crear nuevo plan comercial</h3>
                </div>
              </div>
              <ZHField label="Nombre del plan">
                <input className="zh-input" value={newPlanName} onChange={(e) => setNewPlanName(e.target.value)} placeholder="Ej. Premium" />
              </ZHField>
              <ZHField label="Precio mensual (USD)">
                <input
                  className="zh-input"
                  type="number"
                  value={newPlanMonthly}
                  onChange={(e) => {
                    const v = e.target.value;
                    setNewPlanMonthly(v);
                    if (!newPlanYearly.trim()) {
                      const n = Number.parseFloat(v);
                      if (Number.isFinite(n)) setNewPlanYearly(String(Math.round(n * 12 * 0.8)));
                    }
                  }}
                />
              </ZHField>
              <ZHField label="Precio anual (USD)">
                <input className="zh-input" type="number" value={newPlanYearly} onChange={(e) => setNewPlanYearly(e.target.value)} placeholder="Calculado automático" />
              </ZHField>
              <ZHField label="Descripción breve">
                <textarea className="zh-input" rows={2} value={newPlanDescription} onChange={(e) => setNewPlanDescription(e.target.value)} placeholder="Características..." />
              </ZHField>
              <ZHField label="Herencia automática (opcional)">
                <div style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-2)' }}>
                  <label className="zh-inline-check">
                    <input type="checkbox" checked={newPlanInheritOnCreate} onChange={(e) => setNewPlanInheritOnCreate(e.target.checked)} />
                    <span>Heredar activaciones desde un plan existente</span>
                  </label>
                  <select
                    className="zh-input"
                    value={newPlanInheritSourcePlanId}
                    onChange={(e) => setNewPlanInheritSourcePlanId(e.target.value)}
                    disabled={!newPlanInheritOnCreate}
                  >
                    <option value="">{planId ? 'Plan actual seleccionado' : 'Selecciona un plan origen'}</option>
                    {crmPlans.map((p) => (
                      <option key={p.id} value={p.id}>
                        {p.code}
                      </option>
                    ))}
                  </select>
                </div>
              </ZHField>
              <div className="pg-actions-bar">
                <div />
                <div className="pg-actions-buttons">
                  <ZHBtn
                    variant="ghost"
                    size="md"
                    type="button"
                    onClick={() => {
                      setNewPlanModalOpen(false);
                      setNewPlanInheritOnCreate(false);
                      setNewPlanInheritSourcePlanId('');
                    }}
                  >
                    Cancelar
                  </ZHBtn>
                  <ZHBtn variant="primary" size="md" type="button" onClick={createNewPlan}>
                    Crear plan
                  </ZHBtn>
                </div>
              </div>
            </div>
          </div>
        ) : null}
        {wizardOpen ? (
          <div className="zh-modal-overlay" role="dialog" aria-modal="true" aria-label="Guía rápida de configuración">
            <div className="zh-modal" style={{ maxWidth: 420 }}>
              <div className="zh-modal-header">
                <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                  <span className="material-symbols-outlined" style={{ fontSize: 20, color: 'var(--color-primary)' }}>help_center</span>
                  <h3 style={{ margin: 0, fontSize: 16, fontWeight: 600 }}>{wizardCurrentStep.title}</h3>
                </div>
              </div>
              <div className="zh-modal-body">
                <p className="subtle">{wizardCurrentStep.body}</p>
                <p className="subtle" style={{ textAlign: 'center', fontSize: 12 }}>Paso {wizardStep + 1} de {wizardSteps.length}</p>
              </div>
              <div className="pg-actions-bar">
                <div />
                <div className="pg-actions-buttons">
                <ZHBtn
                  variant="ghost"
                  size="md"
                  type="button"
                  onClick={() => setWizardStep((s) => Math.max(0, s - 1))}
                  disabled={wizardStep === 0}
                >
                  Anterior
                </ZHBtn>
                <ZHBtn
                  variant="ghost"
                  size="md"
                  type="button"
                  onClick={() => setWizardOpen(false)}
                >
                  Cerrar
                </ZHBtn>
                {wizardStep < wizardSteps.length - 1 ? (
                  <ZHBtn variant="primary" size="md" type="button" onClick={() => setWizardStep((s) => Math.min(wizardSteps.length - 1, s + 1))}>
                    Siguiente
                  </ZHBtn>
                ) : (
                  <ZHBtn variant="primary" size="md" type="button" onClick={() => setWizardOpen(false)}>
                    Finalizar
                  </ZHBtn>
                )}
              </div>
            </div>
          </div>
          </div>
        ) : null}
      </div>
    );
  }

  return (
    <Card>
      <ZHCardSection title={t('superadmin.menuBuilder.title')}>
        <p className="subtle">{t('superadmin.menuBuilder.subtitle')}</p>

        <div className="menu-builder-page-catalog">
          <h3 className="menu-builder-page-catalog__title">{t('superadmin.menuBuilder.treeTitle')}</h3>
          <div className="menu-builder-page-catalog__actions">
            <ZHBtn variant="ghost" size="md" type="button" onClick={() => void syncCatalog()} disabled={busy}>
              {t('superadmin.menuBuilder.syncCatalog')}
            </ZHBtn>
            <ZHBtn variant="ghost" size="md" type="button" onClick={() => void reloadArbol()} disabled={busy}>
              {t('common.refresh')}
            </ZHBtn>
          </div>
        </div>

        <div className="zh-form-tabs menu-plan-composer__legacyTabs" role="tablist">
          <button
            type="button"
            role="tab"
            className={editorMainTab === 'visual' ? 'is-active' : ''}
            onClick={() => {
              try {
                const parsed = JSON.parse(json.trim() || '[]') as SessionMenuGroupDto[];
                const groups = Array.isArray(parsed) ? normalizeParsedMenuGroups(parsed) : [];
                const layout = readPlanCustomMenuBarLayout(groups);
                if (layout) setPreviewLayout(layout);
                commitEditorTree(sessionGroupsToEditorTree(groups, byPerm), false);
              } catch {
                commitEditorTree([], false);
              }
              setEditorMainTab('visual');
            }}
          >
            {t('superadmin.menuBuilder.tabVisual')}
          </button>
          <button
            type="button"
            role="tab"
            className={editorMainTab === 'json' ? 'is-active' : ''}
            onClick={() => setEditorMainTab('json')}
          >
            {t('superadmin.menuBuilder.tabJson')}
          </button>
        </div>

        {editorMainTab === 'visual' ? (
          <div className="menu-plan-composer__legacyBuilderWrap">
            <MenuBuilder
              catalogArbol={arbol}
              tree={visualTree}
              onTreeChange={handleVisualTreeChange}
              viewMode={menuViewMode}
              onViewModeChange={setMenuViewMode}
              previewLayout={previewLayout}
              onPreviewLayoutChange={setPreviewLayout}
              onBuilderMessage={(msg) => setErr(msg)}
            />
          </div>
        ) : null}

        <div className="zh-form-tabs menu-plan-composer__legacyTabs" role="tablist">
          <button type="button" role="tab" className={sub === 'plan' ? 'is-active' : ''} onClick={() => setSub('plan')}>
            {t('superadmin.menuBuilder.byPlan')}
          </button>
          <button type="button" role="tab" className={sub === 'tenant' ? 'is-active' : ''} onClick={() => setSub('tenant')}>
            {t('superadmin.menuBuilder.byTenant')}
          </button>
        </div>

        {err ? <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={err} /> : null}

        {sub === 'plan' ? (
          <>
            <ZHGridRow cols={1}>
              <ZHField label={t('superadmin.menuBuilder.planSelect')}>
                <select className="zh-input" value={planId} onChange={(e) => setPlanId(e.target.value)} disabled={busy}>
                  <option value="">{t('common.select')}</option>
                  {plans.map((p) => (
                    <option key={p.id} value={p.id}>
                      {p.name} ({p.code}){p.hasMenuConfig ? ' *' : ''}
                    </option>
                  ))}
                </select>
              </ZHField>
            </ZHGridRow>
            <div className="subtle menu-plan-composer__legacySubtleLabel">
              {t('superadmin.menuBuilder.copyFromTitle')}
            </div>
            <ZHGridRow cols={1}>
              <ZHField label={t('superadmin.menuBuilder.copySourcePlan')}>
                <select
                  className="zh-input"
                  value={copySourcePlanId}
                  onChange={(e) => setCopySourcePlanId(e.target.value)}
                  disabled={busy || !planId}
                >
                  <option value="">{t('common.select')}</option>
                  {plans
                    .filter((p) => p.id !== planId)
                    .map((p) => (
                      <option key={p.id} value={p.id}>
                        {p.name} ({p.code})
                      </option>
                    ))}
                </select>
              </ZHField>
            </ZHGridRow>
            <ZHGridRow cols={1}>
              <label className="zh-inline-check menu-plan-composer__legacyInlineCheck">
                <input
                  type="checkbox"
                  checked={copyMenu}
                  onChange={(e) => setCopyMenu(e.target.checked)}
                  disabled={busy}
                />
                <span>{t('superadmin.menuBuilder.copyMenuCheck')}</span>
              </label>
            </ZHGridRow>
            <ZHInlineRowRight>
              <ZHBtn
                variant="ghost"
                size="md"
                type="button"
                onClick={() => void runCopyFromPlan()}
                disabled={busy || !planId || !copySourcePlanId}
              >
                {t('superadmin.menuBuilder.copyExecute')}
              </ZHBtn>
            </ZHInlineRowRight>
          </>
        ) : (
          <ZHGridRow cols={1}>
            <ZHField label={t('superadmin.menuBuilder.tenantSelect')}>
              <select className="zh-input" value={tenantId} onChange={(e) => setTenantId(e.target.value)} disabled={busy}>
                <option value="">{t('common.select')}</option>
                {tenants.map((x) => (
                  <option key={x.id} value={x.id}>
                    {x.name} ({x.slug}){x.hasCustomMenu ? ' · menú' : ''}
                  </option>
                ))}
              </select>
            </ZHField>
            {tenantFlags ? (
              <p className="subtle menu-plan-composer__legacySubtleHelp">
                {t('superadmin.menuBuilder.hintFlags')}{' '}
                <strong>
                  {tenantFlags.hasCustomMenu ? 'custom ' : ''}
                  {tenantFlags.usedPlanMenu ? 'plan ' : ''}
                  {tenantFlags.usedGlobalFallback ? 'global' : ''}
                </strong>
              </p>
            ) : null}
          </ZHGridRow>
        )}

        {editorMainTab === 'json' ? (
          <ZHField label={t('superadmin.menuBuilder.jsonLabel')}>
            <textarea
              className="zh-input menu-plan-composer__legacyJsonTextarea"
              rows={18}
              value={json}
              onChange={(e) => setJson(e.target.value)}
              spellCheck={false}
              disabled={busy}
            />
          </ZHField>
        ) : null}

        <ZHInlineRowRight>
          <ZHBtn variant="ghost" size="md" type="button" onClick={() => void fillFromGlobal()} disabled={busy}>
            {t('superadmin.menuBuilder.loadGlobal')}
          </ZHBtn>
          {sub === 'plan' ? (
            <>
              <ZHBtn variant="ghost" size="md" type="button" onClick={() => void loadPlanSaved()} disabled={busy || !planId}>
                {t('superadmin.menuBuilder.loadSavedPlan')}
              </ZHBtn>
              <ZHBtn variant="ghost" size="md" type="button" onClick={() => void clearPlan()} disabled={busy || !planId}>
                {t('superadmin.menuBuilder.clearPlan')}
              </ZHBtn>
              <ZHBtn variant="primary" size="md" type="button" onClick={() => void savePlan()} disabled={busy || !planId}>
                {t('superadmin.menuBuilder.savePlan')}
              </ZHBtn>
            </>
          ) : (
            <>
              <ZHBtn variant="ghost" size="md" type="button" onClick={() => void loadTenantResolved()} disabled={busy || !tenantId}>
                {t('superadmin.menuBuilder.loadResolvedTenant')}
              </ZHBtn>
              <ZHBtn variant="ghost" size="md" type="button" onClick={() => void resetTenant()} disabled={busy || !tenantId}>
                {t('superadmin.menuBuilder.resetTenant')}
              </ZHBtn>
              <ZHBtn variant="primary" size="md" type="button" onClick={() => void saveTenant()} disabled={busy || !tenantId}>
                {t('superadmin.menuBuilder.saveTenant')}
              </ZHBtn>
            </>
          )}
        </ZHInlineRowRight>
      </ZHCardSection>
    </Card>
  );
}
