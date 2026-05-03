import { useEffect, useMemo, useRef, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useI18n } from '../i18n/i18n';
import { useAuthStore } from '../store/authStore';
import { companyService, type CompanyItem } from '../services/companyService';
import { TenantSubscriptionEditor } from '../components/saas/TenantSubscriptionEditor';
import { CompanyModuleChips } from '../components/saas/CompanyModuleChips';
import { createCompanyWithAdminSchema, type CreateCompanyFormValues } from '../schemas/saas/companySchema';
import { useDeployment } from '../deployment/DeploymentContext';
import { PageShell, TableCard, EmptyState, LoadingState, NoAccessPage } from '../components/PageShell';
import { EntityAuditPanel } from '../components/EntityAuditPanel';
import ZHSearchBar from '../components/shared/ZHSearchBar';
import { ZHFormSection, ZHGrid, ZHField, ZHFormAlert, ZHBtn } from '../components/zh/ZHForm';
import { ZHFormCard } from '../components/zh/ZHFormCard';
import './CompaniesPage.css';

type CompanyTab = 'data' | 'list' | 'subscription' | 'audit';

function CompaniesPage() {
  const { t } = useI18n();
  const { maxActiveTenants, maxIdentityUsers } = useDeployment();
  const [searchParams, setSearchParams] = useSearchParams();
  const user = useAuthStore((s) => s.user);
  const tenantId = useAuthStore((s) => s.user?.tenantId ?? '');

  const [items, setItems] = useState<CompanyItem[]>([]);
  const [listQuery, setListQuery] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [creating, setCreating] = useState(false);
  const formRef = useRef<HTMLFormElement>(null);
  const [tab, setTab] = useState<CompanyTab>('data');
  const [auditTenantId, setAuditTenantId] = useState<string | null>(null);
  const [subscriptionTenant, setSubscriptionTenant] = useState<CompanyItem | null>(null);
  const [auditRefreshKey, setAuditRefreshKey] = useState(0);
  const appliedSubscriptionFromUrl = useRef<string | null>(null);
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
      setItems(await companyService.list());
    } catch {
      setError(t('companies.error.load'));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void Promise.resolve().then(refresh);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const clearSubscriptionQuery = () => {
    setSearchParams(
      (prev) => {
        const next = new URLSearchParams(prev);
        next.delete('subscription');
        return next;
      },
      { replace: true }
    );
  };

  const setSubscriptionQuery = (tenantId: string) => {
    setSearchParams(
      (prev) => {
        const next = new URLSearchParams(prev);
        next.set('subscription', tenantId);
        return next;
      },
      { replace: true }
    );
  };

  /** Deep link: `/companies?subscription={tenantId}` (p. ej. desde panel SuperAdmin). Una sola vez por valor de query. */
  useEffect(() => {
    const id = searchParams.get('subscription');
    if (!id) {
      appliedSubscriptionFromUrl.current = null;
      return;
    }
    if (items.length === 0) return;
    const row = items.find((i) => i.id.toLowerCase() === id.trim().toLowerCase());
    if (!row) {
      appliedSubscriptionFromUrl.current = null;
      clearSubscriptionQuery();
      return;
    }
    if (appliedSubscriptionFromUrl.current === id) return;
    appliedSubscriptionFromUrl.current = id;
    setSubscriptionTenant(row);
    setTab('subscription');
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [searchParams, items]);

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
      setTab(session?.tenantId ? 'audit' : 'list');
    } catch (err: unknown) {
      const msg =
        (err as { response?: { data?: { message?: string } } })?.response?.data?.message ??
        t('companies.error.create');
      setError(msg);
    } finally {
      setCreating(false);
    }
  });

  return (
    <PageShell
      kicker={t('app.nav.group.home')}
      title={t('companies.title')}
      subtitle={t('companies.subtitle')}
      action={
        tab === 'data' ? (
          <ZHBtn variant="primary" size="md" type="button" disabled={creating} onClick={() => formRef.current?.requestSubmit()}>
            {creating ? t('companies.form.creating') : t('companies.form.create')}
          </ZHBtn>
        ) : undefined
      }
    >
      <TableCard>
        {error ? <ZHFormAlert type="error" message={t('common.errorPrefix')} detail={error} /> : null}
        <div className="zh-form-tabs" role="tablist">
          <button type="button" className={tab === 'data' ? 'is-active' : ''} onClick={() => setTab('data')}>
            {t('common.formTab.data')}
          </button>
          <button type="button" className={tab === 'list' ? 'is-active' : ''} onClick={() => setTab('list')}>
            {t('companies.tabList')}
          </button>
          <button type="button" className={tab === 'subscription' ? 'is-active' : ''} onClick={() => setTab('subscription')}>
            {t('companies.tabSubscription')}
          </button>
          <button type="button" className={tab === 'audit' ? 'is-active' : ''} onClick={() => setTab('audit')}>
            {t('common.formTab.audit')}
          </button>
        </div>

        {tab === 'data' && (
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
          </ZHFormCard>
        )}

        {tab === 'list' && (
          <>
            <div className="zh-mb-12">
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
                onAction={() => setTab('data')}
              />
            </div>
            {loading ? (
              <LoadingState />
            ) : filtered.length === 0 ? (
              <EmptyState message={items.length === 0 ? t('common.noData') : t('common.listTab.noMatch')} />
            ) : (
              <table className="companies-table">
                <thead>
                  <tr>
                    <th>{t('companies.table.name')}</th>
                    <th>{t('companies.table.slug')}</th>
                    <th>{t('companies.table.plan')}</th>
                    <th>{t('companies.table.modules')}</th>
                    <th>{t('companies.table.id')}</th>
                    <th>{t('companies.table.actions')}</th>
                  </tr>
                </thead>
                <tbody>
                  {filtered.map((x) => (
                    <tr
                      key={x.id}
                      className="zh-tr-clickable"
                      onClick={() => {
                        setAuditTenantId(x.id);
                        setAuditRefreshKey((k) => k + 1);
                        setTab('audit');
                      }}
                    >
                      <td>{x.name}</td>
                      <td>{x.slug}</td>
                      <td className="companies-plan-cell">
                        {x.planCode?.trim() ? x.planCode : '—'}
                      </td>
                      <td className="companies-modules-cell">
                        <CompanyModuleChips company={x} />
                      </td>
                      <td className="mono companies-mono">{x.id}</td>
                      <td className="companies-actions-cell" onClick={(e) => e.stopPropagation()}>
                        <ZHBtn
                          variant="secondary"
                          size="sm"
                          type="button"
                          onClick={() => {
                            setSubscriptionTenant(x);
                            setTab('subscription');
                            setSubscriptionQuery(x.id);
                          }}
                        >
                          {t('companies.subscription.openEditor')}
                        </ZHBtn>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </>
        )}

        {tab === 'subscription' ? (
          subscriptionTenant ? (
            <TenantSubscriptionEditor
              key={subscriptionTenant.id}
              tenant={subscriptionTenant}
              onBack={() => {
                appliedSubscriptionFromUrl.current = null;
                setSubscriptionTenant(null);
                setTab('list');
                clearSubscriptionQuery();
              }}
              onSave={async (body) => {
                await companyService.updateSubscription(subscriptionTenant.id, body);
                await refresh();
                appliedSubscriptionFromUrl.current = null;
                setSubscriptionTenant(null);
                setTab('list');
                clearSubscriptionQuery();
              }}
            />
          ) : (
            <EmptyState message={t('companies.subscription.pickFromList')} />
          )
        ) : null}

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
