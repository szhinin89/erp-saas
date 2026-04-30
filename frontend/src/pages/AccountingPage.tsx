import { useState } from 'react';
import { accountingService } from '../services/accountingService';
import { useAsync } from '../hooks/useAsync';
import { documentStatusLabel, DocumentStatus } from '../types/accounting';
import {
  PageShell, TableCard, EmptyState, ErrorState, LoadingState, Badge,
} from '../components/PageShell';
import { CreateAccountModal } from '../components/CreateAccountModal';
import { CreateJournalEntryModal } from '../components/CreateJournalEntryModal';
import './AccountingPage.css';
import { useI18n } from '../i18n/i18n';

type Tab = 'accounts' | 'journal';

const statusVariant: Record<DocumentStatus, 'blue' | 'green' | 'red'> = {
  [DocumentStatus.Draft]:  'blue',
  [DocumentStatus.Posted]: 'green',
  [DocumentStatus.Voided]: 'red',
};

export function AccountingPage() {
  const { t } = useI18n();
  const [tab, setTab]             = useState<Tab>('accounts');
  const [showAccount, setShowAccount]   = useState(false);
  const [showJournal, setShowJournal]   = useState(false);

  const accounts       = useAsync(accountingService.getAccounts);
  const journalEntries = useAsync(accountingService.getJournalEntries);

  return (
    <PageShell
      title={t('accounting.title')}
      action={
        tab === 'accounts'
          ? <button type="button" onClick={() => setShowAccount(true)}>{t('accounting.actions.createAccount')}</button>
          : <button type="button" onClick={() => setShowJournal(true)}>{t('accounting.actions.createJournal')}</button>
      }
    >
      {showAccount && (
        <CreateAccountModal
          onClose={() => setShowAccount(false)}
          onCreated={accounts.refetch}
        />
      )}
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
          {accounts.loading && <LoadingState />}
          {accounts.error   && <ErrorState message={accounts.error} />}
          {!accounts.loading && !accounts.error && accounts.data?.length === 0 && (
            <EmptyState message={t('accounting.accounts.empty')} />
          )}
          {!accounts.loading && !accounts.error && accounts.data && accounts.data.length > 0 && (
            <TableCard>
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
                  {accounts.data.map((a) => (
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
            </TableCard>
          )}
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
                      <td style={{ color: '#6b7280' }}>{e.description}</td>
                      <td style={{ textAlign: 'center' }}>{e.lines.length}</td>
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
