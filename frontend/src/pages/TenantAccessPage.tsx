import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useI18n } from '../i18n/i18n';
import { useAuthStore } from '../store/authStore';
import { tenantAccessService, type TenantMembershipItem } from '../services/tenantAccessService';
import { profileService, type Profile } from '../services/profileService';
import { PageShell, TableCard, EmptyState, LoadingState, NoAccessPage } from '../components/PageShell';
import { ZHFormSection, ZHGrid, ZHField, ZHFormAlert, ZHBtn } from '../components/zh/ZHForm';
import { ZHActionsRow } from '../components/zh/ZHLayout';
import ZHSearchBar from '../components/shared/ZHSearchBar';
import { ZHFormCard } from '../components/zh/ZHFormCard';
import { ZHConfirmModal } from '../components/zh/ZHConfirmModal';

type AccessTab = 'data' | 'list';

export function TenantAccessPage() {
  const { t } = useI18n();
  const user = useAuthStore((s) => s.user);
  const tenantId = useAuthStore((s) => s.user?.tenantId ?? '');

  const [items, setItems] = useState<TenantMembershipItem[]>([]);
  const [profiles, setProfiles] = useState<Profile[]>([]);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);
  const [revokeConfirmEmail, setRevokeConfirmEmail] = useState<string | null>(null);
  const formRef = useRef<HTMLFormElement>(null);
  const [tab, setTab] = useState<AccessTab>('data');
  const [listQuery, setListQuery] = useState('');

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
    void Promise.resolve().then(async () => {
      if (!cancelled) await refresh();
    });
    return () => {
      cancelled = true;
    };
  }, [refresh]);

  const listFiltered = useMemo(() => {
    const q = listQuery.trim().toLowerCase();
    if (!q) return items;
    return items.filter((m) =>
      `${m.fullName ?? ''} ${m.email} ${m.role} ${m.profileId ?? ''}`.toLowerCase().includes(q)
    );
  }, [items, listQuery]);

  if (!user || (user.role !== 'Admin' && user.role !== 'SuperAdmin')) {
    return <NoAccessPage title={t('tenantAccess.title')} />;
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
      setTab('list');
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
    <PageShell
      kicker={t('app.nav.group.access')}
      title={t('tenantAccess.title')}
      subtitle={t('tenantAccess.subtitle')}
      action={
        tab === 'data' ? (
          <ZHBtn
            variant="primary"
            size="md"
            type="button"
            disabled={loading}
            onClick={() => formRef.current?.requestSubmit()}
          >
            {t('tenantAccess.primaryCreate')}
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
            {t('tenantAccess.tabList')}
          </button>
        </div>

        {tab === 'data' && (
          <ZHFormCard
            ref={formRef}
            hideHeader
            title={t('tenantAccess.title')}
            subtitle={t('tenantAccess.subtitle')}
            onSubmit={submit}
          >
            <input type="hidden" name="tenantId" value={tenantId} />

            <ZHFormSection title={t('tenantAccess.section.manage')}>
              <ZHGrid cols={2}>
                <ZHField label={t('tenantAccess.form.email')} required>
                  <input value={form.email} onChange={(e) => setForm((f) => ({ ...f, email: e.target.value }))} required disabled={loading} />
                </ZHField>
                <ZHField label={t('tenantAccess.form.role.user')} required>
                  <select value={form.role} onChange={(e) => setForm((f) => ({ ...f, role: e.target.value }))} disabled={loading}>
                    <option value="User">{t('tenantAccess.form.role.user')}</option>
                    <option value="Admin">{t('tenantAccess.form.role.admin')}</option>
                  </select>
                </ZHField>
                <ZHField label={t('tenantAccess.form.noProfile')}>
                  <select value={form.profileId} onChange={(e) => setForm((f) => ({ ...f, profileId: e.target.value }))} disabled={loading}>
                    <option value="">{t('tenantAccess.form.noProfile')}</option>
                    {profiles.map((p) => (
                      <option key={p.id} value={p.id}>
                        {p.name}
                      </option>
                    ))}
                  </select>
                </ZHField>
                <ZHField label={t('tenantAccess.form.firstName')}>
                  <input value={form.firstName} onChange={(e) => setForm((f) => ({ ...f, firstName: e.target.value }))} disabled={loading} />
                </ZHField>
                <ZHField label={t('tenantAccess.form.lastName')}>
                  <input value={form.lastName} onChange={(e) => setForm((f) => ({ ...f, lastName: e.target.value }))} disabled={loading} />
                </ZHField>
                <ZHField label={t('tenantAccess.form.password')}>
                  <input type="password" value={form.password} onChange={(e) => setForm((f) => ({ ...f, password: e.target.value }))} disabled={loading} />
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
                placeholder={t('common.zhList.searchPlaceholder')}
                resultCount={listFiltered.length}
                entityLabel={t('common.zhList.entityLabel')}
                loading={loading}
                actionLabel={t('tenantAccess.list.newAction')}
                onAction={() => setTab('data')}
              />
            </div>
            {loading ? (
              <LoadingState />
            ) : items.length === 0 ? (
              <EmptyState message={t('common.noData')} />
            ) : listFiltered.length === 0 ? (
              <EmptyState message={t('common.listTab.noMatch')} />
            ) : (
              <table className="table">
                <thead>
                  <tr>
                    <th>{t('tenantAccess.table.user')}</th>
                    <th>{t('tenantAccess.table.role')}</th>
                    <th>{t('tenantAccess.table.profile')}</th>
                    <th>{t('tenantAccess.table.actions')}</th>
                  </tr>
                </thead>
                <tbody>
                  {listFiltered.map((m) => (
                    <tr key={`${m.identityUserId}-${m.email}`}>
                      <td>
                        <div className="zh-card-section-title">{m.fullName || m.email}</div>
                        <div className="mono">{m.email}</div>
                      </td>
                      <td>{m.role}</td>
                      <td className="mono">{m.profileId ?? '-'}</td>
                      <td>
                        <ZHActionsRow>
                          <ZHBtn variant="destructive" type="button" onClick={() => setRevokeConfirmEmail(m.email)}>
                            {t('tenantAccess.actions.revoke')}
                          </ZHBtn>
                        </ZHActionsRow>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </>
        )}
      </TableCard>

      {revokeConfirmEmail ? (
        <ZHConfirmModal
          title={t('tenantAccess.actions.revoke')}
          message={`${t('common.confirm')} ${t('tenantAccess.actions.revoke')} ${revokeConfirmEmail}?`}
          confirmLabel={t('tenantAccess.actions.revoke')}
          variant="destructive"
          loading={loading}
          onCancel={() => setRevokeConfirmEmail(null)}
          onConfirm={async () => {
            const email = revokeConfirmEmail;
            setRevokeConfirmEmail(null);
            await revoke(email);
          }}
        />
      ) : null}
    </PageShell>
  );
}
