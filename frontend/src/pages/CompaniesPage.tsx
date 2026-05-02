import { useEffect, useMemo, useRef, useState } from 'react';
import { useI18n } from '../i18n/i18n';
import { useAuthStore } from '../store/authStore';
import { companyService, type CreateCompanyWithAdminRequest } from '../services/companyService';
import { PageShell, TableCard, EmptyState, LoadingState, NoAccessPage } from '../components/PageShell';
import { EntityAuditPanel } from '../components/EntityAuditPanel';
import ZHSearchBar from '../components/shared/ZHSearchBar';
import { ZHFormSection, ZHGrid, ZHField, ZHFormAlert, ZHBtn } from '../components/zh/ZHForm';
import { ZHFormCard } from '../components/zh/ZHFormCard';
import './CompaniesPage.css';

type CompanyTab = 'data' | 'list' | 'audit';

function CompaniesPage() {
  const { t } = useI18n();
  const user = useAuthStore((s) => s.user);
  const tenantId = useAuthStore((s) => s.user?.tenantId ?? '');

  const [items, setItems] = useState<Array<{ id: string; name: string; slug: string }>>([]);
  const [listQuery, setListQuery] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [creating, setCreating] = useState(false);
  const formRef = useRef<HTMLFormElement>(null);
  const [tab, setTab] = useState<CompanyTab>('data');
  const [auditTenantId, setAuditTenantId] = useState<string | null>(null);
  const [auditRefreshKey, setAuditRefreshKey] = useState(0);
  const [form, setForm] = useState<CreateCompanyWithAdminRequest>({
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
    passwordResetMode: 1,
  });

  const filtered = useMemo(() => {
    const query = listQuery.trim().toLowerCase();
    if (!query) return items;
    return items.filter((x) => `${x.name} ${x.slug} ${x.id}`.toLowerCase().includes(query));
  }, [listQuery, items]);

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

  if (user?.role !== 'SuperAdmin') {
    return <NoAccessPage title={t('companies.title')} />;
  }

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');
    setCreating(true);
    try {
      const session = await companyService.create(form);
      if (session?.tenantId) {
        setAuditTenantId(session.tenantId);
        setAuditRefreshKey((k) => k + 1);
      }
      setForm({
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
        passwordResetMode: 1,
      });
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
  };

  return (
    <PageShell
      kicker={t('app.nav.group.saas')}
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
          <button type="button" className={tab === 'audit' ? 'is-active' : ''} onClick={() => setTab('audit')}>
            {t('common.formTab.audit')}
          </button>
        </div>

        {tab === 'data' && (
          <ZHFormCard ref={formRef} hideHeader title={t('companies.title')} subtitle={t('companies.subtitle')} onSubmit={submit}>
            <input type="hidden" name="tenantId" value={tenantId} />

            <ZHFormSection title={t('companies.form.create')}>
              <ZHGrid cols={2}>
                <ZHField label={t('companies.form.tenantName')} required>
                  <input value={form.tenantName} onChange={(e) => setForm((f) => ({ ...f, tenantName: e.target.value }))} required disabled={creating} />
                </ZHField>
                <ZHField label={t('companies.form.tenantSlug')} required>
                  <input value={form.tenantSlug} onChange={(e) => setForm((f) => ({ ...f, tenantSlug: e.target.value }))} required disabled={creating} />
                </ZHField>
                <ZHField label={t('companies.form.ruc')}>
                  <input value={form.ruc ?? ''} onChange={(e) => setForm((f) => ({ ...f, ruc: e.target.value }))} disabled={creating} />
                </ZHField>
                <ZHField label={t('companies.form.shortName')}>
                  <input value={form.shortName ?? ''} onChange={(e) => setForm((f) => ({ ...f, shortName: e.target.value }))} disabled={creating} />
                </ZHField>
                <ZHField label={t('companies.form.tradeName')}>
                  <input value={form.tradeName ?? ''} onChange={(e) => setForm((f) => ({ ...f, tradeName: e.target.value }))} disabled={creating} />
                </ZHField>
                <ZHField label={t('companies.form.dinardap')}>
                  <input value={form.dinardap ?? ''} onChange={(e) => setForm((f) => ({ ...f, dinardap: e.target.value }))} disabled={creating} />
                </ZHField>
                <ZHField label={t('companies.form.logoUrl')}>
                  <input value={form.logoUrl ?? ''} onChange={(e) => setForm((f) => ({ ...f, logoUrl: e.target.value }))} disabled={creating} />
                </ZHField>
                <ZHField label={t('companies.form.displayOrder')}>
                  <input type="number" value={form.displayOrder ?? 0} onChange={(e) => setForm((f) => ({ ...f, displayOrder: Number(e.target.value) }))} disabled={creating} />
                </ZHField>
                <ZHField label={t('companies.form.priority')}>
                  <input type="number" value={form.priority ?? 0} onChange={(e) => setForm((f) => ({ ...f, priority: Number(e.target.value) }))} disabled={creating} />
                </ZHField>
                <ZHField label={t('companies.form.adminFirstName')} required>
                  <input value={form.adminFirstName} onChange={(e) => setForm((f) => ({ ...f, adminFirstName: e.target.value }))} required disabled={creating} />
                </ZHField>
                <ZHField label={t('companies.form.adminLastName')} required>
                  <input value={form.adminLastName} onChange={(e) => setForm((f) => ({ ...f, adminLastName: e.target.value }))} required disabled={creating} />
                </ZHField>
                <ZHField label={t('companies.form.adminEmail')} required>
                  <input value={form.adminEmail} onChange={(e) => setForm((f) => ({ ...f, adminEmail: e.target.value }))} required disabled={creating} />
                </ZHField>
                <ZHField label={t('companies.form.adminPassword')} required>
                  <input type="password" value={form.adminPassword} onChange={(e) => setForm((f) => ({ ...f, adminPassword: e.target.value }))} required disabled={creating} />
                </ZHField>
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
                    <th>{t('companies.table.id')}</th>
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
                      <td className="mono companies-mono">{x.id}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
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
