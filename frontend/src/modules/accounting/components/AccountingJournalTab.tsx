import { documentStatusLabel, DocumentStatus } from '../../../types/accounting';
import { EmptyState, LoadingState } from '../../../components/PageShell';
import { ZHBtn, ZHField } from '../../../components/zh/ZHForm';
import { ZHPageNotice } from '../../../components/zh/ZHPageNotice';
import type { AccountingPageContext } from '../hooks/useAccountingPage';

const statusBadgeClass: Record<DocumentStatus, string> = {
  [DocumentStatus.Draft]: 'badge badge--gray badge--md',
  [DocumentStatus.Posted]: 'badge badge--green badge--md',
  [DocumentStatus.Voided]: 'badge badge--red badge--md',
};

type Props = Pick<
  AccountingPageContext,
  | 't'
  | 'canCreateJournal'
  | 'accounts'
  | 'journalEntries'
  | 'jDesde'
  | 'setJDesde'
  | 'jHasta'
  | 'setJHasta'
  | 'filteredJournal'
  | 'expandedEntry'
  | 'setExpandedEntry'
  | 'setShowJournal'
>;

export function AccountingJournalTab({
  t,
  canCreateJournal,
  accounts,
  journalEntries,
  jDesde,
  setJDesde,
  jHasta,
  setJHasta,
  filteredJournal,
  expandedEntry,
  setExpandedEntry,
  setShowJournal,
}: Props) {
  return (
    <div className="pg-section">
      <div className="pg-section-header">
        <div className="pg-section-header-left">
          <span className="material-symbols-outlined pg-section-icon">menu_book</span>
          <span className="pg-section-label">Libro Diario</span>
        </div>
        {canCreateJournal && (
          <ZHBtn variant="primary" size="sm" onClick={() => setShowJournal(true)}>
            <span className="material-symbols-outlined">add</span>
            Asiento manual
          </ZHBtn>
        )}
      </div>
      <div className="pg-section-body acc-tab-filters acc-tab-filters--tight">
        <ZHField label="Desde">
          <input className="zh-input" type="date" value={jDesde} onChange={(e) => setJDesde(e.target.value)} />
        </ZHField>
        <ZHField label="Hasta">
          <input className="zh-input" type="date" value={jHasta} onChange={(e) => setJHasta(e.target.value)} />
        </ZHField>
        <span className="acc-tab-count-hint">
          {filteredJournal.length} asientos
        </span>
      </div>

      {journalEntries.loading && (
        <div className="pg-pad-40">
          <LoadingState />
        </div>
      )}
      {journalEntries.error && (
        <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={journalEntries.error} />
      )}
      {!journalEntries.loading && !journalEntries.error && filteredJournal.length === 0 && (
        <div className="pg-pad-40">
          <EmptyState message={t('finance.journal.empty')} />
        </div>
      )}
      {!journalEntries.loading && !journalEntries.error && filteredJournal.length > 0 && (
        <div className="pg-overflow-x">
          <table className="table">
            <thead>
              <tr>
                <th className="acc-journal-expand-col" aria-hidden />
                <th>{t('finance.journal.table.reference')}</th>
                <th>{t('finance.journal.table.date')}</th>
                <th>{t('finance.journal.table.description')}</th>
                <th className="pg-th-right">Débito</th>
                <th className="pg-th-right">Crédito</th>
                <th>{t('finance.journal.table.status')}</th>
              </tr>
            </thead>
            <tbody>
              {filteredJournal.map((e) => {
                const isExpanded = expandedEntry === e.id;
                const totalDebit = e.lines.reduce((s, l) => s + l.debitAmount, 0);
                const totalCredit = e.lines.reduce((s, l) => s + l.creditAmount, 0);
                const accountMap = accounts.data
                  ? Object.fromEntries(accounts.data.map((a) => [a.id, `${a.code} ${a.name}`]))
                  : {};
                return [
                  <tr
                    key={e.id}
                    className="pg-row-clickable"
                    onClick={() => setExpandedEntry(isExpanded ? null : e.id)}
                  >
                    <td className="acc-journal-expand-icon">
                      <span className="material-symbols-outlined pg-icon-18">
                        {isExpanded ? 'expand_less' : 'expand_more'}
                      </span>
                    </td>
                    <td>
                      <span className="mono">{e.reference}</span>
                    </td>
                    <td>{new Date(e.date).toLocaleDateString('es-EC')}</td>
                    <td className="subtle">{e.description}</td>
                    <td className="pg-td-right pg-cell-strong">${totalDebit.toFixed(2)}</td>
                    <td className="pg-td-right pg-cell-strong">${totalCredit.toFixed(2)}</td>
                    <td>
                      <span className={statusBadgeClass[e.status]}>{documentStatusLabel[e.status]}</span>
                    </td>
                  </tr>,
                  isExpanded && (
                    <tr key={`${e.id}-lines`}>
                      <td colSpan={7} className="acc-journal-expand-cell">
                        <table className="acc-journal-lines-table">
                          <thead>
                            <tr className="acc-journal-lines-thead">
                              <th className="acc-journal-lines-th">Cuenta</th>
                              <th className="acc-journal-lines-th-right">Débito</th>
                              <th className="acc-journal-lines-th-right">Crédito</th>
                            </tr>
                          </thead>
                          <tbody>
                            {e.lines.map((l) => (
                              <tr key={l.id}>
                                <td className="acc-journal-lines-td">
                                  {accountMap[l.accountId] ?? l.accountId.slice(0, 8)}
                                </td>
                                <td className="acc-journal-lines-td-right">
                                  {l.debitAmount > 0 ? `$${l.debitAmount.toFixed(2)}` : ''}
                                </td>
                                <td className="acc-journal-lines-td-right">
                                  {l.creditAmount > 0 ? `$${l.creditAmount.toFixed(2)}` : ''}
                                </td>
                              </tr>
                            ))}
                          </tbody>
                        </table>
                      </td>
                    </tr>
                  ),
                ];
              })}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
