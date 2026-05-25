import { useEffect, useMemo, useState } from 'react';
import { useNavigate, useParams, useSearchParams } from 'react-router-dom';
import { platformService } from '../api/platformService';
import { useAuthStore } from '../../../store/authStore';
import { usePermissionsStore } from '../../../store/permissionsStore';
import { formatApiRequestError } from '../../../modules/lib/apiError';
import {
  buildSubscriberDetailReturnPath,
  startPlatformImpersonation,
  type SaasImpersonationTarget,
} from '../../../navigation/platformImpersonationNav';
import { LoadingState } from '../../../components/PageShell';
import { ZHPageNotice } from '../../../components/zh/ZHPageNotice';
import { ZHFormSection, ZHGrid, ZHField, ZHBtn } from '../../../components/zh/ZHForm';
import { ConfigManagementPanel } from '../../../components/config/ConfigManagementPanel';
import { EntityAuditPanel } from '../../../components/EntityAuditPanel';
import { usePlatformGate } from '../../../hooks/usePlatformGate';
import { useI18n } from '../../../i18n/i18n';
import { ELECTRONIC_BILLING_TRIAL_KEY, useSubscriberDetailPage } from './subscriber-detail/useSubscriberDetailPage';
import './PlatformSubscriberDetailPage.css';

type TabId =
  | 'overview'
  | 'subscription'
  | 'entitlements'
  | 'companies'
  | 'users'
  | 'configuration'
  | 'menu'
  | 'audit'
  | 'metrics';

const TAB_DEFS: { id: TabId; labelKey: string; icon: string }[] = [
  { id: 'overview', labelKey: 'platform.subscriberDetail.tab.overview', icon: 'info' },
  { id: 'subscription', labelKey: 'platform.subscriberDetail.tab.subscription', icon: 'subscriptions' },
  { id: 'entitlements', labelKey: 'platform.subscriberDetail.tab.entitlements', icon: 'verified' },
  { id: 'companies', labelKey: 'platform.subscriberDetail.tab.companies', icon: 'domain' },
  { id: 'users', labelKey: 'platform.subscriberDetail.tab.users', icon: 'group' },
  { id: 'configuration', labelKey: 'platform.subscriberDetail.tab.configuration', icon: 'tune' },
  { id: 'menu', labelKey: 'platform.subscriberDetail.tab.menu', icon: 'menu_book' },
  { id: 'audit', labelKey: 'platform.subscriberDetail.tab.audit', icon: 'history' },
  { id: 'metrics', labelKey: 'platform.subscriberDetail.tab.metrics', icon: 'monitoring' },
];

function parseTab(raw: string | null): TabId {
  const v = (raw ?? 'overview').trim().toLowerCase();
  return (TAB_DEFS.find((t) => t.id === v)?.id ?? 'overview') as TabId;
}

export function PlatformSubscriberDetailPage() {
  const { subscriberId } = useParams<{ subscriberId: string }>();
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const tab = useMemo(() => parseTab(searchParams.get('tab')), [searchParams]);
  const { t } = useI18n();
  const tabs = useMemo(
    () => TAB_DEFS.map((def) => ({ ...def, label: t(def.labelKey) })),
    [t],
  );
  const { isPlatformOperator } = usePlatformGate();
  const { login } = useAuthStore();
  const clearPermissions = usePermissionsStore((s) => s.clearPermissions);

  const page = useSubscriberDetailPage(subscriberId);
  const [actionBusy, setActionBusy] = useState(false);
  const [actionMsg, setActionMsg] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const [changePlanInput, setChangePlanInput] = useState('');
  const [platformAudit, setPlatformAudit] = useState<Awaited<ReturnType<typeof platformService.getPlatformAudit>>>([]);
  const [entLoading, setEntLoading] = useState(false);

  const { subscriber, loading, error } = page;

  useEffect(() => {
    if (subscriber?.planCode) setChangePlanInput(subscriber.planCode);
  }, [subscriber?.planCode]);

  useEffect(() => {
    if (tab !== 'entitlements' || !subscriberId || page.entitlements) return;
    setEntLoading(true);
    void page.loadEntitlements().finally(() => setEntLoading(false));
  }, [tab, subscriberId, page.entitlements, page.loadEntitlements]);

  useEffect(() => {
    if (tab === 'users' && subscriberId) void page.loadSubscriberUsers();
  }, [tab, subscriberId, page]);

  useEffect(() => {
    if (tab !== 'audit' || !subscriberId) return;
    platformService.getPlatformAudit(50, subscriberId).then(setPlatformAudit).catch(() => setPlatformAudit([]));
  }, [tab, subscriberId]);

  const setTab = (next: TabId) => {
    setSearchParams(next === 'overview' ? {} : { tab: next }, { replace: true });
  };

  const doAction = async (fn: () => Promise<unknown>, successMsg: string) => {
    setActionBusy(true);
    setActionError(null);
    setActionMsg(null);
    try {
      await fn();
      setActionMsg(successMsg);
      await page.reloadSummary();
    } catch (e) {
      setActionError(formatApiRequestError(e, { generic: t('platform.subscriberDetail.actionError') }));
    } finally {
      setActionBusy(false);
    }
  };

  const handleImpersonate = async (target: SaasImpersonationTarget) => {
    if (!subscriber || actionBusy || !subscriber.isActive) return;
    setActionBusy(true);
    setActionError(null);
    try {
      await startPlatformImpersonation({
        subscriberId: subscriber.id,
        subscriberName: subscriber.name,
        target,
        returnPath: buildSubscriberDetailReturnPath(subscriber.id, tab),
        navigate,
        login,
        clearPermissions,
      });
    } catch (e) {
      setActionError(formatApiRequestError(e, { generic: t('platform.subscriberDetail.impersonateError') }));
    } finally {
      setActionBusy(false);
    }
  };

  if (loading) {
    return <div className="sa-detail-page"><div className="sa-detail-loading"><LoadingState /></div></div>;
  }

  if (!subscriber || !subscriberId) {
    return (
      <div className="sa-detail-page">
        <ZHPageNotice variant="error" message={error ?? t('platform.subscriberDetail.notFound')} />
        <ZHBtn variant="ghost" size="md" onClick={() => navigate('/platform/subscribers')}>{t('platform.subscriberDetail.back')}</ZHBtn>
      </div>
    );
  }

  const showNotice = error || actionError || page.saveError;
  const showSuccess = actionMsg || page.saveOk;

  return (
    <div className="sa-detail-page">
      <div className="sa-detail-header">
        <button type="button" className="zh-btn zh-btn--ghost zh-btn--sm sa-detail-back" onClick={() => navigate('/platform/subscribers')}>
          <span className="material-symbols-outlined">arrow_back</span>
          {t('platform.subscriberDetail.back')}
        </button>
        <div className="sa-detail-title-row">
          <div className="sa-detail-avatar">{subscriber.name.charAt(0).toUpperCase()}</div>
          <div>
            <h2 className="sa-detail-name">{subscriber.name}</h2>
            <p className="sa-detail-slug subtle">{subscriber.slug}</p>
          </div>
          <div className="sa-detail-status-chips">
            <span className={subscriber.isActive ? 'zh-status zh-status--active' : 'zh-status zh-status--inactive'}>
              {subscriber.isActive ? t('platform.subscriberDetail.status.active') : t('platform.subscriberDetail.status.inactive')}
            </span>
            {subscriber.planCode && <span className="badge badge--blue badge--md badge--upper">{subscriber.planCode}</span>}
          </div>
        </div>
      </div>

      <div className="sa-detail-tabs sa-detail-tabs--scroll">
        {tabs.map((item) => (
          <button key={item.id} type="button" className={`sa-detail-tab${tab === item.id ? ' is-active' : ''}`} onClick={() => setTab(item.id)}>
            <span className="material-symbols-outlined">{item.icon}</span>
            {item.label}
          </button>
        ))}
      </div>

      {showNotice && <ZHPageNotice variant="error" message={showNotice} />}
      {showSuccess && <ZHPageNotice variant="success" message={typeof showSuccess === 'string' ? showSuccess : t('platform.subscriberDetail.saved')} />}

      {tab === 'overview' && (
        <div className="sa-detail-body">
          <div className="sa-detail-card">
            <h3 className="sa-detail-card-title">{t('platform.subscriberDetail.overview.summary')}</h3>
            <div className="sa-detail-grid">
              <div className="sa-detail-field"><span className="sa-detail-label">{t('platform.subscriberDetail.overview.id')}</span><span className="mono">{subscriber.id}</span></div>
              <div className="sa-detail-field"><span className="sa-detail-label">{t('platform.subscriberDetail.overview.users')}</span><span className="mono">{subscriber.activeUsers}/{subscriber.totalUsers}</span></div>
              <div className="sa-detail-field"><span className="sa-detail-label">{t('platform.subscriberDetail.overview.customMenu')}</span><span>{subscriber.hasCustomMenu ? t('platform.subscriberDetail.yes') : t('platform.subscriberDetail.no')}</span></div>
            </div>
          </div>
          <div className="sa-detail-card">
            <h3 className="sa-detail-card-title">{t('platform.subscriberDetail.impersonate.title')}</h3>
            <p className="subtle sa-detail-impersonation-hint">{t('platform.subscriberDetail.impersonate.hint')}</p>
            <div className="sa-detail-impersonation-btns">
              <ZHBtn
                variant="primary"
                size="md"
                disabled={actionBusy || !subscriber.isActive}
                title={!subscriber.isActive ? t('platform.subscriberDetail.impersonate.inactiveTitle') : undefined}
                onClick={() => void handleImpersonate('/saas/overview')}
              >
                {t('platform.subscriberDetail.impersonate.overview')}
              </ZHBtn>
              <ZHBtn
                variant="ghost"
                size="md"
                disabled={actionBusy || !subscriber.isActive}
                onClick={() => void handleImpersonate('/saas/companies')}
              >
                {t('platform.subscriberDetail.impersonate.companies')}
              </ZHBtn>
              <ZHBtn
                variant="ghost"
                size="md"
                disabled={actionBusy || !subscriber.isActive}
                onClick={() => void handleImpersonate('/saas/billing')}
              >
                {t('platform.subscriberDetail.impersonate.billing')}
              </ZHBtn>
            </div>
          </div>
        </div>
      )}

      {tab === 'subscription' && (
        <div className="sa-detail-body">
          <div className="sa-detail-card">
            <h3 className="sa-detail-card-title">{t('platform.subscriberDetail.subscription.lifecycle')}</h3>
            <div className="sa-detail-action-row">
              <ZHBtn variant="primary" size="md" disabled={actionBusy || subscriber.isActive} onClick={() => void doAction(() => platformService.activateSubscriber(subscriber.id), t('platform.subscriberDetail.subscription.activated'))}>{t('platform.subscriberDetail.subscription.activate')}</ZHBtn>
              <ZHBtn variant="ghost" size="md" disabled={actionBusy || !subscriber.isActive} onClick={() => void doAction(() => platformService.suspendSubscriber(subscriber.id), t('platform.subscriberDetail.subscription.suspended'))}>{t('platform.subscriberDetail.subscription.suspend')}</ZHBtn>
            </div>
          </div>
          <div className="sa-detail-card">
            <h3 className="sa-detail-card-title">{t('platform.subscriberDetail.subscription.planTitle')}</h3>
            <div className="sa-detail-action-row">
              <input className="zh-input" value={changePlanInput} onChange={(e) => setChangePlanInput(e.target.value)} placeholder={t('platform.subscriberDetail.subscription.planPlaceholder')} />
              <ZHBtn variant="primary" size="md" disabled={actionBusy || !changePlanInput.trim()} onClick={() => void doAction(() => platformService.changePlan(subscriber.id, changePlanInput.trim()), t('platform.subscriberDetail.subscription.planUpdated'))}>{t('platform.subscriberDetail.subscription.changePlan')}</ZHBtn>
            </div>
          </div>
        </div>
      )}

      {tab === 'entitlements' && (
        <div className="sa-detail-body">
          {entLoading || page.detailLoading ? <LoadingState /> : page.entitlements ? (
            <>
              <p className="subtle">{t('platform.subscriberDetail.entitlements.plan')}: {page.entitlements.planName ?? page.entitlements.planCode ?? '—'} · {t('platform.subscriberDetail.entitlements.billing')}: {page.entitlements.billingAccountStatus ?? '—'}</p>
              <p>{t('platform.subscriberDetail.entitlements.modules')}: {page.entitlements.enabledModules.join(', ') || '—'}</p>
              <p>{t('platform.subscriberDetail.entitlements.features')}: {page.entitlements.enabledFeatures.join(', ') || '—'}</p>
            </>
          ) : <p className="subtle">{t('platform.subscriberDetail.entitlements.empty')}</p>}
        </div>
      )}

      {tab === 'companies' && (
        <div className="sa-detail-body">
          {page.detailLoading ? <LoadingState /> : (
            <form onSubmit={(e) => { e.preventDefault(); void page.saveCompanyProfile(); }}>
              <ZHFormSection title={t('platform.subscriberDetail.companies.profileTitle')}>
                <ZHGrid cols={2}>
                  <ZHField label={t('platform.subscriberDetail.companies.name')}><input className="zh-input" {...page.form.register('subscriberName')} /></ZHField>
                  <ZHField label={t('platform.subscriberDetail.companies.slug')}><input className="zh-input" {...page.form.register('subscriberSlug')} /></ZHField>
                  <ZHField label={t('platform.subscriberDetail.companies.ruc')}><input className="zh-input" {...page.form.register('ruc')} /></ZHField>
                  <ZHField label={t('platform.subscriberDetail.companies.shortName')}><input className="zh-input" {...page.form.register('shortName')} /></ZHField>
                </ZHGrid>
              </ZHFormSection>
              {page.detail && (
                <ZHFormSection title={t('platform.subscriberDetail.companies.operational')}>
                  <p className="subtle mono">{page.detail.currency} · {page.detail.timezone} · {page.detail.language}</p>
                </ZHFormSection>
              )}
              <div className="zh-form-actions-row zh-form-actions-row--end">
                <ZHBtn variant="primary" size="md" type="submit" disabled={page.saving}>{t('platform.subscriberDetail.companies.saveProfile')}</ZHBtn>
              </div>
            </form>
          )}
        </div>
      )}

      {tab === 'users' && (
        <div className="sa-detail-body sa-detail-card">
          <table className="table">
            <thead><tr><th>{t('platform.subscriberDetail.users.email')}</th><th>{t('platform.subscriberDetail.users.name')}</th><th>{t('platform.subscriberDetail.users.type')}</th><th>{t('platform.subscriberDetail.users.status')}</th></tr></thead>
            <tbody>
              {page.subscriberUsers.map((u) => (
                <tr key={u.id}>
                  <td>{u.email}</td>
                  <td>{u.firstName} {u.lastName}</td>
                  <td className="mono subtle">{u.userType}</td>
                  <td>{u.isActive ? t('platform.subscriberDetail.status.active') : t('platform.subscriberDetail.status.inactive')}</td>
                </tr>
              ))}
            </tbody>
          </table>
          {page.subscriberUsers.length === 0 && <p className="subtle">{t('platform.subscriberDetail.users.empty')}</p>}
        </div>
      )}

      {tab === 'configuration' && subscriberId && (
        <div className="sa-detail-body">
          <div className="sa-detail-card">
            <label className="companies-checkbox-label">
              <input type="checkbox" checked={page.electronicBillingTrialEnabled} onChange={(e) => page.setElectronicBillingTrialEnabled(e.target.checked)} />
              {t('platform.subscriberDetail.config.trial', { key: ELECTRONIC_BILLING_TRIAL_KEY })}
            </label>
            <ZHBtn variant="primary" size="sm" className="pg-mt-sm" disabled={page.saving} onClick={() => void page.saveGlobalParameters()}>{t('platform.subscriberDetail.config.saveParams')}</ZHBtn>
            <p className="subtle">{t('platform.subscriberDetail.config.globalKeys')}: {page.globalConfigCount}</p>
          </div>
          <ConfigManagementPanel subscriberId={subscriberId} canManage={isPlatformOperator} title={t('platform.subscriberDetail.config.panelTitle')} subtitle={t('platform.subscriberDetail.config.panelSubtitle')} />
        </div>
      )}

      {tab === 'menu' && (
        <div className="sa-detail-body sa-detail-card">
          <p>{t('platform.subscriberDetail.menu.custom')}: {page.menuFlags?.hasCustomMenu ? t('platform.subscriberDetail.yes') : t('platform.subscriberDetail.no')} · {t('platform.subscriberDetail.menu.planMenu')}: {page.menuFlags?.usedPlanMenu ? t('platform.subscriberDetail.yes') : t('platform.subscriberDetail.no')}</p>
          <ZHBtn variant="ghost" size="md" onClick={() => void page.resetCustomMenu()}>{t('platform.subscriberDetail.menu.reset')}</ZHBtn>
          <p className="subtle pg-mt-md">{t('platform.subscriberDetail.menu.editorLink')} <a className="zh-link" href="/platform/plans?tab=menu">{t('platform.subscriberDetail.menu.editorHref')}</a></p>
        </div>
      )}

      {tab === 'audit' && subscriberId && (
        <div className="sa-detail-body">
          <div className="sa-detail-card">
            <h3 className="sa-detail-card-title">{t('platform.subscriberDetail.audit.platformTitle')}</h3>
            {platformAudit.length === 0 ? <p className="subtle">{t('platform.subscriberDetail.audit.platformEmpty')}</p> : (
              <ul className="subtle">{platformAudit.map((e) => <li key={e.id}>{e.action} — {e.createdAtUtc}</li>)}</ul>
            )}
          </div>
          <EntityAuditPanel entityType="Subscriber" entityId={subscriberId} take={15} />
        </div>
      )}

      {tab === 'metrics' && (
        <div className="sa-detail-body sa-detail-card">
          <p>{t('platform.subscriberDetail.metrics.activeUsers')}: {subscriber.activeUsers} / {subscriber.totalUsers}</p>
          <p>{t('platform.subscriberDetail.metrics.modules')}: {subscriber.hasModuleRestrictions ? subscriber.enabledModules?.join(', ') : t('platform.subscriberDetail.metrics.noRestriction')}</p>
          <ZHBtn variant="ghost" size="sm" onClick={() => navigate('/platform/observability')}>{t('platform.subscriberDetail.metrics.observability')}</ZHBtn>
        </div>
      )}
    </div>
  );
}
