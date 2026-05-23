import { useEffect } from 'react';
import type { MenuBuilderViewMode } from '../menu-builder/MenuBuilder';
import type { MenuPreviewLayout } from '../menu-builder/MenuPreview';
import type { FuncionalidadArbolDto } from '../../modules/platform/api/platformService';
import {
  normalizeParsedMenuGroups,
  readPlanCustomMenuBarLayout,
  serializeEditorTreeToMenuJson,
  sessionGroupsToEditorTree,
  type EditorMenuItem,
} from '../menu-builder/menuBuilderTypes';
import { platformService } from '../../modules/platform/api/platformService';
import { adminNavigationToSessionMenu } from '../../modules/platform/adminNavigationToSessionMenu';
import { normalizePlanActiveById } from './crmPlanIntegrity';
import {
  cloneDefaultCrmTreeSeed,
  CRM_AUDIT_STORAGE_KEY,
  CRM_PLAN_ACTIVE_STORAGE_KEY,
  CRM_TREE_STORAGE_KEY,
  type CrmLocalPlan,
  type EditorMainTab,
  type SubMode,
} from './platformMenuBuilderUtils';

export type PlatformMenuBuilderCrmEffectsParams = {
  crmWorkspace: boolean;
  planId: string;
  setPlanId: (v: string) => void;
  setSub: (v: SubMode) => void;
  setEditorMainTab: (v: EditorMainTab) => void;
  setMenuViewMode: (v: MenuBuilderViewMode) => void;
  setWizardOpen: (v: boolean) => void;
  setWizardStep: (v: number) => void;
  setPreviewLayout: (v: MenuPreviewLayout) => void;
  setPlanActiveById: React.Dispatch<React.SetStateAction<Record<string, string[]>>>;
  setJson: (v: string) => void;
  crmPlans: CrmLocalPlan[];
  visualTree: EditorMenuItem[];
  visualTreeIds: string[];
  visualTreeIdSet: Set<string>;
  auditLines: string[];
  planActiveById: Record<string, string[]>;
  json: string;
  previewLayout: MenuPreviewLayout;
  visualLabelKey: string;
  byPerm: Map<string, FuncionalidadArbolDto>;
  applyMenuJsonString: (raw: string) => void;
  appendAudit: (line: string) => void;
  resetEditorTree: (tree: EditorMenuItem[]) => void;
  persistCrmPlan: () => Promise<void>;
  hydratingPlanRef: React.MutableRefObject<boolean>;
  planSwitchClock: React.MutableRefObject<number>;
};

export function usePlatformMenuBuilderCrmEffects(params: PlatformMenuBuilderCrmEffectsParams): void {
  const {
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
  } = params;

  useEffect(() => {
    if (!crmWorkspace) return;
    setSub('plan');
    setEditorMainTab('visual');
    setMenuViewMode('split');
    setWizardOpen(false);
    setWizardStep(0);
  }, [crmWorkspace, setEditorMainTab, setMenuViewMode, setSub, setWizardOpen, setWizardStep]);

  useEffect(() => {
    if (!crmWorkspace) return;
    if (planId) return;
    const defaultPlan = crmPlans.find((p) => p.isPubliclyVisible) ?? crmPlans[0];
    if (defaultPlan) {
      setPlanId(defaultPlan.id);
      setPreviewLayout(defaultPlan.layout);
    }
  }, [crmWorkspace, planId, crmPlans, setPlanId, setPreviewLayout]);

  useEffect(() => {
    if (!crmWorkspace || !planId) return;
    if (typeof window === 'undefined') return;
    let cancelled = false;
    hydratingPlanRef.current = true;
    planSwitchClock.current = Date.now();
    void (async () => {
      try {
        const bundle = await platformService.getPlanMenu(planId);
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
          const inherited = await platformService.getPlanMenu(previousPlan.id);
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
            await platformService.setPlanMenuJson(planId, inheritedPretty, nextLayout);
            appendAudit(`Plan ${planId} heredó menú desde ${previousPlan.code}`);
            return;
          }
        } catch {
          // fallback to global menu below.
        }
      }

      try {
        const menu = await platformService.getNavigationMenu();
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
  }, [
    appendAudit,
    applyMenuJsonString,
    byPerm,
    crmPlans,
    crmWorkspace,
    hydratingPlanRef,
    planId,
    planSwitchClock,
    previewLayout,
    resetEditorTree,
    setJson,
    setPlanActiveById,
    setPreviewLayout,
    visualLabelKey,
  ]);

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
    if (!crmWorkspace) return;
    if (!planId) return;
    if (!visualTreeIds.length) return;
    setPlanActiveById((prev) => {
      const exists = prev[planId];
      if (exists) return prev;
      return { ...prev, [planId]: [...visualTreeIds] };
    });
  }, [crmWorkspace, planId, setPlanActiveById, visualTreeIds]);

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
  }, [crmWorkspace, crmPlans, setPlanActiveById, visualTreeIdSet]);

  useEffect(() => {
    if (!planId) return;
    if (hydratingPlanRef.current) return;
    if (Date.now() - planSwitchClock.current < 900) return;
    const tid = window.setTimeout(() => {
      void persistCrmPlan();
    }, 900);
    return () => window.clearTimeout(tid);
  }, [planId, json, previewLayout, persistCrmPlan, hydratingPlanRef, planSwitchClock]);
}
