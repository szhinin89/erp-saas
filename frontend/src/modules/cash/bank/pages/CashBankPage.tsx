import { useCallback, useEffect, useState } from 'react';
import { EmptyState, LoadingState, NoAccessPage } from '../../../../components/PageShell';
import { ErpPageTemplate } from '../../../../templates/ErpPageTemplate';
import { ZHPageNotice } from '../../../../components/zh/ZHPageNotice';
import { useI18n } from '../../../../i18n/i18n';
import { usePermissionsStore } from '../../../../store/permissionsStore';
import {
  cashBankService,
  type BankAccount,
  type BankStatement,
  type BankTransaction,
  type PettyCash,
} from '../api/cashBankService';

function money(value: number, currency = 'USD'): string {
  return value.toLocaleString('es-EC', { style: 'currency', currency, minimumFractionDigits: 2 });
}

export function CashBankPage() {
  const { t } = useI18n();
  const hasPerm = usePermissionsStore((s) => s.has);
  const canViewAccounts = hasPerm('cash.bank.view');
  const canViewPetty    = hasPerm('cash.bank.cajachica.view');

  const [accounts,   setAccounts]   = useState<BankAccount[]>([]);
  const [pettyCashes, setPettyCashes] = useState<PettyCash[]>([]);
  const [statements,  setStatements]  = useState<BankStatement[]>([]);
  const [movements,   setMovements]   = useState<BankTransaction[]>([]);
  const [selectedAccountId, setSelectedAccountId] = useState('');
  const [selectedStatementId, setSelectedStatementId] = useState('');
  const [loading, setLoading] = useState(true);
  const [loadingStatements, setLoadingStatements] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const loadMain = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const tasks: Promise<void>[] = [];
      if (canViewAccounts) {
        tasks.push(
          cashBankService.listBankAccounts().then((rows) => {
            setAccounts(rows);
            setSelectedAccountId((prev) => prev || rows[0]?.id || '');
          }),
        );
      }
      if (canViewPetty) {
        tasks.push(cashBankService.listPettyCashes().then(setPettyCashes));
      }
      await Promise.all(tasks);
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    } finally {
      setLoading(false);
    }
  }, [canViewAccounts, canViewPetty]);

  useEffect(() => {
    if (canViewAccounts || canViewPetty) void loadMain();
  }, [canViewAccounts, canViewPetty, loadMain]);

  useEffect(() => {
    if (!selectedAccountId || !canViewAccounts) {
      setStatements([]);
      setMovements([]);
      setSelectedStatementId('');
      return;
    }
    let cancelled = false;
    setLoadingStatements(true);
    cashBankService
      .listStatements(selectedAccountId)
      .then((rows) => {
        if (cancelled) return;
        setStatements(rows);
        setSelectedStatementId(rows[0]?.id ?? '');
      })
      .catch((e) => {
        if (!cancelled) setError(e instanceof Error ? e.message : String(e));
      })
      .finally(() => {
        if (!cancelled) setLoadingStatements(false);
      });
    return () => { cancelled = true; };
  }, [selectedAccountId, canViewAccounts]);

  useEffect(() => {
    if (!selectedStatementId) {
      setMovements([]);
      return;
    }
    let cancelled = false;
    cashBankService
      .getStatementDetail(selectedStatementId)
      .then((detail) => {
        if (!cancelled) setMovements(detail?.rows ?? []);
      })
      .catch((e) => {
        if (!cancelled) setError(e instanceof Error ? e.message : String(e));
      });
    return () => { cancelled = true; };
  }, [selectedStatementId]);

  if (!canViewAccounts && !canViewPetty) {
    return <NoAccessPage title={t('app.nav.item.cash.bank')} />;
  }

  const selectedAccount = accounts.find((a) => a.id === selectedAccountId);

  return (
    <ErpPageTemplate
      kicker={t('app.nav.group.cash')}
      title={t('app.nav.item.cash.bank')}
      subtitle="Cuentas bancarias, extractos importados y cajas chicas."
      action={
        <button className="zh-btn zh-btn--secondary" type="button" disabled={loading} onClick={() => void loadMain()}>
          <span className="material-symbols-outlined">refresh</span>
          Actualizar
        </button>
      }
    >
      {error && <ZHPageNotice variant="error" message="Error al cargar" detail={error} />}

      {loading ? (
        <div className="pg-pad-40"><LoadingState /></div>
      ) : (
        <div className="pg-page">
          {canViewAccounts && (
            <div className="pg-section">
              <div className="pg-section-header">
                <span className="material-symbols-outlined pg-section-icon">account_balance</span>
                <span className="pg-section-label">Cuentas bancarias</span>
              </div>
              {accounts.length === 0 ? (
                <div className="pg-pad-40"><EmptyState message="No hay cuentas bancarias registradas." /></div>
              ) : (
                <div className="pg-overflow-x">
                  <table className="table">
                    <thead>
                      <tr>
                        <th>Cuenta</th>
                        <th>Número</th>
                        <th>Tipo</th>
                        <th className="pg-th-right">Saldo actual</th>
                        <th>Estado</th>
                      </tr>
                    </thead>
                    <tbody>
                      {accounts.map((a) => (
                        <tr
                          key={a.id}
                          className={a.id === selectedAccountId ? 'pg-row-clickable is-active' : 'pg-row-clickable'}
                          onClick={() => setSelectedAccountId(a.id)}
                        >
                          <td><strong>{a.name}</strong></td>
                          <td className="mono">{a.accountNumber}</td>
                          <td>{a.accountType}</td>
                          <td className="pg-td-right">{money(a.currentBalance, a.currency)}</td>
                          <td>
                            <span className={`badge badge--md ${a.isActive ? 'badge--green' : 'badge--gray'}`}>
                              {a.isActive ? 'Activa' : 'Inactiva'}
                            </span>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}

              {selectedAccount && (
                <>
                  <div className="pg-table-controls pg-mt-16">
                    <div className="pg-table-controls-left">
                      <select
                        className="zh-input"
                        value={selectedStatementId}
                        disabled={loadingStatements || statements.length === 0}
                        onChange={(e) => setSelectedStatementId(e.target.value)}
                      >
                        <option value="">Extracto…</option>
                        {statements.map((s) => (
                          <option key={s.id} value={s.id}>
                            {new Date(s.periodFrom).toLocaleDateString('es')} – {new Date(s.periodTo).toLocaleDateString('es')}
                            {s.isReconciled ? ' (conciliado)' : ''}
                          </option>
                        ))}
                      </select>
                    </div>
                    <div className="pg-table-controls-right">
                      <span>{statements.length} extractos</span>
                    </div>
                  </div>

                  {loadingStatements ? (
                    <div className="pg-pad-40"><LoadingState /></div>
                  ) : statements.length === 0 ? (
                    <EmptyState message="Esta cuenta no tiene extractos importados." />
                  ) : movements.length === 0 ? (
                    <EmptyState message="Seleccione un extracto para ver movimientos." />
                  ) : (
                    <div className="pg-overflow-x">
                      <table className="table">
                        <thead>
                          <tr>
                            <th>Fecha</th>
                            <th>Descripción</th>
                            <th>Referencia</th>
                            <th className="pg-th-right">Monto</th>
                            <th>Estado</th>
                          </tr>
                        </thead>
                        <tbody>
                          {movements.map((m) => (
                            <tr key={m.id}>
                              <td>{new Date(m.fecha).toLocaleDateString('es')}</td>
                              <td>{m.description}</td>
                              <td className="mono">{m.referencia || '—'}</td>
                              <td className="pg-td-right">{money(m.monto, selectedAccount.currency)}</td>
                              <td>{m.status}</td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </div>
                  )}
                </>
              )}
            </div>
          )}

          {canViewPetty && (
            <div className="pg-section">
              <div className="pg-section-header">
                <span className="material-symbols-outlined pg-section-icon">savings</span>
                <span className="pg-section-label">Cajas chicas</span>
              </div>
              {pettyCashes.length === 0 ? (
                <div className="pg-pad-40"><EmptyState message="No hay cajas chicas configuradas." /></div>
              ) : (
                <div className="pg-overflow-x">
                  <table className="table">
                    <thead>
                      <tr>
                        <th>Nombre</th>
                        <th className="pg-th-right">Asignado</th>
                        <th className="pg-th-right">Saldo actual</th>
                        <th>Estado</th>
                      </tr>
                    </thead>
                    <tbody>
                      {pettyCashes.map((c) => (
                        <tr key={c.id}>
                          <td><strong>{c.name}</strong></td>
                          <td className="pg-td-right">{money(c.assignedBalance)}</td>
                          <td className="pg-td-right">{money(c.currentBalance)}</td>
                          <td>
                            <span className={`badge badge--md ${c.isActive ? 'badge--green' : 'badge--gray'}`}>
                              {c.isActive ? 'Activa' : 'Inactiva'}
                            </span>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </div>
          )}
        </div>
      )}
    </ErpPageTemplate>
  );
}
