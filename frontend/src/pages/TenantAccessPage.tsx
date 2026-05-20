import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useI18n } from '../i18n/i18n';
import { useAuthStore } from '../store/authStore';
import { usePermissionsStore } from '../store/permissionsStore';
import { subscriberAccessService, type SubscriberCompanyUserMembershipItem } from '../services/subscriberAccessService';
import { profileService, type Profile } from '../services/profileService';
import { PageShell, EmptyState, LoadingState, NoAccessPage } from '../components/PageShell';
import { ZHFormSection, ZHGrid, ZHField, ZHBtn } from '../components/zh/ZHForm';
import { ZHPageNotice } from '../components/zh/ZHPageNotice';
import { ZHActionsRow } from '../components/zh/ZHLayout';
import { Card, SearchBar } from '../components/ui';
import { ZHConfirmModal } from '../components/zh/ZHConfirmModal';
import { subscriberAccessUpsertSchema, type SubscriberAccessFormValues } from '../schemas/access/subscriberAccessSchema';
import './SubscriberAccessPage.css';

type AccessTab = 'data' | 'list';

export function SubscriberAccessPage() {
  const { t } = useI18n();
  const user = useAuthStore((s) => s.user);
  const subscriberId = useAuthStore((s) => s.user?.subscriberId ?? '');
  const canManageCompanyUserMemberships = usePermissionsStore((s) => s.has('admin.users.view'));

  const [items, setItems] = useState<SubscriberCompanyUserMembershipItem[]>([]);
  const [profiles, setProfiles] = useState<Profile[]>([]);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);
  const [revokeConfirmEmail, setRevokeConfirmEmail] = useState<string | null>(null);
  const formRef = useRef<HTMLFormElement>(null);
  const [tab, setTab] = useState<AccessTab>('data');
  const [listQuery, setListQuery] = useState('');

  const emptyAccessForm = (): SubscriberAccessFormValues => ({
    email: '',
    role: 'User',
    profileId: '',
    firstName: '',
    lastName: '',
    password: '',
  });

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<SubscriberAccessFormValues>({
    resolver: zodResolver(subscriberAccessUpsertSchema),
    defaultValues: emptyAccessForm(),
  });

  const refresh = useCallback(async () => {
    setError('');
    setLoading(true);
    try {
      const [m, p] = await Promise.all([
        subscriberAccessService.listCompanyUserMemberships(false),
        profileService.list(false),
      ]);
      setItems(m);
      setProfiles(p);
    } catch {
      setError(t('subscriberAccess.error.load'));
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

  const isAdminOrSuper = user?.role === 'Admin' || user?.role === 'SuperAdmin';

  const submit = handleSubmit(async (form) => {
    setError('');
    try {
      await subscriberAccessService.upsertCompanyUserMembership({
        email: form.email,
        role: form.role,
        profileId: form.profileId || null,
        firstName: form.firstName || null,
        lastName: form.lastName || null,
        password: form.password || null,
      });
      reset(emptyAccessForm());
      await refresh();
      setTab('list');
    } catch (err: unknown) {
      const msg =
        (err as { response?: { data?: { message?: string } } })?.response?.data?.message ??
        t('subscriberAccess.error.upsert');
      setError(msg);
    }
  });

  const revoke = async (email: string) => {
    setError('');
    try {
      await subscriberAccessService.revokeCompanyUserMembership(email);
      await refresh();
    } catch {
      setError(t('subscriberAccess.error.revoke'));
    }
  };

  if (!user || (!isAdminOrSuper && !canManageCompanyUserMemberships)) {
    return <NoAccessPage title={t('subscriberAccess.title')} />;
  }

  return (
    <PageShell
      kicker={t('app.nav.group.configuracion')}
      title={t('subscriberAccess.title')}
      subtitle={t('subscriberAccess.subtitle')}
      action={
        tab === 'data' ? (
          <ZHBtn
            variant="primary"
            size="md"
            type="button"
            disabled={loading}
            onClick={() => formRef.current?.requestSubmit()}
          >
            {t('subscriberAccess.primaryCreate')}
          </ZHBtn>
        ) : undefined
      }
    >
      <Card>
        {error ? <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={error} /> : null}
        <div className="zh-form-tabs" role="tablist">
          <button type="button" className={tab === 'data' ? 'is-active' : ''} onClick={() => setTab('data')}>
            {t('common.formTab.data')}
          </button>
          <button type="button" className={tab === 'list' ? 'is-active' : ''} onClick={() => setTab('list')}>
            {t('subscriberAccess.tabList')}
          </button>
        </div>

        {tab === 'data' && (
          <Card title={t('subscriberAccess.title')}>
            <form ref={formRef} onSubmit={submit}>
            <input type="hidden" name="subscriberId" value={subscriberId} />

            <ZHFormSection title={t('subscriberAccess.section.manage')}>
              <ZHGrid cols={2}>
                <ZHField label={t('subscriberAccess.form.email')} required fieldError={errors.email?.message}>
                  <input type="email" disabled={loading} {...register('email')} />
                </ZHField>
                <ZHField label={t('subscriberAccess.form.role.user')} required fieldError={errors.role?.message}>
                  <select disabled={loading} {...register('role')}>
                    <option value="User">{t('subscriberAccess.form.role.user')}</option>
                    <option value="Admin">{t('subscriberAccess.form.role.admin')}</option>
                  </select>
                </ZHField>
                <ZHField label={t('subscriberAccess.form.noProfile')} fieldError={errors.profileId?.message}>
                  <select disabled={loading} {...register('profileId')}>
                    <option value="">{t('subscriberAccess.form.noProfile')}</option>
                    {profiles.map((p) => (
                      <option key={p.id} value={p.id}>
                        {p.name}
                      </option>
                    ))}
                  </select>
                </ZHField>
                <ZHField label={t('subscriberAccess.form.firstName')} fieldError={errors.firstName?.message}>
                  <input disabled={loading} {...register('firstName')} />
                </ZHField>
                <ZHField label={t('subscriberAccess.form.lastName')} fieldError={errors.lastName?.message}>
                  <input disabled={loading} {...register('lastName')} />
                </ZHField>
                <ZHField label={t('subscriberAccess.form.password')} fieldError={errors.password?.message}>
                  <input type="password" disabled={loading} {...register('password')} />
                </ZHField>
              </ZHGrid>
            </ZHFormSection>
            </form>
          </Card>
        )}

        {tab === 'list' && (
          <>
            <div className="tenant-access-search-row">
              <SearchBar
                searchQuery={listQuery}
                onSearch={setListQuery}
                onClearAll={() => setListQuery('')}
                filterValues={{}}
                placeholder={t('common.zhList.searchPlaceholder')}
                resultCount={listFiltered.length}
                entityLabel={t('common.zhList.entityLabel')}
                loading={loading}
                actionLabel={t('subscriberAccess.list.newAction')}
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
              <table className="table tenant-access-responsive-table">
                <thead>
                  <tr>
                    <th>{t('subscriberAccess.table.user')}</th>
                    <th>{t('subscriberAccess.table.role')}</th>
                    <th>{t('subscriberAccess.table.profile')}</th>
                    <th>{t('subscriberAccess.table.actions')}</th>
                  </tr>
                </thead>
                <tbody>
                  {listFiltered.map((m) => (
                    <tr key={`${m.identityUserId}-${m.email}`}>
                      <td data-label={t('subscriberAccess.table.user')}>
                        <div className="zh-card-section-title">{m.fullName || m.email}</div>
                        <div className="mono">{m.email}</div>
                      </td>
                      <td data-label={t('subscriberAccess.table.role')}>{m.role}</td>
                      <td data-label={t('subscriberAccess.table.profile')} className="mono">{m.profileId ?? '-'}</td>
                      <td data-label={t('subscriberAccess.table.actions')} className="tenant-access-cell-actions">
                        <ZHActionsRow className="tenant-access-actions">
                          <ZHBtn variant="destructive" type="button" onClick={() => setRevokeConfirmEmail(m.email)}>
                            {t('subscriberAccess.actions.revoke')}
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
      </Card>

      {revokeConfirmEmail ? (
        <ZHConfirmModal
          title={t('subscriberAccess.actions.revoke')}
          message={`${t('common.confirm')} ${t('subscriberAccess.actions.revoke')} ${revokeConfirmEmail}?`}
          confirmLabel={t('subscriberAccess.actions.revoke')}
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
