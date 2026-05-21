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
      <div
        className="pg-section-body"
        style={{
          display: 'flex',
          gap: 'var(--space-4)',
          flexWrap: 'wrap',
          alignItems: 'flex-end',
          marginBottom: 'var(--space-3)',
        }}
      >
        <ZHField label="Desde">
          <input className="zh-input" type="date" value={jDesde} onChange={(e) => setJDesde(e.target.value)} />
        </ZHField>
        <ZHField label="Hasta">
          <input className="zh-input" type="date" value={jHasta} onChange={(e) => setJHasta(e.target.value)} />
        </ZHField>
        <span style={{ color: 'var(--color-text-secondary)', fontSize: 'var(--text-sm)', paddingBottom: 2 }}>
          {filteredJournal.length} asientos
        </span>
      </div>

      {journalEntries.loading && (
        <div style={{ padding: '40px' }}>
          <LoadingState />
        </div>
      )}
      {journalEntries.error && (
        <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={journalEntries.error} />
      )}
      {!journalEntries.loading && !journalEntries.error && filteredJournal.length === 0 && (
        <div style={{ padding: '40px' }}>
          <EmptyState message={t('finance.journal.empty')} />
        </div>
      )}
      {!journalEntries.loading && !journalEntries.error && filteredJournal.length > 0 && (
        <div style={{ overflowX: 'auto' }}>
          <table className="table">
            <thead>
              <tr>
                <th style={{ width: 28 }}></th>
                <th>{t('finance.journal.table.reference')}</th>
                <th>{t('finance.journal.table.date')}</th>
                <th>{t('finance.journal.table.description')}</th>
                <th style={{ textAlign: 'right' }}>Débito</th>
                <th style={{ textAlign: 'right' }}>Crédito</th>
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
                    style={{ cursor: 'pointer' }}
                    onClick={() => setExpandedEntry(isExpanded ? null : e.id)}
                  >
                    <td style={{ textAlign: 'center', color: 'var(--color-text-secondary)' }}>
                      <span className="material-symbols-outlined" style={{ fontSize: 18 }}>
                        {isExpanded ? 'expand_less' : 'expand_more'}
                      </span>
                    </td>
                    <td>
                      <span className="mono">{e.reference}</span>
                    </td>
                    <td>{new Date(e.date).toLocaleDateString('es-EC')}</td>
                    <td className="subtle">{e.description}</td>
                    <td style={{ textAlign: 'right', fontWeight: 600 }}>${totalDebit.toFixed(2)}</td>
                    <td style={{ textAlign: 'right', fontWeight: 600 }}>${totalCredit.toFixed(2)}</td>
                    <td>
                      <span className={statusBadgeClass[e.status]}>{documentStatusLabel[e.status]}</span>
                    </td>
                  </tr>,
                  isExpanded && (
                    <tr key={`${e.id}-lines`}>
                      <td colSpan={7} style={{ background: 'var(--color-surface-raised)', padding: 0 }}>
                        <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 'var(--text-sm)' }}>
                          <thead>
                            <tr style={{ background: 'var(--color-surface)' }}>
                              <th style={{ padding: '6px 12px', textAlign: 'left' }}>Cuenta</th>
                              <th style={{ padding: '6px 12px', textAlign: 'right' }}>Débito</th>
                              <th style={{ padding: '6px 12px', textAlign: 'right' }}>Crédito</th>
                            </tr>
                          </thead>
                          <tbody>
                            {e.lines.map((l) => (
                              <tr key={l.id}>
                                <td style={{ padding: '4px 12px' }}>
                                  {accountMap[l.accountId] ?? l.accountId.slice(0, 8)}
                                </td>
                                <td style={{ padding: '4px 12px', textAlign: 'right' }}>
                                  {l.debitAmount > 0 ? `$${l.debitAmount.toFixed(2)}` : ''}
                                </td>
                                <td style={{ padding: '4px 12px', textAlign: 'right' }}>
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
