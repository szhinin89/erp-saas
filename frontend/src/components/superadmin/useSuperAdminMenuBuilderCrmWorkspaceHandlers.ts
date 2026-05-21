import { useMemo } from 'react';
import type { EditorMenuItem } from '../menu-builder/menuBuilderTypes';
import { superAdminService } from '../../modules/superadmin/api/superAdminService';
import { formatApiRequestError } from '../../modules/lib/apiError';
import {
  parseImportedCrmWorkspace,
  resolveInheritedActiveNodeIds,
  type CrmWorkspaceExportPayload,
} from './crmPlanIntegrity';
import {
  downloadJsonFile,
  makeExportFileName,
  MENU_BUILDER_SCHEMA_VERSION,
  type CrmLocalPlan,
} from './superAdminMenuBuilderUtils';
import type { UseSuperAdminMenuBuilderReturn } from './useSuperAdminMenuBuilder';
import type { WizardStep } from './SuperAdminMenuBuilderCrmModals';

export type SuperAdminMenuBuilderCrmWorkspaceHandlersParams = Pick<
  UseSuperAdminMenuBuilderReturn,
  | 't'
  | 'planId'
  | 'setPlanId'
  | 'setErr'
  | 'setBusy'
  | 'plans'
  | 'setPlans'
  | 'planActiveById'
  | 'setPlanActiveById'
  | 'visualTreeIdSet'
  | 'visualTree'
  | 'resetEditorTree'
  | 'setPreviewLayout'
  | 'previewLayout'
  | 'auditLines'
  | 'setAuditLines'
  | 'crmPlans'
  | 'appendAudit'
  | 'applyMenuJsonString'
  | 'importWorkspaceInputRef'
  | 'newPlanName'
  | 'newPlanMonthly'
  | 'newPlanYearly'
  | 'newPlanInheritOnCreate'
  | 'newPlanInheritSourcePlanId'
  | 'setNewPlanModalOpen'
  | 'setNewPlanName'
  | 'setNewPlanMonthly'
  | 'setNewPlanYearly'
  | 'setNewPlanDescription'
  | 'setNewPlanInheritOnCreate'
  | 'setNewPlanInheritSourcePlanId'
  | 'wizardStep'
>;

export function useSuperAdminMenuBuilderCrmWorkspaceHandlers(params: SuperAdminMenuBuilderCrmWorkspaceHandlersParams) {
  const {
    t,
    planId,
    setPlanId,
    setErr,
    setBusy,
    plans,
    setPlans,
    planActiveById,
    setPlanActiveById,
    visualTreeIdSet,
    visualTree,
    resetEditorTree,
    setPreviewLayout,
    previewLayout,
    auditLines,
    setAuditLines,
    crmPlans,
    appendAudit,
    applyMenuJsonString,
    importWorkspaceInputRef,
    newPlanName,
    newPlanMonthly,
    newPlanInheritOnCreate,
    newPlanInheritSourcePlanId,
    setNewPlanModalOpen,
    setNewPlanName,
    setNewPlanMonthly,
    setNewPlanYearly,
    setNewPlanDescription,
    setNewPlanInheritOnCreate,
    setNewPlanInheritSourcePlanId,
    wizardStep,
  } = params;

  const activePlan = crmPlans.find((p) => p.id === planId);
  const activePlanIndex = crmPlans.findIndex((p) => p.id === planId);
  const previousPlanForInheritance = activePlanIndex > 0 ? crmPlans[activePlanIndex - 1] : null;

  const wizardSteps = useMemo(
    () =>
      [
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
      ] as const,
    [],
  );
  const wizardCurrentStep: WizardStep = wizardSteps[wizardStep] ?? wizardSteps[0];
  const currentActiveSet = useMemo(() => new Set(planActiveById[planId] ?? []), [planActiveById, planId]);

  const planCardFeatures = useMemo(
    () =>
      visualTree
        .filter((x) => currentActiveSet.has(x.uid))
        .map((x) => x.nombre)
        .slice(0, 14),
    [visualTree, currentActiveSet],
  );

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
      const newPlanId = await superAdminService.createCommercialPlan({
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

      const nextPlans = await superAdminService.listCommercialPlansAdmin();
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
      const next = await superAdminService.listCommercialPlansAdmin();
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

  return {
    activePlan,
    previousPlanForInheritance,
    wizardSteps,
    wizardCurrentStep,
    currentActiveSet,
    planCardFeatures,
    onToggleNodeActive,
    createNewPlan,
    inheritFromPreviousPlanNow,
    exportWorkspaceSnapshot,
    exportAuditSnapshot,
    triggerImportWorkspace,
    importWorkspaceSnapshot,
  };
}

export type CrmWorkspaceDerivedState = {
  activePlan: CrmLocalPlan | undefined;
  previousPlanForInheritance: CrmLocalPlan | null;
  wizardSteps: readonly WizardStep[];
  wizardCurrentStep: WizardStep;
  currentActiveSet: Set<string>;
  planCardFeatures: string[];
};
