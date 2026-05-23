import { EmptyState, LoadingState, Badge } from '../../components/PageShell';
import { ZHBtn, ZHField } from '../../components/zh/ZHForm';
import { ZHCard } from '../../components/zh/ZHCard';
import { ZHInlineRowRight } from '../../components/zh/ZHLayout';
import { CompanyModuleChips } from '../../components/saas/CompanyModuleChips';
import { goToSubscriberDetail } from '../../navigation/platformSubscriberDetailNav';
import type { PlatformPanelPageState } from './usePlatformPanelPage';

type Props = Pick<
  PlatformPanelPageState,
  | 't'
  | 'navigate'
  | 'loading'
  | 'error'
  | 'subscribers'
  | 'q'
  | 'setQ'
  | 'filtered'
  | 'planLabelForSubscriber'
  | 'openCreateSubscriber'
  | 'openSubscriptionModal'
>;

export function PlatformPanelCompaniesTab({
  t,
  navigate,
  loading,
  error,
  subscribers,
  q,
  setQ,
  filtered,
  planLabelForSubscriber,
  openCreateSubscriber,
  openSubscriptionModal,
}: Props) {
  return (
    <ZHCard
      title={t('platform.subscriberPicker')}
      actions={
        <ZHBtn variant="primary" size="sm" onClick={openCreateSubscriber} disabled={loading}>
          {t('platform.createSubscriber')}
        </ZHBtn>
      }
    >
      <div className="sa-search-row">
        <ZHField label={t('platform.searchPlaceholder')}>
          <input
            className="zh-input"
            value={q}
            onChange={(e) => setQ(e.target.value)}
            placeholder={t('platform.searchPlaceholder')}
            disabled={loading}
          />
        </ZHField>
      </div>

      {loading ? (
        <LoadingState />
      ) : error && subscribers.length === 0 ? (
        <EmptyState message={t('platform.sectionLoadHint')} />
      ) : filtered.length === 0 ? (
        <EmptyState message={t('common.noData')} />
      ) : (
        <div className="sa-subscriberList">
          {filtered.map((subscriber) => (
            <div key={subscriber.id} className="sa-subscriberRow">
              <div className="sa-subscriberName">{subscriber.name}</div>
              <div className="sa-subscriberMeta">
                <span className="mono">{subscriber.slug}</span>
                <span className="mono">{subscriber.id}</span>
              </div>
              <div className="sa-subscriber-stats">
                <Badge
                  label={subscriber.isActive ? t('common.active') : t('common.inactive')}
                  variant={subscriber.isActive ? 'green' : 'gray'}
                />
                <span className="subtle">
                  {t('common.users')}: <strong>{subscriber.totalUsers}</strong> · {t('common.active')}:{' '}
                  <strong>{subscriber.activeUsers}</strong>
                </span>
                <span className="subtle">{new Date(subscriber.createdAt).toLocaleDateString()}</span>
              </div>
              <div className="sa-subscriberPlanModules">
                <div className="sa-subscriberPlanRow subtle">
                  <span className="sa-subscriberPlanKey">{t('platform.subscriberRow.plan')}:</span>{' '}
                  <strong className="mono">
                    {planLabelForSubscriber(subscriber.planCode) || t('platform.subscriberRow.planUnset')}
                  </strong>
                </div>
                <div className="sa-subscriberModulesRow">
                  <span className="sa-subscriberModulesKey subtle">{t('platform.subscriberRow.modules')}:</span>{' '}
                  <CompanyModuleChips
                    company={{
                      enabledModules: subscriber.enabledModules,
                      hasModuleRestrictions: subscriber.hasModuleRestrictions,
                    }}
                  />
                </div>
              </div>
              <div className="sa-subscriber-actions">
                <ZHInlineRowRight>
                  <ZHBtn
                    variant="secondary"
                    size="sm"
                    onClick={() => openSubscriptionModal(subscriber)}
                  >
                    {t('platform.subscriberRow.changeSubscription')}
                  </ZHBtn>
                  <ZHBtn
                    variant="primary"
                    size="sm"
                    onClick={() => goToSubscriberDetail(navigate, subscriber.id)}
                  >
                    {t('platform.subscriberRow.openSheet')}
                  </ZHBtn>
                </ZHInlineRowRight>
              </div>
            </div>
          ))}
        </div>
      )}
    </ZHCard>
  );
}
