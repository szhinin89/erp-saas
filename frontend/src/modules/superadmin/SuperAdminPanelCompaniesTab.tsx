import { EmptyState, LoadingState, Badge } from '../../components/PageShell';
import { ZHBtn, ZHField } from '../../components/zh/ZHForm';
import { ZHCard } from '../../components/zh/ZHCard';
import { ZHInlineRowRight } from '../../components/zh/ZHLayout';
import { CompanyModuleChips } from '../../components/saas/CompanyModuleChips';
import { goToCompaniesSubscriberDetail } from '../../navigation/companiesSubscriberDetailNav';
import type { SuperAdminPanelPageState } from './useSuperAdminPanelPage';

type Props = Pick<
  SuperAdminPanelPageState,
  | 't'
  | 'navigate'
  | 'loading'
  | 'error'
  | 'subscribers'
  | 'q'
  | 'setQ'
  | 'switching'
  | 'filtered'
  | 'planLabelForSubscriber'
  | 'openCreateSubscriber'
  | 'openSubscriptionModal'
  | 'handleSwitch'
>;

export function SuperAdminPanelCompaniesTab({
  t,
  navigate,
  loading,
  error,
  subscribers,
  q,
  setQ,
  switching,
  filtered,
  planLabelForSubscriber,
  openCreateSubscriber,
  openSubscriptionModal,
  handleSwitch,
}: Props) {
  return (
    <ZHCard
      title={t('superadmin.subscriberPicker')}
      actions={
        <ZHBtn variant="primary" size="sm" onClick={openCreateSubscriber} disabled={loading || switching !== null}>
          {t('superadmin.createSubscriber')}
        </ZHBtn>
      }
    >
      <div className="sa-search-row">
        <ZHField label={t('superadmin.searchPlaceholder')}>
          <input
            className="zh-input"
            value={q}
            onChange={(e) => setQ(e.target.value)}
            placeholder={t('superadmin.searchPlaceholder')}
            disabled={loading || switching !== null}
          />
        </ZHField>
      </div>

      {loading ? (
        <LoadingState />
      ) : error && subscribers.length === 0 ? (
        <EmptyState message={t('superadmin.sectionLoadHint')} />
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
                  {t('common.users') ?? 'Usuarios'}: <strong>{subscriber.totalUsers}</strong> · {t('common.active')}:{' '}
                  <strong>{subscriber.activeUsers}</strong>
                </span>
                <span className="subtle">{new Date(subscriber.createdAt).toLocaleDateString()}</span>
              </div>
              <div className="sa-subscriberPlanModules">
                <div className="sa-subscriberPlanRow subtle">
                  <span className="sa-subscriberPlanKey">{t('superadmin.subscriberRow.plan')}:</span>{' '}
                  <strong className="mono">
                    {planLabelForSubscriber(subscriber.planCode) || t('superadmin.subscriberRow.planUnset')}
                  </strong>
                </div>
                <div className="sa-subscriberModulesRow">
                  <span className="sa-subscriberModulesKey subtle">{t('superadmin.subscriberRow.modules')}:</span>{' '}
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
                    disabled={switching !== null}
                    onClick={() => openSubscriptionModal(subscriber)}
                  >
                    {t('superadmin.subscriberRow.changeSubscription')}
                  </ZHBtn>
                  <ZHBtn
                    variant="secondary"
                    size="sm"
                    disabled={switching !== null}
                    onClick={() => goToCompaniesSubscriberDetail(navigate, subscriber.id)}
                  >
                    {t('superadmin.subscriberRow.companyData')}
                  </ZHBtn>
                  <ZHBtn
                    variant="primary"
                    size="sm"
                    onClick={() => void handleSwitch(subscriber)}
                    disabled={switching !== null || !subscriber.isActive}
                    title={!subscriber.isActive ? t('superadmin.subscriberRow.enterDisabledInactive') : undefined}
                  >
                    {switching === subscriber.id ? t('superadmin.switching') : t('superadmin.enter')}
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
