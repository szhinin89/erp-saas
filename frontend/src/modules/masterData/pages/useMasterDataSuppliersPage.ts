import { useMemo, useState } from 'react';
import { useCompanyScopedAsync } from '../../../hooks/useCompanyScopedAsync';
import { usePermissionsUi } from '../../../access/usePermissionsUi';
import { businessPartnerFacade } from '../api/businessPartnerFacade';
import type { BusinessPartnerDto, CreateBusinessPartnerBody } from '../types/businessPartner.types';
import { formatApiError } from '../../lib/formatApiError';

export function useMasterDataSuppliersPage() {
  const { canShow } = usePermissionsUi();
  const canView = canShow('masterdata.businesspartners.view');
  const canCreate = canShow('masterdata.businesspartners.create');
  const canDisable = canShow('masterdata.businesspartners.disable');
  const canConfigure = canShow('masterdata.businesspartners.configure-company');

  const [search, setSearch] = useState('');
  const [showInactive, setShowInactive] = useState(false);
  const [modalOpen, setModalOpen] = useState(false);
  const [settingsBp, setSettingsBp] = useState<BusinessPartnerDto | null>(null);
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

  const saveCompanySettings = async (
    id: string,
    payload: { creditLimit?: number | null; paymentDays: number; isBlocked: boolean },
  ) => {
    setSaving(true);
    setActionError(null);
    try {
      await businessPartnerFacade.upsertCompanySettings(id, payload);
      setSettingsBp(null);
    } catch (err) {
      setActionError(formatApiError(err));
    } finally {
      setSaving(false);
    }
  };

  return {
    canView,
    canCreate,
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
    createSupplier,
    disableSupplier,
    settingsBp,
    setSettingsBp,
    saveCompanySettings,
    saving,
  };
}
