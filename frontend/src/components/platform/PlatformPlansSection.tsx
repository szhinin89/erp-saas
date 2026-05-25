import { LoadingState } from '../PageShell';
import { ZHPageNotice } from '../zh/ZHPageNotice';
import { ZHConfirmModal } from '../zh/ZHConfirmModal';
import { ZHBtn } from '../zh/ZHForm';
import { usePlatformPlansSection } from './usePlatformPlansSection';
import { PlatformPlansSectionList } from './PlatformPlansSectionList';
import { PlatformPlansSectionSubscribers } from './PlatformPlansSectionSubscribers';
import { PlatformPlansSectionFormModal } from './PlatformPlansSectionFormModal';
import { formatPlanMoney } from './platformPlansSectionUtils';
import './PlatformPlansSection.css';

/** Panel de planes SaaS y subscribers; el catálogo menú ↔ plan se administra en Empresas → Plan ↔ menú. */
export function PlatformPlansSection() {
  const s = usePlatformPlansSection();

  return (
    <div className="sap-plansSection">
      <div className="sap-dash-head">
        <div className="sap-dash-headRow">
          <div className="sap-dash-headText">
            <p className="sap-dash-eyebrow">{s.t('platform.plansDashboard.eyebrow')}</p>
            <p className="subtle sap-dash-lead">{s.t('platform.plansAdmin.subtitle')}</p>
          </div>
          <div className="sap-dash-actions">
            <ZHBtn
              variant="ghost"
              size="md"
              type="button"
              onClick={s.exportSubscribersCsv}
              disabled={s.busy || s.filteredSubscribersForTable.length === 0}
            >
              {s.t('platform.plansDashboard.exportCsv')}
            </ZHBtn>
            <ZHBtn variant="ghost" size="md" type="button" onClick={() => void s.loadAll()} disabled={s.busy}>
              {s.t('platform.plansAdmin.refresh')}
            </ZHBtn>
            <ZHBtn variant="primary" size="md" type="button" onClick={s.openCreatePlan} disabled={s.busy}>
              {s.t('platform.plansDashboard.newPlan')}
            </ZHBtn>
          </div>
        </div>
        <p className="subtle sap-publicHint">
          {s.t('platform.plansAdmin.publicHintPrefix')} <strong>{s.publicPlans.length}</strong>{' '}
          {s.t('platform.plansAdmin.publicHintSuffix')}
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
                    ? s.t('platform.plansDashboard.kpi.activeSubscribersSub').replace('{{total}}', String(s.totals.totalSubscribers))
                    : '—'}
                </p>
                <p className="pg-kpi-label">{s.t('platform.plansDashboard.kpi.activeSubscribers')}</p>
              </div>
            </div>
            <div className="pg-kpi sap-kpi--success">
              <div className="pg-kpi-bottom">
                <p className="pg-kpi-value">{formatPlanMoney(s.approxMrr, s.defaultCurrency)}</p>
                <p className="pg-kpi-unit">{s.t('platform.plansDashboard.kpi.mrrSub')}</p>
                <p className="pg-kpi-label">{s.t('platform.plansDashboard.kpi.mrr')}</p>
              </div>
            </div>
            <div className="pg-kpi sap-kpi--neutral">
              <div className="pg-kpi-bottom">
                <p className="pg-kpi-value">{s.activePlansCount}</p>
                <p className="pg-kpi-unit">{s.activePlanNames || '—'}</p>
                <p className="pg-kpi-label">{s.t('platform.plansDashboard.kpi.catalogPlans')}</p>
              </div>
            </div>
            <div className="pg-kpi sap-kpi--warn">
              <div className="pg-kpi-bottom">
                <p className="pg-kpi-value">{s.inactivePct}%</p>
                <p className="pg-kpi-unit">
                  {s.totals
                    ? s.t('platform.plansDashboard.kpi.inactiveSub').replace('{{n}}', String(s.inactiveSubscribers))
                    : '—'}
                </p>
                <p className="pg-kpi-label">{s.t('platform.plansDashboard.kpi.inactiveRatio')}</p>
              </div>
            </div>
          </div>

          <PlatformPlansSectionList
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

          <PlatformPlansSectionSubscribers
            plans={s.plans}
            planByCode={s.planByCode}
            filteredSubscribers={s.filteredSubscribersForTable}
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
        <PlatformPlansSectionFormModal
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
          title={s.t('platform.plansAdmin.deleteTitle')}
          message={s.t('platform.plansAdmin.deleteMessage')}
          confirmLabel={s.t('common.delete')}
          loading={s.busy}
          onCancel={() => s.setDeletePlanId(null)}
          onConfirm={() => void s.handleDelete()}
        />
      ) : null}
    </div>
  );
}
