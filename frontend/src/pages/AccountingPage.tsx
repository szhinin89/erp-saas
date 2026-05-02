import { useMemo, useRef, useState, type FormEvent } from 'react';
import { accountingService, type CreateAccountRequest } from '../services/accountingService';
import { useAsync } from '../hooks/useAsync';
import { documentStatusLabel, DocumentStatus } from '../types/accounting';
import {
  PageShell, TableCard, EmptyState, ErrorState, LoadingState, Badge,
} from '../components/PageShell';
import { CreateJournalEntryModal } from '../components/CreateJournalEntryModal';
import './AccountingPage.css';
import { useI18n } from '../i18n/i18n';
import { ZHBtn, ZHFormBody, ZHFormSection, ZHGrid, ZHField, ZHFormAlert } from '../components/zh/ZHForm';
import { ZHColSpan } from '../components/zh/ZHLayout';
import ZHSearchBar from '../components/shared/ZHSearchBar';
import { useAuthStore } from '../store/authStore';

type Tab = 'accounts' | 'journal';

const EMPTY: CreateAccountRequest = { code: '', name: '', type: 0, nature: 0, parentId: null };

const statusVariant: Record<DocumentStatus, 'blue' | 'green' | 'red'> = {
  [DocumentStatus.Draft]:  'blue',
  [DocumentStatus.Posted]: 'green',
  [DocumentStatus.Voided]: 'red',
};

export function AccountingPage() {
  const { t } = useI18n();
  const tenantId = useAuthStore((s) => s.user?.tenantId ?? '');
  const [tab, setTab]           = useState<Tab>('accounts');
  const [showJournal, setShowJournal] = useState(false);

  const accounts       = useAsync(accountingService.getAccounts);
  const journalEntries = useAsync(accountingService.getJournalEntries);

  const formRef = useRef<HTMLFormElement>(null);
  const [form, setForm]         = useState<CreateAccountRequest>(EMPTY);
  const [formError, setFormError]   = useState('');
  const [formLoading, setFormLoading] = useState(false);
  const [accountSubTab, setAccountSubTab] = useState<'data' | 'list'>('data');
  const [accountListQuery, setAccountListQuery] = useState('');

  const filteredAccounts = useMemo(() => {
    const data = accounts.data ?? [];
    const q = accountListQuery.trim().toLowerCase();
    if (!q) return data;
    return data.filter((a) => `${a.code} ${a.name} ${a.type} ${a.nature}`.toLowerCase().includes(q));
  }, [accounts.data, accountListQuery]);

  const setField = (field: keyof CreateAccountRequest, value: unknown) =>
    setForm((f) => ({ ...f, [field]: value }));

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

  const submitAccount = async (e: FormEvent) => {
    e.preventDefault();
    setFormError('');
    setFormLoading(true);
    try {
      await accountingService.createAccount({
        ...form,
        parentId: form.parentId || null,
      });
      setForm(EMPTY);
      accounts.refetch();
      setAccountSubTab('list');
    } catch (err: unknown) {
      setFormError(
        (err as { response?: { data?: { error?: string } } })?.response?.data?.error
          ?? t('accounting.accounts.modal.create.error')
      );
    } finally {
      setFormLoading(false);
    }
  };

  return (
    <PageShell
      kicker={t('app.nav.group.accounting')}
      title={tab === 'accounts' ? t('accounting.tabs.accounts') : t('accounting.tabs.journal')}
      action={
        tab === 'accounts' && accountSubTab === 'data' ? (
          <ZHBtn
            variant="primary"
            size="md"
            type="button"
            disabled={formLoading}
            onClick={() => formRef.current?.requestSubmit()}
          >
            {formLoading ? t('common.saving') : t('accounting.accounts.modal.create.submit')}
          </ZHBtn>
        ) : tab === 'journal' ? (
          <ZHBtn variant="primary" size="md" type="button" onClick={() => setShowJournal(true)}>
            {t('accounting.journal.primaryCreate')}
          </ZHBtn>
        ) : undefined
      }
    >
      {showJournal && (
        <CreateJournalEntryModal
          accounts={accounts.data ?? []}
          onClose={() => setShowJournal(false)}
          onCreated={journalEntries.refetch}
        />
      )}
      <div className="tabs">
        <button
          className={`tab${tab === 'accounts' ? ' tab--active' : ''}`}
          onClick={() => setTab('accounts')}
        >
          {t('accounting.tabs.accounts')}
        </button>
        <button
          className={`tab${tab === 'journal' ? ' tab--active' : ''}`}
          onClick={() => setTab('journal')}
        >
          {t('accounting.tabs.journal')}
        </button>
      </div>

      {tab === 'accounts' && (
        <>
          <TableCard>
            {formError ? <ZHFormAlert type="error" message={t('common.errorPrefix')} detail={formError} /> : null}
            <div className="zh-form-tabs" role="tablist">
              <button type="button" className={accountSubTab === 'data' ? 'is-active' : ''} onClick={() => setAccountSubTab('data')}>
                {t('common.formTab.data')}
              </button>
              <button type="button" className={accountSubTab === 'list' ? 'is-active' : ''} onClick={() => setAccountSubTab('list')}>
                {t('accounting.accounts.tabList')}
              </button>
            </div>

            {accountSubTab === 'data' && (
              <form ref={formRef} onSubmit={submitAccount}>
                <input type="hidden" name="tenantId" value={tenantId} />
                <div className="zh-form">
                  <ZHFormBody>
                    <ZHFormSection title={t('accounting.accounts.modal.create.title')}>
                      <ZHGrid cols={2}>
                        <ZHField label={t('accounting.accounts.form.code')} required>
                          <input
                            id="code"
                            value={form.code}
                            onChange={(e) => setField('code', e.target.value)}
                            placeholder={t('accounting.accounts.form.code.placeholder')}
                            required
                            disabled={formLoading}
                          />
                        </ZHField>

                        <ZHField label={t('accounting.accounts.form.name')} required>
                          <input
                            id="name"
                            value={form.name}
                            onChange={(e) => setField('name', e.target.value)}
                            placeholder={t('accounting.accounts.form.name.placeholder')}
                            required
                            disabled={formLoading}
                          />
                        </ZHField>

                        <ZHField label={t('accounting.accounts.form.type')} required>
                          <select
                            id="type"
                            value={form.type}
                            onChange={(e) => setField('type', Number(e.target.value))}
                            disabled={formLoading}
                          >
                            {accountTypes.map((x) => (
                              <option key={x.value} value={x.value}>{x.label}</option>
                            ))}
                          </select>
                        </ZHField>

                        <ZHField label={t('accounting.accounts.form.nature')} required>
                          <select
                            id="nature"
                            value={form.nature}
                            onChange={(e) => setField('nature', Number(e.target.value))}
                            disabled={formLoading}
                          >
                            {accountNatures.map((x) => (
                              <option key={x.value} value={x.value}>{x.label}</option>
                            ))}
                          </select>
                        </ZHField>

                        <ZHColSpan span={2}>
                          <ZHField label={t('accounting.accounts.form.parentId')} hint={t('common.guid.placeholder')} hintType="info">
                            <input
                              id="parentId"
                              value={form.parentId ?? ''}
                              onChange={(e) => setField('parentId', e.target.value || null)}
                              placeholder={t('common.guid.placeholder')}
                              disabled={formLoading}
                            />
                          </ZHField>
                        </ZHColSpan>
                      </ZHGrid>
                    </ZHFormSection>
                  </ZHFormBody>
                </div>
              </form>
            )}

            {accountSubTab === 'list' && (
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
                    onAction={() => setAccountSubTab('data')}
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

      {tab === 'journal' && (
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
