import { useCallback, useState } from 'react';
import { useCompanyScopedAsync } from '../../../hooks/useCompanyScopedAsync';
import { useDebounce } from '../../../hooks/useDebounce';
import { usePermissionsUi } from '../../../access/usePermissionsUi';
import { businessPartnerFacade } from '../api/businessPartnerFacade';
import type {
  BusinessPartnerSummaryDto,
  CompanyBpTradingSettingsDto,
  CreateBusinessPartnerBody,
  SupplierConfigBody,
  UpdateBusinessPartnerBody,
} from '../types/businessPartner.types';
import { RoleTypeEnum } from '../types/businessPartner.types';
import { formatApiError } from '../../lib/formatApiError';

export function useMasterDataSuppliersPage() {
  const { canShow } = usePermissionsUi();
  const canView      = canShow('masterdata.businesspartners.view');
  const canCreate    = canShow('masterdata.businesspartners.create');
  const canUpdate    = canShow('masterdata.businesspartners.update');
  const canDisable   = canShow('masterdata.businesspartners.disable');
  const canConfigure = canShow('masterdata.businesspartners.configure-company');

  const [search, setSearch]             = useState('');
  const debouncedSearch                 = useDebounce(search, 300);
  const [showInactive, setShowInactive] = useState(false);
  const [page, setPage]                 = useState(1);
  const PAGE_SIZE                       = 50;

  const [modalOpen, setModalOpen]         = useState(false);
  const [editBp, setEditBp]               = useState<BusinessPartnerSummaryDto | null>(null);
  const [settingsBp, setSettingsBp]       = useState<BusinessPartnerSummaryDto | null>(null);
  const [settingsData, setSettingsData]   = useState<CompanyBpTradingSettingsDto | null>(null);
  // supplierProfileBp: store the bp + roleId for updating supplier config
  const [supplierConfigBp, setSupplierConfigBp] = useState<{ bp: BusinessPartnerSummaryDto; roleId: string } | null>(null);
  const [saving, setSaving]               = useState(false);
  const [inlineError, setInlineError]     = useState<string | null>(null);
  const [modalError, setModalError]       = useState<string | null>(null);

  const listState = useCompanyScopedAsync(
    () =>
      businessPartnerFacade.searchBusinessPartnersPaged({
        q:        debouncedSearch || undefined,
        isActive: showInactive ? undefined : true,
        roles:    [RoleTypeEnum.Supplier],   // replaces legacy isSupplier: true
        skip:     (page - 1) * PAGE_SIZE,
        take:     PAGE_SIZE,
      }),
    canView,
    [debouncedSearch, showInactive, page],
  );

  const suppliers  = listState.data?.items ?? [];
  const totalCount = listState.data?.totalCount ?? 0;
  const totalPages = Math.max(1, Math.ceil(totalCount / PAGE_SIZE));

  const clearModalError  = () => setModalError(null);
  const setSearchReset   = (v: string)  => { setSearch(v);       setPage(1); };
  const setInactiveReset = (v: boolean) => { setShowInactive(v); setPage(1); };

  // ── Create ─────────────────────────────────────────────────────────────────

  const openCreate  = () => { clearModalError(); setInlineError(null); setModalOpen(true); };
  const closeCreate = useCallback(() => { setModalOpen(false); clearModalError(); }, []);

  const createSupplier = async (body: CreateBusinessPartnerBody): Promise<boolean> => {
    setSaving(true);
    clearModalError();
    try {
      const created = await businessPartnerFacade.createBusinessPartner(body);
      await businessPartnerFacade.assignRole(created.id, { roleType: RoleTypeEnum.Supplier });
      setModalOpen(false);
      listState.refetch();
      return true;
    } catch (err) {
      setModalError(formatApiError(err));
      return false;
    } finally {
      setSaving(false);
    }
  };

  const assignAsSupplier = async (id: string): Promise<boolean> => {
    setSaving(true);
    clearModalError();
    try {
      await businessPartnerFacade.assignRole(id, { roleType: RoleTypeEnum.Supplier });
      setModalOpen(false);
      listState.refetch();
      return true;
    } catch (err) {
      setModalError(formatApiError(err));
      return false;
    } finally {
      setSaving(false);
    }
  };

  // ── Update ─────────────────────────────────────────────────────────────────

  const openEdit  = (bp: BusinessPartnerSummaryDto) => { clearModalError(); setInlineError(null); setEditBp(bp); };
  const closeEdit = useCallback(() => { setEditBp(null); clearModalError(); }, []);

  const updateSupplier = async (id: string, body: UpdateBusinessPartnerBody): Promise<boolean> => {
    setSaving(true);
    clearModalError();
    try {
      await businessPartnerFacade.updateBusinessPartner(id, body);
      setEditBp(null);
      listState.refetch();
      return true;
    } catch (err) {
      setModalError(formatApiError(err));
      return false;
    } finally {
      setSaving(false);
    }
  };

  // ── Supplier config (SRI defaults) ────────────────────────────────────────

  const openSupplierConfig = async (bp: BusinessPartnerSummaryDto) => {
    clearModalError();
    try {
      const roles = await businessPartnerFacade.getRoles(bp.id, true);
      const supplierRole = roles.find((r) => r.roleType === 'Supplier');
      if (supplierRole) setSupplierConfigBp({ bp, roleId: supplierRole.id });
    } catch { /* no action */ }
  };

  const closeSupplierConfig = useCallback(() => { setSupplierConfigBp(null); clearModalError(); }, []);

  const saveSupplierConfig = async (bpId: string, roleId: string, config: SupplierConfigBody): Promise<boolean> => {
    setSaving(true);
    clearModalError();
    try {
      await businessPartnerFacade.updateSupplierConfig(bpId, roleId, config);
      setSupplierConfigBp(null);
      return true;
    } catch (err) {
      setModalError(formatApiError(err));
      return false;
    } finally {
      setSaving(false);
    }
  };

  // ── Inline actions ─────────────────────────────────────────────────────────

  const disableSupplier = async (id: string) => {
    setSaving(true);
    setInlineError(null);
    try {
      await businessPartnerFacade.deactivateBusinessPartner(id);
      listState.refetch();
    } catch (err) {
      setInlineError(formatApiError(err));
    } finally {
      setSaving(false);
    }
  };

  const activateSupplier = async (id: string) => {
    setSaving(true);
    setInlineError(null);
    try {
      await businessPartnerFacade.activateBusinessPartner(id);
      listState.refetch();
    } catch (err) {
      setInlineError(formatApiError(err));
    } finally {
      setSaving(false);
    }
  };

  const addAsCustomer = async (id: string) => {
    setSaving(true);
    setInlineError(null);
    try {
      await businessPartnerFacade.assignRole(id, { roleType: RoleTypeEnum.Customer });
      listState.refetch();
    } catch (err) {
      setInlineError(formatApiError(err));
    } finally {
      setSaving(false);
    }
  };

  // ── Trading Settings ───────────────────────────────────────────────────────

  const openSettings = async (bp: BusinessPartnerSummaryDto) => {
    clearModalError();
    setInlineError(null);
    setSettingsBp(bp);
    try {
      const data = await businessPartnerFacade.getTradingSettings(bp.id);
      setSettingsData(data);
    } catch {
      setSettingsData(null);
    }
  };

  const closeSettings = useCallback(() => {
    setSettingsBp(null);
    setSettingsData(null);
    clearModalError();
  }, []);

  const saveSettings = async (
    id: string,
    payload: { creditLimit: number; paymentDays: number; creditCurrencyCode: string },
  ) => {
    setSaving(true);
    clearModalError();
    try {
      await businessPartnerFacade.upsertTradingSettings(id, payload);
      await openSettings({ ...settingsBp!, id });
      listState.refetch();
    } catch (err) {
      setModalError(formatApiError(err));
    } finally {
      setSaving(false);
    }
  };

  const blockSupplier = async (id: string, reason: string) => {
    setSaving(true);
    clearModalError();
    try {
      await businessPartnerFacade.blockBusinessPartner(id, { reason });
      await openSettings({ ...settingsBp!, id });
    } catch (err) {
      setModalError(formatApiError(err));
    } finally {
      setSaving(false);
    }
  };

  const unblockSupplier = async (id: string) => {
    setSaving(true);
    clearModalError();
    try {
      await businessPartnerFacade.unblockBusinessPartner(id);
      await openSettings({ ...settingsBp!, id });
    } catch (err) {
      setModalError(formatApiError(err));
    } finally {
      setSaving(false);
    }
  };

  return {
    canView, canCreate, canUpdate, canDisable, canConfigure,
    search, setSearch: setSearchReset,
    showInactive, setShowInactive: setInactiveReset,
    page, setPage,
    totalCount, totalPages,
    suppliers,
    loading:    listState.loading,
    listError:  listState.error,
    inlineError, modalError,
    modalOpen, openCreate, closeCreate,
    editBp, openEdit, closeEdit,
    createSupplier, updateSupplier, assignAsSupplier,
    disableSupplier, activateSupplier, addAsCustomer,
    supplierConfigBp, openSupplierConfig, closeSupplierConfig, saveSupplierConfig,
    settingsBp, settingsData, openSettings, closeSettings,
    saveSettings, blockSupplier, unblockSupplier,
    saving,
    refetch: listState.refetch,
  };
}
