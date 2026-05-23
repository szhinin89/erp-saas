import { useMemo, useState } from 'react';
import { useCompanyScopedAsync } from '../../../hooks/useCompanyScopedAsync';
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
  const [supplierProfileBp, setSupplierProfileBp] = useState<BusinessPartnerDto | null>(null);
  const [saving, setSaving] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);

  const listState = useCompanyScopedAsync(
    () =>
      businessPartnerFacade.searchBusinessPartners({
        q: search || undefined,
        isActive: showInactive ? undefined : true,
        isSupplier: true,
        take: 200,
      }),
    canView,
    [search, showInactive],
  );

  const suppliers = useMemo(
    () => (listState.data ?? []).filter((bp) => bp.isSupplier),
    [listState.data],
  );

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

  const createSupplier = async (body: CreateBusinessPartnerBody) => {
    setSaving(true);
    setActionError(null);
    try {
      await businessPartnerFacade.createBusinessPartner({ ...body, asCustomer: false, asSupplier: true });
      setModalOpen(false);
      listState.refetch();
    } catch (err) {
      setActionError(formatApiError(err));
    } finally {
      setSaving(false);
    }
  };

  const updateSupplier = async (id: string, body: UpdateBusinessPartnerBody) => {
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

  const disableSupplier = async (id: string) => {
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

  const activateSupplier = async (id: string) => {
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

  const saveSupplierProfile = async (id: string, body: UpdateSupplierProfileBody) => {
    setSaving(true);
    setActionError(null);
    try {
      await businessPartnerFacade.updateSupplierProfile(id, body);
      setSupplierProfileBp(null);
    } catch (err) {
      setActionError(formatApiError(err));
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
    setSearch,
    showInactive,
    setShowInactive,
    suppliers,
    loading: listState.loading,
    error: listState.error ?? actionError,
    modalOpen,
    setModalOpen,
    editBp,
    setEditBp,
    openEdit,
    createSupplier,
    updateSupplier,
    disableSupplier,
    activateSupplier,
    settingsBp,
    settingsData,
    openSettings,
    setSettingsBp,
    saveCompanySettings,
    supplierProfileBp,
    setSupplierProfileBp,
    saveSupplierProfile,
    saving,
  };
}
