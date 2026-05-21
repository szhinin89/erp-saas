import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import {
  accountingService,
  type CreateAccountRequest,
  type MayorGeneralLineDto,
  type BalanceComprobacionLineDto,
} from '../api/accountingService';
import {
  accountingConfigService,
  type ConfiguracionContableEmpresaDto,
  type CreateGastoCategoriaRequest,
} from '../api/accountingConfigService';
import { useAsync } from '../../../hooks/useAsync';
import { documentStatusLabel, DocumentStatus } from '../../../types/accounting';
import { EmptyState, LoadingState, NoAccessPage } from '../../../components/PageShell';
import { CreateJournalEntryModal } from '../../../components/CreateJournalEntryModal';
import './AccountingPage.css';
import { useI18n } from '../../../i18n/i18n';
import { ZHBtn, ZHField } from '../../../components/zh/ZHForm';
import { ZHPageNotice } from '../../../components/zh/ZHPageNotice';
import { AccountTreeSelect } from '../../../components/accounting/AccountTreeSelect';
import { useAuthStore } from '../../../store/authStore';
import { usePermissionsStore } from '../../../store/permissionsStore';
import { createAccountFormSchema, type CreateAccountFormValues } from '../schemas/accountSchema';

type Tab = 'accounts' | 'journal' | 'mayor' | 'balance' | 'config';

const YEAR_START = `${new Date().getFullYear()}-01-01`;
const TODAY      = new Date().toISOString().split('T')[0]!;

const EMPTY: CreateAccountFormValues = { code: '', name: '', type: 0, nature: 0, parentId: '' };

const statusBadgeClass: Record<DocumentStatus, string> = {
  [DocumentStatus.Draft]:  'badge badge--gray badge--md',
  [DocumentStatus.Posted]: 'badge badge--green badge--md',
  [DocumentStatus.Voided]: 'badge badge--red badge--md',
};

export function AccountingPage() {
  const { t } = useI18n();
  const subscriberId = useAuthStore((s) => s.user?.subscriberId ?? '');
  const role     = useAuthStore((s) => s.user?.role ?? '');
  const isAdmin  = role === 'Admin' || role === 'SuperAdmin';
  const hasPerm  = usePermissionsStore((s) => s.has);

  const canViewAccounts  = isAdmin || hasPerm('finance.accounts.view');
  const canCreateAccount = isAdmin || hasPerm('finance.accounts.create');
  const canViewJournal   = isAdmin || hasPerm('finance.journal.view');
  const canCreateJournal = isAdmin || hasPerm('finance.journal.create');
  const canViewConfig    = isAdmin || hasPerm('finance.config.view');
  const canEditConfig    = isAdmin || hasPerm('finance.config.edit');

  const [tab,          setTab]          = useState<Tab>('accounts');
  const [showJournal,  setShowJournal]  = useState(false);

  // Libro diario — filtros
  const [jDesde,       setJDesde]       = useState(YEAR_START);
  const [jHasta,       setJHasta]       = useState(TODAY);
  const [expandedEntry, setExpandedEntry] = useState<string | null>(null);

  // Mayor General
  const [mayorAccountId, setMayorAccountId] = useState('');
  const [mayorDesde,     setMayorDesde]     = useState(YEAR_START);
  const [mayorHasta,     setMayorHasta]     = useState(TODAY);
  const [mayorData,      setMayorData]      = useState<MayorGeneralLineDto[]>([]);
  const [mayorLoading,   setMayorLoading]   = useState(false);
  const [mayorError,     setMayorError]     = useState<string | null>(null);

  // Balance de Comprobación
  const [balDesde,    setBalDesde]    = useState(YEAR_START);
  const [balHasta,    setBalHasta]    = useState(TODAY);
  const [balData,     setBalData]     = useState<BalanceComprobacionLineDto[]>([]);
  const [balLoading,  setBalLoading]  = useState(false);
  const [balError,    setBalError]    = useState<string | null>(null);

  const accounts      = useAsync(() => accountingService.getAccounts(),     canViewAccounts);
  const journalEntries= useAsync(() => accountingService.getJournalEntries(), canViewJournal);
  const config        = useAsync(() => accountingConfigService.getConfig(),  canViewConfig);
  const gastoMappings = useAsync(() => accountingConfigService.listGastoMappings(), canViewConfig);

  const fetchMayor = useCallback(async () => {
    if (!mayorAccountId) return;
    setMayorLoading(true);
    setMayorError(null);
    try {
      const data = await accountingService.getMayorGeneral(mayorAccountId, mayorDesde, mayorHasta);
      setMayorData(data);
    } catch (e) {
      setMayorError(e instanceof Error ? e.message : 'Error al cargar el mayor');
    } finally {
      setMayorLoading(false);
    }
  }, [mayorAccountId, mayorDesde, mayorHasta]);

  const fetchBalance = useCallback(async () => {
    setBalLoading(true);
    setBalError(null);
    try {
      const data = await accountingService.getBalanceComprobacion(balDesde, balHasta);
      setBalData(data);
    } catch (e) {
      setBalError(e instanceof Error ? e.message : 'Error al cargar el balance');
    } finally {
      setBalLoading(false);
    }
  }, [balDesde, balHasta]);

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

  // Filtro de Libro Diario por fecha
  const filteredJournal = useMemo(() => {
    const desde = new Date(jDesde);
    const hasta  = new Date(jHasta);
    return (journalEntries.data ?? []).filter((e) => {
      const d = new Date(e.date);
      return d >= desde && d <= hasta;
    });
  }, [journalEntries.data, jDesde, jHasta]);

  const activeAccountSubTab = canCreateAccount ? accountSubTab : 'list';

  const filteredAccounts = useMemo(() => {
    const data = accounts.data ?? [];
    const q = accountListQuery.trim().toLowerCase();
    if (!q) return data;
    return data.filter((a) => `${a.code} ${a.name} ${a.type} ${a.nature}`.toLowerCase().includes(q));
  }, [accounts.data, accountListQuery]);

  const accountTypes = useMemo(() => [
    { value: 0, label: t('finance.accounts.type.asset') },
    { value: 1, label: t('finance.accounts.type.liability') },
    { value: 2, label: t('finance.accounts.type.equity') },
    { value: 3, label: t('finance.accounts.type.income') },
    { value: 4, label: t('finance.accounts.type.expense') },
  ], [t]);

  const accountNatures = useMemo(() => [
    { value: 0, label: t('finance.accounts.nature.debit') },
    { value: 1, label: t('finance.accounts.nature.credit') },
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
        t('finance.accounts.modal.create.error')
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
    if (!categoria)      { setGastoError(t('finance.config.expenses.validation.categoryRequired')); return; }
    if (!newGastoCuentaId) { setGastoError(t('finance.config.expenses.validation.accountRequired')); return; }
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
              {formLoading ? t('common.saving') : t('finance.accounts.modal.create.submit')}
            </ZHBtn>
          )}
          {displayTab === 'journal' && canCreateJournal && (
            <ZHBtn variant="primary" size="md" type="button" onClick={() => setShowJournal(true)}>
              {t('finance.journal.primaryCreate')}
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
        {canViewJournal && (
          <button type="button" role="tab" aria-selected={displayTab === 'mayor'}
            className={displayTab === 'mayor' ? 'is-active' : ''}
            onClick={() => setTab('mayor')}>
            Mayor General
          </button>
        )}
        {canViewJournal && (
          <button type="button" role="tab" aria-selected={displayTab === 'balance'}
            className={displayTab === 'balance' ? 'is-active' : ''}
            onClick={() => { setTab('balance'); void fetchBalance(); }}>
            Balance de Comprobación
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
              {t('finance.accounts.tabList')}
            </button>
          </div>

          {activeAccountSubTab === 'data' && canCreateAccount && (
            <form ref={formRef} onSubmit={submitAccount} noValidate>
              <input type="hidden" name="subscriberId" value={subscriberId} />
              <div className="pg-section-body">
                <div className="pg-form-grid pg-form-grid--2">
                  <ZHField label={t('finance.accounts.form.code')} required error={errors.code?.message}>
                    <input className="zh-input"
                      placeholder={t('finance.accounts.form.code.placeholder')}
                      disabled={formLoading} {...register('code')} />
                  </ZHField>

                  <ZHField label={t('finance.accounts.form.name')} required error={errors.name?.message}>
                    <input className="zh-input"
                      placeholder={t('finance.accounts.form.name.placeholder')}
                      disabled={formLoading} {...register('name')} />
                  </ZHField>

                  <ZHField label={t('finance.accounts.form.type')} required error={errors.type?.message}>
                    <select className="zh-input" disabled={formLoading} {...register('type', { valueAsNumber: true })}>
                      {accountTypes.map((x) => <option key={x.value} value={x.value}>{x.label}</option>)}
                    </select>
                  </ZHField>

                  <ZHField label={t('finance.accounts.form.nature')} required error={errors.nature?.message}>
                    <select className="zh-input" disabled={formLoading} {...register('nature', { valueAsNumber: true })}>
                      {accountNatures.map((x) => <option key={x.value} value={x.value}>{x.label}</option>)}
                    </select>
                  </ZHField>

                  <ZHField label={t('finance.accounts.form.parentId')} error={errors.parentId?.message}
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
                      {t('finance.accounts.listNewAction')}
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
                <div style={{ padding: '40px' }}><EmptyState message={t('finance.accounts.empty')} /></div>
              )}
              {!accounts.loading && !accounts.error && filteredAccounts.length === 0 && (accounts.data?.length ?? 0) > 0 && (
                <div style={{ padding: '40px' }}><EmptyState message={t('common.listTab.noMatch')} /></div>
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
                          <td><span className="mono">{a.code}</span></td>
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
                                  <span className="material-symbols-outlined" style={{ fontSize: 16 }}>block</span>
                                </button>
                              ) : (
                                <button
                                  className="zh-btn zh-btn--ghost zh-btn--sm"
                                  style={{ color: 'var(--color-success)' }}
                                  onClick={() => accountingService.enableAccount(a.id).then(() => accounts.refetch())}
                                  title="Habilitar cuenta"
                                >
                                  <span className="material-symbols-outlined" style={{ fontSize: 16 }}>check_circle</span>
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
                  <span className="pg-section-label">{t('finance.config.title')}</span>
                </div>
              </div>
              <div className="pg-section-body">
                <div className="pg-form-grid pg-form-grid--2">
                  {([
                    ['cuentaInventarioId', t('finance.config.fields.cuentaInventario')],
                    ['cuentaProveedoresId', t('finance.config.fields.cuentaProveedores')],
                    ['cuentaVentasId', t('finance.config.fields.cuentaVentas')],
                    ['cuentaClientesId', t('finance.config.fields.cuentaClientes')],
                    ['cuentaIvaComprasId', t('finance.config.fields.cuentaIvaCompras')],
                    ['cuentaIvaVentasId', t('finance.config.fields.cuentaIvaVentas')],
                    ['cuentaEfectivoId', t('finance.config.fields.cuentaEfectivo')],
                    ['cuentaBancoId', t('finance.config.fields.cuentaBanco')],
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
                  <span className="pg-section-label">{t('finance.config.expenses.title')}</span>
                </div>
              </div>
              <div className="pg-section-body">
                {gastoError && <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={gastoError} />}
                <div className="pg-form-grid pg-form-grid--2" style={{ marginBottom: 'var(--space-3)' }}>
                  <ZHField label={t('finance.config.expenses.fields.category')}>
                    <input className="zh-input" value={newGastoCategoria}
                      disabled={!canEditConfig || gastoSaving}
                      onChange={(e) => setNewGastoCategoria(e.target.value)}
                      placeholder={t('finance.config.expenses.fields.categoryPlaceholder')} />
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
                <ZHBtn variant="secondary" size="md" type="button"
                  disabled={!canEditConfig || gastoSaving}
                  onClick={() => void createGastoMapping()}>
                  {gastoSaving ? t('common.saving') : t('finance.config.expenses.actions.add')}
                </ZHBtn>

                {gastoMappings.loading && <div style={{ padding: '24px' }}><LoadingState /></div>}
                {gastoMappings.error   && <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={gastoMappings.error} />}
                {!gastoMappings.loading && !gastoMappings.error && (gastoMappings.data ?? []).length === 0 && (
                  <div style={{ padding: '24px' }}><EmptyState message={t('finance.config.expenses.empty')} /></div>
                )}
                {!gastoMappings.loading && !gastoMappings.error && (gastoMappings.data ?? []).length > 0 && (
                  <div style={{ overflowX: 'auto', marginTop: 'var(--space-4)' }}>
                    <table className="table">
                      <thead>
                        <tr>
                          <th>{t('finance.config.expenses.table.category')}</th>
                          <th>{t('finance.config.expenses.table.account')}</th>
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
          {/* Filtros de fecha */}
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
          <div className="pg-section-body" style={{ display: 'flex', gap: 'var(--space-4)', flexWrap: 'wrap', alignItems: 'flex-end', marginBottom: 'var(--space-3)' }}>
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

          {journalEntries.loading && <div style={{ padding: '40px' }}><LoadingState /></div>}
          {journalEntries.error   && <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={journalEntries.error} />}
          {!journalEntries.loading && !journalEntries.error && filteredJournal.length === 0 && (
            <div style={{ padding: '40px' }}><EmptyState message={t('finance.journal.empty')} /></div>
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
                    const totalDebit  = e.lines.reduce((s, l) => s + l.debitAmount, 0);
                    const totalCredit = e.lines.reduce((s, l) => s + l.creditAmount, 0);
                    const accountMap  = accounts.data
                      ? Object.fromEntries(accounts.data.map((a) => [a.id, `${a.code} ${a.name}`]))
                      : {};
                    return [
                      <tr key={e.id} style={{ cursor: 'pointer' }} onClick={() => setExpandedEntry(isExpanded ? null : e.id)}>
                        <td style={{ textAlign: 'center', color: 'var(--color-text-secondary)' }}>
                          <span className="material-symbols-outlined" style={{ fontSize: 18 }}>
                            {isExpanded ? 'expand_less' : 'expand_more'}
                          </span>
                        </td>
                        <td><span className="mono">{e.reference}</span></td>
                        <td>{new Date(e.date).toLocaleDateString('es-EC')}</td>
                        <td className="subtle">{e.description}</td>
                        <td style={{ textAlign: 'right', fontWeight: 600 }}>${totalDebit.toFixed(2)}</td>
                        <td style={{ textAlign: 'right', fontWeight: 600 }}>${totalCredit.toFixed(2)}</td>
                        <td><span className={statusBadgeClass[e.status]}>{documentStatusLabel[e.status]}</span></td>
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
                                    <td style={{ padding: '4px 12px' }}>{accountMap[l.accountId] ?? l.accountId.slice(0, 8)}</td>
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
      )}

      {/* ── Mayor General tab ── */}
      {displayTab === 'mayor' && canViewJournal && (
        <div className="pg-section">
          <div className="pg-section-header">
            <div className="pg-section-header-left">
              <span className="material-symbols-outlined pg-section-icon">account_balance_wallet</span>
              <span className="pg-section-label">Mayor General</span>
            </div>
          </div>
          <div className="pg-section-body" style={{ display: 'flex', gap: 'var(--space-4)', flexWrap: 'wrap', alignItems: 'flex-end', marginBottom: 'var(--space-4)' }}>
            <ZHField label="Cuenta" style={{ minWidth: 260 }}>
              <select
                className="zh-input"
                value={mayorAccountId}
                onChange={(e) => setMayorAccountId(e.target.value)}
                disabled={accounts.loading}
              >
                <option value="">-- Seleccionar cuenta --</option>
                {(accounts.data ?? []).filter((a) => a.isActive).map((a) => (
                  <option key={a.id} value={a.id}>{a.code} — {a.name}</option>
                ))}
              </select>
            </ZHField>
            <ZHField label="Desde">
              <input className="zh-input" type="date" value={mayorDesde} onChange={(e) => setMayorDesde(e.target.value)} />
            </ZHField>
            <ZHField label="Hasta">
              <input className="zh-input" type="date" value={mayorHasta} onChange={(e) => setMayorHasta(e.target.value)} />
            </ZHField>
            <ZHBtn variant="primary" size="md" onClick={() => void fetchMayor()} disabled={!mayorAccountId || mayorLoading}>
              {mayorLoading ? 'Cargando...' : 'Consultar'}
            </ZHBtn>
          </div>

          {mayorError && <ZHPageNotice variant="error" message={mayorError} />}
          {mayorLoading && <div style={{ padding: 40 }}><LoadingState /></div>}
          {!mayorLoading && !mayorError && mayorData.length === 0 && mayorAccountId && (
            <div style={{ padding: 40 }}><EmptyState message="Sin movimientos en el período seleccionado." /></div>
          )}
          {!mayorLoading && mayorData.length > 0 && (
            <>
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
                        <td style={{ color: 'var(--color-text-secondary)' }}>{new Date(l.date).toLocaleDateString('es-EC')}</td>
                        <td><span className="mono">{l.reference}</span></td>
                        <td>{l.description}</td>
                        <td style={{ textAlign: 'right' }}>{l.debit > 0 ? `$${l.debit.toFixed(2)}` : ''}</td>
                        <td style={{ textAlign: 'right' }}>{l.credit > 0 ? `$${l.credit.toFixed(2)}` : ''}</td>
                        <td style={{ textAlign: 'right', fontWeight: 700, color: l.balance >= 0 ? 'var(--color-primary)' : 'var(--color-error)' }}>
                          ${l.balance.toFixed(2)}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                  <tfoot>
                    <tr style={{ background: 'var(--color-surface-raised)', fontWeight: 700 }}>
                      <td colSpan={3}>TOTALES</td>
                      <td style={{ textAlign: 'right' }}>${mayorData.reduce((s, l) => s + l.debit, 0).toFixed(2)}</td>
                      <td style={{ textAlign: 'right' }}>${mayorData.reduce((s, l) => s + l.credit, 0).toFixed(2)}</td>
                      <td style={{ textAlign: 'right' }}>${mayorData[mayorData.length - 1]?.balance.toFixed(2) ?? '0.00'}</td>
                    </tr>
                  </tfoot>
                </table>
              </div>
            </>
          )}
        </div>
      )}

      {/* ── Balance de Comprobación tab ── */}
      {displayTab === 'balance' && canViewJournal && (
        <div className="pg-section">
          <div className="pg-section-header">
            <div className="pg-section-header-left">
              <span className="material-symbols-outlined pg-section-icon">balance</span>
              <span className="pg-section-label">Balance de Comprobación</span>
            </div>
          </div>
          <div className="pg-section-body" style={{ display: 'flex', gap: 'var(--space-4)', flexWrap: 'wrap', alignItems: 'flex-end', marginBottom: 'var(--space-4)' }}>
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
          {balLoading && <div style={{ padding: 40 }}><LoadingState /></div>}
          {!balLoading && !balError && balData.length === 0 && (
            <div style={{ padding: 40 }}><EmptyState message="Haz clic en 'Generar' para calcular el balance." /></div>
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
                      <td><span className="mono">{l.accountCode}</span></td>
                      <td>{l.accountName}</td>
                      <td style={{ color: 'var(--color-text-secondary)' }}>{l.accountType}</td>
                      <td style={{ textAlign: 'right' }}>${l.totalDebit.toFixed(2)}</td>
                      <td style={{ textAlign: 'right' }}>${l.totalCredit.toFixed(2)}</td>
                      <td style={{ textAlign: 'right', fontWeight: 700, color: l.netBalance >= 0 ? 'var(--color-success)' : 'var(--color-error)' }}>
                        ${l.netBalance.toFixed(2)}
                      </td>
                    </tr>
                  ))}
                </tbody>
                <tfoot>
                  <tr style={{ background: 'var(--color-surface-raised)', fontWeight: 700 }}>
                    <td colSpan={3}>TOTALES</td>
                    <td style={{ textAlign: 'right' }}>${balData.reduce((s, l) => s + l.totalDebit, 0).toFixed(2)}</td>
                    <td style={{ textAlign: 'right' }}>${balData.reduce((s, l) => s + l.totalCredit, 0).toFixed(2)}</td>
                    <td style={{ textAlign: 'right' }}>${balData.reduce((s, l) => s + l.netBalance, 0).toFixed(2)}</td>
                  </tr>
                </tfoot>
              </table>
            </div>
          )}
        </div>
      )}
    </div>
  );
}
