import { useCallback, useMemo, useState } from 'react';
import { useCompanyScopedAsync } from '../../../hooks/useCompanyScopedAsync';
import { useDebounce } from '../../../hooks/useDebounce';
import { usePermissionsUi } from '../../../access/usePermissionsUi';
import { businessPartnerFacade } from '../api/businessPartnerFacade';
import type {
  BusinessPartnerDto,
  CompanyBpSettingsDto,
  CreateBusinessPartnerBody,
  UpdateBusinessPartnerBody,
} from '../types/businessPartner.types';
import { formatApiError } from '../../lib/formatApiError';

export function useMasterDataCustomersPage() {
  const { canShow } = usePermissionsUi();
  const canView      = canShow('masterdata.businesspartners.view');
  const canCreate    = canShow('masterdata.businesspartners.create');
  const canUpdate    = canShow('masterdata.businesspartners.update');
  const canDisable   = canShow('masterdata.businesspartners.disable');
  const canConfigure = canShow('masterdata.businesspartners.configure-company');

  const [search, setSearch]             = useState('');
  const debouncedSearch                 = useDebounce(search, 300);
  const [showInactive, setShowInactive] = useState(false);
  const [modalOpen, setModalOpen]       = useState(false);
  const [editBp, setEditBp]             = useState<BusinessPartnerDto | null>(null);
  const [settingsBp, setSettingsBp]     = useState<BusinessPartnerDto | null>(null);
  const [settingsData, setSettingsData] = useState<CompanyBpSettingsDto | null>(null);
  const [saving, setSaving]             = useState(false);
  const [notesBp, setNotesBp]           = useState<BusinessPartnerDto | null>(null);

  // Errores de carga de lista — banner en la página
  const [listError, setListError]   = useState<string | null>(null);
  // Errores de acciones inline (disable/activate) — banner en la página
  const [inlineError, setInlineError] = useState<string | null>(null);
  // Errores de guardado en modal — banner DENTRO del modal activo
  const [modalError, setModalError] = useState<string | null>(null);

  const listState = useCompanyScopedAsync(
    () =>
      businessPartnerFacade.searchBusinessPartners({
        q: debouncedSearch || undefined,
        isActive: showInactive ? undefined : true,
        isCustomer: true,
        take: 200,
      }),
    canView,
    [debouncedSearch, showInactive],
  );

  const customers = useMemo(
    () => (listState.data ?? []).filter((bp) => bp.isCustomer),
    [listState.data],
  );

  const clearModalError = () => setModalError(null);

  const openCreate = () => {
    clearModalError();
    setInlineError(null);
    setModalOpen(true);
  };

  const openEdit = (bp: BusinessPartnerDto) => {
    clearModalError();
    setInlineError(null);
    setEditBp(bp);
  };

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

  const createCustomer = async (body: CreateBusinessPartnerBody) => {
    setSaving(true);
    clearModalError();
    try {
      await businessPartnerFacade.createBusinessPartner({ ...body, asCustomer: true, asSupplier: false });
      setModalOpen(false);
      listState.refetch();
    } catch (err) {
      setModalError(formatApiError(err));
    } finally {
      setSaving(false);
    }
  };

  const updateCustomer = async (id: string, body: UpdateBusinessPartnerBody) => {
    setSaving(true);
    clearModalError();
    try {
      await businessPartnerFacade.updateBusinessPartner(id, body);
      setEditBp(null);
      listState.refetch();
    } catch (err) {
      setModalError(formatApiError(err));
    } finally {
      setSaving(false);
    }
  };

  // Acciones inline — sin modal: error va al banner de página
  const disableCustomer = async (id: string) => {
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

  const activateCustomer = async (id: string) => {
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

  const openNotes = (bp: BusinessPartnerDto) => {
    clearModalError();
    setInlineError(null);
    setNotesBp(bp);
  };

  const closeNotes = useCallback(() => {
    setNotesBp(null);
    clearModalError();
  }, []);

  const saveNotes = async (id: string, notes: string | null) => {
    setSaving(true);
    clearModalError();
    try {
      await businessPartnerFacade.updateCustomerNotes(id, notes);
      setNotesBp(null);
      listState.refetch();
    } catch (err) {
      setModalError(formatApiError(err));
    } finally {
      setSaving(false);
    }
  };

  const closeSettings = useCallback(() => {
    setSettingsBp(null);
    setSettingsData(null);
    clearModalError();
  }, []);

  const closeCreate = useCallback(() => {
    setModalOpen(false);
    clearModalError();
  }, []);

  const closeEdit = useCallback(() => {
    setEditBp(null);
    clearModalError();
  }, []);

  return {
    canView,
    canCreate,
    canUpdate,
    canDisable,
    canConfigure,
    search,
    setSearch,
    showInactive,
    setShowInactive,
    customers,
    loading:     listState.loading,
    listError:   listState.error ?? listError,
    inlineError,
    modalError,
    modalOpen,
    closeCreate,
    openCreate,
    editBp,
    closeEdit,
    openEdit,
    createCustomer,
    updateCustomer,
    disableCustomer,
    activateCustomer,
    settingsBp,
    settingsData,
    openSettings,
    saveCompanySettings,
    closeSettings,
    notesBp,
    openNotes,
    closeNotes,
    saveNotes,
    saving,
    refetch: listState.refetch,
  };
}
