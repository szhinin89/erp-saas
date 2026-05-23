import { useCallback, useMemo, useState } from 'react';
import { useCompanyScopedAsync } from '../../../hooks/useCompanyScopedAsync';
import { usePermissionsUi } from '../../../access/usePermissionsUi';
import { businessPartnerFacade } from '../api/businessPartnerFacade';
import type { BusinessPartnerDto, CreateBusinessPartnerBody } from '../types/businessPartner.types';
import { formatApiError } from '../../lib/formatApiError';

export function useMasterDataCustomersPage() {
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

  const closeSettings = useCallback(() => setSettingsBp(null), []);

  return {
    canView,
    canCreate,
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
    openCreate,
    createCustomer,
    disableCustomer,
    settingsBp,
    setSettingsBp,
    saveCompanySettings,
    closeSettings,
    saving,
    refetch: listState.refetch,
  };
}
