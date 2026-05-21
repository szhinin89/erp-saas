import { useCallback, useEffect, useMemo, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { EmptyState, LoadingState, Badge } from '../components/PageShell';
import { ZHPageNotice } from '../components/zh/ZHPageNotice';
import { SuperAdminPageTemplate } from '../components/superadmin/SuperAdminPageTemplate';
import { useSuperAdminGate } from '../hooks/useSuperAdminGate';
import { Modal } from '../components/Modal';
import { ZHModalHeader } from '../components/zh/ZHModalHeader';
import { SUBSCRIBER_MODULE_KEYS } from '../constants/subscriptionModules';
import { superAdminService, type CreateSubscriberWithAdminBody, type SuperAdminPlan, type SuperAdminSubscriber } from '../services/superAdminService';
import { useAuthStore } from '../store/authStore';
import { usePermissionsStore } from '../store/permissionsStore';
import { CompanyModuleChips } from '../components/saas/CompanyModuleChips';
import { useI18n } from '../i18n/i18n';
import { ZHField } from '../components/zh/ZHForm';
import { ZHGridRow, ZHInlineRowRight } from '../components/zh/ZHLayout';
import { ZHDashboardScaffold, ZHKpiPanel } from '../components/zh/ZHDashboard';
import { SuperAdminGrowthSection } from '../components/superadmin/SuperAdminGrowthSection';
import { SuperAdminPlansSection } from '../components/superadmin/SuperAdminPlansSection';
import { SuperAdminMenuBuilderSection } from '../components/superadmin/SuperAdminMenuBuilderSection';
import { Button, Card, Input } from '../components/ui';
import { formatApiRequestError } from '../modules/lib/apiError';
import { goToCompaniesSubscriberDetail } from '../navigation/companiesSubscriberDetailNav';
import type { SessionResponse } from '../types/access';
import '../components/zh/ZHFormTabs.css';
import './SuperAdminPanelPage.css';

type SuperAdminHomeTab = 'overview' | 'companies' | 'plans' | 'menus';

export type SuperAdminPanelPageProps = {
  /** Cuando se usa dentro de <SuperAdminLayout>: oculta pestañas y fija la sección. */
  embeddedTab?: SuperAdminHomeTab;
  shellLayout?: boolean;
};

function defaultModuleChecksAllOn(): Record<string, boolean> {
  const o: Record<string, boolean> = {};
  for (const k of SUBSCRIBER_MODULE_KEYS) o[k] = true;
  return o;
}

/** Si no restringe, o marca todos, equivale a sin JSON de módulos en backend. */
function normalizeEnabledModulesForApi(
  restrict: boolean,
  checks: Record<string, boolean>,
): string[] | undefined {
  if (!restrict) return undefined;
  const selected = SUBSCRIBER_MODULE_KEYS.filter((k) => checks[k]);
  if (selected.length === 0) return undefined;
  if (selected.length === SUBSCRIBER_MODULE_KEYS.length) return undefined;
  return [...selected];
}

function storeImpersonationSubscriberName(name: string) {
  localStorage.setItem('superadmin-impersonation-subscriber-name', name);
}

export function SuperAdminPanelPage({ embeddedTab, shellLayout }: SuperAdminPanelPageProps = {}) {
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const { login } = useAuthStore();
  const clearPermissions = usePermissionsStore((s) => s.clearPermissions);
  const { isSuperAdmin, hasSelectedSubscriber } = useSuperAdminGate();
  const { t } = useI18n();

  const tabParam = searchParams.get('tab');
  const homeTab: SuperAdminHomeTab = embeddedTab
    ? embeddedTab
    : tabParam === 'companies' || tabParam === 'plans' || tabParam === 'menus'
      ? tabParam
      : 'overview';

  const selectHomeTab = (tab: SuperAdminHomeTab) => {
    if (shellLayout) {
      if (tab === 'overview') void navigate('/superadmin/overview');
      else if (tab === 'companies') void navigate('/superadmin/companies');
      else if (tab === 'plans') void navigate('/superadmin/menu-plans?tab=plans');
      else if (tab === 'menus') void navigate('/superadmin/menu-plans?tab=menu');
      return;
    }
    if (tab === 'overview') setSearchParams({}, { replace: true });
    else setSearchParams({ tab }, { replace: true });
  };

  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [metrics, setMetrics] = useState<Awaited<ReturnType<typeof superAdminService.getMetrics>> | null>(null);
  const [subscribers, setSubscribers] = useState<SuperAdminSubscriber[]>([]);
  const [q, setQ] = useState('');
  const [switching, setSwitching] = useState<string | null>(null);
  const [plans, setPlans] = useState<SuperAdminPlan[]>([]);

  const [createSubscriberOpen, setCreateSubscriberOpen] = useState(false);
  const [createBusy, setCreateBusy] = useState(false);
  const [createError, setCreateError] = useState('');
  const [createForm, setCreateForm] = useState<CreateSubscriberWithAdminBody>({
    subscriberName: '',
    subscriberSlug: '',
    ruc: '',
    countryCode: 'ECU',
    timezone: 'America/Guayaquil',
    adminFirstName: '',
    adminLastName: '',
    adminEmail: '',
    adminPassword: '',
    passwordResetMode: 0,
    linkExistingAdmin: false,
  });
  const [createPlanCode, setCreatePlanCode] = useState('');
  const [createRestrictModules, setCreateRestrictModules] = useState(false);
  const [createModuleChecks, setCreateModuleChecks] = useState<Record<string, boolean>>(defaultModuleChecksAllOn);

  const [subModalOpen, setSubModalOpen] = useState(false);
  const [subModalSubscriber, setSubModalSubscriber] = useState<SuperAdminSubscriber | null>(null);
  const [subPlanCode, setSubPlanCode] = useState('');
  const [subRestrict, setSubRestrict] = useState(false);
  const [subModuleChecks, setSubModuleChecks] = useState<Record<string, boolean>>(defaultModuleChecksAllOn);
  const [subBusy, setSubBusy] = useState(false);
  const [subError, setSubError] = useState('');

  const slugify = (name: string) =>
    (name ?? '')
      .trim()
      .toLowerCase()
      .replace(/[^a-z0-9]+/g, '-')
      .replace(/(^-|-$)/g, '')
      .slice(0, 64);

  const refreshSubscribersAndMetrics = useCallback(async () => {
    const [m, tns] = await Promise.all([
      superAdminService.getMetrics(),
      superAdminService.getSubscribers(),
    ]);
    setMetrics(m);
    setSubscribers(tns);
  }, []);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        setLoading(true);
        setError('');
        await refreshSubscribersAndMetrics();
        if (cancelled) return;
      } catch (e) {
        if (cancelled) return;
        setError(formatApiRequestError(e, { offline: t('common.apiUnreachable'), generic: t('common.errorGeneric') }));
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => { cancelled = true; };
  }, [refreshSubscribersAndMetrics, t]);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const list = await superAdminService.getPlansCatalog();
        if (!cancelled) setPlans(list);
      } catch {
        if (!cancelled) setPlans([]);
      }
    })();
    return () => { cancelled = true; };
  }, [t]);

  const filtered = useMemo(() => {
    const query = q.trim().toLowerCase();
    if (!query) return subscribers;
    return subscribers.filter((t) =>
      t.name.toLowerCase().includes(query) || t.slug.toLowerCase().includes(query) || t.id.toLowerCase().includes(query)
    );
  }, [subscribers, q]);

  const planLabelForSubscriber = useMemo(() => {
    const map = new Map<string, string>();
    for (const p of plans) {
      map.set(p.code.trim().toLowerCase(), p.name.trim() ? `${p.name} (${p.code})` : p.code);
    }
    return (planCode: string | null | undefined) => {
      const c = (planCode ?? '').trim();
      if (!c) return '';
      return map.get(c.toLowerCase()) ?? c;
    };
  }, [plans]);

  const activePlans = useMemo(
    () => [...plans].filter((p) => p.isActive).sort((a, b) => (a.sortOrder ?? 0) - (b.sortOrder ?? 0)),
    [plans],
  );

  const moduleLabel = useCallback(
    (key: string) => t(`companies.subscription.module.${key}`, key),
    [t],
  );

  const handleSwitch = async (subscriber: SuperAdminSubscriber) => {
    if (!subscriber.isActive) return;
    setSwitching(subscriber.id);
    setError('');
    try {
      const auth = await superAdminService.switchSubscriber(subscriber.id);
      storeImpersonationSubscriberName(subscriber.name);
      clearPermissions();
      login(auth);
      navigate('/dashboard');
    } catch (e) {
      setError(formatApiRequestError(e, { offline: t('common.apiUnreachable'), generic: t('common.errorGeneric') }));
    } finally {
      setSwitching(null);
    }
  };

  const openCreateSubscriber = () => {
    setCreateError('');
    setCreatePlanCode('');
    setCreateRestrictModules(false);
    setCreateModuleChecks(defaultModuleChecksAllOn());
    setCreateForm({
      subscriberName: '',
      subscriberSlug: '',
      adminFirstName: '',
      adminLastName: '',
      adminEmail: '',
      adminPassword: '',
      passwordResetMode: 0,
      linkExistingAdmin: false,
    });
    setCreateSubscriberOpen(true);
  };

  const openSubscriptionModal = (subscriber: SuperAdminSubscriber) => {
    setSubModalSubscriber(subscriber);
    setSubPlanCode((subscriber.planCode ?? '').trim());
    const restricted = !!subscriber.hasModuleRestrictions;
    setSubRestrict(restricted);
    const checks: Record<string, boolean> = {};
    for (const k of SUBSCRIBER_MODULE_KEYS) {
      if (!restricted) {
        checks[k] = true;
      } else {
        checks[k] = (subscriber.enabledModules ?? []).some((em: string) => em.toLowerCase() === k.toLowerCase());
      }
    }
    setSubModuleChecks(checks);
    setSubError('');
    setSubModalOpen(true);
  };

  const saveCreateSubscriber = async () => {
    setCreateBusy(true);
    setCreateError('');
    try {
      const subscriberName = createForm.subscriberName.trim();
      const subscriberSlug = (createForm.subscriberSlug.trim() || slugify(subscriberName)).toLowerCase();
      const adminEmail = createForm.adminEmail.trim().toLowerCase();

      if (activePlans.length === 0) {
        setCreateError(t('superadmin.createSubscriber.error.noPlans'));
        return;
      }
      if (!createPlanCode.trim()) {
        setCreateError(t('superadmin.createSubscriber.error.planRequired'));
        return;
      }

      if (createRestrictModules) {
        const n = SUBSCRIBER_MODULE_KEYS.filter((k) => createModuleChecks[k]).length;
        if (n === 0) {
          setCreateError(t('superadmin.createSubscriber.error.noModulesSelected'));
          return;
        }
      }

      const planCodeNorm = createPlanCode.trim();
      const enabledModules = normalizeEnabledModulesForApi(createRestrictModules, createModuleChecks);

      const session: SessionResponse = await superAdminService.createSubscriberWithAdmin({
        ...createForm,
        subscriberName,
        subscriberSlug,
        adminEmail,
        ruc: createForm.ruc?.trim() || null,
        countryCode: createForm.countryCode?.trim() || 'ECU',
        timezone: createForm.timezone?.trim() || 'America/Guayaquil',
        adminPassword: createForm.linkExistingAdmin ? '' : createForm.adminPassword,
        planCode: planCodeNorm,
        enabledModules: enabledModules === undefined ? null : enabledModules,
      });

      setError('');
      setCreateSubscriberOpen(false);
      goToCompaniesSubscriberDetail(navigate, session.subscriberId);
    } catch (e) {
      setCreateError(formatApiRequestError(e, { offline: t('common.apiUnreachable'), generic: t('common.errorGeneric') }));
    } finally {
      setCreateBusy(false);
    }
  };

  const saveSubscriptionModal = async () => {
    if (!subModalSubscriber) return;
    setSubBusy(true);
    setSubError('');
    try {
      if (subRestrict) {
        const n = SUBSCRIBER_MODULE_KEYS.filter((k) => subModuleChecks[k]).length;
        if (n === 0) {
          setSubError(t('superadmin.changeSubscription.error.noModulesSelected'));
          return;
        }
      }
      const planCode = subPlanCode.trim() || null;
      const enabledModulesNorm = normalizeEnabledModulesForApi(subRestrict, subModuleChecks);

      await superAdminService.updateSubscriberSubscription(subModalSubscriber.id, {
        planCode,
        enabledModules: enabledModulesNorm === undefined ? null : enabledModulesNorm,
      });
      await refreshSubscribersAndMetrics();
      setSubModalOpen(false);
      setSubModalSubscriber(null);
    } catch (e) {
      setSubError(formatApiRequestError(e, { offline: t('common.apiUnreachable'), generic: t('common.errorGeneric') }));
    } finally {
      setSubBusy(false);
    }
  };

  return (
    <SuperAdminPageTemplate
      title={t('superadmin.title')}
      subtitle={isSuperAdmin && !hasSelectedSubscriber ? t('superadmin.subtitle') : undefined}
      subscriberGuardAction={
        <Button variant="primary" size="sm" onClick={() => navigate('/dashboard')}>
          {t('superadmin.goToSubscriber')}
        </Button>
      }
    >
      {error ? <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={error} /> : null}

      <ZHDashboardScaffold>
        {!shellLayout ? (
          <div className="sa-panelTabsWrap">
            <div className="zh-form-tabs sa-panelTabs" role="tablist" aria-label={t('superadmin.title')}>
              <button
                type="button"
                role="tab"
                aria-selected={homeTab === 'overview'}
                className={homeTab === 'overview' ? 'is-active' : ''}
                onClick={() => selectHomeTab('overview')}
              >
                {t('superadmin.tabOverview')}
              </button>
              <button
                type="button"
                role="tab"
                aria-selected={homeTab === 'companies'}
                className={homeTab === 'companies' ? 'is-active' : ''}
                onClick={() => selectHomeTab('companies')}
              >
                {t('superadmin.tabCompanies')}
              </button>
              <button
                type="button"
                role="tab"
                aria-selected={homeTab === 'plans'}
                className={homeTab === 'plans' ? 'is-active' : ''}
                onClick={() => selectHomeTab('plans')}
              >
                {t('superadmin.tabPlans')}
              </button>
              <button
                type="button"
                role="tab"
                aria-selected={homeTab === 'menus'}
                className={homeTab === 'menus' ? 'is-active' : ''}
                onClick={() => selectHomeTab('menus')}
              >
                {t('superadmin.tabMenus')}
              </button>
            </div>
          </div>
        ) : null}

        {homeTab === 'overview' ? (
          <div className="sa-overviewKpi">
            {!loading && subscribers.length === 0 && isSuperAdmin ? (
              <Card title={t('superadmin.welcomeNoSubscribersTitle')}>
                  <p className="subtle sa-welcome-note">
                    {t('superadmin.welcomeNoSubscribersBody')}
                  </p>
                  <ZHInlineRowRight>
                    <Button variant="secondary" size="sm" onClick={() => selectHomeTab('plans')}>
                      {t('superadmin.welcomeGoPlans')}
                    </Button>
                    <Button
                      variant="primary"
                      size="sm"
                      onClick={() => {
                        selectHomeTab('companies');
                        openCreateSubscriber();
                      }}
                      disabled={activePlans.length === 0}
                      title={activePlans.length === 0 ? t('superadmin.createSubscriber.error.noPlans') : undefined}
                    >
                      {t('superadmin.welcomeCreateFirstCompany')}
                    </Button>
                  </ZHInlineRowRight>
              </Card>
            ) : null}
            {loading ? (
              <Card>
                <LoadingState />
              </Card>
            ) : metrics ? (
              <ZHKpiPanel
                title={t('superadmin.metrics')}
                items={[
                  { label: t('superadmin.totalSubscribers'), value: String(metrics.totals.totalSubscribers), tone: 'neutral' },
                  { label: t('superadmin.activeSubscribers'), value: String(metrics.totals.activeSubscribers), tone: 'info' },
                  { label: t('superadmin.totalUsers'), value: String(metrics.totals.totalUsers), tone: 'neutral' },
                  { label: t('superadmin.activeUsers'), value: String(metrics.totals.activeUsers), tone: 'success' },
                ]}
              />
            ) : null}
            {isSuperAdmin ? <SuperAdminGrowthSection /> : null}
            <Card title="Dashboard SuperAdmin">
                <p className="subtle sa-overviewHubIntro">
                  Acceso rápido a todos los módulos de administración global.
                </p>
                <div className="sa-overviewHubGrid" role="list" aria-label="Módulos de SuperAdmin">
                  <article className="sa-overviewHubCard" role="listitem">
                    <div className="sa-overviewHubCardTop">
                      <div className="sa-overviewHubCardTitle">🏢 {t('superadmin.tabCompanies')}</div>
                      {metrics ? <span className="badge badge--gray">{metrics.totals.totalSubscribers} subscribers</span> : null}
                    </div>
                    <p className="subtle sa-overviewHubCardBody">Gestiona empresas, suscripciones y acceso a subscriber.</p>
                    <Button className="sa-overviewHubCardAction" variant="secondary" size="sm" onClick={() => selectHomeTab('companies')}>
                      Ir a empresas
                    </Button>
                  </article>
                  <article className="sa-overviewHubCard" role="listitem">
                    <div className="sa-overviewHubCardTop">
                      <div className="sa-overviewHubCardTitle">💳 {t('superadmin.tabPlans')}</div>
                      {activePlans.length > 0 ? <span className="badge badge--gray">{activePlans.length} activos</span> : null}
                    </div>
                    <p className="subtle sa-overviewHubCardBody">Configura planes, precios y asignaciones comerciales.</p>
                    <Button className="sa-overviewHubCardAction" variant="secondary" size="sm" onClick={() => selectHomeTab('plans')}>
                      Ir a planes
                    </Button>
                  </article>
                  <article className="sa-overviewHubCard" role="listitem">
                    <div className="sa-overviewHubCardTop">
                      <div className="sa-overviewHubCardTitle">🧰 Configuración mínima</div>
                      <span className="badge badge--gray">Simplificado</span>
                    </div>
                    <p className="subtle sa-overviewHubCardBody">El sistema usa plan comercial sin configuración granular de features.</p>
                    <Button className="sa-overviewHubCardAction" variant="secondary" size="sm" onClick={() => selectHomeTab('menus')}>
                      Ir a configuración
                    </Button>
                  </article>
                  <article className="sa-overviewHubCard" role="listitem">
                    <div className="sa-overviewHubCardTop">
                      <div className="sa-overviewHubCardTitle">🧭 {t('superadmin.shell.menuAndPlans')}</div>
                      {metrics ? <span className="badge badge--gray">{metrics.totals.activeUsers} usuarios activos</span> : null}
                    </div>
                    <p className="subtle sa-overviewHubCardBody">Define menú maestro, activaciones por plan y vista previa.</p>
                    <Button className="sa-overviewHubCardAction" variant="primary" size="sm" onClick={() => selectHomeTab('menus')}>
                      Ir a menú y planes
                    </Button>
                  </article>
                </div>
            </Card>
          </div>
        ) : homeTab === 'companies' ? (
          <Card
            title={t('superadmin.subscriberPicker')}
            actions={
              <Button variant="primary" size="sm" onClick={openCreateSubscriber} disabled={loading || switching !== null}>
                {t('superadmin.createSubscriber')}
              </Button>
            }
          >
              <div className="sa-search-row">
                <Input
                  label={t('superadmin.searchPlaceholder')}
                  value={q}
                  onChange={(e) => setQ(e.target.value)}
                  placeholder={t('superadmin.searchPlaceholder')}
                  disabled={loading || switching !== null}
                />
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
                          <Button
                            variant="secondary"
                            size="sm"
                            disabled={switching !== null}
                            onClick={() => openSubscriptionModal(subscriber)}
                          >
                            {t('superadmin.subscriberRow.changeSubscription')}
                          </Button>
                          <Button
                            variant="secondary"
                            size="sm"
                            disabled={switching !== null}
                            onClick={() => goToCompaniesSubscriberDetail(navigate, subscriber.id)}
                          >
                            {t('superadmin.subscriberRow.companyData')}
                          </Button>
                          <Button
                            variant="primary"
                            size="sm"
                            onClick={() => void handleSwitch(subscriber)}
                            disabled={switching !== null || !subscriber.isActive}
                            title={!subscriber.isActive ? t('superadmin.subscriberRow.enterDisabledInactive') : undefined}
                          >
                            {switching === subscriber.id ? t('superadmin.switching') : t('superadmin.enter')}
                          </Button>
                        </ZHInlineRowRight>
                      </div>
                    </div>
                  ))}
                </div>
              )}
          </Card>
        ) : homeTab === 'plans' ? (
          <>{isSuperAdmin ? <SuperAdminPlansSection /> : null}</>
        ) : (
          <>{isSuperAdmin ? <SuperAdminMenuBuilderSection /> : null}</>
        )}
      </ZHDashboardScaffold>

      {createSubscriberOpen ? (
        <Modal
          onClose={() => (createBusy ? undefined : setCreateSubscriberOpen(false))}
          size="lg"
          header={
            <ZHModalHeader
              title={t('superadmin.createSubscriber')}
              subtitle={t('superadmin.createSubscriberSubtitle')}
              onClose={() => (createBusy ? undefined : setCreateSubscriberOpen(false))}
            />
          }
        >
          {createError ? <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={createError} /> : null}
          <ZHGridRow cols={2}>
            <ZHField label={t('superadmin.createSubscriber.field.name')}>
              <input
                className="zh-input"
                value={createForm.subscriberName}
                onChange={(e) =>
                  setCreateForm((s) => {
                    const name = e.target.value;
                    const nextSlug = s.subscriberSlug.trim() ? s.subscriberSlug : slugify(name);
                    return { ...s, subscriberName: name, subscriberSlug: nextSlug };
                  })
                }
                disabled={createBusy}
              />
            </ZHField>
            <ZHField label={t('superadmin.createSubscriber.field.slug')}>
              <input
                className="zh-input"
                value={createForm.subscriberSlug}
                onChange={(e) => setCreateForm((s) => ({ ...s, subscriberSlug: e.target.value }))}
                disabled={createBusy}
              />
            </ZHField>
          </ZHGridRow>
          <ZHGridRow cols={3}>
            <ZHField label={t('superadmin.createSubscriber.field.ruc')}>
              <input
                className="zh-input"
                value={createForm.ruc ?? ''}
                onChange={(e) => setCreateForm((s) => ({ ...s, ruc: e.target.value }))}
                placeholder={t('superadmin.createSubscriber.field.rucPlaceholder')}
                disabled={createBusy}
              />
            </ZHField>
            <ZHField label={t('superadmin.createSubscriber.field.countryCode')}>
              <input
                className="zh-input"
                value={createForm.countryCode ?? 'ECU'}
                onChange={(e) => setCreateForm((s) => ({ ...s, countryCode: e.target.value }))}
                disabled={createBusy}
              />
            </ZHField>
            <ZHField label={t('superadmin.createSubscriber.field.timezone')}>
              <input
                className="zh-input"
                value={createForm.timezone ?? 'America/Guayaquil'}
                onChange={(e) => setCreateForm((s) => ({ ...s, timezone: e.target.value }))}
                disabled={createBusy}
              />
            </ZHField>
          </ZHGridRow>
          <ZHGridRow cols={2}>
            <ZHField label={t('superadmin.createSubscriber.field.planCode')}>
              <select
                className="zh-input"
                value={createPlanCode}
                onChange={(e) => setCreatePlanCode(e.target.value)}
                disabled={createBusy || activePlans.length === 0}
                required
              >
                <option value="">
                  {activePlans.length === 0
                    ? t('superadmin.createSubscriber.planSelectNoPlans')
                    : t('superadmin.createSubscriber.planSelectPlaceholder')}
                </option>
                {activePlans.map((p) => (
                  <option key={p.id} value={p.code}>
                    {p.name.trim() ? `${p.name} (${p.code})` : p.code}
                  </option>
                ))}
              </select>
            </ZHField>
            <div />
          </ZHGridRow>
          <label className="zh-inline-check">
            <input
              type="checkbox"
              checked={createRestrictModules}
              onChange={(e) => {
                const on = e.target.checked;
                setCreateRestrictModules(on);
                if (on) setCreateModuleChecks(defaultModuleChecksAllOn());
              }}
              disabled={createBusy}
            />
            {t('superadmin.createSubscriber.field.restrictModules')}
          </label>
          {createRestrictModules ? (
            <p className="subtle sa-modules-hint">
              {t('superadmin.createSubscriber.modulesHint')}
            </p>
          ) : null}
          {createRestrictModules ? (
            <div className="sa-moduleChecks">
              {SUBSCRIBER_MODULE_KEYS.map((k) => (
                <label key={k} className="zh-inline-check sa-moduleCheck">
                  <input
                    type="checkbox"
                    checked={!!createModuleChecks[k]}
                    onChange={() =>
                      setCreateModuleChecks((s) => ({
                        ...s,
                        [k]: !s[k],
                      }))
                    }
                    disabled={createBusy}
                  />
                  {moduleLabel(k)}
                </label>
              ))}
            </div>
          ) : null}
          <ZHGridRow cols={2}>
            <ZHField label={t('superadmin.createSubscriber.field.adminFirstName')}>
              <input
                className="zh-input"
                value={createForm.adminFirstName}
                onChange={(e) => setCreateForm((s) => ({ ...s, adminFirstName: e.target.value }))}
                disabled={createBusy}
              />
            </ZHField>
            <ZHField label={t('superadmin.createSubscriber.field.adminLastName')}>
              <input
                className="zh-input"
                value={createForm.adminLastName}
                onChange={(e) => setCreateForm((s) => ({ ...s, adminLastName: e.target.value }))}
                disabled={createBusy}
              />
            </ZHField>
          </ZHGridRow>
          <ZHGridRow cols={2}>
            <ZHField label={t('superadmin.createSubscriber.field.adminEmail')}>
              <input
                className="zh-input"
                value={createForm.adminEmail}
                onChange={(e) => setCreateForm((s) => ({ ...s, adminEmail: e.target.value }))}
                disabled={createBusy}
              />
            </ZHField>
            <ZHField label={t('superadmin.createSubscriber.field.passwordResetMode')}>
              <select
                className="zh-input"
                value={String(createForm.passwordResetMode ?? 0)}
                onChange={(e) =>
                  setCreateForm((s) => ({ ...s, passwordResetMode: Number(e.target.value) }))
                }
                disabled={createBusy}
              >
                <option value={0}>{t('superadmin.createSubscriber.passwordResetMode.disabled')}</option>
                <option value={2}>{t('superadmin.createSubscriber.passwordResetMode.email')}</option>
                <option value={1}>{t('superadmin.createSubscriber.passwordResetMode.admin')}</option>
              </select>
            </ZHField>
          </ZHGridRow>
          <label className="zh-inline-check">
            <input
              type="checkbox"
              checked={!!createForm.linkExistingAdmin}
              onChange={(e) => setCreateForm((s) => ({ ...s, linkExistingAdmin: e.target.checked }))}
              disabled={createBusy}
            />
            {t('superadmin.createSubscriber.field.linkExistingAdmin')}
          </label>
          {!createForm.linkExistingAdmin ? (
            <ZHField label={t('superadmin.createSubscriber.field.adminPassword')}>
              <input
                className="zh-input"
                type="password"
                value={createForm.adminPassword}
                onChange={(e) => setCreateForm((s) => ({ ...s, adminPassword: e.target.value }))}
                disabled={createBusy}
              />
            </ZHField>
          ) : null}
          <ZHInlineRowRight>
            <Button variant="secondary" size="sm" onClick={() => setCreateSubscriberOpen(false)} disabled={createBusy}>
              {t('common.cancel')}
            </Button>
            <Button variant="primary" size="sm" onClick={() => void saveCreateSubscriber()} disabled={createBusy}>
              {t('common.save')}
            </Button>
          </ZHInlineRowRight>
        </Modal>
      ) : null}

      {subModalOpen && subModalSubscriber ? (
        <Modal
          onClose={() => (subBusy ? undefined : setSubModalOpen(false))}
          size="lg"
          header={
            <ZHModalHeader
              title={t('superadmin.changeSubscription.title')}
              subtitle={t('superadmin.changeSubscription.subtitle').replace(
                '{name}',
                subModalSubscriber.name,
              )}
              onClose={() => (subBusy ? undefined : setSubModalOpen(false))}
            />
          }
        >
          {subError ? <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={subError} /> : null}
          <ZHGridRow cols={2}>
            <ZHField label={t('superadmin.createSubscriber.field.planCode')}>
              <select
                className="zh-input"
                value={subPlanCode}
                onChange={(e) => setSubPlanCode(e.target.value)}
                disabled={subBusy}
              >
                <option value="">{t('superadmin.createSubscriber.planOptional')}</option>
                {activePlans.map((p) => (
                  <option key={p.id} value={p.code}>
                    {p.name.trim() ? `${p.name} (${p.code})` : p.code}
                  </option>
                ))}
              </select>
            </ZHField>
            <div />
          </ZHGridRow>
          <label className="zh-inline-check">
            <input
              type="checkbox"
              checked={subRestrict}
              onChange={(e) => {
                const on = e.target.checked;
                setSubRestrict(on);
                if (on) setSubModuleChecks(defaultModuleChecksAllOn());
              }}
              disabled={subBusy}
            />
            {t('superadmin.createSubscriber.field.restrictModules')}
          </label>
          {subRestrict ? (
            <p className="subtle sa-modules-hint">
              {t('superadmin.createSubscriber.modulesHint')}
            </p>
          ) : null}
          {subRestrict ? (
            <div className="sa-moduleChecks">
              {SUBSCRIBER_MODULE_KEYS.map((k) => (
                <label key={k} className="zh-inline-check sa-moduleCheck">
                  <input
                    type="checkbox"
                    checked={!!subModuleChecks[k]}
                    onChange={() =>
                      setSubModuleChecks((s) => ({
                        ...s,
                        [k]: !s[k],
                      }))
                    }
                    disabled={subBusy}
                  />
                  {moduleLabel(k)}
                </label>
              ))}
            </div>
          ) : null}
          <ZHInlineRowRight>
            <Button variant="secondary" size="sm" onClick={() => setSubModalOpen(false)} disabled={subBusy}>
              {t('common.cancel')}
            </Button>
            <Button variant="primary" size="sm" onClick={() => void saveSubscriptionModal()} disabled={subBusy}>
              {t('common.save')}
            </Button>
          </ZHInlineRowRight>
        </Modal>
      ) : null}
    </SuperAdminPageTemplate>
  );
}

