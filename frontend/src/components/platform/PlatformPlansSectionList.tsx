import { NavLink } from 'react-router-dom';
import { EmptyState, Badge } from '../PageShell';
import { ZHCard } from '../zh/ZHCard';
import { ZHBtn } from '../zh/ZHForm';
import type { CommercialPlanAdmin, PlatformSubscriber } from '../../modules/platform/api/platformService';
import { formatPlanMoney, planVisualTier } from './platformPlansSectionUtils';

type PlanSubscriberStats = { counts: Map<string, number>; max: number };

type Props = {
  plans: CommercialPlanAdmin[];
  subscribers: PlatformSubscriber[];
  planSubscriberStats: PlanSubscriberStats;
  approxMrr: number;
  inactivePct: number;
  defaultCurrency: string;
  busy: boolean;
  t: (key: string) => string;
  onEditPlan: (plan: CommercialPlanAdmin) => void;
  onMovePlan: (index: number, dir: -1 | 1) => void;
  onSetRecommended: (planId: string) => void;
  onDeletePlan: (planId: string) => void;
};

export function PlatformPlansSectionList({
  plans,
  subscribers,
  planSubscriberStats,
  approxMrr,
  inactivePct,
  defaultCurrency,
  busy,
  t,
  onEditPlan,
  onMovePlan,
  onSetRecommended,
  onDeletePlan,
}: Props) {
  if (plans.length === 0) {
    return (
      <ZHCard>
        <EmptyState message={t('platform.plans.empty')} />
      </ZHCard>
    );
  }

  const activeSubscriberCount = subscribers.filter((tn) => tn.isActive).length;

  return (
    <>
      <div className="sap-pricing-grid">
        {plans.map((plan, index) => {
          const tier = planVisualTier(plan.code);
          const codeKey = plan.code.trim().toLowerCase();
          const taglineKey = `platform.plansCard.tagline.${codeKey}`;
          const taglineResolved = t(taglineKey);
          const taglineText =
            taglineResolved !== taglineKey ? taglineResolved : t('platform.plansCard.taglineFallback');
          const subscriberCount = planSubscriberStats.counts.get(codeKey) ?? 0;
          const usagePct =
            planSubscriberStats.max > 0 ? Math.round((subscriberCount / planSubscriberStats.max) * 100) : 0;
          const billingCycle = (plan.billingCycle ?? 'monthly').toLowerCase();
          const billingKey = `platform.plansCard.billingSuffix.${billingCycle}`;
          const billingSuffix = t(billingKey);
          const billingLabel =
            billingSuffix !== billingKey ? billingSuffix : t('platform.plansCard.billingSuffix.monthly');

          return (
            <article
              key={plan.id}
              className={[
                'sap-pricing-card',
                `sap-pricing-card--tier-${tier}`,
                plan.isRecommended ? 'sap-pricing-card--popular' : '',
                plan.isActive ? '' : 'sap-pricing-card--inactive',
              ]
                .filter(Boolean)
                .join(' ')}
            >
              <div className="sap-pricing-card-inner">
                <div className="sap-pricing-topBadges">
                  {plan.isRecommended ? (
                    <span className="sap-pricing-ribbon">{t('platform.plansCard.mostPopular')}</span>
                  ) : null}
                  <span className="badge badge--upper mono sap-pricing-planBadge">{plan.shortLabel ?? plan.code.toUpperCase()}</span>
                </div>
                <h3 className="sap-pricing-title">
                  {plan.name}
                  {plan.isRecommended ? <span className="sap-pricing-titleStar" aria-hidden> ★</span> : null}
                </h3>
                <p className="sap-pricing-subtitle">{taglineText}</p>
                <div className="sap-pricing-priceRow">
                  <span className="sap-pricing-price">{formatPlanMoney(plan.priceAmount, plan.currency)}</span>
                  <span className="sap-pricing-period">/{billingLabel}</span>
                </div>
                {plan.features.length > 0 && (
                  <div className="sap-pricing-features">
                    {plan.features.map((f) => (
                      <div key={f.featureId} className="sap-pricing-feature">
                        <span
                          className={`material-symbols-outlined sap-pricing-feature-icon sap-pricing-feature-icon--${f.isIncluded ? 'check' : 'x'}`}
                        >
                          {f.isIncluded ? 'check_circle' : 'cancel'}
                        </span>
                        <span className={`sap-pricing-feature-text${f.isIncluded ? '' : ' sap-pricing-feature-text--off'}`}>
                          {f.featureName}
                          {f.limitPerPeriod != null ? ` (${f.limitPerPeriod})` : ''}
                        </span>
                      </div>
                    ))}
                  </div>
                )}
                <div className="sap-pricing-usage">
                  <div className="sap-pricing-usageLine">
                    <span>
                      {subscriberCount} {t('platform.plansCard.subscribersUnit')}
                    </span>
                    <span className="sap-pricing-usagePct">{usagePct}%</span>
                  </div>
                  <div className="sap-pricing-bar" aria-hidden>
                    <svg viewBox="0 0 100 6" preserveAspectRatio="none" className="sap-pricing-barSvg">
                      <rect
                        className="sap-pricing-barRect"
                        x="0"
                        y="0"
                        width={Math.max(0, Math.min(100, usagePct))}
                        height="6"
                        rx="3"
                      />
                    </svg>
                  </div>
                </div>
                <div className="sap-pricing-metaRow subtle">
                  <Badge label={plan.isActive ? t('common.active') : t('common.inactive')} variant={plan.isActive ? 'green' : 'gray'} />
                  {plan.isPubliclyVisible ? (
                    <Badge label={t('platform.plansAdmin.public')} variant="green" />
                  ) : (
                    <Badge label={t('platform.plansAdmin.hidden')} variant="gray" />
                  )}
                  <span className="mono">{plan.code}</span>
                </div>
                <footer className="sap-pricing-footer">
                  <div className="sap-pricing-footerMain">
                    <ZHBtn variant="secondary" size="md" type="button" onClick={() => onEditPlan(plan)} disabled={busy}>
                      {t('common.edit')}
                    </ZHBtn>
                    <NavLink
                      className="zh-btn zh-btn--ghost zh-btn--md sap-pricing-linkBtn"
                      to={`/platform/plans?plan=${encodeURIComponent(plan.code)}`}
                    >
                      {t('platform.plansCard.viewSubscribers')}
                    </NavLink>
                  </div>
                  <div className="sap-pricing-footerExtra">
                    <ZHBtn variant="ghost" size="sm" type="button" onClick={() => onMovePlan(index, -1)} disabled={busy || index === 0}>
                      {t('platform.plansAdmin.up')}
                    </ZHBtn>
                    <ZHBtn
                      variant="ghost"
                      size="sm"
                      type="button"
                      onClick={() => onMovePlan(index, 1)}
                      disabled={busy || index === plans.length - 1}
                    >
                      {t('platform.plansAdmin.down')}
                    </ZHBtn>
                    <ZHBtn
                      variant="ghost"
                      size="sm"
                      type="button"
                      onClick={() => onSetRecommended(plan.id)}
                      disabled={busy || plan.isRecommended}
                    >
                      {t('platform.plansAdmin.recommend')}
                    </ZHBtn>
                    <ZHBtn variant="destructive" size="sm" type="button" onClick={() => onDeletePlan(plan.id)} disabled={busy}>
                      {t('common.delete')}
                    </ZHBtn>
                  </div>
                </footer>
              </div>
            </article>
          );
        })}
      </div>

      <div className="sap-plans-summary">
        <div className="sap-plans-summary-left">
          <div className="sap-plans-summary-icon">
            <span className="material-symbols-outlined">loyalty</span>
          </div>
          <div>
            <p className="sap-plans-summary-label">{t('platform.plansDashboard.summary.label')}</p>
            <p className="sap-plans-summary-value">
              {subscribers.length} {t('platform.plansDashboard.summary.subscriptions')}
            </p>
          </div>
        </div>
        <div className="sap-plans-summary-stats">
          <div className="sap-plans-summary-stat">
            <p className="sap-plans-summary-stat-label">MRR</p>
            <p className="sap-plans-summary-stat-value">{formatPlanMoney(approxMrr, defaultCurrency)}</p>
          </div>
          <div className="sap-plans-summary-stat">
            <p className="sap-plans-summary-stat-label">{t('platform.plansDashboard.kpi.inactiveRatio')}</p>
            <p className="sap-plans-summary-stat-value">{inactivePct}%</p>
          </div>
          <div className="sap-plans-summary-stat">
            <p className="sap-plans-summary-stat-label">ARPU</p>
            <p className="sap-plans-summary-stat-value sap-plans-summary-stat-value--success">
              {activeSubscriberCount > 0 ? formatPlanMoney(approxMrr / activeSubscriberCount, defaultCurrency) : '—'}
            </p>
          </div>
        </div>
      </div>
    </>
  );
}
