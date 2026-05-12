import { useCallback, useEffect, useMemo, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { TableCard, EmptyState, LoadingState, Badge } from '../components/PageShell';
import { ZHPageNotice } from '../components/zh/ZHPageNotice';
import { SuperAdminPageTemplate } from '../components/superadmin/SuperAdminPageTemplate';
import { useSuperAdminGate } from '../hooks/useSuperAdminGate';
import { Modal } from '../components/Modal';
import { ZHModalHeader } from '../components/zh/ZHModalHeader';
import { TENANT_MODULE_KEYS } from '../constants/subscriptionModules';
import { superAdminService, type CreateTenantWithAdminBody, type SuperAdminPlan, type SuperAdminTenant } from '../services/superAdminService';
import { useAuthStore } from '../store/authStore';
import { usePermissionsStore } from '../store/permissionsStore';
import { CompanyModuleChips } from '../components/saas/CompanyModuleChips';
import { useI18n } from '../i18n/i18n';
import { ZHBtn, ZHField } from '../components/zh/ZHForm';
import { ZHCardSection, ZHGridRow, ZHInlineRowRight } from '../components/zh/ZHLayout';
import { ZHDashboardScaffold, ZHKpiPanel } from '../components/zh/ZHDashboard';
import { SuperAdminGrowthSection } from '../components/superadmin/SuperAdminGrowthSection';
import { SuperAdminFeaturesSection } from '../components/superadmin/SuperAdminFeaturesSection';
import { SuperAdminPlansSection } from '../components/superadmin/SuperAdminPlansSection';
import { SuperAdminMenuBuilderSection } from '../components/superadmin/SuperAdminMenuBuilderSection';
import { formatApiRequestError } from '../modules/lib/apiError';
import { goToCompaniesTenantDetail } from '../navigation/companiesTenantDetailNav';
import type { SessionResponse } from '../types/access';
import '../components/zh/ZHFormTabs.css';
import './SuperAdminPanelPage.css';

type SuperAdminHomeTab = 'overview' | 'companies' | 'features' | 'plans' | 'menus';

function defaultModuleChecksAllOn(): Record<string, boolean> {
  const o: Record<string, boolean> = {};
  for (const k of TENANT_MODULE_KEYS) o[k] = true;
  return o;
}

/** Si no restringe, o marca todos, equivale a sin JSON de módulos en backend. */
function normalizeEnabledModulesForApi(
  restrict: boolean,
  checks: Record<string, boolean>,
): string[] | undefined {
  if (!restrict) return undefined;
  const selected = TENANT_MODULE_KEYS.filter((k) => checks[k]);
  if (selected.length === 0) return undefined;
  if (selected.length === TENANT_MODULE_KEYS.length) return undefined;
  return [...selected];
}

function storeImpersonationTenantName(name: string) {
  localStorage.setItem('superadmin-impersonation-tenant-name', name);
}

export function SuperAdminPanelPage() {
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const { login } = useAuthStore();
  const clearPermissions = usePermissionsStore((s) => s.clearPermissions);
  const { isSuperAdmin, hasSelectedTenant } = useSuperAdminGate();
  const { t } = useI18n();

  const tabParam = searchParams.get('tab');
  const homeTab: SuperAdminHomeTab =
    tabParam === 'companies' || tabParam === 'features' || tabParam === 'plans' || tabParam === 'menus'
      ? tabParam
      : 'overview';

  const selectHomeTab = (tab: SuperAdminHomeTab) => {
    if (tab === 'overview') setSearchParams({}, { replace: true });
    else setSearchParams({ tab }, { replace: true });
  };

  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [metrics, setMetrics] = useState<Awaited<ReturnType<typeof superAdminService.getMetrics>> | null>(null);
  const [tenants, setTenants] = useState<SuperAdminTenant[]>([]);
  const [q, setQ] = useState('');
  const [switching, setSwitching] = useState<string | null>(null);
  const [plans, setPlans] = useState<SuperAdminPlan[]>([]);

  const [createTenantOpen, setCreateTenantOpen] = useState(false);
  const [createBusy, setCreateBusy] = useState(false);
  const [createError, setCreateError] = useState('');
  const [createForm, setCreateForm] = useState<CreateTenantWithAdminBody>({
    tenantName: '',
    tenantSlug: '',
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
  const [subModalTenant, setSubModalTenant] = useState<SuperAdminTenant | null>(null);
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

  const refreshTenantsAndMetrics = useCallback(async () => {
    const [m, tns] = await Promise.all([
      superAdminService.getMetrics(),
      superAdminService.getTenants(),
    ]);
    setMetrics(m);
    setTenants(tns);
  }, []);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        setLoading(true);
        setError('');
        await refreshTenantsAndMetrics();
        if (cancelled) return;
      } catch (e) {
        if (cancelled) return;
        setError(formatApiRequestError(e, { offline: t('common.apiUnreachable'), generic: t('common.errorGeneric') }));
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => { cancelled = true; };
  }, [refreshTenantsAndMetrics, t]);

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
    if (!query) return tenants;
    return tenants.filter((t) =>
      t.name.toLowerCase().includes(query) || t.slug.toLowerCase().includes(query) || t.id.toLowerCase().includes(query)
    );
  }, [tenants, q]);

  const planLabelForTenant = useMemo(() => {
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

  const handleSwitch = async (tenant: SuperAdminTenant) => {
    if (!tenant.isActive) return;
    setSwitching(tenant.id);
    setError('');
    try {
      const auth = await superAdminService.switchTenant(tenant.id);
      storeImpersonationTenantName(tenant.name);
      clearPermissions();
      login(auth);
      navigate('/dashboard');
    } catch (e) {
      setError(formatApiRequestError(e, { offline: t('common.apiUnreachable'), generic: t('common.errorGeneric') }));
    } finally {
      setSwitching(null);
    }
  };

  const openCreateTenant = () => {
    setCreateError('');
    setCreatePlanCode('');
    setCreateRestrictModules(false);
    setCreateModuleChecks(defaultModuleChecksAllOn());
    setCreateForm({
      tenantName: '',
      tenantSlug: '',
      adminFirstName: '',
      adminLastName: '',
      adminEmail: '',
      adminPassword: '',
      passwordResetMode: 0,
      linkExistingAdmin: false,
    });
    setCreateTenantOpen(true);
  };

  const openSubscriptionModal = (tenant: SuperAdminTenant) => {
    setSubModalTenant(tenant);
    setSubPlanCode((tenant.planCode ?? '').trim());
    const has =
      !!tenant.hasModuleRestrictions &&
      !!tenant.enabledModules &&
      tenant.enabledModules.length > 0;
    setSubRestrict(has);
    const checks: Record<string, boolean> = {};
    for (const k of TENANT_MODULE_KEYS) {
      if (has && tenant.enabledModules?.length) {
        checks[k] = tenant.enabledModules.some((em) => em.toLowerCase() === k.toLowerCase());
      } else {
        checks[k] = true;
      }
    }
    setSubModuleChecks(checks);
    setSubError('');
    setSubModalOpen(true);
  };

  const saveCreateTenant = async () => {
    setCreateBusy(true);
    setCreateError('');
    try {
      const tenantName = createForm.tenantName.trim();
      const tenantSlug = (createForm.tenantSlug.trim() || slugify(tenantName)).toLowerCase();
      const adminEmail = createForm.adminEmail.trim().toLowerCase();

      if (activePlans.length === 0) {
        setCreateError(t('superadmin.createTenant.error.noPlans'));
        return;
      }
      if (!createPlanCode.trim()) {
        setCreateError(t('superadmin.createTenant.error.planRequired'));
        return;
      }

      if (createRestrictModules) {
        const n = TENANT_MODULE_KEYS.filter((k) => createModuleChecks[k]).length;
        if (n === 0) {
          setCreateError(t('superadmin.createTenant.error.noModulesSelected'));
          return;
        }
      }

      const planCodeNorm = createPlanCode.trim();
      const enabledModules = normalizeEnabledModulesForApi(createRestrictModules, createModuleChecks);

      const session: SessionResponse = await superAdminService.createTenantWithAdmin({
        ...createForm,
        tenantName,
        tenantSlug,
        adminEmail,
        adminPassword: createForm.linkExistingAdmin ? '' : createForm.adminPassword,
        planCode: planCodeNorm,
        enabledModules: enabledModules === undefined ? null : enabledModules,
      });

      setError('');
      setCreateTenantOpen(false);
      goToCompaniesTenantDetail(navigate, session.tenantId);
    } catch (e) {
      setCreateError(formatApiRequestError(e, { offline: t('common.apiUnreachable'), generic: t('common.errorGeneric') }));
    } finally {
      setCreateBusy(false);
    }
  };

  const saveSubscriptionModal = async () => {
    if (!subModalTenant) return;
    setSubBusy(true);
    setSubError('');
    try {
      if (subRestrict) {
        const n = TENANT_MODULE_KEYS.filter((k) => subModuleChecks[k]).length;
        if (n === 0) {
          setSubError(t('superadmin.changeSubscription.error.noModulesSelected'));
          return;
        }
      }
      const planCode = subPlanCode.trim() || null;
      const enabledModulesNorm = normalizeEnabledModulesForApi(subRestrict, subModuleChecks);

      await superAdminService.updateTenantSubscription(subModalTenant.id, {
        planCode,
        enabledModules: enabledModulesNorm === undefined ? null : enabledModulesNorm,
      });
      await refreshTenantsAndMetrics();
      setSubModalOpen(false);
      setSubModalTenant(null);
    } catch (e) {
      setSubError(formatApiRequestError(e, { offline: t('common.apiUnreachable'), generic: t('common.errorGeneric') }));
    } finally {
      setSubBusy(false);
    }
  };

  return (
    <SuperAdminPageTemplate
      title={t('superadmin.title')}
      subtitle={isSuperAdmin && !hasSelectedTenant ? t('superadmin.subtitle') : undefined}
      tenantGuardAction={
        <ZHBtn variant="primary" size="md" type="button" onClick={() => navigate('/dashboard')}>
          {t('superadmin.goToTenant')}
        </ZHBtn>
      }
    >
      {error ? <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={error} /> : null}

      <ZHDashboardScaffold>
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
              aria-selected={homeTab === 'features'}
              className={homeTab === 'features' ? 'is-active' : ''}
              onClick={() => selectHomeTab('features')}
            >
              {t('superadmin.tabFeatures')}
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

        {homeTab === 'overview' ? (
          <div className="sa-overviewKpi">
            {!loading && tenants.length === 0 && isSuperAdmin ? (
              <TableCard>
                <ZHCardSection title={t('superadmin.welcomeNoTenantsTitle')}>
                  <p className="subtle" style={{ marginBottom: '1rem' }}>
                    {t('superadmin.welcomeNoTenantsBody')}
                  </p>
                  <ZHInlineRowRight>
                    <ZHBtn
                      variant="ghost"
                      size="md"
                      type="button"
                      onClick={() => selectHomeTab('plans')}
                    >
                      {t('superadmin.welcomeGoPlans')}
                    </ZHBtn>
                    <ZHBtn
                      variant="primary"
                      size="md"
                      type="button"
                      onClick={() => {
                        selectHomeTab('companies');
                        openCreateTenant();
                      }}
                      disabled={activePlans.length === 0}
                      title={activePlans.length === 0 ? t('superadmin.createTenant.error.noPlans') : undefined}
                    >
                      {t('superadmin.welcomeCreateFirstCompany')}
                    </ZHBtn>
                  </ZHInlineRowRight>
                </ZHCardSection>
              </TableCard>
            ) : null}
            {loading ? (
              <TableCard>
                <LoadingState />
              </TableCard>
            ) : metrics ? (
              <ZHKpiPanel
                title={t('superadmin.metrics')}
                items={[
                  { label: t('superadmin.totalTenants'), value: String(metrics.totals.totalTenants), tone: 'neutral' },
                  { label: t('superadmin.activeTenants'), value: String(metrics.totals.activeTenants), tone: 'info' },
                  { label: t('superadmin.totalUsers'), value: String(metrics.totals.totalUsers), tone: 'neutral' },
                  { label: t('superadmin.activeUsers'), value: String(metrics.totals.activeUsers), tone: 'success' },
                ]}
              />
            ) : null}
            {isSuperAdmin ? <SuperAdminGrowthSection /> : null}
          </div>
        ) : homeTab === 'companies' ? (
          <TableCard>
            <ZHCardSection title={t('superadmin.tenantPicker')}>
              <ZHInlineRowRight>
                <ZHBtn variant="primary" size="md" type="button" onClick={openCreateTenant} disabled={loading || switching !== null}>
                  {t('superadmin.createTenant')}
                </ZHBtn>
              </ZHInlineRowRight>
              <ZHGridRow cols={1}>
                <ZHField label={t('superadmin.searchPlaceholder')}>
                  <input
                    value={q}
                    onChange={(e) => setQ(e.target.value)}
                    placeholder={t('superadmin.searchPlaceholder')}
                    disabled={loading || switching !== null}
                  />
                </ZHField>
              </ZHGridRow>

              {loading ? (
                <LoadingState />
              ) : error && tenants.length === 0 ? (
                <EmptyState message={t('superadmin.sectionLoadHint')} />
              ) : filtered.length === 0 ? (
                <EmptyState message={t('common.noData')} />
              ) : (
                <div className="sa-tenantList">
                  {filtered.map((tenant) => (
                    <div key={tenant.id} className="sa-tenantRow">
                      <div className="sa-tenantName">{tenant.name}</div>
                      <div className="sa-tenantMeta">
                        <span className="mono">{tenant.slug}</span>
                        <span className="mono">{tenant.id}</span>
                      </div>
                      <div className="sa-tenant-stats">
                        <Badge
                          label={tenant.isActive ? t('common.active') : t('common.inactive')}
                          variant={tenant.isActive ? 'green' : 'gray'}
                        />
                        <span className="subtle">
                          {t('common.users') ?? 'Usuarios'}: <strong>{tenant.totalUsers}</strong> · {t('common.active')}:{' '}
                          <strong>{tenant.activeUsers}</strong>
                        </span>
                        <span className="subtle">{new Date(tenant.createdAt).toLocaleDateString()}</span>
                      </div>
                      <div className="sa-tenantPlanModules">
                        <div className="sa-tenantPlanRow subtle">
                          <span className="sa-tenantPlanKey">{t('superadmin.tenantRow.plan')}:</span>{' '}
                          <strong className="mono">
                            {planLabelForTenant(tenant.planCode) || t('superadmin.tenantRow.planUnset')}
                          </strong>
                        </div>
                        <div className="sa-tenantModulesRow">
                          <span className="sa-tenantModulesKey subtle">{t('superadmin.tenantRow.modules')}:</span>{' '}
                          <CompanyModuleChips
                            company={{
                              enabledModules: tenant.enabledModules,
                              hasModuleRestrictions: tenant.hasModuleRestrictions,
                            }}
                          />
                        </div>
                      </div>
                      <div className="sa-tenant-actions">
                        <ZHInlineRowRight>
                          <ZHBtn
                            variant="ghost"
                            size="md"
                            type="button"
                            disabled={switching !== null}
                            onClick={() => openSubscriptionModal(tenant)}
                          >
                            {t('superadmin.tenantRow.changeSubscription')}
                          </ZHBtn>
                          <ZHBtn
                            variant="ghost"
                            size="md"
                            type="button"
                            disabled={switching !== null}
                            onClick={() => goToCompaniesTenantDetail(navigate, tenant.id)}
                          >
                            {t('superadmin.tenantRow.companyData')}
                          </ZHBtn>
                          <ZHBtn
                            variant="primary"
                            size="md"
                            type="button"
                            onClick={() => void handleSwitch(tenant)}
                            disabled={switching !== null || !tenant.isActive}
                            title={!tenant.isActive ? t('superadmin.tenantRow.enterDisabledInactive') : undefined}
                          >
                            {switching === tenant.id ? t('superadmin.switching') : t('superadmin.enter')}
                          </ZHBtn>
                        </ZHInlineRowRight>
                      </div>
                    </div>
                  ))}
                </div>
              )}
            </ZHCardSection>
          </TableCard>
        ) : homeTab === 'features' ? (
          <>{isSuperAdmin ? <SuperAdminFeaturesSection /> : null}</>
        ) : homeTab === 'plans' ? (
          <>{isSuperAdmin ? <SuperAdminPlansSection /> : null}</>
        ) : (
          <>{isSuperAdmin ? <SuperAdminMenuBuilderSection /> : null}</>
        )}
      </ZHDashboardScaffold>

      {createTenantOpen ? (
        <Modal
          onClose={() => (createBusy ? undefined : setCreateTenantOpen(false))}
          size="lg"
          header={
            <ZHModalHeader
              title={t('superadmin.createTenant')}
              subtitle={t('superadmin.createTenantSubtitle')}
              onClose={() => (createBusy ? undefined : setCreateTenantOpen(false))}
            />
          }
        >
          {createError ? <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={createError} /> : null}
          <ZHGridRow cols={2}>
            <ZHField label={t('superadmin.createTenant.field.name')}>
              <input
                className="zh-input"
                value={createForm.tenantName}
                onChange={(e) =>
                  setCreateForm((s) => {
                    const name = e.target.value;
                    const nextSlug = s.tenantSlug.trim() ? s.tenantSlug : slugify(name);
                    return { ...s, tenantName: name, tenantSlug: nextSlug };
                  })
                }
                disabled={createBusy}
              />
            </ZHField>
            <ZHField label={t('superadmin.createTenant.field.slug')}>
              <input
                className="zh-input"
                value={createForm.tenantSlug}
                onChange={(e) => setCreateForm((s) => ({ ...s, tenantSlug: e.target.value }))}
                disabled={createBusy}
              />
            </ZHField>
          </ZHGridRow>
          <ZHGridRow cols={2}>
            <ZHField label={t('superadmin.createTenant.field.planCode')}>
              <select
                className="zh-input"
                value={createPlanCode}
                onChange={(e) => setCreatePlanCode(e.target.value)}
                disabled={createBusy || activePlans.length === 0}
                required
              >
                <option value="">
                  {activePlans.length === 0
                    ? t('superadmin.createTenant.planSelectNoPlans')
                    : t('superadmin.createTenant.planSelectPlaceholder')}
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
          <label className="sap-check">
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
            {t('superadmin.createTenant.field.restrictModules')}
          </label>
          {createRestrictModules ? (
            <p className="subtle" style={{ marginTop: 6 }}>
              {t('superadmin.createTenant.modulesHint')}
            </p>
          ) : null}
          {createRestrictModules ? (
            <div className="sa-moduleChecks">
              {TENANT_MODULE_KEYS.map((k) => (
                <label key={k} className="sap-check sa-moduleCheck">
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
            <ZHField label={t('superadmin.createTenant.field.adminFirstName')}>
              <input
                className="zh-input"
                value={createForm.adminFirstName}
                onChange={(e) => setCreateForm((s) => ({ ...s, adminFirstName: e.target.value }))}
                disabled={createBusy}
              />
            </ZHField>
            <ZHField label={t('superadmin.createTenant.field.adminLastName')}>
              <input
                className="zh-input"
                value={createForm.adminLastName}
                onChange={(e) => setCreateForm((s) => ({ ...s, adminLastName: e.target.value }))}
                disabled={createBusy}
              />
            </ZHField>
          </ZHGridRow>
          <ZHGridRow cols={2}>
            <ZHField label={t('superadmin.createTenant.field.adminEmail')}>
              <input
                className="zh-input"
                value={createForm.adminEmail}
                onChange={(e) => setCreateForm((s) => ({ ...s, adminEmail: e.target.value }))}
                disabled={createBusy}
              />
            </ZHField>
            <ZHField label={t('superadmin.createTenant.field.passwordResetMode')}>
              <select
                className="zh-input"
                value={String(createForm.passwordResetMode ?? 0)}
                onChange={(e) =>
                  setCreateForm((s) => ({ ...s, passwordResetMode: Number(e.target.value) }))
                }
                disabled={createBusy}
              >
                <option value={0}>{t('superadmin.createTenant.passwordResetMode.disabled')}</option>
                <option value={2}>{t('superadmin.createTenant.passwordResetMode.email')}</option>
                <option value={1}>{t('superadmin.createTenant.passwordResetMode.admin')}</option>
              </select>
            </ZHField>
          </ZHGridRow>
          <label className="sap-check">
            <input
              type="checkbox"
              checked={!!createForm.linkExistingAdmin}
              onChange={(e) => setCreateForm((s) => ({ ...s, linkExistingAdmin: e.target.checked }))}
              disabled={createBusy}
            />
            {t('superadmin.createTenant.field.linkExistingAdmin')}
          </label>
          {!createForm.linkExistingAdmin ? (
            <ZHField label={t('superadmin.createTenant.field.adminPassword')}>
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
            <ZHBtn variant="ghost" type="button" onClick={() => setCreateTenantOpen(false)} disabled={createBusy}>
              {t('common.cancel')}
            </ZHBtn>
            <ZHBtn variant="primary" type="button" onClick={() => void saveCreateTenant()} disabled={createBusy}>
              {t('common.save')}
            </ZHBtn>
          </ZHInlineRowRight>
        </Modal>
      ) : null}

      {subModalOpen && subModalTenant ? (
        <Modal
          onClose={() => (subBusy ? undefined : setSubModalOpen(false))}
          size="lg"
          header={
            <ZHModalHeader
              title={t('superadmin.changeSubscription.title')}
              subtitle={t('superadmin.changeSubscription.subtitle').replace(
                '{name}',
                subModalTenant.name,
              )}
              onClose={() => (subBusy ? undefined : setSubModalOpen(false))}
            />
          }
        >
          {subError ? <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={subError} /> : null}
          <ZHGridRow cols={2}>
            <ZHField label={t('superadmin.createTenant.field.planCode')}>
              <select
                className="zh-input"
                value={subPlanCode}
                onChange={(e) => setSubPlanCode(e.target.value)}
                disabled={subBusy}
              >
                <option value="">{t('superadmin.createTenant.planOptional')}</option>
                {activePlans.map((p) => (
                  <option key={p.id} value={p.code}>
                    {p.name.trim() ? `${p.name} (${p.code})` : p.code}
                  </option>
                ))}
              </select>
            </ZHField>
            <div />
          </ZHGridRow>
          <label className="sap-check">
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
            {t('superadmin.createTenant.field.restrictModules')}
          </label>
          {subRestrict ? (
            <p className="subtle" style={{ marginTop: 6 }}>
              {t('superadmin.createTenant.modulesHint')}
            </p>
          ) : null}
          {subRestrict ? (
            <div className="sa-moduleChecks">
              {TENANT_MODULE_KEYS.map((k) => (
                <label key={k} className="sap-check sa-moduleCheck">
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
            <ZHBtn variant="ghost" type="button" onClick={() => setSubModalOpen(false)} disabled={subBusy}>
              {t('common.cancel')}
            </ZHBtn>
            <ZHBtn variant="primary" type="button" onClick={() => void saveSubscriptionModal()} disabled={subBusy}>
              {t('common.save')}
            </ZHBtn>
          </ZHInlineRowRight>
        </Modal>
      ) : null}
    </SuperAdminPageTemplate>
  );
}

