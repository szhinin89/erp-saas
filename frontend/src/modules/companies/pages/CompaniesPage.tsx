import { useEffect, useLayoutEffect, useMemo, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useI18n } from '../../../i18n/i18n';
import { useAuthStore } from '../../../store/authStore';
import { companyService, type CompanyItem, type TenantDetailDto } from '../../../services/companyService';
import { CompanyModuleChips } from '../../../components/saas/CompanyModuleChips';
import {
  createCompanyWithAdminSchema,
  updateTenantCompanySchema,
  type CreateCompanyFormValues,
  type UpdateSubscriberCompanyFormValues,
} from '../../../schemas/saas/companySchema';
import { formatApiRequestError } from '../../lib/apiError';
import { useDeployment } from '../../../deployment/DeploymentContext';
import { PageShell, EmptyState, LoadingState, NoAccessPage, Badge } from '../../../components/PageShell';
import { EntityAuditPanel } from '../../../components/EntityAuditPanel';
import { ZHFormSection, ZHFormBody, ZHGrid, ZHField } from '../../../components/zh/ZHForm';
import { ZHPageNotice } from '../../../components/zh/ZHPageNotice';
import { Button, Card, Input } from '../../../components/ui';
import {
  clearCompaniesDetailSubscriberId,
  clearCompaniesSubscriptionSubscriberId,
  persistCompaniesDetailSubscriberId,
  readCompaniesDetailSubscriberId,
} from '../../../navigation/companiesSubscriberDetailNav';
import { NavigationMenuEditorPanel } from '../../../components/superadmin/NavigationMenuEditorPanel';
import { ConfigManagementPanel } from '../../../components/config/ConfigManagementPanel';
import { FeatureGate, useConfig } from '../../config';
import './CompaniesPage.css';

/** Pestañas sin «Ver empresas»: listado integrado en Datos. */
type CompanyTab = 'data' | 'globalParameters' | 'mainNavigation' | 'audit';
const ELECTRONIC_BILLING_TRIAL_KEY = 'billing.electronic.trial_enabled';

function CompaniesPage() {
  const { t } = useI18n();
  const { maxActiveTenants, maxIdentityUsers } = useDeployment();
  const [searchParams, setSearchParams] = useSearchParams();
  const user = useAuthStore((s) => s.user);
  const subscriberId = useAuthStore((s) => s.user?.subscriberId ?? '');
  const { getValue } = useConfig();

  const [items, setItems] = useState<CompanyItem[]>([]);
  const [listQuery, setListQuery] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [creating, setCreating] = useState(false);
  const [tab, setTab] = useState<CompanyTab>('data');
  const [auditSubscriberId, setAuditSubscriberId] = useState<string | null>(null);
  const [auditRefreshKey, setAuditRefreshKey] = useState(0);
  const emptyCompanyForm = (): CreateCompanyFormValues => ({
    subscriberName: '',
    subscriberSlug: '',
    ruc: '',
    shortName: '',
    tradeName: '',
    dinardap: '',
    logoUrl: '',
    displayOrder: 0,
    priority: 0,
    adminFirstName: '',
    adminLastName: '',
    adminEmail: '',
    adminPassword: '',
    linkExistingAdmin: false,
    passwordResetMode: 1,
  });

  const {
    register,
    handleSubmit,
    reset,
    watch,
    formState: { errors },
  } = useForm<CreateCompanyFormValues>({
    resolver: zodResolver(createCompanyWithAdminSchema),
    defaultValues: emptyCompanyForm(),
  });

  const linkExistingAdmin = watch('linkExistingAdmin');

  const planFilter = (searchParams.get('plan') ?? '').trim().toLowerCase();

  /** Subscriber cuya ficha se muestra en Datos; el id vive en memoria + sessionStorage (no en la URL). */
  const [detailSubscriberId, setDetailSubscriberId] = useState<string | null>(null);
  const [tenantDetail, setTenantDetail] = useState<TenantDetailDto | null>(null);
  const [detailLoading, setDetailLoading] = useState(false);
  const [detailError, setDetailError] = useState('');
  const [detailSaving, setDetailSaving] = useState(false);
  const [detailSaveError, setDetailSaveError] = useState('');
  const [detailSaveOk, setDetailSaveOk] = useState(false);
  const [globalParamSaving, setGlobalParamSaving] = useState(false);
  const [globalParamError, setGlobalParamError] = useState('');
  const [globalParamOk, setGlobalParamOk] = useState(false);
  const [electronicBillingTrialEnabled, setElectronicBillingTrialEnabled] = useState(false);
  const [globalParamScopeResolved, setGlobalParamScopeResolved] = useState<'global' | 'module' | 'feature' | ''>('');
  const [globalConfigCount, setGlobalConfigCount] = useState(0);
  const stockNegativeEnabled = Boolean(getValue<boolean>('ventas.stock.permitir_negativo', 'ventas', undefined, false));

  const emptyDetailCompanyForm = (): UpdateSubscriberCompanyFormValues => ({
    subscriberName: '',
    subscriberSlug: '',
    ruc: '',
    shortName: '',
    tradeName: '',
    dinardap: '',
    logoUrl: '',
    displayOrder: 0,
    priority: 0,
  });

  const {
    register: registerDetail,
    handleSubmit: handleSubmitDetail,
    reset: resetDetailForm,
    formState: { errors: detailFieldErrors },
  } = useForm<UpdateSubscriberCompanyFormValues>({
    resolver: zodResolver(updateTenantCompanySchema),
    defaultValues: emptyDetailCompanyForm(),
  });

  const filtered = useMemo(() => {
    let base = items;
    if (planFilter) {
      base = base.filter((x) => (x.planCode ?? '').trim().toLowerCase() === planFilter);
    }
    const query = listQuery.trim().toLowerCase();
    if (!query) return base;
    return base.filter((x) => `${x.name} ${x.slug} ${x.id}`.toLowerCase().includes(query));
  }, [listQuery, items, planFilter]);

  const refresh = async () => {
    setError('');
    setLoading(true);
    try {
      const nextItems = await companyService.list();
      setItems(Array.isArray(nextItems) ? nextItems : []);
    } catch {
      setError(t('companies.error.load'));
      setItems([]);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void Promise.resolve().then(refresh);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const clearTenantDetailView = () => {
    clearCompaniesDetailSubscriberId();
    clearCompaniesSubscriptionSubscriberId();
    setDetailSubscriberId(null);
    setTenantDetail(null);
    setDetailError('');
    setSearchParams(
      (prev) => {
        const next = new URLSearchParams(prev);
        next.delete('data');
        return next;
      },
      { replace: true },
    );
  };

  /** Migra queries legacy (`?data=`, `?subscription=`) a ficha en Datos y rehidrata al montar. */
  useLayoutEffect(() => {
    const openNav = searchParams.get('mainNavigation');
    if (openNav === '1' || openNav === 'true') {
      setTab('mainNavigation');
      setSearchParams(
        (prev) => {
          const next = new URLSearchParams(prev);
          next.delete('mainNavigation');
          return next;
        },
        { replace: true },
      );
      return;
    }
    const fromUrl = (searchParams.get('data') ?? '').trim();
    if (fromUrl) {
      clearCompaniesSubscriptionSubscriberId();
      persistCompaniesDetailSubscriberId(fromUrl);
      setDetailSubscriberId(fromUrl);
      setTab('data');
      setSearchParams(
        (prev) => {
          const next = new URLSearchParams(prev);
          next.delete('data');
          return next;
        },
        { replace: true },
      );
      return;
    }
    const subFromUrl = (searchParams.get('subscription') ?? '').trim();
    if (subFromUrl) {
      clearCompaniesSubscriptionSubscriberId();
      persistCompaniesDetailSubscriberId(subFromUrl);
      setDetailSubscriberId(subFromUrl);
      setTab('data');
      setSearchParams(
        (prev) => {
          const next = new URLSearchParams(prev);
          next.delete('subscription');
          return next;
        },
        { replace: true },
      );
    }
    const stored = readCompaniesDetailSubscriberId();
    if (stored) {
      setDetailSubscriberId((prev) => prev ?? stored);
      setTab('data');
    }
  }, [searchParams, setSearchParams]);

  useEffect(() => {
    if (!detailSubscriberId) {
      setTenantDetail(null);
      setDetailError('');
      setDetailLoading(false);
      return;
    }
    let cancelled = false;
    setDetailLoading(true);
    setDetailError('');
    (async () => {
      try {
        const d = await companyService.getTenant(detailSubscriberId);
        if (!cancelled) setTenantDetail(d);
      } catch {
        if (!cancelled) {
          setTenantDetail(null);
          setDetailError(t('companies.error.detailLoad'));
        }
      } finally {
        if (!cancelled) setDetailLoading(false);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [detailSubscriberId, t]);

  useEffect(() => {
    if (!tenantDetail) return;
    resetDetailForm({
      subscriberName: tenantDetail.name,
      subscriberSlug: tenantDetail.slug,
      ruc: tenantDetail.ruc ?? '',
      shortName: tenantDetail.shortName ?? '',
      tradeName: tenantDetail.tradeName ?? '',
      dinardap: tenantDetail.dinardap ?? '',
      logoUrl: tenantDetail.logoUrl ?? '',
      displayOrder: tenantDetail.displayOrder,
      priority: tenantDetail.priority,
    });
    setDetailSaveError('');
    setDetailSaveOk(false);
    setGlobalParamError('');
    setGlobalParamOk(false);
    setGlobalParamScopeResolved('');
    setGlobalConfigCount(0);
  }, [tenantDetail, resetDetailForm]);

  useEffect(() => {
    if (!detailSubscriberId || !tenantDetail) return;
    let cancelled = false;
    (async () => {
      try {
        const [resolved, globals] = await Promise.all([
          companyService.resolveTenantConfig(detailSubscriberId, ELECTRONIC_BILLING_TRIAL_KEY),
          companyService.listTenantGlobalConfig(detailSubscriberId),
        ]);
        if (cancelled) return;
        const resolvedValue = resolved?.value?.trim().toLowerCase();
        if (resolvedValue === 'true' || resolvedValue === 'false') {
          setElectronicBillingTrialEnabled(resolvedValue === 'true');
          setGlobalParamScopeResolved(
            resolved?.scopeResolved === 'feature' || resolved?.scopeResolved === 'module' || resolved?.scopeResolved === 'global'
              ? resolved.scopeResolved
              : '',
          );
        } else {
          setElectronicBillingTrialEnabled(tenantDetail.electronicBillingTrialEnabled);
          setGlobalParamScopeResolved('');
        }
        setGlobalConfigCount(Array.isArray(globals) ? globals.length : 0);
      } catch {
        if (!cancelled) {
          setElectronicBillingTrialEnabled(tenantDetail.electronicBillingTrialEnabled);
          setGlobalParamScopeResolved('');
          setGlobalConfigCount(0);
        }
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [detailSubscriberId, tenantDetail]);

  if (user?.role !== 'SuperAdmin') {
    return <NoAccessPage title={t('companies.title')} />;
  }

  const submit = handleSubmit(async (form) => {
    setError('');
    setCreating(true);
    try {
      const session = await companyService.create({
        subscriberName: form.subscriberName,
        subscriberSlug: form.subscriberSlug,
        ruc: form.ruc?.trim() || null,
        shortName: form.shortName?.trim() || null,
        tradeName: form.tradeName?.trim() || null,
        dinardap: form.dinardap?.trim() || null,
        logoUrl: form.logoUrl?.trim() || null,
        displayOrder: form.displayOrder,
        priority: form.priority,
        adminFirstName: form.linkExistingAdmin ? '' : form.adminFirstName,
        adminLastName: form.linkExistingAdmin ? '' : form.adminLastName,
        adminEmail: form.adminEmail,
        adminPassword: form.linkExistingAdmin ? '' : form.adminPassword,
        linkExistingAdmin: form.linkExistingAdmin,
        passwordResetMode: 1,
      });
      if (session?.subscriberId) {
        setAuditSubscriberId(session.subscriberId);
        setAuditRefreshKey((k) => k + 1);
      }
      reset(emptyCompanyForm());
      await refresh();
      setTab('data');
      if (session?.subscriberId) {
        persistCompaniesDetailSubscriberId(session.subscriberId);
        setDetailSubscriberId(session.subscriberId);
        setAuditSubscriberId(session.subscriberId);
        setAuditRefreshKey((k) => k + 1);
      }
    } catch (err: unknown) {
      const msg =
        (err as { response?: { data?: { message?: string } } })?.response?.data?.message ??
        t('companies.error.create');
      setError(msg);
    } finally {
      setCreating(false);
    }
  });

  const saveTenantCompanyDetail = handleSubmitDetail(async (values) => {
    if (!detailSubscriberId) return;
    setDetailSaving(true);
    setDetailSaveError('');
    setDetailSaveOk(false);
    try {
      const slug = (values.subscriberSlug.trim() || values.subscriberName.trim()).toLowerCase();
      const updated = await companyService.updateTenantCompany(detailSubscriberId, {
        name: values.subscriberName.trim(),
        slug,
        ruc: values.ruc?.trim() || null,
        shortName: values.shortName?.trim() || null,
        tradeName: values.tradeName?.trim() || null,
        dinardap: values.dinardap?.trim() || null,
        logoUrl: values.logoUrl?.trim() || null,
        displayOrder: values.displayOrder,
        priority: values.priority,
      });
      setTenantDetail(updated);
      await refresh();
      setDetailSaveOk(true);
      window.setTimeout(() => setDetailSaveOk(false), 5000);
    } catch (e) {
      setDetailSaveError(
        formatApiRequestError(e, {
          offline: t('common.apiUnreachable'),
          generic: t('companies.error.updateCompany'),
        }),
      );
    } finally {
      setDetailSaving(false);
    }
  });

  const saveGlobalParameters = async () => {
    if (!detailSubscriberId || !tenantDetail) return;
    setGlobalParamSaving(true);
    setGlobalParamError('');
    setGlobalParamOk(false);
    try {
      await companyService.upsertTenantGlobalConfig(detailSubscriberId, {
        key: ELECTRONIC_BILLING_TRIAL_KEY,
        value: electronicBillingTrialEnabled ? 'true' : 'false',
        dataType: 'bool',
      });
      setGlobalParamScopeResolved('global');
      const globals = await companyService.listTenantGlobalConfig(detailSubscriberId);
      setGlobalConfigCount(Array.isArray(globals) ? globals.length : 0);
      await refresh();
      setGlobalParamOk(true);
      window.setTimeout(() => setGlobalParamOk(false), 5000);
    } catch (e) {
      setGlobalParamError(
        formatApiRequestError(e, {
          offline: t('common.apiUnreachable'),
          generic: t('companies.globalParams.saveError'),
        }),
      );
    } finally {
      setGlobalParamSaving(false);
    }
  };

  return (
    <PageShell kicker={t('app.nav.group.home')} title={t('companies.title')} subtitle={t('companies.subtitle')}>
      <Card
        title={t('companies.title')}
        actions={
          tab === 'data' ? (
            <Button variant="secondary" size="sm" onClick={() => void refresh()} disabled={loading}>
              {t('companies.refresh')}
            </Button>
          ) : null
        }
      >
        {error ? <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={error} /> : null}
        <div className="zh-form-tabs" role="tablist">
          <button type="button" className={tab === 'data' ? 'is-active' : ''} onClick={() => setTab('data')}>
            {t('common.formTab.data')}
          </button>
          <button type="button" className={tab === 'globalParameters' ? 'is-active' : ''} onClick={() => setTab('globalParameters')}>
            {t('companies.tabGlobalParameters')}
          </button>
          <button type="button" className={tab === 'mainNavigation' ? 'is-active' : ''} onClick={() => setTab('mainNavigation')}>
            {t('companies.tabMainNavigation')}
          </button>
          <button type="button" className={tab === 'audit' ? 'is-active' : ''} onClick={() => setTab('audit')}>
            {t('common.formTab.audit')}
          </button>
        </div>

        {tab === 'mainNavigation' ? (
          <div className="companies-nav-menu-tab">
            <p className="zh-help-text companies-nav-menu-intro">{t('superadmin.navigationMenu.subtitle')}</p>
            <NavigationMenuEditorPanel />
          </div>
        ) : null}

        {tab === 'globalParameters' ? (
          !detailSubscriberId ? (
            <EmptyState message={t('audit.pickRow')} />
          ) : detailLoading ? (
            <LoadingState />
          ) : detailError ? (
            <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={detailError} />
          ) : tenantDetail ? (
            !tenantDetail.planCode?.trim() ? (
              <ZHPageNotice
                variant="warning"
                message={t('companies.globalParams.noPlanTitle')}
                detail={t('companies.globalParams.noPlanDetail')}
              />
            ) : (
              <div className="companies-inner-stack">
                <Card title={t('companies.globalParams.title')}>
                  <form
                    onSubmit={(e) => {
                      e.preventDefault();
                      void saveGlobalParameters();
                    }}
                  >
                  {globalParamOk ? (
                    <ZHPageNotice variant="success" message={t('companies.globalParams.saveSuccess')} />
                  ) : null}
                  {globalParamError ? (
                    <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={globalParamError} />
                  ) : null}
                  <ZHPageNotice
                    variant="info"
                    message={t('companies.globalParams.modelTitle')}
                    detail={`${t('companies.globalParams.modelDetail')} ${t('companies.globalParams.structuredKeyExample')}: ${ELECTRONIC_BILLING_TRIAL_KEY}`}
                  />
                  <FeatureGate feature="ui.cliente.mostrar_ruc" fallback={<ZHPageNotice variant="warning" message="Feature flag ui.cliente.mostrar_ruc está desactivada en este tenant." />}>
                    <ZHPageNotice variant="info" message="Feature flag ui.cliente.mostrar_ruc activa para el tenant actual." />
                  </FeatureGate>
                  <ZHPageNotice
                    variant={stockNegativeEnabled ? 'warning' : 'info'}
                    message={
                      stockNegativeEnabled
                        ? 'ventas.stock.permitir_negativo = true (se permite stock negativo).'
                        : 'ventas.stock.permitir_negativo = false (stock negativo bloqueado).'
                    }
                  />
                  <ZHFormSection title={t('companies.globalParams.entitiesTitle')}>
                    <ZHGrid cols={3}>
                      <ZHField label={t('companies.globalParams.entity.global.title')} hint={t('companies.globalParams.entity.global.hint')}>
                        <div className="companies-readonly-value">{t('companies.globalParams.entity.global.help')}</div>
                      </ZHField>
                      <ZHField label={t('companies.globalParams.entity.module.title')} hint={t('companies.globalParams.entity.module.hint')}>
                        <div className="companies-readonly-value">{t('companies.globalParams.entity.module.help')}</div>
                      </ZHField>
                      <ZHField label={t('companies.globalParams.entity.feature.title')} hint={t('companies.globalParams.entity.feature.hint')}>
                        <div className="companies-readonly-value">{t('companies.globalParams.entity.feature.help')}</div>
                      </ZHField>
                    </ZHGrid>
                  </ZHFormSection>
                  <ZHFormSection title={t('companies.globalParams.sectionBilling')}>
                    <ZHField
                      label={t('companies.globalParams.electronicBillingTrial.label')}
                      hint={`${t('companies.globalParams.electronicBillingTrial.hint')} · ${t('companies.globalParams.structuredKeyLabel')}: ${ELECTRONIC_BILLING_TRIAL_KEY}`}
                    >
                      <label className="companies-checkbox-label">
                        <input
                          type="checkbox"
                          checked={electronicBillingTrialEnabled}
                          disabled={globalParamSaving}
                          onChange={(e) => setElectronicBillingTrialEnabled(e.target.checked)}
                        />
                        <span>{t('companies.globalParams.electronicBillingTrial.help')}</span>
                      </label>
                    </ZHField>
                    <ZHGrid cols={2}>
                      <ZHField label={t('companies.globalParams.activeScopeLabel')}>
                        <div className="companies-readonly-value">
                          {globalParamScopeResolved
                            ? t(`companies.globalParams.scope.${globalParamScopeResolved}`)
                            : t('companies.globalParams.scope.notFound')}
                        </div>
                      </ZHField>
                      <ZHField label={t('companies.globalParams.totalGlobalKeysLabel')}>
                        <div className="companies-readonly-value">{globalConfigCount}</div>
                      </ZHField>
                    </ZHGrid>
                  </ZHFormSection>
                  <div className="zh-form-actions-row zh-form-actions-row--end">
                    <Button variant="primary" size="sm" type="submit" disabled={globalParamSaving}>
                      {globalParamSaving ? t('companies.globalParams.saving') : t('companies.globalParams.save')}
                    </Button>
                  </div>
                  </form>
                </Card>
                <ConfigManagementPanel
                  subscriberId={detailSubscriberId}
                  canManage={user?.role === 'SuperAdmin' || user?.role === 'Admin'}
                  title="CRUD de configuración por alcance"
                  subtitle="Permite crear y editar claves estructuradas para Global, Module y Feature con validación por tipo."
                />
              </div>
            )
          ) : null
        ) : null}

        {tab === 'data' && (
          <>
            <div className="companies-data-picker zh-mb-12">
              <div className="companies-picker-toolbar">
                <Input
                  label={t('companies.search')}
                  placeholder={t('companies.search')}
                  value={listQuery}
                  onChange={(event) => setListQuery(event.target.value)}
                  disabled={loading}
                />
                <div className="companies-picker-actions">
                  <Button
                    variant="secondary"
                    size="sm"
                    onClick={() => setListQuery('')}
                    disabled={loading || !listQuery.trim()}
                  >
                    {t('common.clear')}
                  </Button>
                  <Button variant="primary" size="sm" onClick={clearTenantDetailView} disabled={loading}>
                    {t('companies.list.newAction')}
                  </Button>
                </div>
              </div>
              <p className="subtle companies-picker-meta">
                {filtered.length} {t('companies.list.entityLabel')}
              </p>
              {loading ? (
                <LoadingState />
              ) : filtered.length === 0 ? (
                <EmptyState message={items.length === 0 ? t('common.noData') : t('common.listTab.noMatch')} />
              ) : (
                <table className="companies-table companies-table--embedded companies-responsive-table">
                  <thead>
                    <tr>
                      <th>{t('companies.table.name')}</th>
                      <th>{t('companies.table.slug')}</th>
                      <th>{t('companies.table.plan')}</th>
                      <th>{t('companies.table.modules')}</th>
                      <th>{t('companies.table.id')}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {filtered.map((x) => (
                      <tr
                        key={x.id}
                        className="zh-tr-clickable"
                        onClick={() => {
                          clearCompaniesSubscriptionSubscriberId();
                          persistCompaniesDetailSubscriberId(x.id);
                          setDetailSubscriberId(x.id);
                          setAuditSubscriberId(x.id);
                          setAuditRefreshKey((k) => k + 1);
                          setTab('data');
                        }}
                      >
                        <td data-label={t('companies.table.name')}>{x.name}</td>
                        <td data-label={t('companies.table.slug')}>{x.slug}</td>
                        <td data-label={t('companies.table.plan')} className="companies-plan-cell">{x.planCode?.trim() ? x.planCode : '—'}</td>
                        <td data-label={t('companies.table.modules')} className="companies-modules-cell">
                          <CompanyModuleChips company={x} />
                        </td>
                        <td data-label={t('companies.table.id')} className="mono companies-mono">{x.id}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              )}
            </div>

            {!detailSubscriberId ? (
              <div className="companies-inner-stack">
                <Card title={t('companies.form.create')}>
                  <form onSubmit={submit}>
                  <input type="hidden" name="subscriberId" value={subscriberId} />

                  {maxActiveTenants != null ? (
                    <p className="companies-quota-hint" role="note">
                      {t('companies.deployment.maxTenantsHint')} <strong>{maxActiveTenants}</strong>
                    </p>
                  ) : null}
                  {maxIdentityUsers != null ? (
                    <p className="companies-quota-hint" role="note">
                      {t('companies.deployment.maxUsersHint')} <strong>{maxIdentityUsers}</strong>
                    </p>
                  ) : null}
                  <ZHGrid cols={2}>
                    <ZHField label={t('companies.form.subscriberName')} required fieldError={errors.subscriberName?.message}>
                      <input disabled={creating} {...register('subscriberName')} />
                    </ZHField>
                    <ZHField label={t('companies.form.subscriberSlug')} required fieldError={errors.subscriberSlug?.message}>
                      <input disabled={creating} {...register('subscriberSlug')} />
                    </ZHField>
                    <ZHField label={t('companies.form.ruc')} fieldError={errors.ruc?.message}>
                      <input disabled={creating} {...register('ruc')} />
                    </ZHField>
                    <ZHField label={t('companies.form.shortName')} fieldError={errors.shortName?.message}>
                      <input disabled={creating} {...register('shortName')} />
                    </ZHField>
                    <ZHField label={t('companies.form.tradeName')} fieldError={errors.tradeName?.message}>
                      <input disabled={creating} {...register('tradeName')} />
                    </ZHField>
                    <ZHField label={t('companies.form.dinardap')} fieldError={errors.dinardap?.message}>
                      <input disabled={creating} {...register('dinardap')} />
                    </ZHField>
                    <ZHField label={t('companies.form.logoUrl')} fieldError={errors.logoUrl?.message}>
                      <input disabled={creating} {...register('logoUrl')} />
                    </ZHField>
                    <ZHField label={t('companies.form.displayOrder')} fieldError={errors.displayOrder?.message}>
                      <input type="number" disabled={creating} {...register('displayOrder', { valueAsNumber: true })} />
                    </ZHField>
                    <ZHField label={t('companies.form.priority')} fieldError={errors.priority?.message}>
                      <input type="number" disabled={creating} {...register('priority', { valueAsNumber: true })} />
                    </ZHField>
                    <ZHField
                      label={t('companies.form.adminFirstName')}
                      required={!linkExistingAdmin}
                      fieldError={errors.adminFirstName?.message}
                    >
                      <input disabled={creating || linkExistingAdmin} {...register('adminFirstName')} />
                    </ZHField>
                    <ZHField
                      label={t('companies.form.adminLastName')}
                      required={!linkExistingAdmin}
                      fieldError={errors.adminLastName?.message}
                    >
                      <input disabled={creating || linkExistingAdmin} {...register('adminLastName')} />
                    </ZHField>
                    <ZHField label={t('companies.form.adminEmail')} required fieldError={errors.adminEmail?.message}>
                      <input type="email" disabled={creating} {...register('adminEmail')} />
                    </ZHField>
                    <ZHField label={t('companies.form.linkExistingAdmin')}>
                      <label className="companies-checkbox-label">
                        <input type="checkbox" disabled={creating} {...register('linkExistingAdmin')} />
                        <span>{t('companies.form.linkExistingAdminHint')}</span>
                      </label>
                    </ZHField>
                    {!linkExistingAdmin ? (
                      <ZHField label={t('companies.form.adminPassword')} required fieldError={errors.adminPassword?.message}>
                        <input type="password" disabled={creating} autoComplete="new-password" {...register('adminPassword')} />
                      </ZHField>
                    ) : null}
                  </ZHGrid>
                  <div className="zh-form-actions-row zh-form-actions-row--end">
                    <Button variant="primary" size="sm" type="submit" disabled={creating}>
                      {creating ? t('companies.form.creating') : t('companies.form.create')}
                    </Button>
                  </div>
                  </form>
                </Card>
              </div>
            ) : detailLoading ? (
              <LoadingState />
            ) : detailError ? (
              <>
                <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={detailError} />
                <div className="zh-mt-12">
                  <Button variant="secondary" size="sm" type="button" onClick={clearTenantDetailView}>
                    {t('companies.data.newCompany')}
                  </Button>
                </div>
              </>
            ) : tenantDetail ? (
              <div className="companies-inner-stack">
                <Card title={t('companies.data.sectionMeta')}>
                  <ZHFormBody standalone>
                    <ZHGrid cols={2}>
                      <ZHField label={t('companies.table.id')}>
                        <div className="companies-readonly-value mono">{tenantDetail.id}</div>
                      </ZHField>
                      <ZHField label={t('companies.data.createdAt')}>
                        <div className="companies-readonly-value">
                          {new Date(tenantDetail.createdAt).toLocaleString()}
                        </div>
                      </ZHField>
                      <ZHField label={t('common.status')}>
                        <div className="companies-readonly-value">
                          <Badge
                            label={tenantDetail.isActive ? t('common.active') : t('common.inactive')}
                            variant={tenantDetail.isActive ? 'green' : 'gray'}
                          />
                        </div>
                      </ZHField>
                    </ZHGrid>
                  </ZHFormBody>
                </Card>

                <Card title={t('companies.data.sectionCompany')}>
                  <form
                    onSubmit={(e) => {
                      e.preventDefault();
                      void saveTenantCompanyDetail();
                    }}
                  >
                  {detailSaveOk ? (
                    <ZHPageNotice variant="success" message={t('companies.data.saveSuccess')} />
                  ) : null}
                  {detailSaveError ? (
                    <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={detailSaveError} />
                  ) : null}
                  <ZHFormSection title={t('companies.data.sectionCompany')}>
                    <ZHGrid cols={2}>
                      <ZHField label={t('companies.form.subscriberName')} required fieldError={detailFieldErrors.subscriberName?.message}>
                        <input disabled={detailSaving} {...registerDetail('subscriberName')} />
                      </ZHField>
                      <ZHField label={t('companies.form.subscriberSlug')} required fieldError={detailFieldErrors.subscriberSlug?.message}>
                        <input disabled={detailSaving} className="mono" {...registerDetail('subscriberSlug')} />
                      </ZHField>
                      <ZHField label={t('companies.form.ruc')} fieldError={detailFieldErrors.ruc?.message}>
                        <input disabled={detailSaving} {...registerDetail('ruc')} />
                      </ZHField>
                      <ZHField label={t('companies.form.shortName')} fieldError={detailFieldErrors.shortName?.message}>
                        <input disabled={detailSaving} {...registerDetail('shortName')} />
                      </ZHField>
                      <ZHField label={t('companies.form.tradeName')} fieldError={detailFieldErrors.tradeName?.message}>
                        <input disabled={detailSaving} {...registerDetail('tradeName')} />
                      </ZHField>
                      <ZHField label={t('companies.form.dinardap')} fieldError={detailFieldErrors.dinardap?.message}>
                        <input disabled={detailSaving} {...registerDetail('dinardap')} />
                      </ZHField>
                      <ZHField label={t('companies.form.logoUrl')} fieldError={detailFieldErrors.logoUrl?.message}>
                        <input disabled={detailSaving} {...registerDetail('logoUrl')} />
                      </ZHField>
                      <ZHField label={t('companies.form.displayOrder')} fieldError={detailFieldErrors.displayOrder?.message}>
                        <input type="number" disabled={detailSaving} {...registerDetail('displayOrder', { valueAsNumber: true })} />
                      </ZHField>
                      <ZHField label={t('companies.form.priority')} fieldError={detailFieldErrors.priority?.message}>
                        <input type="number" disabled={detailSaving} {...registerDetail('priority', { valueAsNumber: true })} />
                      </ZHField>
                    </ZHGrid>
                  </ZHFormSection>
                  <div className="zh-form-actions-row zh-form-actions-row--end">
                    <Button variant="primary" size="sm" type="submit" disabled={detailSaving}>
                      {detailSaving ? t('companies.data.saving') : t('companies.data.updateGeneral')}
                    </Button>
                  </div>
                  </form>
                </Card>
              </div>
            ) : null}
          </>
        )}

        {tab === 'audit' ? (
          auditSubscriberId ? (
            <EntityAuditPanel
              entityType="Tenant"
              entityId={auditSubscriberId}
              take={10}
              refreshKey={auditRefreshKey}
            />
          ) : (
            <EmptyState message={t('audit.pickRow')} />
          )
        ) : null}
      </Card>
    </PageShell>
  );
}

export { CompaniesPage };
export default CompaniesPage;
