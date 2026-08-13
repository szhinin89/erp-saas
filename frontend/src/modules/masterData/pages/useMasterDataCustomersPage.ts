import { useCallback, useState } from "react";
import { useCompanyScopedAsync } from "../../../hooks/useCompanyScopedAsync";
import { useDebounce } from "../../../hooks/useDebounce";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { businessPartnerFacade } from "../api/businessPartnerFacade";
import type {
  BusinessPartnerStatusFilter,
  BusinessPartnerSummaryDto,
  CompanyBpTradingSettingsDto,
  CreateBusinessPartnerBody,
  CustomerConfigBody,
  UpdateBusinessPartnerBody,
} from "../types/businessPartner.types";
import { RoleTypeEnum } from "../types/businessPartner.types";
import { formatApiRequestError } from "../../lib/apiError";

function toIsActiveParam(status: BusinessPartnerStatusFilter): boolean | undefined {
  if (status === "active") return true;
  if (status === "inactive") return false;
  return undefined;
}

export function useMasterDataCustomersPage() {
  const { canShow } = usePermissionsUi();
  const canView = canShow("masterdata.businesspartners.view");
  const canCreate = canShow("masterdata.businesspartners.create");
  const canUpdate = canShow("masterdata.businesspartners.update");
  const canDisable = canShow("masterdata.businesspartners.disable");
  const canConfigure = canShow("masterdata.businesspartners.configure-company");

  const [search, setSearch] = useState("");
  const debouncedSearch = useDebounce(search, 300);
  const [statusFilter, setStatusFilter] =
    useState<BusinessPartnerStatusFilter>("active");
  const [page, setPage] = useState(1);
  const PAGE_SIZE = 50;

  const [modalOpen, setModalOpen] = useState(false);
  const [editBp, setEditBp] = useState<BusinessPartnerSummaryDto | null>(null);
  const [settingsBp, setSettingsBp] =
    useState<BusinessPartnerSummaryDto | null>(null);
  const [settingsData, setSettingsData] =
    useState<CompanyBpTradingSettingsDto | null>(null);
  const [saving, setSaving] = useState(false);
  const [inlineError, setInlineError] = useState<string | null>(null);
  const [modalError, setModalError] = useState<string | null>(null);
  const [customerConfigBp, setCustomerConfigBp] = useState<{
    bp: BusinessPartnerSummaryDto;
    roleId: string;
  } | null>(null);

  const listState = useCompanyScopedAsync(
    () =>
      businessPartnerFacade.searchBusinessPartnersPaged({
        q: debouncedSearch || undefined,
        isActive: toIsActiveParam(statusFilter),
        roles: [RoleTypeEnum.Customer], // replaces legacy isCustomer: true
        skip: (page - 1) * PAGE_SIZE,
        take: PAGE_SIZE,
      }),
    canView,
    [debouncedSearch, statusFilter, page],
  );

  const customers = listState.data?.items ?? [];
  const totalCount = listState.data?.totalCount ?? 0;
  const totalPages = Math.max(1, Math.ceil(totalCount / PAGE_SIZE));

  const clearModalError = () => setModalError(null);
  const setSearchReset = (v: string) => {
    setSearch(v);
    setPage(1);
  };
  const setStatusFilterReset = (v: BusinessPartnerStatusFilter) => {
    setStatusFilter(v);
    setPage(1);
  };

  // ── Create ─────────────────────────────────────────────────────────────────

  const openCreate = () => {
    clearModalError();
    setInlineError(null);
    setModalOpen(true);
  };
  const closeCreate = useCallback(() => {
    setModalOpen(false);
    clearModalError();
  }, []);

  /** Creates the BP identity and immediately assigns the Customer role. */
  const createCustomer = async (
    body: CreateBusinessPartnerBody,
  ): Promise<void> => {
    setSaving(true);
    clearModalError();
    try {
      const created = await businessPartnerFacade.createBusinessPartner(body);
      await businessPartnerFacade.assignRole(created.id, {
        roleType: RoleTypeEnum.Customer,
      });
      setModalOpen(false);
      listState.refetch();
    } finally {
      setSaving(false);
    }
  };

  /** Assigns the Customer role to an existing BP that was found via search. */
  const assignAsCustomer = async (id: string): Promise<void> => {
    setSaving(true);
    clearModalError();
    try {
      await businessPartnerFacade.assignRole(id, {
        roleType: RoleTypeEnum.Customer,
      });
      setModalOpen(false);
      listState.refetch();
    } finally {
      setSaving(false);
    }
  };

  // ── Update ─────────────────────────────────────────────────────────────────

  const openEdit = (bp: BusinessPartnerSummaryDto) => {
    clearModalError();
    setInlineError(null);
    setEditBp(bp);
  };
  const closeEdit = useCallback(() => {
    setEditBp(null);
    clearModalError();
  }, []);

  const updateCustomer = async (
    id: string,
    body: UpdateBusinessPartnerBody,
  ): Promise<void> => {
    setSaving(true);
    clearModalError();
    try {
      await businessPartnerFacade.updateBusinessPartner(id, body);
      setEditBp(null);
      listState.refetch();
    } finally {
      setSaving(false);
    }
  };

  // ── Inline actions ─────────────────────────────────────────────────────────

  const disableCustomer = async (id: string) => {
    setSaving(true);
    setInlineError(null);
    try {
      await businessPartnerFacade.deactivateBusinessPartner(id);
      listState.refetch();
    } catch (err) {
      setInlineError(
        formatApiRequestError(err, {
          generic: "Error al procesar la operación.",
        }),
      );
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
      setInlineError(
        formatApiRequestError(err, {
          generic: "Error al procesar la operación.",
        }),
      );
    } finally {
      setSaving(false);
    }
  };

  const addAsSupplier = async (id: string) => {
    setSaving(true);
    setInlineError(null);
    try {
      await businessPartnerFacade.assignRole(id, {
        roleType: RoleTypeEnum.Supplier,
      });
      listState.refetch();
    } catch (err) {
      setInlineError(
        formatApiRequestError(err, {
          generic: "Error al procesar la operación.",
        }),
      );
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
    payload: {
      creditLimit: number;
      paymentDays: number;
      creditCurrencyCode: string;
    },
  ): Promise<void> => {
    setSaving(true);
    clearModalError();
    try {
      await businessPartnerFacade.upsertTradingSettings(id, payload);
    } finally {
      setSaving(false);
    }
    listState.refetch();
    openSettings({ ...settingsBp!, id }).catch(() => {});
  };

  // ── Customer Config (CRM fields) ──────────────────────────────────────────

  const openCustomerConfig = async (bp: BusinessPartnerSummaryDto) => {
    clearModalError();
    try {
      const roles = await businessPartnerFacade.getRoles(bp.id, true);
      const customerRole = roles.find((r) => r.roleType === "Customer");
      if (customerRole) setCustomerConfigBp({ bp, roleId: customerRole.id });
    } catch {
      /* no action — BP may not have Customer role yet */
    }
  };

  const closeCustomerConfig = useCallback(() => {
    setCustomerConfigBp(null);
    clearModalError();
  }, []);

  const saveCustomerConfig = async (
    bpId: string,
    roleId: string,
    config: CustomerConfigBody,
  ): Promise<boolean> => {
    setSaving(true);
    clearModalError();
    try {
      await businessPartnerFacade.updateCustomerConfig(bpId, roleId, config);
      setCustomerConfigBp(null);
      return true;
    } catch (err) {
      setModalError(
        formatApiRequestError(err, {
          generic: "Error al procesar la operación.",
        }),
      );
      return false;
    } finally {
      setSaving(false);
    }
  };

  const blockCustomer = async (id: string, reason: string) => {
    setSaving(true);
    clearModalError();
    try {
      await businessPartnerFacade.blockBusinessPartner(id, { reason });
      await openSettings({ ...settingsBp!, id });
    } catch (err) {
      setModalError(
        formatApiRequestError(err, {
          generic: "Error al procesar la operación.",
        }),
      );
    } finally {
      setSaving(false);
    }
  };

  const unblockCustomer = async (id: string) => {
    setSaving(true);
    clearModalError();
    try {
      await businessPartnerFacade.unblockBusinessPartner(id);
      await openSettings({ ...settingsBp!, id });
    } catch (err) {
      setModalError(
        formatApiRequestError(err, {
          generic: "Error al procesar la operación.",
        }),
      );
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
    statusFilter,
    setStatusFilter: setStatusFilterReset,
    page,
    setPage,
    totalCount,
    totalPages,
    customers,
    loading: listState.loading,
    listError: listState.error,
    inlineError,
    modalError,
    modalOpen,
    openCreate,
    closeCreate,
    editBp,
    openEdit,
    closeEdit,
    createCustomer,
    updateCustomer,
    assignAsCustomer,
    disableCustomer,
    activateCustomer,
    addAsSupplier,
    settingsBp,
    settingsData,
    openSettings,
    closeSettings,
    saveSettings,
    blockCustomer,
    unblockCustomer,
    saving,
    refetch: listState.refetch,
    customerConfigBp,
    openCustomerConfig,
    closeCustomerConfig,
    saveCustomerConfig,
  };
}
