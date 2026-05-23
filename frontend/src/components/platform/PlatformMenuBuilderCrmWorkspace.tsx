import { ZHBtn } from '../zh/ZHForm';
import { ZHPageNotice } from '../zh/ZHPageNotice';
import { MenuBuilder } from '../menu-builder/MenuBuilder';
import { buildCrmComposerPanels } from './PlatformMenuBuilderCrmComposerPanels';
import { PlatformMenuBuilderCrmPreviewSection } from './PlatformMenuBuilderCrmPreviewSection';
import { PlatformMenuBuilderCrmAuditSection } from './PlatformMenuBuilderCrmAuditSection';
import { PlatformMenuBuilderCrmModals } from './PlatformMenuBuilderCrmModals';
import { usePlatformMenuBuilderCrmWorkspaceHandlers } from './usePlatformMenuBuilderCrmWorkspaceHandlers';
import type { UsePlatformMenuBuilderReturn } from './usePlatformMenuBuilder';

export type PlatformMenuBuilderCrmWorkspaceProps = UsePlatformMenuBuilderReturn;

export function PlatformMenuBuilderCrmWorkspace(props: PlatformMenuBuilderCrmWorkspaceProps) {
  const {
    t,
    locale,
    planId,
    setPlanId,
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
    busy,
    setBusy,
    setErr,
    err,
    filteredArbol,
    visualTree,
    undoEditorTree,
    redoEditorTree,
    canUndoEditorTree,
    canRedoEditorTree,
    menuViewMode,
    setMenuViewMode,
    previewLayout,
    setPreviewLayout,
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
    plans,
    setPlans,
    planActiveById,
    setPlanActiveById,
    previewData,
    syncCatalog,
    handleVisualTreeChange,
    savePlan,
    reloadArbol,
    appendAudit,
    applyMenuJsonString,
    resetEditorTree,
  } = props;

  const handlers = usePlatformMenuBuilderCrmWorkspaceHandlers({
    t,
    planId,
    setPlanId,
    setErr,
    setBusy,
    plans,
    setPlans,
    planActiveById,
    setPlanActiveById,
    visualTreeIdSet: props.visualTreeIdSet,
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
    newPlanYearly,
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
  });

  const { crmToolbar, crmMasterStack, crmLibraryStack, crmMasterFooter } = buildCrmComposerPanels({
    planId,
    setPlanId,
    setErr,
    catalogSearch,
    setCatalogSearch,
    catalogSearchField,
    setCatalogSearchField,
    catalogNodeType,
    setCatalogNodeType,
    catalogOnlyWithRoute,
    setCatalogOnlyWithRoute,
    busy,
    savingAuto,
    crmPlans,
    setPreviewLayout,
    undoEditorTree,
    redoEditorTree,
    canUndoEditorTree,
    canRedoEditorTree,
    syncCatalog,
    reloadArbol,
    savePlan,
    setInheritPlanConfirmOpen,
    setResetPlanConfirmOpen,
  });

  return (
    <div className="pg-page">
      <div className="pg-section">
        <div className="pg-section-header">
          <div className="pg-section-header-left">
            <span className="material-symbols-outlined pg-section-icon">settings_accessibility</span>
            <h2 className="pg-section-label">Panel platform</h2>
            <p className="subtle smb-workspace-subtitle">
              Gestión completa de menús, planes comerciales y configuración global
            </p>
          </div>
          <div className="smb-header-actions">
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
              <span className="material-symbols-outlined pg-icon-16">help</span>
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
          hideCrmPreview
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
          activeNodeIds={handlers.currentActiveSet}
          onToggleNodeActive={handlers.onToggleNodeActive}
          panelTitles={{
            canvas: '📁 Árbol maestro',
            library: '📋 Formularios disponibles (arrastra al árbol)',
          }}
        />
      </div>

      <PlatformMenuBuilderCrmPreviewSection
        previewData={previewData}
        previewLayout={previewLayout}
        setPreviewLayout={setPreviewLayout}
        busy={busy}
        savingAuto={savingAuto}
        activePlan={handlers.activePlan}
        showAnnual={showAnnual}
        setShowAnnual={setShowAnnual}
        locale={locale}
        planCardFeatures={handlers.planCardFeatures}
      />

      <PlatformMenuBuilderCrmAuditSection
        auditLines={auditLines}
        setAuditLines={setAuditLines}
        importWorkspaceInputRef={importWorkspaceInputRef}
        onExportAudit={handlers.exportAuditSnapshot}
        onExportWorkspace={handlers.exportWorkspaceSnapshot}
        onTriggerImport={handlers.triggerImportWorkspace}
        onImportFile={handlers.importWorkspaceSnapshot}
      />

      <PlatformMenuBuilderCrmModals
        resetPlanConfirmOpen={resetPlanConfirmOpen}
        setResetPlanConfirmOpen={setResetPlanConfirmOpen}
        inheritPlanConfirmOpen={inheritPlanConfirmOpen}
        setInheritPlanConfirmOpen={setInheritPlanConfirmOpen}
        newPlanModalOpen={newPlanModalOpen}
        setNewPlanModalOpen={setNewPlanModalOpen}
        wizardOpen={wizardOpen}
        setWizardOpen={setWizardOpen}
        wizardStep={wizardStep}
        setWizardStep={setWizardStep}
        wizardSteps={handlers.wizardSteps}
        wizardCurrentStep={handlers.wizardCurrentStep}
        planId={planId}
        activePlan={handlers.activePlan}
        previousPlanForInheritance={handlers.previousPlanForInheritance}
        crmPlans={crmPlans}
        newPlanName={newPlanName}
        setNewPlanName={setNewPlanName}
        newPlanMonthly={newPlanMonthly}
        setNewPlanMonthly={setNewPlanMonthly}
        newPlanYearly={newPlanYearly}
        setNewPlanYearly={setNewPlanYearly}
        newPlanDescription={newPlanDescription}
        setNewPlanDescription={setNewPlanDescription}
        newPlanInheritOnCreate={newPlanInheritOnCreate}
        setNewPlanInheritOnCreate={setNewPlanInheritOnCreate}
        newPlanInheritSourcePlanId={newPlanInheritSourcePlanId}
        setNewPlanInheritSourcePlanId={setNewPlanInheritSourcePlanId}
        setPlanActiveById={setPlanActiveById}
        appendAudit={appendAudit}
        onCreateNewPlan={() => void handlers.createNewPlan()}
        onInheritFromPreviousPlan={handlers.inheritFromPreviousPlanNow}
      />
    </div>
  );
}
