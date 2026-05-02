import { useMemo, useRef, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { accountingService, type CreateAccountRequest } from '../services/accountingService';
import { useAsync } from '../hooks/useAsync';
import { documentStatusLabel, DocumentStatus } from '../types/accounting';
import {
  PageShell, TableCard, EmptyState, ErrorState, LoadingState, Badge, NoAccessPage,
} from '../components/PageShell';
import { CreateJournalEntryModal } from '../components/CreateJournalEntryModal';
import './AccountingPage.css';
import { useI18n } from '../i18n/i18n';
import { ZHBtn, ZHFormBody, ZHFormSection, ZHGrid, ZHField, ZHFormAlert } from '../components/zh/ZHForm';
import { ZHColSpan } from '../components/zh/ZHLayout';
import ZHSearchBar from '../components/shared/ZHSearchBar';
import { useAuthStore } from '../store/authStore';
import { usePermissionsStore } from '../store/permissionsStore';
import { createAccountFormSchema, type CreateAccountFormValues } from '../schemas/accounting/accountSchema';

type Tab = 'accounts' | 'journal';

const EMPTY: CreateAccountFormValues = { code: '', name: '', type: 0, nature: 0, parentId: '' };

const statusVariant: Record<DocumentStatus, 'blue' | 'green' | 'red'> = {
  [DocumentStatus.Draft]:  'blue',
  [DocumentStatus.Posted]: 'green',
  [DocumentStatus.Voided]: 'red',
};

export function AccountingPage() {
  const { t } = useI18n();
  const tenantId = useAuthStore((s) => s.user?.tenantId ?? '');
  const role = useAuthStore((s) => s.user?.role ?? '');
  const isAdmin = role === 'Admin' || role === 'SuperAdmin';
  const hasPerm = usePermissionsStore((s) => s.has);

  const canViewAccounts = isAdmin || hasPerm('accounting.accounts.view');
  const canCreateAccount = isAdmin || hasPerm('accounting.accounts.create');
  const canViewJournal = isAdmin || hasPerm('accounting.journal.view');
  const canCreateJournal = isAdmin || hasPerm('accounting.journal.create');

  const [tab, setTab] = useState<Tab>('accounts');
  const [showJournal, setShowJournal] = useState(false);

  const accounts = useAsync(() => accountingService.getAccounts(), canViewAccounts);
  const journalEntries = useAsync(() => accountingService.getJournalEntries(), canViewJournal);

  const formRef = useRef<HTMLFormElement>(null);
  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isSubmitting: formLoading },
  } = useForm<CreateAccountFormValues>({
    resolver: zodResolver(createAccountFormSchema),
    defaultValues: EMPTY,
  });
  const [formError, setFormError] = useState('');
  const [accountSubTab, setAccountSubTab] = useState<'data' | 'list'>('list');
  const [accountListQuery, setAccountListQuery] = useState('');

  const canUseModule = canViewAccounts || canViewJournal;
  const permsHydrated = usePermissionsStore((s) => s.hasHydrated);

  /** Pestaña principal efectiva según permisos (sin sincronizar con efectos). */
  const displayTab = useMemo<Tab>(() => {
    if (!canViewAccounts && canViewJournal) return 'journal';
    if (canViewAccounts && !canViewJournal) return 'accounts';
    return tab;
  }, [canViewAccounts, canViewJournal, tab]);

  /** Si no puede crear cuentas, el formulario «datos» no aplica; se fuerza listado. */
  const activeAccountSubTab = canCreateAccount ? accountSubTab : 'list';

  const filteredAccounts = useMemo(() => {
    const data = accounts.data ?? [];
    const q = accountListQuery.trim().toLowerCase();
    if (!q) return data;
    return data.filter((a) => `${a.code} ${a.name} ${a.type} ${a.nature}`.toLowerCase().includes(q));
  }, [accounts.data, accountListQuery]);

  const accountTypes = useMemo(
    () => [
      { value: 0, label: t('accounting.accounts.type.asset') },
      { value: 1, label: t('accounting.accounts.type.liability') },
      { value: 2, label: t('accounting.accounts.type.equity') },
      { value: 3, label: t('accounting.accounts.type.income') },
      { value: 4, label: t('accounting.accounts.type.expense') },
    ],
    [t]
  );

  const accountNatures = useMemo(
    () => [
      { value: 0, label: t('accounting.accounts.nature.debit') },
      { value: 1, label: t('accounting.accounts.nature.credit') },
    ],
    [t]
  );

  const submitAccount = handleSubmit(async (form) => {
    setFormError('');
    try {
      const payload: CreateAccountRequest = {
        code: form.code,
        name: form.name,
        type: form.type,
        nature: form.nature,
        parentId: form.parentId?.trim() ? form.parentId.trim() : null,
      };
      await accountingService.createAccount(payload);
      reset(EMPTY);
      accounts.refetch();
      setAccountSubTab('list');
    } catch (err: unknown) {
      setFormError(
        (err as { response?: { data?: { error?: string } } })?.response?.data?.error ??
          t('accounting.accounts.modal.create.error')
      );
    }
  });

  if (!permsHydrated && !isAdmin) {
    return (
      <PageShell kicker={t('app.nav.group.accounting')} title={t('app.nav.accounting')}>
        <LoadingState />
      </PageShell>
    );
  }

  if (!canUseModule) {
    return <NoAccessPage title={t('app.nav.accounting')} />;
  }

  return (
    <PageShell
      kicker={t('app.nav.group.accounting')}
      title={displayTab === 'accounts' ? t('accounting.tabs.accounts') : t('accounting.tabs.journal')}
      action={
        displayTab === 'accounts' && activeAccountSubTab === 'data' && canCreateAccount ? (
          <ZHBtn
            variant="primary"
            size="md"
            type="button"
            disabled={formLoading}
            onClick={() => formRef.current?.requestSubmit()}
          >
            {formLoading ? t('common.saving') : t('accounting.accounts.modal.create.submit')}
          </ZHBtn>
        ) : displayTab === 'journal' && canCreateJournal ? (
          <ZHBtn variant="primary" size="md" type="button" onClick={() => setShowJournal(true)}>
            {t('accounting.journal.primaryCreate')}
          </ZHBtn>
        ) : undefined
      }
    >
      {showJournal && canCreateJournal && (
        <CreateJournalEntryModal
          accounts={accounts.data ?? []}
          onClose={() => setShowJournal(false)}
          onCreated={journalEntries.refetch}
        />
      )}
      {(canViewAccounts && canViewJournal) ? (
        <div className="tabs">
          <button
            type="button"
            className={`tab${displayTab === 'accounts' ? ' tab--active' : ''}`}
            onClick={() => setTab('accounts')}
          >
            {t('accounting.tabs.accounts')}
          </button>
          <button
            type="button"
            className={`tab${displayTab === 'journal' ? ' tab--active' : ''}`}
            onClick={() => setTab('journal')}
          >
            {t('accounting.tabs.journal')}
          </button>
        </div>
      ) : null}

      {displayTab === 'accounts' && canViewAccounts && (
        <>
          <TableCard>
            {formError ? <ZHFormAlert type="error" message={t('common.errorPrefix')} detail={formError} /> : null}
            <div className="zh-form-tabs" role="tablist">
              {canCreateAccount ? (
                <button type="button" className={activeAccountSubTab === 'data' ? 'is-active' : ''} onClick={() => setAccountSubTab('data')}>
                  {t('common.formTab.data')}
                </button>
              ) : null}
              <button type="button" className={activeAccountSubTab === 'list' ? 'is-active' : ''} onClick={() => setAccountSubTab('list')}>
                {t('accounting.accounts.tabList')}
              </button>
            </div>

            {activeAccountSubTab === 'data' && canCreateAccount && (
              <form ref={formRef} onSubmit={submitAccount} noValidate>
                <input type="hidden" name="tenantId" value={tenantId} />
                <div className="zh-form">
                  <ZHFormBody>
                    <ZHFormSection title={t('accounting.accounts.modal.create.title')}>
                      <ZHGrid cols={2}>
                        <ZHField label={t('accounting.accounts.form.code')} required fieldError={errors.code?.message}>
                          <input
                            id="code"
                            placeholder={t('accounting.accounts.form.code.placeholder')}
                            disabled={formLoading}
                            {...register('code')}
                          />
                        </ZHField>

                        <ZHField label={t('accounting.accounts.form.name')} required fieldError={errors.name?.message}>
                          <input
                            id="name"
                            placeholder={t('accounting.accounts.form.name.placeholder')}
                            disabled={formLoading}
                            {...register('name')}
                          />
                        </ZHField>

                        <ZHField label={t('accounting.accounts.form.type')} required fieldError={errors.type?.message}>
                          <select id="type" disabled={formLoading} {...register('type', { valueAsNumber: true })}>
                            {accountTypes.map((x) => (
                              <option key={x.value} value={x.value}>
                                {x.label}
                              </option>
                            ))}
                          </select>
                        </ZHField>

                        <ZHField label={t('accounting.accounts.form.nature')} required fieldError={errors.nature?.message}>
                          <select id="nature" disabled={formLoading} {...register('nature', { valueAsNumber: true })}>
                            {accountNatures.map((x) => (
                              <option key={x.value} value={x.value}>
                                {x.label}
                              </option>
                            ))}
                          </select>
                        </ZHField>

                        <ZHColSpan span={2}>
                          <ZHField
                            label={t('accounting.accounts.form.parentId')}
                            hint={t('common.guid.placeholder')}
                            hintType="info"
                            fieldError={errors.parentId?.message}
                          >
                            <input
                              id="parentId"
                              placeholder={t('common.guid.placeholder')}
                              disabled={formLoading}
                              {...register('parentId')}
                            />
                          </ZHField>
                        </ZHColSpan>
                      </ZHGrid>
                    </ZHFormSection>
                  </ZHFormBody>
                </div>
              </form>
            )}

            {activeAccountSubTab === 'list' && (
              <>
                <div className="zh-mb-12">
                  <ZHSearchBar
                    searchQuery={accountListQuery}
                    onSearch={setAccountListQuery}
                    onClearAll={() => setAccountListQuery('')}
                    filterValues={{}}
                    placeholder={t('common.zhList.searchPlaceholder')}
                    resultCount={filteredAccounts.length}
                    entityLabel={t('common.zhList.entityLabel')}
                    loading={accounts.loading}
                    actionLabel={t('accounting.accounts.listNewAction')}
                    onAction={() => {
                      if (canCreateAccount) setAccountSubTab('data');
                    }}
                  />
                </div>
                {accounts.loading && <LoadingState />}
                {accounts.error && <ErrorState message={accounts.error} />}
                {!accounts.loading && !accounts.error && accounts.data?.length === 0 && (
                  <EmptyState message={t('accounting.accounts.empty')} />
                )}
                {!accounts.loading && !accounts.error && accounts.data && accounts.data.length > 0 && filteredAccounts.length === 0 && (
                  <EmptyState message={t('common.listTab.noMatch')} />
                )}
                {!accounts.loading && !accounts.error && accounts.data && accounts.data.length > 0 && filteredAccounts.length > 0 && (
                  <table>
                    <thead>
                      <tr>
                        <th>{t('accounting.accounts.table.code')}</th>
                        <th>{t('accounting.accounts.table.name')}</th>
                        <th>{t('accounting.accounts.table.type')}</th>
                        <th>{t('accounting.accounts.table.nature')}</th>
                        <th>{t('accounting.accounts.table.status')}</th>
                      </tr>
                    </thead>
                    <tbody>
                      {filteredAccounts.map((a) => (
                        <tr key={a.id}>
                          <td><span className="mono">{a.code}</span></td>
                          <td>{a.name}</td>
                          <td>{a.type}</td>
                          <td>{a.nature}</td>
                          <td>
                            <Badge
                              label={a.isActive ? t('common.active') : t('common.inactive')}
                              variant={a.isActive ? 'green' : 'red'}
                            />
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                )}
              </>
            )}
          </TableCard>
        </>
      )}

      {displayTab === 'journal' && canViewJournal && (
        <>
          {journalEntries.loading && <LoadingState />}
          {journalEntries.error   && <ErrorState message={journalEntries.error} />}
          {!journalEntries.loading && !journalEntries.error && journalEntries.data?.length === 0 && (
            <EmptyState message={t('accounting.journal.empty')} />
          )}
          {!journalEntries.loading && !journalEntries.error && journalEntries.data && journalEntries.data.length > 0 && (
            <TableCard>
              <table>
                <thead>
                  <tr>
                    <th>{t('accounting.journal.table.reference')}</th>
                    <th>{t('accounting.journal.table.date')}</th>
                    <th>{t('accounting.journal.table.description')}</th>
                    <th>{t('accounting.journal.table.lines')}</th>
                    <th>{t('accounting.journal.table.status')}</th>
                  </tr>
                </thead>
                <tbody>
                  {journalEntries.data.map((e) => (
                    <tr key={e.id}>
                      <td><span className="mono">{e.reference}</span></td>
                      <td>{new Date(e.date).toLocaleDateString('es-EC')}</td>
                      <td className="subtle">{e.description}</td>
                      <td className="zh-text-center">{e.lines.length}</td>
                      <td>
                        <Badge
                          label={documentStatusLabel[e.status]}
                          variant={statusVariant[e.status]}
                        />
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </TableCard>
          )}
        </>
      )}
    </PageShell>
  );
}
