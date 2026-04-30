import { useEffect, useMemo, useState } from 'react';
import { useI18n } from '../i18n/i18n';
import { useAuthStore } from '../store/authStore';
import { companyService, type CreateCompanyWithAdminRequest } from '../services/companyService';
import './CompaniesPage.css';

export function CompaniesPage() {
  const { t } = useI18n();
  const user = useAuthStore((s) => s.user);

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
    return <div className="page-shell"><h1>{t('companies.title')}</h1><p>{t('companies.noAccess')}</p></div>;
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
    <div className="page-shell">
      <div className="companies-header">
        <div>
          <h1 className="page-title">{t('companies.title')}</h1>
          <p className="page-subtitle">{t('companies.subtitle')}</p>
        </div>
      </div>

      <form className="companies-form" onSubmit={submit}>
        <div className="grid">
          <input placeholder={t('companies.form.tenantName')} value={form.tenantName} onChange={(e) => setForm((f) => ({ ...f, tenantName: e.target.value }))} required />
          <input placeholder={t('companies.form.tenantSlug')} value={form.tenantSlug} onChange={(e) => setForm((f) => ({ ...f, tenantSlug: e.target.value }))} required />
          <input placeholder={t('companies.form.ruc')} value={form.ruc ?? ''} onChange={(e) => setForm((f) => ({ ...f, ruc: e.target.value }))} />
          <input placeholder={t('companies.form.shortName')} value={form.shortName ?? ''} onChange={(e) => setForm((f) => ({ ...f, shortName: e.target.value }))} />
          <input placeholder={t('companies.form.tradeName')} value={form.tradeName ?? ''} onChange={(e) => setForm((f) => ({ ...f, tradeName: e.target.value }))} />
          <input placeholder={t('companies.form.dinardap')} value={form.dinardap ?? ''} onChange={(e) => setForm((f) => ({ ...f, dinardap: e.target.value }))} />
          <input placeholder={t('companies.form.logoUrl')} value={form.logoUrl ?? ''} onChange={(e) => setForm((f) => ({ ...f, logoUrl: e.target.value }))} />
          <input placeholder={t('companies.form.displayOrder')} type="number" value={form.displayOrder ?? 0} onChange={(e) => setForm((f) => ({ ...f, displayOrder: Number(e.target.value) }))} />
          <input placeholder={t('companies.form.priority')} type="number" value={form.priority ?? 0} onChange={(e) => setForm((f) => ({ ...f, priority: Number(e.target.value) }))} />
          <input placeholder={t('companies.form.adminFirstName')} value={form.adminFirstName} onChange={(e) => setForm((f) => ({ ...f, adminFirstName: e.target.value }))} required />
          <input placeholder={t('companies.form.adminLastName')} value={form.adminLastName} onChange={(e) => setForm((f) => ({ ...f, adminLastName: e.target.value }))} required />
          <input placeholder={t('companies.form.adminEmail')} value={form.adminEmail} onChange={(e) => setForm((f) => ({ ...f, adminEmail: e.target.value }))} required />
          <input placeholder={t('companies.form.adminPassword')} type="password" value={form.adminPassword} onChange={(e) => setForm((f) => ({ ...f, adminPassword: e.target.value }))} required />
        </div>
        {error && <p className="form-error">{error}</p>}
        <button className="primary-btn" disabled={creating}>
          {creating ? t('companies.form.creating') : t('companies.form.create')}
        </button>
      </form>

      <div className="companies-list">
        <div className="companies-toolbar">
          <input className="search" placeholder={t('companies.search')} value={q} onChange={(e) => setQ(e.target.value)} />
          <button className="secondary-btn" onClick={refresh} disabled={loading}>{t('companies.refresh')}</button>
        </div>
        <div className="table">
          <div className="row head">
            <div>{t('companies.table.name')}</div>
            <div>{t('companies.table.slug')}</div>
            <div>{t('companies.table.id')}</div>
          </div>
          {filtered.map((x) => (
            <div key={x.id} className="row">
              <div>{x.name}</div>
              <div>{x.slug}</div>
              <div className="mono">{x.id}</div>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}

