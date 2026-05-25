import { useCallback } from 'react';
import type { MenuPreviewLayout } from '../menu-builder/MenuPreview';
import {
  validateSessionMenuGroups,
  type EditorMenuItem,
} from '../menu-builder/menuBuilderTypes';
import {
  platformService,
  type CommercialPlanAdmin,
} from '../../modules/platform/api/platformService';
import type { SessionMenuGroupDto } from '../../types/access';
import { formatApiRequestError } from '../../modules/lib/apiError';
import { adminNavigationToSessionMenu } from '../../modules/platform/adminNavigationToSessionMenu';
import { buildPersistableMenuJson } from './platformMenuBuilderPersist';

export type PlatformMenuBuilderActionsParams = {
  crmWorkspace: boolean;
  t: (key: string) => string;
  planId: string;
  subscriberId: string;
  json: string;
  previewLayout: MenuPreviewLayout;
  visualLabelKey: string;
  visualTree: EditorMenuItem[];
  planActiveById: Record<string, string[]>;
  copySourcePlanId: string;
  copyMenu: boolean;
  setBusy: (v: boolean) => void;
  setErr: (v: string) => void;
  setPlans: (v: CommercialPlanAdmin[]) => void;
  setPreviewLayout: (v: MenuPreviewLayout) => void;
  setSavingAuto: (v: boolean) => void;
  setPlanActiveById: React.Dispatch<React.SetStateAction<Record<string, string[]>>>;
  setSubscriberMenuFlags: (v: {
    hasCustomMenu: boolean;
    usedPlanMenu: boolean;
    usedGlobalFallback: boolean;
  } | null) => void;
  applyMenuJsonString: (raw: string) => void;
  appendAudit: (line: string) => void;
  commitEditorTree: (next: EditorMenuItem[], record: boolean) => void;
  reloadArbol: () => Promise<void>;
  hydratingPlanRef: React.MutableRefObject<boolean>;
  planSwitchClock: React.MutableRefObject<number>;
};

export function usePlatformMenuBuilderActions(params: PlatformMenuBuilderActionsParams) {
  const {
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
  } = params;

  const getPersistableMenuJson = useCallback(
    () =>
      buildPersistableMenuJson({
        crmWorkspace,
        json,
        planId,
        planActiveById,
        previewLayout,
        visualLabelKey,
        visualTree,
      }),
    [crmWorkspace, json, planId, planActiveById, previewLayout, visualLabelKey, visualTree],
  );

  const syncCatalog = async () => {
    setBusy(true);
    setErr('');
    try {
      await platformService.syncFuncionalidadesCatalogo();
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
    [commitEditorTree, crmWorkspace, planId, setPlanActiveById, visualTree],
  );

  const fillFromGlobal = async () => {
    setErr('');
    try {
      const menu = await platformService.getNavigationMenu();
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
      const bundle = await platformService.getPlanMenu(planId);
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
      const trimmed = getPersistableMenuJson();
      if (trimmed.length > 0) {
        const groups = JSON.parse(trimmed) as SessionMenuGroupDto[];
        const v = validateSessionMenuGroups(groups);
        if (v.length) {
          setErr(v.join(' '));
          return;
        }
      }
      await platformService.setPlanMenuJson(
        planId,
        trimmed.length === 0 ? null : trimmed,
        previewLayout,
      );
      const next = await platformService.listCommercialPlansAdmin();
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
      await platformService.setPlanMenuJson(planId, null, 'horizontal');
      applyMenuJsonString('[]');
      const next = await platformService.listCommercialPlansAdmin();
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

  const loadSubscriberResolved = async () => {
    if (!subscriberId) return;
    setErr('');
    try {
      const r = await platformService.getSubscriberResolvedMenu(subscriberId);
      applyMenuJsonString(JSON.stringify(r.menu, null, 2));
      setSubscriberMenuFlags({
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

  const saveSubscriber = async () => {
    if (!subscriberId) return;
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
      await platformService.putSubscriberCustomMenu(subscriberId, json);
      await loadSubscriberResolved();
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

  const resetSubscriber = async () => {
    if (!subscriberId) return;
    setBusy(true);
    setErr('');
    try {
      await platformService.deleteSubscriberCustomMenu(subscriberId);
      await loadSubscriberResolved();
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
      setErr(t('platform.menuBuilder.copyPickPlans'));
      return;
    }
    if (!copyMenu) {
      setErr(t('platform.menuBuilder.copyPickOptions'));
      return;
    }
    setBusy(true);
    setErr('');
    try {
      await platformService.copyPlanFrom(planId, copySourcePlanId, {
        copyMenu,
      });
      const next = await platformService.listCommercialPlansAdmin();
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
    const trimmed = getPersistableMenuJson();
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
      await platformService.setPlanMenuJson(
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
  }, [appendAudit, getPersistableMenuJson, hydratingPlanRef, planId, planSwitchClock, previewLayout, setErr, setSavingAuto, t]);

  return {
    syncCatalog,
    handleVisualTreeChange,
    fillFromGlobal,
    loadPlanSaved,
    savePlan,
    clearPlan,
    loadSubscriberResolved,
    saveSubscriber,
    resetSubscriber,
    runCopyFromPlan,
    persistCrmPlan,
  };
}
