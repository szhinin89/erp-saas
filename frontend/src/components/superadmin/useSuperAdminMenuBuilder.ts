import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import type { MenuBuilderViewMode } from '../menu-builder/MenuBuilder';
import type { MenuPreviewLayout } from '../menu-builder/MenuPreview';
import {
  buildFuncionalidadMaps,
  editorToMenuItems,
  normalizeParsedMenuGroups,
  readPlanCustomMenuBarLayout,
  sessionGroupsToEditorTree,
  type EditorMenuItem,
  type MenuItem,
} from '../menu-builder/menuBuilderTypes';
import { useEditorTreeHistory } from '../menu-builder/withHistory';
import {
  superAdminService,
  type CommercialPlanAdmin,
  type FuncionalidadArbolDto,
  type SuperAdminSubscriber,
} from '../../modules/superadmin/api/superAdminService';
import type { SessionMenuGroupDto } from '../../types/access';
import { useI18n } from '../../i18n/i18n';
import {
  CRM_AUDIT_STORAGE_KEY,
  CRM_PLAN_ACTIVE_STORAGE_KEY,
  filterFuncionalidadesArbol,
  mapCommercialPlanAdminToCrmPlan,
  type CatalogNodeType,
  type CatalogSearchField,
  type EditorMainTab,
  type SubMode,
} from './superAdminMenuBuilderUtils';
import { useSuperAdminMenuBuilderActions } from './useSuperAdminMenuBuilderActions';
import { useSuperAdminMenuBuilderCrmEffects } from './useSuperAdminMenuBuilderCrmEffects';
import { useSuperAdminMenuBuilderSharedEffects } from './useSuperAdminMenuBuilderSharedEffects';

export function useSuperAdminMenuBuilder(crmWorkspace: boolean) {
  const { t, locale } = useI18n();
  const [sub, setSub] = useState<SubMode>('plan');
  const [plans, setPlans] = useState<CommercialPlanAdmin[]>([]);
  const [subscribers, setSubscribers] = useState<SuperAdminSubscriber[]>([]);
  const [planId, setPlanId] = useState('');
  const [subscriberId, setSubscriberId] = useState('');
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
  const [subscriberMenuFlags, setSubscriberMenuFlags] = useState<{
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
    () => plans.map(mapCommercialPlanAdminToCrmPlan),
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
      .listCommercialPlansAdmin()
      .then(setPlans)
      .catch(() => setPlans([]));
    void superAdminService
      .getSubscribers()
      .then(setSubscribers)
      .catch(() => setSubscribers([]));
    void reloadArbol();
  }, [reloadArbol]);

  const {
    syncCatalog,
    handleVisualTreeChange,
    fillFromGlobal,
    loadPlanSaved,
    savePlan,
    clearPlan,
    loadTenantResolved,
    saveSubscriber,
    resetSubscriber,
    runCopyFromPlan,
    persistCrmPlan,
  } = useSuperAdminMenuBuilderActions({
    crmWorkspace,
    t,
    planId,
    subscriberId,
    json,
    previewLayout,
    visualLabelKey,
    visualTree,
    planActiveById,
    copySourcePlanId,
    copyMenu,
    setBusy,
    setErr,
    setPlans,
    setPreviewLayout,
    setSavingAuto,
    setPlanActiveById,
    setSubscriberMenuFlags,
    applyMenuJsonString,
    appendAudit,
    commitEditorTree,
    reloadArbol,
    hydratingPlanRef,
    planSwitchClock,
  });

  useSuperAdminMenuBuilderCrmEffects({
    crmWorkspace,
    planId,
    setPlanId,
    setSub,
    setEditorMainTab,
    setMenuViewMode,
    setWizardOpen,
    setWizardStep,
    setPreviewLayout,
    setPlanActiveById,
    setJson,
    crmPlans,
    visualTree,
    visualTreeIds,
    visualTreeIdSet,
    auditLines,
    planActiveById,
    json,
    previewLayout,
    visualLabelKey,
    byPerm,
    applyMenuJsonString,
    appendAudit,
    resetEditorTree,
    persistCrmPlan,
    hydratingPlanRef,
    planSwitchClock,
  });

  useSuperAdminMenuBuilderSharedEffects({
    crmWorkspace,
    planId,
    plans,
    editorMainTab,
    visualTree,
    visualLabelKey,
    previewLayout,
    setPreviewLayout,
    setJson,
    setErr,
    applyMenuJsonString,
    t,
    hydratingPlanRef,
    planSwitchClock,
  });

  const previewData = useMemo(() => {
    if (!crmWorkspace) return [];
    const items = editorToMenuItems(visualTree);
    const activeSet = new Set(planActiveById[planId] ?? []);
    if (!activeSet.size) return [];
    const filter = (nodes: MenuItem[]): MenuItem[] => {
      const out: MenuItem[] = [];
      for (const n of nodes) {
        const children = filter(n.children ?? []);
        if (activeSet.has(n.id) || children.length > 0) out.push({ ...n, children });
      }
      return out;
    };
    return filter(items);
  }, [crmWorkspace, visualTree, planId, planActiveById]);

  return {
    t,
    locale,
    sub,
    setSub,
    plans,
    setPlans,
    subscribers,
    planId,
    setPlanId,
    subscriberId,
    setSubscriberId,
    catalogSearch,
    setCatalogSearch,
    catalogSearchField,
    setCatalogSearchField,
    catalogNodeType,
    setCatalogNodeType,
    catalogOnlyWithRoute,
    setCatalogOnlyWithRoute,
    wizardOpen,
    setWizardOpen,
    wizardStep,
    setWizardStep,
    auditLines,
    setAuditLines,
    json,
    setJson,
    busy,
    setBusy,
    setErr,
    err,
    subscriberMenuFlags,
    arbol,
    filteredArbol,
    byPerm,
    editorMainTab,
    setEditorMainTab,
    visualTree,
    resetEditorTree,
    commitEditorTree,
    undoEditorTree,
    redoEditorTree,
    canUndoEditorTree,
    canRedoEditorTree,
    menuViewMode,
    setMenuViewMode,
    previewLayout,
    setPreviewLayout,
    copySourcePlanId,
    setCopySourcePlanId,
    copyMenu,
    setCopyMenu,
    savingAuto,
    resetPlanConfirmOpen,
    setResetPlanConfirmOpen,
    inheritPlanConfirmOpen,
    setInheritPlanConfirmOpen,
    showAnnual,
    setShowAnnual,
    newPlanModalOpen,
    setNewPlanModalOpen,
    newPlanName,
    setNewPlanName,
    newPlanMonthly,
    setNewPlanMonthly,
    newPlanYearly,
    setNewPlanYearly,
    newPlanDescription,
    setNewPlanDescription,
    newPlanInheritOnCreate,
    setNewPlanInheritOnCreate,
    newPlanInheritSourcePlanId,
    setNewPlanInheritSourcePlanId,
    importWorkspaceInputRef,
    crmPlans,
    planActiveById,
    setPlanActiveById,
    visualTreeIdSet,
    previewData,
    syncCatalog,
    handleVisualTreeChange,
    fillFromGlobal,
    loadPlanSaved,
    savePlan,
    clearPlan,
    loadTenantResolved,
    saveSubscriber,
    resetSubscriber,
    runCopyFromPlan,
    reloadArbol,
    appendAudit,
    applyMenuJsonString,
  };
}

export type UseSuperAdminMenuBuilderReturn = ReturnType<typeof useSuperAdminMenuBuilder>;
