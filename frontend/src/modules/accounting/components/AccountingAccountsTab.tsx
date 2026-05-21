import { accountingService } from '../api/accountingService';
import { EmptyState, LoadingState } from '../../../components/PageShell';
import { ZHBtn, ZHField } from '../../../components/zh/ZHForm';
import { ZHPageNotice } from '../../../components/zh/ZHPageNotice';
import type { AccountingPageContext } from '../hooks/useAccountingPage';

type Props = Pick<
  AccountingPageContext,
  | 't'
  | 'subscriberId'
  | 'canCreateAccount'
  | 'accounts'
  | 'formRef'
  | 'register'
  | 'errors'
  | 'formLoading'
  | 'formError'
  | 'accountSubTab'
  | 'setAccountSubTab'
  | 'activeAccountSubTab'
  | 'accountListQuery'
  | 'setAccountListQuery'
  | 'filteredAccounts'
  | 'accountTypes'
  | 'accountNatures'
  | 'submitAccount'
>;

export function AccountingAccountsTab({
  t,
  subscriberId,
  canCreateAccount,
  accounts,
  formRef,
  register,
  errors,
  formLoading,
  formError,
  setAccountSubTab,
  activeAccountSubTab,
  accountListQuery,
  setAccountListQuery,
  filteredAccounts,
  accountTypes,
  accountNatures,
  submitAccount,
}: Props) {
  return (
    <div className="pg-section">
      {formError && <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={formError} />}

      <div className="zh-form-tabs" role="tablist">
        {canCreateAccount && (
          <button
            type="button"
            className={activeAccountSubTab === 'data' ? 'is-active' : ''}
            onClick={() => setAccountSubTab('data')}
          >
            {t('common.formTab.data')}
          </button>
        )}
        <button
          type="button"
          className={activeAccountSubTab === 'list' ? 'is-active' : ''}
          onClick={() => setAccountSubTab('list')}
        >
          {t('finance.accounts.tabList')}
        </button>
      </div>

      {activeAccountSubTab === 'data' && canCreateAccount && (
        <form ref={formRef} onSubmit={submitAccount} noValidate>
          <input type="hidden" name="subscriberId" value={subscriberId} />
          <div className="pg-section-body">
            <div className="pg-form-grid pg-form-grid--2">
              <ZHField label={t('finance.accounts.form.code')} required error={errors.code?.message}>
                <input
                  className="zh-input"
                  placeholder={t('finance.accounts.form.code.placeholder')}
                  disabled={formLoading}
                  {...register('code')}
                />
              </ZHField>

              <ZHField label={t('finance.accounts.form.name')} required error={errors.name?.message}>
                <input
                  className="zh-input"
                  placeholder={t('finance.accounts.form.name.placeholder')}
                  disabled={formLoading}
                  {...register('name')}
                />
              </ZHField>

              <ZHField label={t('finance.accounts.form.type')} required error={errors.type?.message}>
                <select className="zh-input" disabled={formLoading} {...register('type', { valueAsNumber: true })}>
                  {accountTypes.map((x) => (
                    <option key={x.value} value={x.value}>
                      {x.label}
                    </option>
                  ))}
                </select>
              </ZHField>

              <ZHField label={t('finance.accounts.form.nature')} required error={errors.nature?.message}>
                <select className="zh-input" disabled={formLoading} {...register('nature', { valueAsNumber: true })}>
                  {accountNatures.map((x) => (
                    <option key={x.value} value={x.value}>
                      {x.label}
                    </option>
                  ))}
                </select>
              </ZHField>

              <ZHField
                label={t('finance.accounts.form.parentId')}
                error={errors.parentId?.message}
                style={{ gridColumn: '1 / -1' }}
              >
                <input
                  className="zh-input"
                  placeholder={t('common.guid.placeholder')}
                  disabled={formLoading}
                  {...register('parentId')}
                />
              </ZHField>
            </div>
          </div>
        </form>
      )}

      {activeAccountSubTab === 'list' && (
        <>
          <div className="pg-table-controls">
            <div className="pg-table-controls-left">
              <div className="pg-search">
                <span className="material-symbols-outlined">search</span>
                <input
                  type="text"
                  placeholder={t('common.zhList.searchPlaceholder')}
                  value={accountListQuery}
                  onChange={(e) => setAccountListQuery(e.target.value)}
                  disabled={accounts.loading}
                />
              </div>
              {canCreateAccount && (
                <ZHBtn variant="ghost" size="sm" type="button" onClick={() => setAccountSubTab('data')}>
                  {t('finance.accounts.listNewAction')}
                </ZHBtn>
              )}
            </div>
            <div className="pg-table-controls-right">
              <span>
                {filteredAccounts.length} de {accounts.data?.length ?? 0}
              </span>
            </div>
          </div>

          {accounts.loading && (
            <div style={{ padding: '40px' }}>
              <LoadingState />
            </div>
          )}
          {accounts.error && (
            <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={accounts.error} />
          )}
          {!accounts.loading && !accounts.error && (accounts.data?.length ?? 0) === 0 && (
            <div style={{ padding: '40px' }}>
              <EmptyState message={t('finance.accounts.empty')} />
            </div>
          )}
          {!accounts.loading && !accounts.error && filteredAccounts.length === 0 && (accounts.data?.length ?? 0) > 0 && (
            <div style={{ padding: '40px' }}>
              <EmptyState message={t('common.listTab.noMatch')} />
            </div>
          )}
          {!accounts.loading && !accounts.error && filteredAccounts.length > 0 && (
            <div style={{ overflowX: 'auto' }}>
              <table className="table">
                <thead>
                  <tr>
                    <th>{t('finance.accounts.table.code')}</th>
                    <th>{t('finance.accounts.table.name')}</th>
                    <th>{t('finance.accounts.table.type')}</th>
                    <th>{t('finance.accounts.table.nature')}</th>
                    <th>{t('finance.accounts.table.status')}</th>
                    {canCreateAccount && <th></th>}
                  </tr>
                </thead>
                <tbody>
                  {filteredAccounts.map((a) => (
                    <tr key={a.id}>
                      <td>
                        <span className="mono">{a.code}</span>
                      </td>
                      <td>{a.name}</td>
                      <td>{a.type}</td>
                      <td>{a.nature}</td>
                      <td>
                        <span className={a.isActive ? 'zh-status zh-status--active' : 'zh-status zh-status--inactive'}>
                          {a.isActive ? t('common.active') : t('common.inactive')}
                        </span>
                      </td>
                      {canCreateAccount && (
                        <td>
                          {a.isActive ? (
                            <button
                              className="zh-btn zh-btn--ghost zh-btn--sm"
                              style={{ color: 'var(--color-error)' }}
                              onClick={() => accountingService.disableAccount(a.id).then(() => accounts.refetch())}
                              title="Deshabilitar cuenta"
                            >
                              <span className="material-symbols-outlined" style={{ fontSize: 16 }}>
                                block
                              </span>
                            </button>
                          ) : (
                            <button
                              className="zh-btn zh-btn--ghost zh-btn--sm"
                              style={{ color: 'var(--color-success)' }}
                              onClick={() => accountingService.enableAccount(a.id).then(() => accounts.refetch())}
                              title="Habilitar cuenta"
                            >
                              <span className="material-symbols-outlined" style={{ fontSize: 16 }}>
                                check_circle
                              </span>
                            </button>
                          )}
                        </td>
                      )}
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </>
      )}
    </div>
  );
}
