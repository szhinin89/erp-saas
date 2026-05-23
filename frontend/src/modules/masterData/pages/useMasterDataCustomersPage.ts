import { useCallback, useMemo, useState } from 'react';
import { useCompanyScopedAsync } from '../../../hooks/useCompanyScopedAsync';
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
  const canView = canShow('masterdata.businesspartners.view');
  const canCreate = canShow('masterdata.businesspartners.create');
  const canUpdate = canShow('masterdata.businesspartners.update');
  const canDisable = canShow('masterdata.businesspartners.disable');
  const canConfigure = canShow('masterdata.businesspartners.configure-company');

  const [search, setSearch] = useState('');
  const [showInactive, setShowInactive] = useState(false);
  const [modalOpen, setModalOpen] = useState(false);
  const [editBp, setEditBp] = useState<BusinessPartnerDto | null>(null);
  const [settingsBp, setSettingsBp] = useState<BusinessPartnerDto | null>(null);
  const [settingsData, setSettingsData] = useState<CompanyBpSettingsDto | null>(null);
  const [saving, setSaving] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);

  const listState = useCompanyScopedAsync(
    () =>
      businessPartnerFacade.searchBusinessPartners({
        q: search || undefined,
        isActive: showInactive ? undefined : true,
        isCustomer: true,
        take: 200,
      }),
    canView,
    [search, showInactive],
  );

  const customers = useMemo(
    () => (listState.data ?? []).filter((bp) => bp.isCustomer),
    [listState.data],
  );

  const openCreate = () => {
    setActionError(null);
    setModalOpen(true);
  };

  const openEdit = (bp: BusinessPartnerDto) => {
    setActionError(null);
    setEditBp(bp);
  };

  const openSettings = async (bp: BusinessPartnerDto) => {
    setActionError(null);
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
    setActionError(null);
    try {
      await businessPartnerFacade.createBusinessPartner({ ...body, asCustomer: true, asSupplier: false });
      setModalOpen(false);
      listState.refetch();
    } catch (err) {
      setActionError(formatApiError(err));
    } finally {
      setSaving(false);
    }
  };

  const updateCustomer = async (id: string, body: UpdateBusinessPartnerBody) => {
    setSaving(true);
    setActionError(null);
    try {
      await businessPartnerFacade.updateBusinessPartner(id, body);
      setEditBp(null);
      listState.refetch();
    } catch (err) {
      setActionError(formatApiError(err));
    } finally {
      setSaving(false);
    }
  };

  const disableCustomer = async (id: string) => {
    setSaving(true);
    setActionError(null);
    try {
      await businessPartnerFacade.disableBusinessPartner(id);
      listState.refetch();
    } catch (err) {
      setActionError(formatApiError(err));
    } finally {
      setSaving(false);
    }
  };

  const activateCustomer = async (id: string) => {
    setSaving(true);
    setActionError(null);
    try {
      await businessPartnerFacade.activateBusinessPartner(id);
      listState.refetch();
    } catch (err) {
      setActionError(formatApiError(err));
    } finally {
      setSaving(false);
    }
  };

  const saveCompanySettings = async (
    id: string,
    payload: { creditLimit?: number | null; paymentDays: number; isBlocked: boolean },
  ) => {
    setSaving(true);
    setActionError(null);
    try {
      await businessPartnerFacade.upsertCompanySettings(id, payload);
      setSettingsBp(null);
      setSettingsData(null);
    } catch (err) {
      setActionError(formatApiError(err));
    } finally {
      setSaving(false);
    }
  };

  const closeSettings = useCallback(() => {
    setSettingsBp(null);
    setSettingsData(null);
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
    loading: listState.loading,
    error: listState.error ?? actionError,
    modalOpen,
    setModalOpen,
    editBp,
    setEditBp,
    openCreate,
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
    saving,
    refetch: listState.refetch,
  };
}
