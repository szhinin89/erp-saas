import { EmptyState, LoadingState } from '../../../components/PageShell';
import { ZHBtn, ZHField } from '../../../components/zh/ZHForm';
import { ZHPageNotice } from '../../../components/zh/ZHPageNotice';
import type { AccountingPageContext } from '../hooks/useAccountingPage';

type Props = Pick<
  AccountingPageContext,
  | 'accounts'
  | 'mayorAccountId'
  | 'setMayorAccountId'
  | 'mayorDesde'
  | 'setMayorDesde'
  | 'mayorHasta'
  | 'setMayorHasta'
  | 'mayorData'
  | 'mayorLoading'
  | 'mayorError'
  | 'fetchMayor'
>;

export function AccountingMayorTab({
  accounts,
  mayorAccountId,
  setMayorAccountId,
  mayorDesde,
  setMayorDesde,
  mayorHasta,
  setMayorHasta,
  mayorData,
  mayorLoading,
  mayorError,
  fetchMayor,
}: Props) {
  return (
    <div className="pg-section">
      <div className="pg-section-header">
        <div className="pg-section-header-left">
          <span className="material-symbols-outlined pg-section-icon">account_balance_wallet</span>
          <span className="pg-section-label">Mayor General</span>
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
        <ZHField label="Cuenta" style={{ minWidth: 260 }}>
          <select
            className="zh-input"
            value={mayorAccountId}
            onChange={(e) => setMayorAccountId(e.target.value)}
            disabled={accounts.loading}
          >
            <option value="">-- Seleccionar cuenta --</option>
            {(accounts.data ?? [])
              .filter((a) => a.isActive)
              .map((a) => (
                <option key={a.id} value={a.id}>
                  {a.code} — {a.name}
                </option>
              ))}
          </select>
        </ZHField>
        <ZHField label="Desde">
          <input
            className="zh-input"
            type="date"
            value={mayorDesde}
            onChange={(e) => setMayorDesde(e.target.value)}
          />
        </ZHField>
        <ZHField label="Hasta">
          <input
            className="zh-input"
            type="date"
            value={mayorHasta}
            onChange={(e) => setMayorHasta(e.target.value)}
          />
        </ZHField>
        <ZHBtn
          variant="primary"
          size="md"
          onClick={() => void fetchMayor()}
          disabled={!mayorAccountId || mayorLoading}
        >
          {mayorLoading ? 'Cargando...' : 'Consultar'}
        </ZHBtn>
      </div>

      {mayorError && <ZHPageNotice variant="error" message={mayorError} />}
      {mayorLoading && (
        <div style={{ padding: 40 }}>
          <LoadingState />
        </div>
      )}
      {!mayorLoading && !mayorError && mayorData.length === 0 && mayorAccountId && (
        <div style={{ padding: 40 }}>
          <EmptyState message="Sin movimientos en el período seleccionado." />
        </div>
      )}
      {!mayorLoading && mayorData.length > 0 && (
        <div style={{ overflowX: 'auto' }}>
          <table className="table">
            <thead>
              <tr>
                <th>Fecha</th>
                <th>Referencia</th>
                <th>Descripción</th>
                <th style={{ textAlign: 'right' }}>Débito</th>
                <th style={{ textAlign: 'right' }}>Crédito</th>
                <th style={{ textAlign: 'right' }}>Saldo</th>
              </tr>
            </thead>
            <tbody>
              {mayorData.map((l, i) => (
                <tr key={i}>
                  <td style={{ color: 'var(--color-text-secondary)' }}>
                    {new Date(l.date).toLocaleDateString('es-EC')}
                  </td>
                  <td>
                    <span className="mono">{l.reference}</span>
                  </td>
                  <td>{l.description}</td>
                  <td style={{ textAlign: 'right' }}>{l.debit > 0 ? `$${l.debit.toFixed(2)}` : ''}</td>
                  <td style={{ textAlign: 'right' }}>{l.credit > 0 ? `$${l.credit.toFixed(2)}` : ''}</td>
                  <td
                    style={{
                      textAlign: 'right',
                      fontWeight: 700,
                      color: l.balance >= 0 ? 'var(--color-primary)' : 'var(--color-error)',
                    }}
                  >
                    ${l.balance.toFixed(2)}
                  </td>
                </tr>
              ))}
            </tbody>
            <tfoot>
              <tr style={{ background: 'var(--color-surface-raised)', fontWeight: 700 }}>
                <td colSpan={3}>TOTALES</td>
                <td style={{ textAlign: 'right' }}>
                  ${mayorData.reduce((s, l) => s + l.debit, 0).toFixed(2)}
                </td>
                <td style={{ textAlign: 'right' }}>
                  ${mayorData.reduce((s, l) => s + l.credit, 0).toFixed(2)}
                </td>
                <td style={{ textAlign: 'right' }}>
                  ${mayorData[mayorData.length - 1]?.balance.toFixed(2) ?? '0.00'}
                </td>
              </tr>
            </tfoot>
          </table>
        </div>
      )}
    </div>
  );
}
