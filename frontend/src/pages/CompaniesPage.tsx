import { useEffect, useMemo, useState } from 'react';
import { useI18n } from '../i18n/i18n';
import { useAuthStore } from '../store/authStore';
import { companyService, type CreateCompanyWithAdminRequest } from '../services/companyService';
import { PageShell, TableCard, EmptyState, LoadingState, NoAccessPage, PageToolbar } from '../components/PageShell';
import { ZHFormSection, ZHGrid, ZHField, ZHFormAlert, ZHFormActions, ZHBtn } from '../components/zh/ZHForm';
import { ZHSection } from '../components/zh/ZHLayout';
import { ZHFormCard } from '../components/zh/ZHFormCard';
import './CompaniesPage.css';

export function CompaniesPage() {
  const { t } = useI18n();
  const user = useAuthStore((s) => s.user);
  const tenantId = useAuthStore((s) => s.user?.tenantId ?? '');

  const [items, setItems] = useState<Array<{ id: string; name: string; slug: string }>>([]);
  const [q, setQ] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [creating, setCreating] = useState(false);
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
    const query = q.trim().toLowerCase();
    if (!query) return items;
    return items.filter((x) => `${x.name} ${x.slug} ${x.id}`.toLowerCase().includes(query));
  }, [q, items]);

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
    // Defer para evitar regla eslint react-hooks/set-state-in-effect
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
      await companyService.create(form);
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
    <PageShell kicker={t('app.nav.group.saas')} title={t('companies.title')} subtitle={t('companies.subtitle')}>
      <ZHFormCard hideHeader title={t('companies.title')} subtitle={t('companies.subtitle')} onSubmit={submit}>
        <input type="hidden" name="tenantId" value={tenantId} />
        {error ? <ZHFormAlert type="error" message={t('common.errorPrefix')} detail={error} /> : null}

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

        <ZHFormActions
          onCancel={() => setForm((s) => ({ ...s, adminPassword: '' }))}
          onDraft={undefined}
          onSave={undefined}
          disableDraft
          disableSave={creating}
          saveButtonType="submit"
          labels={{ cancel: t('common.cancel'), draft: t('common.saveDraft') ?? 'Guardar borrador', save: creating ? t('companies.form.creating') : t('companies.form.create') }}
        />
      </ZHFormCard>

      <ZHSection top={16}>
        <TableCard>
          <PageToolbar>
            <input
              className="companies-search"
              placeholder={t('companies.search')}
              value={q}
              onChange={(e) => setQ(e.target.value)}
              disabled={loading}
            />
            <ZHBtn variant="secondary" type="button" onClick={refresh} disabled={loading}>
              {t('companies.refresh')}
            </ZHBtn>
          </PageToolbar>
          {loading ? (
            <LoadingState />
          ) : filtered.length === 0 ? (
            <EmptyState message={t('common.noData')} />
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
                  <tr key={x.id}>
                    <td>{x.name}</td>
                    <td>{x.slug}</td>
                    <td className="mono companies-mono">{x.id}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </TableCard>
      </ZHSection>
    </PageShell>
  );
}

