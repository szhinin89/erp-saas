import { EmptyState, LoadingState } from '../../../components/PageShell';
import { ZHBtn, ZHField } from '../../../components/zh/ZHForm';
import { ZHPageNotice } from '../../../components/zh/ZHPageNotice';
import type { AccountingPageContext } from '../hooks/useAccountingPage';

type Props = Pick<
  AccountingPageContext,
  | 'balDesde'
  | 'setBalDesde'
  | 'balHasta'
  | 'setBalHasta'
  | 'balData'
  | 'balLoading'
  | 'balError'
  | 'fetchBalance'
>;

export function AccountingBalanceTab({
  balDesde,
  setBalDesde,
  balHasta,
  setBalHasta,
  balData,
  balLoading,
  balError,
  fetchBalance,
}: Props) {
  return (
    <div className="pg-section">
      <div className="pg-section-header">
        <div className="pg-section-header-left">
          <span className="material-symbols-outlined pg-section-icon">balance</span>
          <span className="pg-section-label">Balance de Comprobación</span>
        </div>
      </div>
      <div className="pg-section-body acc-tab-filters">
        <ZHField label="Desde">
          <input className="zh-input" type="date" value={balDesde} onChange={(e) => setBalDesde(e.target.value)} />
        </ZHField>
        <ZHField label="Hasta">
          <input className="zh-input" type="date" value={balHasta} onChange={(e) => setBalHasta(e.target.value)} />
        </ZHField>
        <ZHBtn variant="primary" size="md" onClick={() => void fetchBalance()} disabled={balLoading}>
          {balLoading ? 'Cargando...' : 'Generar'}
        </ZHBtn>
      </div>

      {balError && <ZHPageNotice variant="error" message={balError} />}
      {balLoading && (
        <div className="pg-pad-40">
          <LoadingState />
        </div>
      )}
      {!balLoading && !balError && balData.length === 0 && (
        <div className="pg-pad-40">
          <EmptyState message="Haz clic en 'Generar' para calcular el balance." />
        </div>
      )}
      {!balLoading && balData.length > 0 && (
        <div className="pg-overflow-x">
          <table className="table">
            <thead>
              <tr>
                <th>Código</th>
                <th>Cuenta</th>
                <th>Tipo</th>
                <th className="pg-th-right">Total Débito</th>
                <th className="pg-th-right">Total Crédito</th>
                <th className="pg-th-right">Saldo Neto</th>
              </tr>
            </thead>
            <tbody>
              {balData.map((l, i) => (
                <tr key={i}>
                  <td>
                    <span className="mono">{l.accountCode}</span>
                  </td>
                  <td>{l.accountName}</td>
                  <td className="pg-cell-muted">{l.accountType}</td>
                  <td className="pg-td-right">${l.totalDebit.toFixed(2)}</td>
                  <td className="pg-td-right">${l.totalCredit.toFixed(2)}</td>
                  <td
                    className={`acc-cell-balance ${l.netBalance >= 0 ? 'acc-cell-balance--pos' : 'acc-cell-balance--neg'}`}
                  >
                    ${l.netBalance.toFixed(2)}
                  </td>
                </tr>
              ))}
            </tbody>
            <tfoot>
              <tr className="acc-table-foot-row">
                <td colSpan={3}>TOTALES</td>
                <td className="pg-td-right">
                  ${balData.reduce((s, l) => s + l.totalDebit, 0).toFixed(2)}
                </td>
                <td className="pg-td-right">
                  ${balData.reduce((s, l) => s + l.totalCredit, 0).toFixed(2)}
                </td>
                <td className="pg-td-right">
                  ${balData.reduce((s, l) => s + l.netBalance, 0).toFixed(2)}
                </td>
              </tr>
            </tfoot>
          </table>
        </div>
      )}
    </div>
  );
}
