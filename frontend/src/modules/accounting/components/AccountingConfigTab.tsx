import type { ConfiguracionContableEmpresaDto } from '../api/accountingConfigService';
import { EmptyState, LoadingState } from '../../../components/PageShell';
import { ZHBtn, ZHField } from '../../../components/zh/ZHForm';
import { ZHPageNotice } from '../../../components/zh/ZHPageNotice';
import { AccountTreeSelect } from '../../../components/accounting/AccountTreeSelect';
import type { AccountingPageContext } from '../hooks/useAccountingPage';

type Props = Pick<
  AccountingPageContext,
  | 't'
  | 'canEditConfig'
  | 'accounts'
  | 'config'
  | 'configError'
  | 'configForm'
  | 'setConfigForm'
  | 'configSaving'
  | 'gastoError'
  | 'gastoSaving'
  | 'gastoMappings'
  | 'newGastoCategoria'
  | 'setNewGastoCategoria'
  | 'newGastoCuentaId'
  | 'setNewGastoCuentaId'
  | 'createGastoMapping'
  | 'deleteGastoMapping'
>;

const CONFIG_FIELDS: [keyof ConfiguracionContableEmpresaDto, string][] = [
  ['cuentaInventarioId', 'finance.config.fields.cuentaInventario'],
  ['cuentaProveedoresId', 'finance.config.fields.cuentaProveedores'],
  ['cuentaVentasId', 'finance.config.fields.cuentaVentas'],
  ['cuentaClientesId', 'finance.config.fields.cuentaClientes'],
  ['cuentaIvaComprasId', 'finance.config.fields.cuentaIvaCompras'],
  ['cuentaIvaVentasId', 'finance.config.fields.cuentaIvaVentas'],
  ['cuentaEfectivoId', 'finance.config.fields.cuentaEfectivo'],
  ['cuentaBancoId', 'finance.config.fields.cuentaBanco'],
];

export function AccountingConfigTab({
  t,
  canEditConfig,
  accounts,
  config,
  configError,
  configForm,
  setConfigForm,
  configSaving,
  gastoError,
  gastoSaving,
  gastoMappings,
  newGastoCategoria,
  setNewGastoCategoria,
  newGastoCuentaId,
  setNewGastoCuentaId,
  createGastoMapping,
  deleteGastoMapping,
}: Props) {
  return (
    <div className="pg-section">
      {configError && <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={configError} />}
      {config.loading && (
        <div className="pg-pad-40">
          <LoadingState />
        </div>
      )}
      {config.error && <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={config.error} />}

      {!config.loading && !config.error && (
        <>
          <div className="pg-section-header">
            <div className="pg-section-header-left">
              <span className="material-symbols-outlined pg-section-icon">account_balance</span>
              <span className="pg-section-label">{t('finance.config.title')}</span>
            </div>
          </div>
          <div className="pg-section-body">
            <div className="pg-form-grid pg-form-grid--2">
              {CONFIG_FIELDS.map(([key, labelKey]) => (
                <ZHField key={key} label={t(labelKey)}>
                  <AccountTreeSelect
                    value={configForm[key] ?? null}
                    onChange={(next) => setConfigForm((s) => ({ ...s, [key]: next }))}
                    accounts={accounts.data ?? []}
                    disabled={!canEditConfig || configSaving}
                    placeholder={t('common.select')}
                  />
                </ZHField>
              ))}
            </div>
          </div>

          <div className="pg-section-header acc-config-section-head">
            <div className="pg-section-header-left">
              <span className="material-symbols-outlined pg-section-icon">category</span>
              <span className="pg-section-label">{t('finance.config.expenses.title')}</span>
            </div>
          </div>
          <div className="pg-section-body">
            {gastoError && <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={gastoError} />}
            <div className="pg-form-grid pg-form-grid--2 acc-form-grid-mb">
              <ZHField label={t('finance.config.expenses.fields.category')}>
                <input
                  className="zh-input"
                  value={newGastoCategoria}
                  disabled={!canEditConfig || gastoSaving}
                  onChange={(e) => setNewGastoCategoria(e.target.value)}
                  placeholder={t('finance.config.expenses.fields.categoryPlaceholder')}
                />
              </ZHField>
              <ZHField label={t('finance.config.expenses.fields.account')}>
                <AccountTreeSelect
                  value={newGastoCuentaId || null}
                  onChange={(next) => setNewGastoCuentaId(next ?? '')}
                  accounts={accounts.data ?? []}
                  disabled={!canEditConfig || gastoSaving}
                  placeholder={t('common.select')}
                />
              </ZHField>
            </div>
            <ZHBtn
              variant="secondary"
              size="md"
              type="button"
              disabled={!canEditConfig || gastoSaving}
              onClick={() => void createGastoMapping()}
            >
              {gastoSaving ? t('common.saving') : t('finance.config.expenses.actions.add')}
            </ZHBtn>

            {gastoMappings.loading && (
              <div className="pg-state-pad-24">
                <LoadingState />
              </div>
            )}
            {gastoMappings.error && (
              <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={gastoMappings.error} />
            )}
            {!gastoMappings.loading && !gastoMappings.error && (gastoMappings.data ?? []).length === 0 && (
              <div className="pg-state-pad-24">
                <EmptyState message={t('finance.config.expenses.empty')} />
              </div>
            )}
            {!gastoMappings.loading && !gastoMappings.error && (gastoMappings.data ?? []).length > 0 && (
              <div className="pg-overflow-x acc-table-scroll-mt">
                <table className="table">
                  <thead>
                    <tr>
                      <th>{t('finance.config.expenses.table.category')}</th>
                      <th>{t('finance.config.expenses.table.account')}</th>
                      <th className="pg-th-right">{t('common.actions')}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {(gastoMappings.data ?? []).map((row) => {
                      const acc = (accounts.data ?? []).find((a) => a.id === row.cuentaGastoId);
                      return (
                        <tr key={row.id}>
                          <td>{row.categoria}</td>
                          <td>{acc ? `${acc.code} — ${acc.name}` : row.cuentaGastoId}</td>
                          <td className="pg-td-right">
                            <ZHBtn
                              variant="destructive"
                              size="sm"
                              type="button"
                              disabled={!canEditConfig}
                              onClick={() => void deleteGastoMapping(row.id)}
                            >
                              {t('common.delete')}
                            </ZHBtn>
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            )}
          </div>
        </>
      )}
    </div>
  );
}
