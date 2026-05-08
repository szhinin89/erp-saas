import { useEffect, useLayoutEffect, useMemo, useRef, useState } from 'react';
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
  type UpdateTenantCompanyFormValues,
} from '../../../schemas/saas/companySchema';
import { formatApiRequestError } from '../../lib/apiError';
import { useDeployment } from '../../../deployment/DeploymentContext';
import { PageShell, TableCard, EmptyState, LoadingState, NoAccessPage, Badge } from '../../../components/PageShell';
import { EntityAuditPanel } from '../../../components/EntityAuditPanel';
import ZHSearchBar from '../../../components/shared/ZHSearchBar';
import { ZHFormSection, ZHFormBody, ZHGrid, ZHField, ZHBtn } from '../../../components/zh/ZHForm';
import { ZHPageNotice } from '../../../components/zh/ZHPageNotice';
import { ZHFormCard } from '../../../components/zh/ZHFormCard';
import {
  clearCompaniesDetailTenantId,
  clearCompaniesSubscriptionTenantId,
  persistCompaniesDetailTenantId,
  readCompaniesDetailTenantId,
} from '../../../navigation/companiesTenantDetailNav';
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
  const tenantId = useAuthStore((s) => s.user?.tenantId ?? '');
  const { getValue } = useConfig();

  const [items, setItems] = useState<CompanyItem[]>([]);
  const [listQuery, setListQuery] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [creating, setCreating] = useState(false);
  const formRef = useRef<HTMLFormElement>(null);
  const generalCompanyFormRef = useRef<HTMLFormElement>(null);
  const [tab, setTab] = useState<CompanyTab>('data');
  const [auditTenantId, setAuditTenantId] = useState<string | null>(null);
  const [auditRefreshKey, setAuditRefreshKey] = useState(0);
  const emptyCompanyForm = (): CreateCompanyFormValues => ({
    tenantName: '',
    tenantSlug: '',
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

  /** Tenant cuya ficha se muestra en Datos; el id vive en memoria + sessionStorage (no en la URL). */
  const [detailTenantId, setDetailTenantId] = useState<string | null>(null);
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

  const emptyDetailCompanyForm = (): UpdateTenantCompanyFormValues => ({
    tenantName: '',
    tenantSlug: '',
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
  } = useForm<UpdateTenantCompanyFormValues>({
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
    clearCompaniesDetailTenantId();
    clearCompaniesSubscriptionTenantId();
    setDetailTenantId(null);
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
      clearCompaniesSubscriptionTenantId();
      persistCompaniesDetailTenantId(fromUrl);
      setDetailTenantId(fromUrl);
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
      clearCompaniesSubscriptionTenantId();
      persistCompaniesDetailTenantId(subFromUrl);
      setDetailTenantId(subFromUrl);
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
    const stored = readCompaniesDetailTenantId();
    if (stored) {
      setDetailTenantId((prev) => prev ?? stored);
      setTab('data');
    }
  }, [searchParams, setSearchParams]);

  useEffect(() => {
    if (!detailTenantId) {
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
        const d = await companyService.getTenant(detailTenantId);
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
  }, [detailTenantId, t]);

  useEffect(() => {
    if (!tenantDetail) return;
    resetDetailForm({
      tenantName: tenantDetail.name,
      tenantSlug: tenantDetail.slug,
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
    if (!detailTenantId || !tenantDetail) return;
    let cancelled = false;
    (async () => {
      try {
        const [resolved, globals] = await Promise.all([
          companyService.resolveTenantConfig(detailTenantId, ELECTRONIC_BILLING_TRIAL_KEY),
          companyService.listTenantGlobalConfig(detailTenantId),
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
  }, [detailTenantId, tenantDetail]);

  if (user?.role !== 'SuperAdmin') {
    return <NoAccessPage title={t('companies.title')} />;
  }

  const submit = handleSubmit(async (form) => {
    setError('');
    setCreating(true);
    try {
      const session = await companyService.create({
        tenantName: form.tenantName,
        tenantSlug: form.tenantSlug,
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
      if (session?.tenantId) {
        setAuditTenantId(session.tenantId);
        setAuditRefreshKey((k) => k + 1);
      }
      reset(emptyCompanyForm());
      await refresh();
      setTab('data');
      if (session?.tenantId) {
        persistCompaniesDetailTenantId(session.tenantId);
        setDetailTenantId(session.tenantId);
        setAuditTenantId(session.tenantId);
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
    if (!detailTenantId) return;
    setDetailSaving(true);
    setDetailSaveError('');
    setDetailSaveOk(false);
    try {
      const slug = (values.tenantSlug.trim() || values.tenantName.trim()).toLowerCase();
      const updated = await companyService.updateTenantCompany(detailTenantId, {
        name: values.tenantName.trim(),
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
    if (!detailTenantId || !tenantDetail) return;
    setGlobalParamSaving(true);
    setGlobalParamError('');
    setGlobalParamOk(false);
    try {
      await companyService.upsertTenantGlobalConfig(detailTenantId, {
        key: ELECTRONIC_BILLING_TRIAL_KEY,
        value: electronicBillingTrialEnabled ? 'true' : 'false',
        dataType: 'bool',
      });
      setGlobalParamScopeResolved('global');
      const globals = await companyService.listTenantGlobalConfig(detailTenantId);
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
      <TableCard>
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
          !detailTenantId ? (
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
                <ZHFormCard
                  hideHeader
                  title={t('companies.globalParams.title')}
                  subtitle={`${tenantDetail.name} · ${tenantDetail.planCode}`}
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
                    <ZHBtn variant="primary" size="md" type="submit" disabled={globalParamSaving}>
                      {globalParamSaving ? t('companies.globalParams.saving') : t('companies.globalParams.save')}
                    </ZHBtn>
                  </div>
                </ZHFormCard>
                <ConfigManagementPanel
                  tenantId={detailTenantId}
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
              <ZHSearchBar
                searchQuery={listQuery}
                onSearch={setListQuery}
                onClearAll={() => setListQuery('')}
                filterValues={{}}
                placeholder={t('companies.search')}
                resultCount={filtered.length}
                entityLabel={t('companies.list.entityLabel')}
                loading={loading}
                extraActions={
                  <ZHBtn variant="secondary" size="md" type="button" onClick={() => void refresh()} disabled={loading}>
                    {t('companies.refresh')}
                  </ZHBtn>
                }
                actionLabel={t('companies.list.newAction')}
                onAction={() => {
                  clearTenantDetailView();
                }}
              />
              {loading ? (
                <LoadingState />
              ) : filtered.length === 0 ? (
                <EmptyState message={items.length === 0 ? t('common.noData') : t('common.listTab.noMatch')} />
              ) : (
                <table className="companies-table companies-table--embedded">
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
                          clearCompaniesSubscriptionTenantId();
                          persistCompaniesDetailTenantId(x.id);
                          setDetailTenantId(x.id);
                          setAuditTenantId(x.id);
                          setAuditRefreshKey((k) => k + 1);
                          setTab('data');
                        }}
                      >
                        <td>{x.name}</td>
                        <td>{x.slug}</td>
                        <td className="companies-plan-cell">{x.planCode?.trim() ? x.planCode : '—'}</td>
                        <td className="companies-modules-cell">
                          <CompanyModuleChips company={x} />
                        </td>
                        <td className="mono companies-mono">{x.id}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              )}
            </div>

            {!detailTenantId ? (
              <div className="companies-inner-stack">
                <ZHFormCard ref={formRef} hideHeader title={t('companies.title')} subtitle={t('companies.subtitle')} onSubmit={submit}>
                  <input type="hidden" name="tenantId" value={tenantId} />

                  <ZHFormSection title={t('companies.form.create')}>
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
                      <ZHField label={t('companies.form.tenantName')} required fieldError={errors.tenantName?.message}>
                        <input disabled={creating} {...register('tenantName')} />
                      </ZHField>
                      <ZHField label={t('companies.form.tenantSlug')} required fieldError={errors.tenantSlug?.message}>
                        <input disabled={creating} {...register('tenantSlug')} />
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
                  </ZHFormSection>
                  <div className="zh-form-actions-row zh-form-actions-row--end">
                    <ZHBtn variant="primary" size="md" type="submit" disabled={creating}>
                      {creating ? t('companies.form.creating') : t('companies.form.create')}
                    </ZHBtn>
                  </div>
                </ZHFormCard>
              </div>
            ) : detailLoading ? (
              <LoadingState />
            ) : detailError ? (
              <>
                <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={detailError} />
                <div className="zh-mt-12">
                  <ZHBtn variant="secondary" size="md" type="button" onClick={clearTenantDetailView}>
                    {t('companies.data.newCompany')}
                  </ZHBtn>
                </div>
              </>
            ) : tenantDetail ? (
              <div className="companies-inner-stack">
                <TableCard>
                  <ZHFormBody standalone>
                    <ZHFormSection title={t('companies.data.sectionMeta')}>
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
                    </ZHFormSection>
                  </ZHFormBody>
                </TableCard>

                <ZHFormCard
                  ref={generalCompanyFormRef}
                  hideHeader
                  title={t('companies.data.sectionCompany')}
                  subtitle={tenantDetail.name}
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
                      <ZHField label={t('companies.form.tenantName')} required fieldError={detailFieldErrors.tenantName?.message}>
                        <input disabled={detailSaving} {...registerDetail('tenantName')} />
                      </ZHField>
                      <ZHField label={t('companies.form.tenantSlug')} required fieldError={detailFieldErrors.tenantSlug?.message}>
                        <input disabled={detailSaving} className="mono" {...registerDetail('tenantSlug')} />
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
                    <ZHBtn variant="primary" size="md" type="submit" disabled={detailSaving}>
                      {detailSaving ? t('companies.data.saving') : t('companies.data.updateGeneral')}
                    </ZHBtn>
                  </div>
                </ZHFormCard>
              </div>
            ) : null}
          </>
        )}

        {tab === 'audit' ? (
          auditTenantId ? (
            <EntityAuditPanel
              entityType="Tenant"
              entityId={auditTenantId}
              take={10}
              refreshKey={auditRefreshKey}
            />
          ) : (
            <EmptyState message={t('audit.pickRow')} />
          )
        ) : null}
      </TableCard>
    </PageShell>
  );
}

export { CompaniesPage };
export default CompaniesPage;
