import { useCallback, useEffect, useState } from 'react';
import { useI18n } from '../i18n/i18n';
import { useAuthStore } from '../store/authStore';
import { tenantAccessService, type TenantMembershipItem } from '../services/tenantAccessService';
import { profileService, type Profile } from '../services/profileService';

export function TenantAccessPage() {
  const { t } = useI18n();
  const user = useAuthStore((s) => s.user);

  const [items, setItems] = useState<TenantMembershipItem[]>([]);
  const [profiles, setProfiles] = useState<Profile[]>([]);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  const [form, setForm] = useState({
    email: '',
    role: 'User',
    profileId: '',
    firstName: '',
    lastName: '',
    password: '',
  });

  const refresh = useCallback(async () => {
    setError('');
    setLoading(true);
    try {
      const [m, p] = await Promise.all([
        tenantAccessService.listMemberships(false),
        profileService.list(false),
      ]);
      setItems(m);
      setProfiles(p);
    } catch {
      setError(t('tenantAccess.error.load'));
    } finally {
      setLoading(false);
    }
  }, [t]);

  useEffect(() => {
    let cancelled = false;
    // Defer para evitar regla eslint react-hooks/set-state-in-effect
    void Promise.resolve().then(async () => {
      if (!cancelled) await refresh();
    });
    return () => { cancelled = true; };
  }, [refresh]);

  if (!user || (user.role !== 'Admin' && user.role !== 'SuperAdmin')) {
    return <div className="page-shell"><h1>{t('tenantAccess.title')}</h1><p>{t('tenantAccess.noAccess')}</p></div>;
  }

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');
    try {
      await tenantAccessService.upsertMembership({
        email: form.email,
        role: form.role,
        profileId: form.profileId || null,
        firstName: form.firstName || null,
        lastName: form.lastName || null,
        password: form.password || null,
      });
      setForm({ email: '', role: 'User', profileId: '', firstName: '', lastName: '', password: '' });
      await refresh();
    } catch (err: unknown) {
      const msg =
        (err as { response?: { data?: { message?: string } } })?.response?.data?.message ??
        t('tenantAccess.error.upsert');
      setError(msg);
    }
  };

  const revoke = async (email: string) => {
    setError('');
    try {
      await tenantAccessService.revokeMembership(email);
      await refresh();
    } catch {
      setError(t('tenantAccess.error.revoke'));
    }
  };

  return (
    <div className="page-shell">
      <h1 className="page-title">{t('tenantAccess.title')}</h1>
      <p className="page-subtitle">{t('tenantAccess.subtitle')}</p>

      <form className="companies-form" onSubmit={submit}>
        <div className="grid">
          <input placeholder={t('tenantAccess.form.email')} value={form.email} onChange={(e) => setForm((f) => ({ ...f, email: e.target.value }))} required />
          <select value={form.role} onChange={(e) => setForm((f) => ({ ...f, role: e.target.value }))}>
            <option value="User">{t('tenantAccess.form.role.user')}</option>
            <option value="Admin">{t('tenantAccess.form.role.admin')}</option>
          </select>
          <select value={form.profileId} onChange={(e) => setForm((f) => ({ ...f, profileId: e.target.value }))}>
            <option value="">{t('tenantAccess.form.noProfile')}</option>
            {profiles.map((p) => (
              <option key={p.id} value={p.id}>{p.name}</option>
            ))}
          </select>
          <input placeholder={t('tenantAccess.form.firstName')} value={form.firstName} onChange={(e) => setForm((f) => ({ ...f, firstName: e.target.value }))} />
          <input placeholder={t('tenantAccess.form.lastName')} value={form.lastName} onChange={(e) => setForm((f) => ({ ...f, lastName: e.target.value }))} />
          <input placeholder={t('tenantAccess.form.password')} type="password" value={form.password} onChange={(e) => setForm((f) => ({ ...f, password: e.target.value }))} />
        </div>
        {error && <p className="form-error">{error}</p>}
        <button className="primary-btn" disabled={loading}>{t('tenantAccess.form.save')}</button>
      </form>

      <div className="companies-list">
        <div className="table">
          <div className="row head">
            <div>{t('tenantAccess.table.user')}</div>
            <div>{t('tenantAccess.table.role')}</div>
            <div>{t('tenantAccess.table.profile')}</div>
            <div>{t('tenantAccess.table.actions')}</div>
          </div>
          {items.map((m) => (
            <div key={`${m.identityUserId}-${m.email}`} className="row">
              <div>
                <div style={{ fontWeight: 800 }}>{m.fullName || m.email}</div>
                <div className="mono">{m.email}</div>
              </div>
              <div>{m.role}</div>
              <div className="mono">{m.profileId ?? '-'}</div>
              <div>
                <button className="secondary-btn" type="button" onClick={() => revoke(m.email)}>
                  {t('tenantAccess.actions.revoke')}
                </button>
              </div>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}

