import { useCallback, useState } from 'react';
import { useCompanyScopedAsync } from '../../../hooks/useCompanyScopedAsync';
import { useDebounce } from '../../../hooks/useDebounce';
import { usePermissionsUi } from '../../../access/usePermissionsUi';
import { businessPartnerFacade } from '../api/businessPartnerFacade';
import type {
  BusinessPartnerDto,
  CompanyBpSettingsDto,
  CreateBusinessPartnerBody,
  UpdateBusinessPartnerBody,
  UpdateSupplierProfileBody,
} from '../types/businessPartner.types';
import { formatApiError } from '../../lib/formatApiError';

export function useMasterDataSuppliersPage() {
  const { canShow } = usePermissionsUi();
  const canView      = canShow('masterdata.businesspartners.view');
  const canCreate    = canShow('masterdata.businesspartners.create');
  const canUpdate    = canShow('masterdata.businesspartners.update');
  const canDisable   = canShow('masterdata.businesspartners.disable');
  const canConfigure = canShow('masterdata.businesspartners.configure-company');

  const [search, setSearch]                         = useState('');
  const debouncedSearch                             = useDebounce(search, 300);
  const [showInactive, setShowInactive]             = useState(false);
  const [page, setPage]                             = useState(1);
  const PAGE_SIZE                                   = 50;
  const [modalOpen, setModalOpen]                   = useState(false);
  const [editBp, setEditBp]                         = useState<BusinessPartnerDto | null>(null);
  const [settingsBp, setSettingsBp]                 = useState<BusinessPartnerDto | null>(null);
  const [settingsData, setSettingsData]             = useState<CompanyBpSettingsDto | null>(null);
  const [supplierProfileBp, setSupplierProfileBp]   = useState<BusinessPartnerDto | null>(null);
  const [saving, setSaving]                         = useState(false);

  const [inlineError, setInlineError] = useState<string | null>(null);
  const [modalError, setModalError]   = useState<string | null>(null);

  const listState = useCompanyScopedAsync(
    () =>
      businessPartnerFacade.searchBusinessPartnersPaged({
        q: debouncedSearch || undefined,
        isActive: showInactive ? undefined : true,
        isSupplier: true,
        skip: (page - 1) * PAGE_SIZE,
        take: PAGE_SIZE,
      }),
    canView,
    [debouncedSearch, showInactive, page],
  );

  const suppliers  = listState.data?.items ?? [];
  const totalCount = listState.data?.totalCount ?? 0;
  const totalPages = Math.max(1, Math.ceil(totalCount / PAGE_SIZE));

  const setSearchReset = (v: string) => { setSearch(v); setPage(1); };
  const setShowInactiveReset = (v: boolean) => { setShowInactive(v); setPage(1); };

  const clearModalError = () => setModalError(null);

  const openCreate = () => {
    clearModalError();
    setInlineError(null);
    setModalOpen(true);
  };

  const closeCreate = useCallback(() => {
    setModalOpen(false);
    clearModalError();
  }, []);

  const openEdit = (bp: BusinessPartnerDto) => {
    clearModalError();
    setInlineError(null);
    setEditBp(bp);
  };

  const closeEdit = useCallback(() => {
    setEditBp(null);
    clearModalError();
  }, []);

  const openSettings = async (bp: BusinessPartnerDto) => {
    clearModalError();
    setInlineError(null);
    setSettingsBp(bp);
    try {
      const data = await businessPartnerFacade.getCompanySettings(bp.id);
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

  const openSupplierProfile = (bp: BusinessPartnerDto) => {
    clearModalError();
    setInlineError(null);
    setSupplierProfileBp(bp);
  };

  const closeSupplierProfile = useCallback(() => {
    setSupplierProfileBp(null);
    clearModalError();
  }, []);

  const createSupplier = async (body: CreateBusinessPartnerBody): Promise<boolean> => {
    setSaving(true);
    clearModalError();
    try {
      await businessPartnerFacade.createBusinessPartner({ ...body, asCustomer: false, asSupplier: true });
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

  const disableSupplier = async (id: string) => {
    setSaving(true);
    setInlineError(null);
    try {
      await businessPartnerFacade.disableBusinessPartner(id);
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

  const saveCompanySettings = async (
    id: string,
    payload: { creditLimit?: number | null; paymentDays: number; isBlocked: boolean },
  ) => {
    setSaving(true);
    clearModalError();
    try {
      await businessPartnerFacade.upsertCompanySettings(id, payload);
      setSettingsBp(null);
      setSettingsData(null);
      listState.refetch();
    } catch (err) {
      setModalError(formatApiError(err));
    } finally {
      setSaving(false);
    }
  };

  const addAsCustomer = async (id: string) => {
    setSaving(true);
    setInlineError(null);
    try {
      await businessPartnerFacade.addRole(id, true, false);
      listState.refetch();
    } catch (err) {
      setInlineError(formatApiError(err));
    } finally {
      setSaving(false);
    }
  };

  const assignAsSupplier = async (id: string): Promise<boolean> => {
    setSaving(true);
    clearModalError();
    try {
      await businessPartnerFacade.addRole(id, false, true);
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

  const saveSupplierProfile = async (id: string, body: UpdateSupplierProfileBody) => {
    setSaving(true);
    clearModalError();
    try {
      await businessPartnerFacade.updateSupplierProfile(id, body);
      setSupplierProfileBp(null);
      listState.refetch();
    } catch (err) {
      setModalError(formatApiError(err));
    } finally {
      setSaving(false);
    }
  };

  return {
    canView,
    canCreate,
    canUpdate,
    canDisable,
    canConfigure,
    search,
    setSearch: setSearchReset,
    showInactive,
    setShowInactive: setShowInactiveReset,
    page,
    setPage,
    totalCount,
    totalPages,
    suppliers,
    loading:     listState.loading,
    listError:   listState.error,
    inlineError,
    modalError,
    modalOpen,
    openCreate,
    closeCreate,
    editBp,
    openEdit,
    closeEdit,
    createSupplier,
    updateSupplier,
    disableSupplier,
    activateSupplier,
    settingsBp,
    settingsData,
    openSettings,
    closeSettings,
    addAsCustomer,
    assignAsSupplier,
    supplierProfileBp,
    openSupplierProfile,
    closeSupplierProfile,
    saveCompanySettings,
    saveSupplierProfile,
    saving,
  };
}
