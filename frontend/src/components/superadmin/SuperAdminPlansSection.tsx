import { LoadingState } from '../PageShell';
import { ZHPageNotice } from '../zh/ZHPageNotice';
import { ZHConfirmModal } from '../zh/ZHConfirmModal';
import { ZHBtn } from '../zh/ZHForm';
import { useSuperAdminPlansSection } from './useSuperAdminPlansSection';
import { SuperAdminPlansSectionList } from './SuperAdminPlansSectionList';
import { SuperAdminPlansSectionSubscribers } from './SuperAdminPlansSectionSubscribers';
import { SuperAdminPlansSectionFormModal } from './SuperAdminPlansSectionFormModal';
import { formatPlanMoney } from './superAdminPlansSectionUtils';
import './SuperAdminPlansSection.css';

/** Panel de planes SaaS y subscribers; el catálogo menú ↔ plan se administra en Empresas → Plan ↔ menú. */
export function SuperAdminPlansSection() {
  const s = useSuperAdminPlansSection();

  return (
    <div className="sap-plansSection">
      <div className="sap-dash-head">
        <div className="sap-dash-headRow">
          <div className="sap-dash-headText">
            <p className="sap-dash-eyebrow">{s.t('superadmin.plansDashboard.eyebrow')}</p>
            <p className="subtle sap-dash-lead">{s.t('superadmin.plansAdmin.subtitle')}</p>
          </div>
          <div className="sap-dash-actions">
            <ZHBtn
              variant="ghost"
              size="md"
              type="button"
              onClick={s.exportTenantsCsv}
              disabled={s.busy || s.filteredTenantsForTable.length === 0}
            >
              {s.t('superadmin.plansDashboard.exportCsv')}
            </ZHBtn>
            <ZHBtn variant="ghost" size="md" type="button" onClick={() => void s.loadAll()} disabled={s.busy}>
              {s.t('superadmin.plansAdmin.refresh')}
            </ZHBtn>
            <ZHBtn variant="primary" size="md" type="button" onClick={s.openCreatePlan} disabled={s.busy}>
              {s.t('superadmin.plansDashboard.newPlan')}
            </ZHBtn>
          </div>
        </div>
        <p className="subtle sap-publicHint">
          {s.t('superadmin.plansAdmin.publicHintPrefix')} <strong>{s.publicPlans.length}</strong>{' '}
          {s.t('superadmin.plansAdmin.publicHintSuffix')}
        </p>
      </div>

      {s.error ? <ZHPageNotice variant="error" message={s.t('common.errorPrefix')} detail={s.error} /> : null}

      {s.loading ? (
        <LoadingState />
      ) : (
        <>
          <div className="pg-kpis">
            <div className="pg-kpi sap-kpi--info">
              <div className="pg-kpi-bottom">
                <p className="pg-kpi-value">{s.totals?.activeSubscribers ?? s.subscribers.length}</p>
                <p className="pg-kpi-unit">
                  {s.totals
                    ? s.t('superadmin.plansDashboard.kpi.activeSubscribersSub').replace('{{total}}', String(s.totals.totalSubscribers))
                    : '—'}
                </p>
                <p className="pg-kpi-label">{s.t('superadmin.plansDashboard.kpi.activeSubscribers')}</p>
              </div>
            </div>
            <div className="pg-kpi sap-kpi--success">
              <div className="pg-kpi-bottom">
                <p className="pg-kpi-value">{formatPlanMoney(s.approxMrr, s.defaultCurrency)}</p>
                <p className="pg-kpi-unit">{s.t('superadmin.plansDashboard.kpi.mrrSub')}</p>
                <p className="pg-kpi-label">{s.t('superadmin.plansDashboard.kpi.mrr')}</p>
              </div>
            </div>
            <div className="pg-kpi sap-kpi--neutral">
              <div className="pg-kpi-bottom">
                <p className="pg-kpi-value">{s.activePlansCount}</p>
                <p className="pg-kpi-unit">{s.activePlanNames || '—'}</p>
                <p className="pg-kpi-label">{s.t('superadmin.plansDashboard.kpi.catalogPlans')}</p>
              </div>
            </div>
            <div className="pg-kpi sap-kpi--warn">
              <div className="pg-kpi-bottom">
                <p className="pg-kpi-value">{s.inactivePct}%</p>
                <p className="pg-kpi-unit">
                  {s.totals
                    ? s.t('superadmin.plansDashboard.kpi.inactiveSub').replace('{{n}}', String(s.inactiveSubscribers))
                    : '—'}
                </p>
                <p className="pg-kpi-label">{s.t('superadmin.plansDashboard.kpi.inactiveRatio')}</p>
              </div>
            </div>
          </div>

          <SuperAdminPlansSectionList
            plans={s.plans}
            subscribers={s.subscribers}
            planSubscriberStats={s.planSubscriberStats}
            approxMrr={s.approxMrr}
            inactivePct={s.inactivePct}
            defaultCurrency={s.defaultCurrency}
            busy={s.busy}
            t={s.t}
            onEditPlan={s.openEditPlan}
            onMovePlan={(index, dir) => void s.movePlan(index, dir)}
            onSetRecommended={(id) => void s.setRecommendedOnly(id)}
            onDeletePlan={s.setDeletePlanId}
          />

          <SuperAdminPlansSectionSubscribers
            plans={s.plans}
            planByCode={s.planByCode}
            filteredTenants={s.filteredTenantsForTable}
            subscriberSearch={s.subscriberSearch}
            setSubscriberSearch={s.setSubscriberSearch}
            subscriberPlanFilter={s.subscriberPlanFilter}
            setSubscriberPlanFilter={s.setSubscriberPlanFilter}
            subscriberStatusFilter={s.subscriberStatusFilter}
            setSubscriberStatusFilter={s.setSubscriberStatusFilter}
            t={s.t}
          />
        </>
      )}

      {s.planModal !== 'closed' ? (
        <SuperAdminPlansSectionFormModal
          mode={s.planModal}
          planForm={s.planForm}
          setPlanForm={s.setPlanForm}
          busy={s.busy}
          t={s.t}
          onClose={s.closePlanModal}
          onSave={() => void s.savePlan()}
        />
      ) : null}

      {s.deletePlanId ? (
        <ZHConfirmModal
          title={s.t('superadmin.plansAdmin.deleteTitle')}
          message={s.t('superadmin.plansAdmin.deleteMessage')}
          confirmLabel={s.t('common.delete')}
          loading={s.busy}
          onCancel={() => s.setDeletePlanId(null)}
          onConfirm={() => void s.handleDelete()}
        />
      ) : null}
    </div>
  );
}
