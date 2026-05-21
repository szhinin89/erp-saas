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
      <div
        className="pg-section-body"
        style={{
          display: 'flex',
          gap: 'var(--space-4)',
          flexWrap: 'wrap',
          alignItems: 'flex-end',
          marginBottom: 'var(--space-4)',
        }}
      >
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
        <div style={{ padding: 40 }}>
          <LoadingState />
        </div>
      )}
      {!balLoading && !balError && balData.length === 0 && (
        <div style={{ padding: 40 }}>
          <EmptyState message="Haz clic en 'Generar' para calcular el balance." />
        </div>
      )}
      {!balLoading && balData.length > 0 && (
        <div style={{ overflowX: 'auto' }}>
          <table className="table">
            <thead>
              <tr>
                <th>Código</th>
                <th>Cuenta</th>
                <th>Tipo</th>
                <th style={{ textAlign: 'right' }}>Total Débito</th>
                <th style={{ textAlign: 'right' }}>Total Crédito</th>
                <th style={{ textAlign: 'right' }}>Saldo Neto</th>
              </tr>
            </thead>
            <tbody>
              {balData.map((l, i) => (
                <tr key={i}>
                  <td>
                    <span className="mono">{l.accountCode}</span>
                  </td>
                  <td>{l.accountName}</td>
                  <td style={{ color: 'var(--color-text-secondary)' }}>{l.accountType}</td>
                  <td style={{ textAlign: 'right' }}>${l.totalDebit.toFixed(2)}</td>
                  <td style={{ textAlign: 'right' }}>${l.totalCredit.toFixed(2)}</td>
                  <td
                    style={{
                      textAlign: 'right',
                      fontWeight: 700,
                      color: l.netBalance >= 0 ? 'var(--color-success)' : 'var(--color-error)',
                    }}
                  >
                    ${l.netBalance.toFixed(2)}
                  </td>
                </tr>
              ))}
            </tbody>
            <tfoot>
              <tr style={{ background: 'var(--color-surface-raised)', fontWeight: 700 }}>
                <td colSpan={3}>TOTALES</td>
                <td style={{ textAlign: 'right' }}>
                  ${balData.reduce((s, l) => s + l.totalDebit, 0).toFixed(2)}
                </td>
                <td style={{ textAlign: 'right' }}>
                  ${balData.reduce((s, l) => s + l.totalCredit, 0).toFixed(2)}
                </td>
                <td style={{ textAlign: 'right' }}>
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
