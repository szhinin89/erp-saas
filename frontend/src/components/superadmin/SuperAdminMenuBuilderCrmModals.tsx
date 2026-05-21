import { ZHBtn, ZHField } from '../zh/ZHForm';
import { ZHConfirmModal } from '../zh/ZHConfirmModal';
import type { CrmLocalPlan } from './superAdminMenuBuilderUtils';

export type WizardStep = {
  title: string;
  body: string;
};

export type SuperAdminMenuBuilderCrmModalsProps = {
  resetPlanConfirmOpen: boolean;
  setResetPlanConfirmOpen: (v: boolean) => void;
  inheritPlanConfirmOpen: boolean;
  setInheritPlanConfirmOpen: (v: boolean) => void;
  newPlanModalOpen: boolean;
  setNewPlanModalOpen: (v: boolean) => void;
  wizardOpen: boolean;
  setWizardOpen: (v: boolean) => void;
  wizardStep: number;
  setWizardStep: React.Dispatch<React.SetStateAction<number>>;
  wizardSteps: readonly WizardStep[];
  wizardCurrentStep: WizardStep;
  planId: string;
  activePlan: CrmLocalPlan | undefined;
  previousPlanForInheritance: CrmLocalPlan | null;
  crmPlans: CrmLocalPlan[];
  newPlanName: string;
  setNewPlanName: (v: string) => void;
  newPlanMonthly: string;
  setNewPlanMonthly: (v: string) => void;
  newPlanYearly: string;
  setNewPlanYearly: (v: string) => void;
  newPlanDescription: string;
  setNewPlanDescription: (v: string) => void;
  newPlanInheritOnCreate: boolean;
  setNewPlanInheritOnCreate: (v: boolean) => void;
  newPlanInheritSourcePlanId: string;
  setNewPlanInheritSourcePlanId: (v: string) => void;
  setPlanActiveById: React.Dispatch<React.SetStateAction<Record<string, string[]>>>;
  appendAudit: (line: string) => void;
  onCreateNewPlan: () => void;
  onInheritFromPreviousPlan: () => void;
};

export function SuperAdminMenuBuilderCrmModals({
  resetPlanConfirmOpen,
  setResetPlanConfirmOpen,
  inheritPlanConfirmOpen,
  setInheritPlanConfirmOpen,
  newPlanModalOpen,
  setNewPlanModalOpen,
  wizardOpen,
  setWizardOpen,
  wizardStep,
  setWizardStep,
  wizardSteps,
  wizardCurrentStep,
  planId,
  activePlan,
  previousPlanForInheritance,
  crmPlans,
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
  setPlanActiveById,
  appendAudit,
  onCreateNewPlan,
  onInheritFromPreviousPlan,
}: SuperAdminMenuBuilderCrmModalsProps) {
  return (
    <>
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
            void onInheritFromPreviousPlan();
          }}
        />
      ) : null}

      {newPlanModalOpen ? (
        <div className="zh-modal-overlay" role="dialog" aria-modal="true" aria-label="Crear nuevo plan">
          <div className="zh-modal pg-modal--480">
            <div className="zh-modal-header">
              <div className="smb-modal-head">
                <span className="material-symbols-outlined smb-modal-head-icon">add_circle</span>
                <h3 className="smb-modal-head-title">Crear nuevo plan comercial</h3>
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
              <div className="smb-inherit-stack">
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
                <ZHBtn variant="primary" size="md" type="button" onClick={onCreateNewPlan}>
                  Crear plan
                </ZHBtn>
              </div>
            </div>
          </div>
        </div>
      ) : null}
      {wizardOpen ? (
        <div className="zh-modal-overlay" role="dialog" aria-modal="true" aria-label="Guía rápida de configuración">
          <div className="zh-modal pg-modal--sm">
            <div className="zh-modal-header">
              <div className="smb-modal-head">
                <span className="material-symbols-outlined smb-modal-head-icon">help_center</span>
                <h3 className="smb-modal-head-title">{wizardCurrentStep.title}</h3>
              </div>
            </div>
            <div className="zh-modal-body">
              <p className="subtle">{wizardCurrentStep.body}</p>
              <p className="subtle smb-wizard-step">
                Paso {wizardStep + 1} de {wizardSteps.length}
              </p>
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
    </>
  );
}
