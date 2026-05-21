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
      <div className="pg-section-body acc-tab-filters">
        <div className="acc-tab-field-account">
          <ZHField label="Cuenta">
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
        </div>
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
        <div className="pg-pad-40">
          <LoadingState />
        </div>
      )}
      {!mayorLoading && !mayorError && mayorData.length === 0 && mayorAccountId && (
        <div className="pg-pad-40">
          <EmptyState message="Sin movimientos en el período seleccionado." />
        </div>
      )}
      {!mayorLoading && mayorData.length > 0 && (
        <div className="pg-overflow-x">
          <table className="table">
            <thead>
              <tr>
                <th>Fecha</th>
                <th>Referencia</th>
                <th>Descripción</th>
                <th className="pg-th-right">Débito</th>
                <th className="pg-th-right">Crédito</th>
                <th className="pg-th-right">Saldo</th>
              </tr>
            </thead>
            <tbody>
              {mayorData.map((l, i) => (
                <tr key={i}>
                  <td className="pg-cell-muted">
                    {new Date(l.date).toLocaleDateString('es-EC')}
                  </td>
                  <td>
                    <span className="mono">{l.reference}</span>
                  </td>
                  <td>{l.description}</td>
                  <td className="pg-td-right">{l.debit > 0 ? `$${l.debit.toFixed(2)}` : ''}</td>
                  <td className="pg-td-right">{l.credit > 0 ? `$${l.credit.toFixed(2)}` : ''}</td>
                  <td
                    className={`acc-cell-balance ${l.balance >= 0 ? 'acc-cell-balance-mayor--pos' : 'acc-cell-balance-mayor--neg'}`}
                  >
                    ${l.balance.toFixed(2)}
                  </td>
                </tr>
              ))}
            </tbody>
            <tfoot>
              <tr className="acc-table-foot-row">
                <td colSpan={3}>TOTALES</td>
                <td className="pg-td-right">
                  ${mayorData.reduce((s, l) => s + l.debit, 0).toFixed(2)}
                </td>
                <td className="pg-td-right">
                  ${mayorData.reduce((s, l) => s + l.credit, 0).toFixed(2)}
                </td>
                <td className="pg-td-right">
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
