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

type Tab = 'accounts' | 'journal';

const statusVariant: Record<DocumentStatus, 'blue' | 'green' | 'red'> = {
  [DocumentStatus.Draft]:  'blue',
  [DocumentStatus.Posted]: 'green',
  [DocumentStatus.Voided]: 'red',
};

export function AccountingPage() {
  const [tab, setTab]             = useState<Tab>('accounts');
  const [showAccount, setShowAccount]   = useState(false);
  const [showJournal, setShowJournal]   = useState(false);

  const accounts       = useAsync(accountingService.getAccounts);
  const journalEntries = useAsync(accountingService.getJournalEntries);

  return (
    <PageShell
      title="Contabilidad"
      action={
        tab === 'accounts'
          ? <button type="button" onClick={() => setShowAccount(true)}>+ Nueva cuenta</button>
          : <button type="button" onClick={() => setShowJournal(true)}>+ Nuevo asiento</button>
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
          Plan de cuentas
        </button>
        <button
          className={`tab${tab === 'journal' ? ' tab--active' : ''}`}
          onClick={() => setTab('journal')}
        >
          Asientos contables
        </button>
      </div>

      {tab === 'accounts' && (
        <>
          {accounts.loading && <LoadingState />}
          {accounts.error   && <ErrorState message={accounts.error} />}
          {!accounts.loading && !accounts.error && accounts.data?.length === 0 && (
            <EmptyState message="No hay cuentas registradas aún." />
          )}
          {!accounts.loading && !accounts.error && accounts.data && accounts.data.length > 0 && (
            <TableCard>
              <table>
                <thead>
                  <tr>
                    <th>Código</th>
                    <th>Nombre</th>
                    <th>Tipo</th>
                    <th>Naturaleza</th>
                    <th>Estado</th>
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
                          label={a.isActive ? 'Activa' : 'Inactiva'}
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
            <EmptyState message="No hay asientos contables registrados aún." />
          )}
          {!journalEntries.loading && !journalEntries.error && journalEntries.data && journalEntries.data.length > 0 && (
            <TableCard>
              <table>
                <thead>
                  <tr>
                    <th>Referencia</th>
                    <th>Fecha</th>
                    <th>Descripción</th>
                    <th>Líneas</th>
                    <th>Estado</th>
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
