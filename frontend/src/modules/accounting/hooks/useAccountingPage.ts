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
import { formatApiError } from '../../lib/formatApiError';
import { useI18n } from '../../../i18n/i18n';
import { useAuthStore } from '../../../store/authStore';
import { usePermissionsUi } from '../../../access/usePermissionsUi';
import { createAccountFormSchema, type CreateAccountFormValues } from '../schemas/accountSchema';

export type AccountingTab = 'accounts' | 'journal' | 'mayor' | 'balance' | 'config';

export const YEAR_START = `${new Date().getFullYear()}-01-01`;
export const TODAY = new Date().toISOString().split('T')[0]!;

export const EMPTY_ACCOUNT_FORM: CreateAccountFormValues = {
  code: '',
  name: '',
  type: 0,
  nature: 0,
  parentId: '',
};

export function useAccountingPage() {
  const { t } = useI18n();
  const subscriberId = useAuthStore((s) => s.user?.subscriberId ?? '');
  const { canShow, hasHydrated: permsHydrated, skipPermissionHydrationWait } = usePermissionsUi();

  const canViewAccounts = canShow('finance.accounts.view');
  const canCreateAccount = canShow('finance.accounts.create');
  const canViewJournal = canShow('finance.journal.view');
  const canCreateJournal = canShow('finance.journal.create');
  const canViewConfig = canShow('finance.config.view');
  const canEditConfig = canShow('finance.config.edit');

  const [tab, setTab] = useState<AccountingTab>('accounts');
  const [showJournal, setShowJournal] = useState(false);

  const [jDesde, setJDesde] = useState(YEAR_START);
  const [jHasta, setJHasta] = useState(TODAY);
  const [expandedEntry, setExpandedEntry] = useState<string | null>(null);

  const [mayorAccountId, setMayorAccountId] = useState('');
  const [mayorDesde, setMayorDesde] = useState(YEAR_START);
  const [mayorHasta, setMayorHasta] = useState(TODAY);
  const [mayorData, setMayorData] = useState<MayorGeneralLineDto[]>([]);
  const [mayorLoading, setMayorLoading] = useState(false);
  const [mayorError, setMayorError] = useState<string | null>(null);

  const [balDesde, setBalDesde] = useState(YEAR_START);
  const [balHasta, setBalHasta] = useState(TODAY);
  const [balData, setBalData] = useState<BalanceComprobacionLineDto[]>([]);
  const [balLoading, setBalLoading] = useState(false);
  const [balError, setBalError] = useState<string | null>(null);

  const accounts = useAsync(() => accountingService.getAccounts(), canViewAccounts);
  const journalEntries = useAsync(() => accountingService.getJournalEntries(), canViewJournal);
  const config = useAsync(() => accountingConfigService.getConfig(), canViewConfig);
  const gastoMappings = useAsync(() => accountingConfigService.listGastoMappings(), canViewConfig);

  const fetchMayor = useCallback(async () => {
    if (!mayorAccountId) return;
    setMayorLoading(true);
    setMayorError(null);
    try {
      const data = await accountingService.getMayorGeneral(mayorAccountId, mayorDesde, mayorHasta);
      setMayorData(data);
    } catch (e) {
      setMayorError(formatApiError(e));
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
      setBalError(formatApiError(e));
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
    defaultValues: EMPTY_ACCOUNT_FORM,
  });
  const [formError, setFormError] = useState('');
  const [accountSubTab, setAccountSubTab] = useState<'data' | 'list'>('list');
  const [accountListQuery, setAccountListQuery] = useState('');

  const canUseModule = canViewAccounts || canViewJournal || canViewConfig;

  const displayTab = useMemo<AccountingTab>(() => {
    if (!canViewAccounts && canViewJournal) return 'journal';
    if (canViewAccounts && !canViewJournal) return 'accounts';
    if (!canViewAccounts && !canViewJournal && canViewConfig) return 'config';
    return tab;
  }, [canViewAccounts, canViewJournal, canViewConfig, tab]);

  const filteredJournal = useMemo(() => {
    const desde = new Date(jDesde);
    const hasta = new Date(jHasta);
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

  const accountTypes = useMemo(
    () => [
      { value: 0, label: t('finance.accounts.type.asset') },
      { value: 1, label: t('finance.accounts.type.liability') },
      { value: 2, label: t('finance.accounts.type.equity') },
      { value: 3, label: t('finance.accounts.type.income') },
      { value: 4, label: t('finance.accounts.type.expense') },
    ],
    [t],
  );

  const accountNatures = useMemo(
    () => [
      { value: 0, label: t('finance.accounts.nature.debit') },
      { value: 1, label: t('finance.accounts.nature.credit') },
    ],
    [t],
  );

  const submitAccount = handleSubmit(async (form) => {
    setFormError('');
    try {
      const payload: CreateAccountRequest = {
        code: form.code,
        name: form.name,
        type: form.type,
        nature: form.nature,
        parentId: form.parentId?.trim() ? form.parentId.trim() : null,
      };
      await accountingService.createAccount(payload);
      reset(EMPTY_ACCOUNT_FORM);
      accounts.refetch();
      setAccountSubTab('list');
    } catch (err: unknown) {
      setFormError(
        (err as { response?: { data?: { error?: string } } })?.response?.data?.error ??
          t('finance.accounts.modal.create.error'),
      );
    }
  });

  const [configError, setConfigError] = useState('');
  const [configSaving, setConfigSaving] = useState(false);
  const [gastoError, setGastoError] = useState('');
  const [gastoSaving, setGastoSaving] = useState(false);

  const [configForm, setConfigForm] = useState<ConfiguracionContableEmpresaDto>({
    cuentaInventarioId: null,
    cuentaCostoVentaId: null,
    cuentaProveedoresId: null,
    cuentaVentasId: null,
    cuentaClientesId: null,
    cuentaIvaComprasId: null,
    cuentaIvaVentasId: null,
    cuentaEfectivoId: null,
    cuentaBancoId: null,
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
  const [newGastoCuentaId, setNewGastoCuentaId] = useState('');

  const saveConfig = async () => {
    if (!canEditConfig) return;
    setConfigError('');
    setConfigSaving(true);
    try {
      await accountingConfigService.upsertConfig(configForm);
      config.refetch();
    } catch (err: unknown) {
      setConfigError(
        (err as { response?: { data?: { error?: string } } })?.response?.data?.error ?? t('common.error'),
      );
    } finally {
      setConfigSaving(false);
    }
  };

  const createGastoMapping = async () => {
    if (!canEditConfig) return;
    setGastoError('');
    const categoria = newGastoCategoria.trim();
    if (!categoria) {
      setGastoError(t('finance.config.expenses.validation.categoryRequired'));
      return;
    }
    if (!newGastoCuentaId) {
      setGastoError(t('finance.config.expenses.validation.accountRequired'));
      return;
    }
    setGastoSaving(true);
    try {
      const payload: CreateGastoCategoriaRequest = { categoria, cuentaGastoId: newGastoCuentaId };
      await accountingConfigService.createGastoMapping(payload);
      setNewGastoCategoria('');
      setNewGastoCuentaId('');
      gastoMappings.refetch();
    } catch (err: unknown) {
      setGastoError(
        (err as { response?: { data?: { error?: string } } })?.response?.data?.error ?? t('common.error'),
      );
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
      setGastoError(
        (err as { response?: { data?: { error?: string } } })?.response?.data?.error ?? t('common.error'),
      );
    }
  };

  const tabTitle =
    displayTab === 'accounts'
      ? t('accounting.tabs.accounts')
      : displayTab === 'journal'
        ? t('accounting.tabs.journal')
        : t('accounting.tabs.config');

  return {
    t,
    subscriberId,
    skipPermissionHydrationWait,
    permsHydrated,
    canUseModule,
    canViewAccounts,
    canCreateAccount,
    canViewJournal,
    canCreateJournal,
    canViewConfig,
    canEditConfig,
    tab,
    setTab,
    displayTab,
    tabTitle,
    showJournal,
    setShowJournal,
    accounts,
    journalEntries,
    config,
    gastoMappings,
    formRef,
    register,
    errors,
    formLoading,
    formError,
    accountSubTab,
    setAccountSubTab,
    activeAccountSubTab,
    accountListQuery,
    setAccountListQuery,
    filteredAccounts,
    accountTypes,
    accountNatures,
    submitAccount,
    jDesde,
    setJDesde,
    jHasta,
    setJHasta,
    expandedEntry,
    setExpandedEntry,
    filteredJournal,
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
    balDesde,
    setBalDesde,
    balHasta,
    setBalHasta,
    balData,
    balLoading,
    balError,
    fetchBalance,
    configError,
    configSaving,
    configForm,
    setConfigForm,
    saveConfig,
    gastoError,
    gastoSaving,
    newGastoCategoria,
    setNewGastoCategoria,
    newGastoCuentaId,
    setNewGastoCuentaId,
    createGastoMapping,
    deleteGastoMapping,
  };
}

export type AccountingPageContext = ReturnType<typeof useAccountingPage>;
