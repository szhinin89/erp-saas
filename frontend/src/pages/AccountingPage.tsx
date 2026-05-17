import { useEffect, useMemo, useRef, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { accountingService, type CreateAccountRequest } from '../services/accountingService';
import {
  accountingConfigService,
  type ConfiguracionContableEmpresaDto,
  type CreateGastoCategoriaRequest,
} from '../services/accountingConfigService';
import { useAsync } from '../hooks/useAsync';
import { documentStatusLabel, DocumentStatus } from '../types/accounting';
import { EmptyState, LoadingState, NoAccessPage } from '../components/PageShell';
import { CreateJournalEntryModal } from '../components/CreateJournalEntryModal';
import './AccountingPage.css';
import { useI18n } from '../i18n/i18n';
import { ZHBtn, ZHField } from '../components/zh/ZHForm';
import { ZHPageNotice } from '../components/zh/ZHPageNotice';
import { AccountTreeSelect } from '../components/accounting/AccountTreeSelect';
import { useAuthStore } from '../store/authStore';
import { usePermissionsStore } from '../store/permissionsStore';
import { createAccountFormSchema, type CreateAccountFormValues } from '../schemas/accounting/accountSchema';

type Tab = 'accounts' | 'journal' | 'config';

const EMPTY: CreateAccountFormValues = { code: '', name: '', type: 0, nature: 0, parentId: '' };

const statusBadgeClass: Record<DocumentStatus, string> = {
  [DocumentStatus.Draft]:  'badge badge--gray badge--md',
  [DocumentStatus.Posted]: 'badge badge--green badge--md',
  [DocumentStatus.Voided]: 'badge badge--red badge--md',
};

export function AccountingPage() {
  const { t } = useI18n();
  const tenantId = useAuthStore((s) => s.user?.tenantId ?? '');
  const role     = useAuthStore((s) => s.user?.role ?? '');
  const isAdmin  = role === 'Admin' || role === 'SuperAdmin';
  const hasPerm  = usePermissionsStore((s) => s.has);

  const canViewAccounts  = isAdmin || hasPerm('accounting.accounts.view');
  const canCreateAccount = isAdmin || hasPerm('accounting.accounts.create');
  const canViewJournal   = isAdmin || hasPerm('accounting.journal.view');
  const canCreateJournal = isAdmin || hasPerm('accounting.journal.create');
  const canViewConfig    = isAdmin || hasPerm('accounting.config.view');
  const canEditConfig    = isAdmin || hasPerm('accounting.config.edit');

  const [tab,          setTab]          = useState<Tab>('accounts');
  const [showJournal,  setShowJournal]  = useState(false);

  const accounts      = useAsync(() => accountingService.getAccounts(),     canViewAccounts);
  const journalEntries= useAsync(() => accountingService.getJournalEntries(),canViewJournal);
  const config        = useAsync(() => accountingConfigService.getConfig(),  canViewConfig);
  const gastoMappings = useAsync(() => accountingConfigService.listGastoMappings(), canViewConfig);

  const formRef = useRef<HTMLFormElement>(null);
  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isSubmitting: formLoading },
  } = useForm<CreateAccountFormValues>({
    resolver: zodResolver(createAccountFormSchema),
    defaultValues: EMPTY,
  });
  const [formError,       setFormError]       = useState('');
  const [accountSubTab,   setAccountSubTab]   = useState<'data' | 'list'>('list');
  const [accountListQuery,setAccountListQuery] = useState('');

  const canUseModule   = canViewAccounts || canViewJournal || canViewConfig;
  const permsHydrated  = usePermissionsStore((s) => s.hasHydrated);

  const displayTab = useMemo<Tab>(() => {
    if (!canViewAccounts && canViewJournal) return 'journal';
    if (canViewAccounts && !canViewJournal) return 'accounts';
    if (!canViewAccounts && !canViewJournal && canViewConfig) return 'config';
    return tab;
  }, [canViewAccounts, canViewJournal, canViewConfig, tab]);

  const activeAccountSubTab = canCreateAccount ? accountSubTab : 'list';

  const filteredAccounts = useMemo(() => {
    const data = accounts.data ?? [];
    const q = accountListQuery.trim().toLowerCase();
    if (!q) return data;
    return data.filter((a) => `${a.code} ${a.name} ${a.type} ${a.nature}`.toLowerCase().includes(q));
  }, [accounts.data, accountListQuery]);

  const accountTypes = useMemo(() => [
    { value: 0, label: t('accounting.accounts.type.asset') },
    { value: 1, label: t('accounting.accounts.type.liability') },
    { value: 2, label: t('accounting.accounts.type.equity') },
    { value: 3, label: t('accounting.accounts.type.income') },
    { value: 4, label: t('accounting.accounts.type.expense') },
  ], [t]);

  const accountNatures = useMemo(() => [
    { value: 0, label: t('accounting.accounts.nature.debit') },
    { value: 1, label: t('accounting.accounts.nature.credit') },
  ], [t]);

  const submitAccount = handleSubmit(async (form) => {
    setFormError('');
    try {
      const payload: CreateAccountRequest = {
        code: form.code, name: form.name, type: form.type, nature: form.nature,
        parentId: form.parentId?.trim() ? form.parentId.trim() : null,
      };
      await accountingService.createAccount(payload);
      reset(EMPTY);
      accounts.refetch();
      setAccountSubTab('list');
    } catch (err: unknown) {
      setFormError(
        (err as { response?: { data?: { error?: string } } })?.response?.data?.error ??
        t('accounting.accounts.modal.create.error')
      );
    }
  });

  // ── Config state ───────────────────────────────────────────────────────────

  const [configError,  setConfigError]  = useState('');
  const [configSaving, setConfigSaving] = useState(false);
  const [gastoError,   setGastoError]   = useState('');
  const [gastoSaving,  setGastoSaving]  = useState(false);

  const [configForm, setConfigForm] = useState<ConfiguracionContableEmpresaDto>({
    cuentaInventarioId: null, cuentaCostoVentaId: null, cuentaProveedoresId: null,
    cuentaVentasId: null, cuentaClientesId: null, cuentaIvaComprasId: null,
    cuentaIvaVentasId: null, cuentaEfectivoId: null, cuentaBancoId: null,
  });

  useEffect(() => {
    if (!config.data) return;
    setConfigForm({
      cuentaInventarioId: config.data.cuentaInventarioId ?? null,
      cuentaCostoVentaId: config.data.cuentaCostoVentaId ?? null,
      cuentaProveedoresId: config.data.cuentaProveedoresId ?? null,
      cuentaVentasId: config.data.cuentaVentasId ?? null,
      cuentaClientesId: config.data.cuentaClientesId ?? null,
      cuentaIvaComprasId: config.data.cuentaIvaComprasId ?? null,
      cuentaIvaVentasId: config.data.cuentaIvaVentasId ?? null,
      cuentaEfectivoId: config.data.cuentaEfectivoId ?? null,
      cuentaBancoId: config.data.cuentaBancoId ?? null,
    });
  }, [config.data]);

  const [newGastoCategoria, setNewGastoCategoria] = useState('');
  const [newGastoCuentaId,  setNewGastoCuentaId]  = useState('');

  const saveConfig = async () => {
    if (!canEditConfig) return;
    setConfigError('');
    setConfigSaving(true);
    try {
      await accountingConfigService.upsertConfig(configForm);
      config.refetch();
    } catch (err: unknown) {
      setConfigError((err as { response?: { data?: { error?: string } } })?.response?.data?.error ?? t('common.error'));
    } finally {
      setConfigSaving(false);
    }
  };

  const createGastoMapping = async () => {
    if (!canEditConfig) return;
    setGastoError('');
    const categoria = newGastoCategoria.trim();
    if (!categoria)      { setGastoError(t('accounting.config.expenses.validation.categoryRequired')); return; }
    if (!newGastoCuentaId) { setGastoError(t('accounting.config.expenses.validation.accountRequired')); return; }
    setGastoSaving(true);
    try {
      const payload: CreateGastoCategoriaRequest = { categoria, cuentaGastoId: newGastoCuentaId };
      await accountingConfigService.createGastoMapping(payload);
      setNewGastoCategoria(''); setNewGastoCuentaId('');
      gastoMappings.refetch();
    } catch (err: unknown) {
      setGastoError((err as { response?: { data?: { error?: string } } })?.response?.data?.error ?? t('common.error'));
    } finally {
      setGastoSaving(false);
    }
  };

  const deleteGastoMapping = async (id: string) => {
    if (!canEditConfig) return;
    setGastoError('');
    try {
      await accountingConfigService.deleteGastoMapping(id);
      gastoMappings.refetch();
    } catch (err: unknown) {
      setGastoError((err as { response?: { data?: { error?: string } } })?.response?.data?.error ?? t('common.error'));
    }
  };

  if (!permsHydrated && !isAdmin) return (
    <div className="pg-page"><div style={{ padding: '40px' }}><LoadingState /></div></div>
  );

  if (!canUseModule) return <NoAccessPage title={t('app.nav.accounting')} />;

  const tabTitle = displayTab === 'accounts'
    ? t('accounting.tabs.accounts')
    : displayTab === 'journal'
      ? t('accounting.tabs.journal')
      : t('accounting.tabs.config');

  return (
    <div className="pg-page">

      {/* ── Header ── */}
      <div className="pg-header-row">
        <div className="pg-header-left">
          <nav className="pg-breadcrumb" aria-label="Navegación">
            <span className="pg-breadcrumb-item">{t('app.nav.group.accounting')}</span>
            <span className="material-symbols-outlined pg-breadcrumb-sep">chevron_right</span>
            <span className="pg-breadcrumb-item">{tabTitle}</span>
          </nav>
          <h1 className="pg-title">{tabTitle}</h1>
        </div>
        <div className="pg-header-right">
          {displayTab === 'accounts' && activeAccountSubTab === 'data' && canCreateAccount && (
            <ZHBtn variant="primary" size="md" type="button" disabled={formLoading}
              onClick={() => formRef.current?.requestSubmit()}>
              {formLoading ? t('common.saving') : t('accounting.accounts.modal.create.submit')}
            </ZHBtn>
          )}
          {displayTab === 'journal' && canCreateJournal && (
            <ZHBtn variant="primary" size="md" type="button" onClick={() => setShowJournal(true)}>
              {t('accounting.journal.primaryCreate')}
            </ZHBtn>
          )}
          {displayTab === 'config' && canEditConfig && (
            <ZHBtn variant="primary" size="md" type="button" disabled={configSaving} onClick={() => void saveConfig()}>
              {configSaving ? t('common.saving') : t('common.save')}
            </ZHBtn>
          )}
        </div>
      </div>

      {showJournal && canCreateJournal && (
        <CreateJournalEntryModal
          accounts={accounts.data ?? []}
          onClose={() => setShowJournal(false)}
          onCreated={journalEntries.refetch}
        />
      )}

      {/* ── Module tabs ── */}
      {((canViewAccounts && canViewJournal) || canViewConfig) && (
        <div className="zh-form-tabs" role="tablist" style={{ marginBottom: 'var(--space-4)' }}>
          {canViewAccounts && (
            <button type="button" role="tab" aria-selected={displayTab === 'accounts'}
              className={displayTab === 'accounts' ? 'is-active' : ''}
              onClick={() => setTab('accounts')}>
              {t('accounting.tabs.accounts')}
            </button>
          )}
          {canViewJournal && (
            <button type="button" role="tab" aria-selected={displayTab === 'journal'}
              className={displayTab === 'journal' ? 'is-active' : ''}
              onClick={() => setTab('journal')}>
              {t('accounting.tabs.journal')}
            </button>
          )}
          {canViewConfig && (
            <button type="button" role="tab" aria-selected={displayTab === 'config'}
              className={displayTab === 'config' ? 'is-active' : ''}
              onClick={() => setTab('config')}>
              {t('accounting.tabs.config')}
            </button>
          )}
        </div>
      )}

      {/* ── Accounts tab ── */}
      {displayTab === 'accounts' && canViewAccounts && (
        <div className="pg-section">
          {formError && <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={formError} />}

          <div className="zh-form-tabs" role="tablist">
            {canCreateAccount && (
              <button type="button" className={activeAccountSubTab === 'data' ? 'is-active' : ''}
                onClick={() => setAccountSubTab('data')}>
                {t('common.formTab.data')}
              </button>
            )}
            <button type="button" className={activeAccountSubTab === 'list' ? 'is-active' : ''}
              onClick={() => setAccountSubTab('list')}>
              {t('accounting.accounts.tabList')}
            </button>
          </div>

          {activeAccountSubTab === 'data' && canCreateAccount && (
            <form ref={formRef} onSubmit={submitAccount} noValidate>
              <input type="hidden" name="tenantId" value={tenantId} />
              <div className="pg-section-body">
                <div className="pg-form-grid pg-form-grid--2">
                  <ZHField label={t('accounting.accounts.form.code')} required error={errors.code?.message}>
                    <input className="zh-input"
                      placeholder={t('accounting.accounts.form.code.placeholder')}
                      disabled={formLoading} {...register('code')} />
                  </ZHField>

                  <ZHField label={t('accounting.accounts.form.name')} required error={errors.name?.message}>
                    <input className="zh-input"
                      placeholder={t('accounting.accounts.form.name.placeholder')}
                      disabled={formLoading} {...register('name')} />
                  </ZHField>

                  <ZHField label={t('accounting.accounts.form.type')} required error={errors.type?.message}>
                    <select className="zh-input" disabled={formLoading} {...register('type', { valueAsNumber: true })}>
                      {accountTypes.map((x) => <option key={x.value} value={x.value}>{x.label}</option>)}
                    </select>
                  </ZHField>

                  <ZHField label={t('accounting.accounts.form.nature')} required error={errors.nature?.message}>
                    <select className="zh-input" disabled={formLoading} {...register('nature', { valueAsNumber: true })}>
                      {accountNatures.map((x) => <option key={x.value} value={x.value}>{x.label}</option>)}
                    </select>
                  </ZHField>

                  <ZHField label={t('accounting.accounts.form.parentId')} error={errors.parentId?.message}
                    style={{ gridColumn: '1 / -1' }}>
                    <input className="zh-input"
                      placeholder={t('common.guid.placeholder')}
                      disabled={formLoading} {...register('parentId')} />
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
                    <input type="text"
                      placeholder={t('common.zhList.searchPlaceholder')}
                      value={accountListQuery}
                      onChange={(e) => setAccountListQuery(e.target.value)}
                      disabled={accounts.loading} />
                  </div>
                  {canCreateAccount && (
                    <ZHBtn variant="ghost" size="sm" type="button"
                      onClick={() => setAccountSubTab('data')}>
                      {t('accounting.accounts.listNewAction')}
                    </ZHBtn>
                  )}
                </div>
                <div className="pg-table-controls-right">
                  <span>{filteredAccounts.length} de {accounts.data?.length ?? 0}</span>
                </div>
              </div>

              {accounts.loading && <div style={{ padding: '40px' }}><LoadingState /></div>}
              {accounts.error  && <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={accounts.error} />}
              {!accounts.loading && !accounts.error && (accounts.data?.length ?? 0) === 0 && (
                <div style={{ padding: '40px' }}><EmptyState message={t('accounting.accounts.empty')} /></div>
              )}
              {!accounts.loading && !accounts.error && filteredAccounts.length === 0 && (accounts.data?.length ?? 0) > 0 && (
                <div style={{ padding: '40px' }}><EmptyState message={t('common.listTab.noMatch')} /></div>
              )}
              {!accounts.loading && !accounts.error && filteredAccounts.length > 0 && (
                <div style={{ overflowX: 'auto' }}>
                  <table className="table">
                    <thead>
                      <tr>
                        <th>{t('accounting.accounts.table.code')}</th>
                        <th>{t('accounting.accounts.table.name')}</th>
                        <th>{t('accounting.accounts.table.type')}</th>
                        <th>{t('accounting.accounts.table.nature')}</th>
                        <th>{t('accounting.accounts.table.status')}</th>
                      </tr>
                    </thead>
                    <tbody>
                      {filteredAccounts.map((a) => (
                        <tr key={a.id}>
                          <td><span className="mono">{a.code}</span></td>
                          <td>{a.name}</td>
                          <td>{a.type}</td>
                          <td>{a.nature}</td>
                          <td>
                            <span className={a.isActive ? 'zh-status zh-status--active' : 'zh-status zh-status--inactive'}>
                              {a.isActive ? t('common.active') : t('common.inactive')}
                            </span>
                          </td>
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

      {/* ── Config tab ── */}
      {displayTab === 'config' && canViewConfig && (
        <div className="pg-section">
          {configError && <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={configError} />}
          {config.loading && <div style={{ padding: '40px' }}><LoadingState /></div>}
          {config.error   && <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={config.error} />}

          {!config.loading && !config.error && (
            <>
              <div className="pg-section-header">
                <div className="pg-section-header-left">
                  <span className="material-symbols-outlined pg-section-icon">account_balance</span>
                  <span className="pg-section-label">{t('accounting.config.title')}</span>
                </div>
              </div>
              <div className="pg-section-body">
                <div className="pg-form-grid pg-form-grid--2">
                  {([
                    ['cuentaInventarioId', t('accounting.config.fields.cuentaInventario')],
                    ['cuentaProveedoresId', t('accounting.config.fields.cuentaProveedores')],
                    ['cuentaVentasId', t('accounting.config.fields.cuentaVentas')],
                    ['cuentaClientesId', t('accounting.config.fields.cuentaClientes')],
                    ['cuentaIvaComprasId', t('accounting.config.fields.cuentaIvaCompras')],
                    ['cuentaIvaVentasId', t('accounting.config.fields.cuentaIvaVentas')],
                    ['cuentaEfectivoId', t('accounting.config.fields.cuentaEfectivo')],
                    ['cuentaBancoId', t('accounting.config.fields.cuentaBanco')],
                  ] as [keyof ConfiguracionContableEmpresaDto, string][]).map(([key, label]) => (
                    <ZHField key={key} label={label}>
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

              {/* Expense mappings */}
              <div className="pg-section-header" style={{ marginTop: 'var(--space-6)' }}>
                <div className="pg-section-header-left">
                  <span className="material-symbols-outlined pg-section-icon">category</span>
                  <span className="pg-section-label">{t('accounting.config.expenses.title')}</span>
                </div>
              </div>
              <div className="pg-section-body">
                {gastoError && <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={gastoError} />}
                <div className="pg-form-grid pg-form-grid--2" style={{ marginBottom: 'var(--space-3)' }}>
                  <ZHField label={t('accounting.config.expenses.fields.category')}>
                    <input className="zh-input" value={newGastoCategoria}
                      disabled={!canEditConfig || gastoSaving}
                      onChange={(e) => setNewGastoCategoria(e.target.value)}
                      placeholder={t('accounting.config.expenses.fields.categoryPlaceholder')} />
                  </ZHField>
                  <ZHField label={t('accounting.config.expenses.fields.account')}>
                    <AccountTreeSelect
                      value={newGastoCuentaId || null}
                      onChange={(next) => setNewGastoCuentaId(next ?? '')}
                      accounts={accounts.data ?? []}
                      disabled={!canEditConfig || gastoSaving}
                      placeholder={t('common.select')}
                    />
                  </ZHField>
                </div>
                <ZHBtn variant="secondary" size="md" type="button"
                  disabled={!canEditConfig || gastoSaving}
                  onClick={() => void createGastoMapping()}>
                  {gastoSaving ? t('common.saving') : t('accounting.config.expenses.actions.add')}
                </ZHBtn>

                {gastoMappings.loading && <div style={{ padding: '24px' }}><LoadingState /></div>}
                {gastoMappings.error   && <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={gastoMappings.error} />}
                {!gastoMappings.loading && !gastoMappings.error && (gastoMappings.data ?? []).length === 0 && (
                  <div style={{ padding: '24px' }}><EmptyState message={t('accounting.config.expenses.empty')} /></div>
                )}
                {!gastoMappings.loading && !gastoMappings.error && (gastoMappings.data ?? []).length > 0 && (
                  <div style={{ overflowX: 'auto', marginTop: 'var(--space-4)' }}>
                    <table className="table">
                      <thead>
                        <tr>
                          <th>{t('accounting.config.expenses.table.category')}</th>
                          <th>{t('accounting.config.expenses.table.account')}</th>
                          <th style={{ textAlign: 'right' }}>{t('common.actions')}</th>
                        </tr>
                      </thead>
                      <tbody>
                        {(gastoMappings.data ?? []).map((row) => {
                          const acc = (accounts.data ?? []).find((a) => a.id === row.cuentaGastoId);
                          return (
                            <tr key={row.id}>
                              <td>{row.categoria}</td>
                              <td>{acc ? `${acc.code} — ${acc.name}` : row.cuentaGastoId}</td>
                              <td style={{ textAlign: 'right' }}>
                                <ZHBtn variant="destructive" size="sm" type="button"
                                  disabled={!canEditConfig}
                                  onClick={() => void deleteGastoMapping(row.id)}>
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
      )}

      {/* ── Journal tab ── */}
      {displayTab === 'journal' && canViewJournal && (
        <div className="pg-section">
          {journalEntries.loading && <div style={{ padding: '40px' }}><LoadingState /></div>}
          {journalEntries.error   && <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={journalEntries.error} />}
          {!journalEntries.loading && !journalEntries.error && (journalEntries.data?.length ?? 0) === 0 && (
            <div style={{ padding: '40px' }}><EmptyState message={t('accounting.journal.empty')} /></div>
          )}
          {!journalEntries.loading && !journalEntries.error && (journalEntries.data?.length ?? 0) > 0 && (
            <div style={{ overflowX: 'auto' }}>
              <table className="table">
                <thead>
                  <tr>
                    <th>{t('accounting.journal.table.reference')}</th>
                    <th>{t('accounting.journal.table.date')}</th>
                    <th>{t('accounting.journal.table.description')}</th>
                    <th style={{ textAlign: 'center' }}>{t('accounting.journal.table.lines')}</th>
                    <th>{t('accounting.journal.table.status')}</th>
                  </tr>
                </thead>
                <tbody>
                  {(journalEntries.data ?? []).map((e) => (
                    <tr key={e.id}>
                      <td><span className="mono">{e.reference}</span></td>
                      <td>{new Date(e.date).toLocaleDateString('es-EC')}</td>
                      <td className="subtle">{e.description}</td>
                      <td style={{ textAlign: 'center' }}>{e.lines.length}</td>
                      <td><span className={statusBadgeClass[e.status]}>{documentStatusLabel[e.status]}</span></td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      )}
    </div>
  );
}
