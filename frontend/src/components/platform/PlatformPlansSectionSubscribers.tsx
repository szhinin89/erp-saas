import { useNavigate } from 'react-router-dom';
import { EmptyState } from '../PageShell';
import { ZHCard } from '../zh/ZHCard';
import { ZHBtn } from '../zh/ZHForm';
import { ZHCardSection } from '../zh/ZHLayout';
import type { CommercialPlanAdmin, PlatformSubscriber } from '../../modules/platform/api/platformService';
import { goToSubscriberDetail } from '../../navigation/platformSubscriberDetailNav';
import { formatPlanMoney, monthlyEquivalentPrice, planVisualTier } from './platformPlansSectionUtils';

type Props = {
  plans: CommercialPlanAdmin[];
  planByCode: Map<string, CommercialPlanAdmin>;
  filteredTenants: PlatformSubscriber[];
  subscriberSearch: string;
  setSubscriberSearch: (value: string) => void;
  subscriberPlanFilter: string;
  setSubscriberPlanFilter: (value: string) => void;
  subscriberStatusFilter: 'all' | 'active' | 'inactive';
  setSubscriberStatusFilter: (value: 'all' | 'active' | 'inactive') => void;
  t: (key: string) => string;
};

export function PlatformPlansSectionSubscribers({
  plans,
  planByCode,
  filteredTenants,
  subscriberSearch,
  setSubscriberSearch,
  subscriberPlanFilter,
  setSubscriberPlanFilter,
  subscriberStatusFilter,
  setSubscriberStatusFilter,
  t,
}: Props) {
  const navigate = useNavigate();

  return (
    <ZHCard>
      <ZHCardSection title={t('superadmin.plansDashboard.subscribersSectionTitle')}>
        <div className="sap-subscriber-toolbar">
          <input
            type="search"
            className="zh-input sap-subscriber-search"
            value={subscriberSearch}
            onChange={(e) => setSubscriberSearch(e.target.value)}
            placeholder={t('superadmin.plansDashboard.subscriberSearchPlaceholder')}
            aria-label={t('superadmin.plansDashboard.subscriberSearchPlaceholder')}
          />
          <select
            className="zh-input sap-subscriber-select"
            value={subscriberPlanFilter}
            onChange={(e) => setSubscriberPlanFilter(e.target.value)}
            aria-label={t('superadmin.plansDashboard.filterPlan')}
          >
            <option value="">{t('superadmin.plansDashboard.filterAllPlans')}</option>
            {plans.map((p) => (
              <option key={p.id} value={p.code.trim().toLowerCase()}>
                {p.shortLabel ?? p.name} ({p.code})
              </option>
            ))}
          </select>
          <select
            className="zh-input sap-subscriber-select"
            value={subscriberStatusFilter}
            onChange={(e) => setSubscriberStatusFilter(e.target.value as 'all' | 'active' | 'inactive')}
            aria-label={t('superadmin.plansDashboard.filterStatus')}
          >
            <option value="all">{t('superadmin.plansDashboard.filterAllStatuses')}</option>
            <option value="active">{t('superadmin.plansDashboard.statusActive')}</option>
            <option value="inactive">{t('superadmin.plansDashboard.statusInactive')}</option>
          </select>
        </div>
        {filteredTenants.length === 0 ? (
          <EmptyState message={t('superadmin.plansDashboard.subscribersEmpty')} />
        ) : (
          <div className="sap-subscriber-tableWrap">
            <table className="table">
              <thead>
                <tr>
                  <th>{t('superadmin.plansDashboard.col.company')}</th>
                  <th>{t('superadmin.plansDashboard.col.plan')}</th>
                  <th>{t('superadmin.plansDashboard.col.users')}</th>
                  <th>{t('superadmin.plansDashboard.col.sinceExpires')}</th>
                  <th>{t('superadmin.plansDashboard.col.mrr')}</th>
                  <th>{t('superadmin.plansDashboard.col.status')}</th>
                  <th>{t('superadmin.plansDashboard.col.actions')}</th>
                </tr>
              </thead>
              <tbody>
                {filteredTenants.map((tn) => {
                  const pc = (tn.planCode ?? '').trim().toLowerCase();
                  const p = pc ? planByCode.get(pc) : undefined;
                  const tier = planVisualTier(pc || 'default');
                  const planLabel = p ? (p.shortLabel ?? p.name) : tn.planCode || '—';
                  const mrrVal = p?.isActive ? monthlyEquivalentPrice(p) : 0;
                  const showMrr = p?.isActive && mrrVal > 0;

                  return (
                    <tr key={tn.id}>
                      <td>
                        <div className="sap-subscriber-company">{tn.name}</div>
                        <div className="mono subtle sap-subscriber-id">{tn.slug}</div>
                      </td>
                      <td>
                        <span className={`badge sap-subscriber-planBadge--tier-${tier}`}>{planLabel}</span>
                      </td>
                      <td className="mono">
                        {tn.activeUsers}/{tn.totalUsers}
                      </td>
                      <td className="sap-subscriber-dates">
                        <div>{new Date(tn.createdAt).toLocaleDateString()}</div>
                        <div className="subtle">{t('superadmin.plansDashboard.expiresNotTracked')}</div>
                      </td>
                      <td className="mono">{showMrr && p ? formatPlanMoney(mrrVal, p.currency) : '—'}</td>
                      <td>
                        <span className={tn.isActive ? 'zh-status zh-status--active' : 'zh-status zh-status--suspended'}>
                          {tn.isActive
                            ? t('superadmin.plansDashboard.statusActive')
                            : t('superadmin.plansDashboard.statusSuspended')}
                        </span>
                      </td>
                      <td>
                        <ZHBtn
                          variant="ghost"
                          size="sm"
                          type="button"
                          onClick={() => goToSubscriberDetail(navigate, tn.id)}
                        >
                          {t('common.edit')}
                        </ZHBtn>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </ZHCardSection>
    </ZHCard>
  );
}
