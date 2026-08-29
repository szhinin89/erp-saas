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
  SupplierClassificationBody,
  SupplierConfigBody,
  UpdateBusinessPartnerBody,
} from "../types/businessPartner.types";
import { RoleTypeEnum } from "../types/businessPartner.types";
import { formatApiRequestError } from "../../lib/apiError";
import { message } from "../../../lib/messages";

function toIsActiveParam(status: BusinessPartnerStatusFilter): boolean | undefined {
  if (status === "active") return true;
  if (status === "inactive") return false;
  return undefined;
}

export function useMasterDataSuppliersPage() {
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
  // supplierProfileBp: store the bp + roleId for updating supplier config
  const [supplierConfigBp, setSupplierConfigBp] = useState<{
    bp: BusinessPartnerSummaryDto;
    roleId: string;
  } | null>(null);
  const [supplierClassificationBp, setSupplierClassificationBp] = useState<{
    bp: BusinessPartnerSummaryDto;
    roleId: string;
  } | null>(null);
  const [saving, setSaving] = useState(false);
  const [inlineError, setInlineError] = useState<string | null>(null);
  const [modalError, setModalError] = useState<string | null>(null);

  const listState = useCompanyScopedAsync(
    () =>
      businessPartnerFacade.searchBusinessPartnersPaged({
        q: debouncedSearch || undefined,
        isActive: toIsActiveParam(statusFilter),
        roles: [RoleTypeEnum.Supplier], // replaces legacy isSupplier: true
        skip: (page - 1) * PAGE_SIZE,
        take: PAGE_SIZE,
      }),
    canView,
    [debouncedSearch, statusFilter, page],
  );

  const suppliers = listState.data?.items ?? [];
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

  const createSupplier = async (
    body: CreateBusinessPartnerBody,
    supplierConfig?: { refundProviderTypeCode: string; paymentTermId: string },
  ): Promise<void> => {
    setSaving(true);
    clearModalError();
    try {
      const created = await businessPartnerFacade.createBusinessPartner(body);
      await businessPartnerFacade.assignRole(created.id, {
        roleType: RoleTypeEnum.Supplier,
        supplierConfig: supplierConfig
          ? {
              paymentTermId: supplierConfig.paymentTermId,
              refundProviderTypeCode: supplierConfig.refundProviderTypeCode,
            }
          : undefined,
      });
      setModalOpen(false);
      listState.refetch();
    } finally {
      setSaving(false);
    }
  };

  const assignAsSupplier = async (id: string): Promise<void> => {
    setSaving(true);
    clearModalError();
    try {
      await businessPartnerFacade.assignRole(id, {
        roleType: RoleTypeEnum.Supplier,
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

  const updateSupplier = async (
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

  // ── Supplier config (SRI defaults) ────────────────────────────────────────

  const openSupplierConfig = async (bp: BusinessPartnerSummaryDto) => {
    clearModalError();
    try {
      const roles = await businessPartnerFacade.getRoles(bp.id, true);
      const supplierRole = roles.find((r) => r.roleType === "Supplier");
      if (supplierRole) setSupplierConfigBp({ bp, roleId: supplierRole.id });
    } catch {
      /* no action */
    }
  };

  const closeSupplierConfig = useCallback(() => {
    setSupplierConfigBp(null);
    clearModalError();
  }, []);
  const closeSupplierClassification = useCallback(() => {
    setSupplierClassificationBp(null);
    clearModalError();
  }, []);

  const saveSupplierConfig = async (
    bpId: string,
    roleId: string,
    config: SupplierConfigBody,
  ): Promise<boolean> => {
    setSaving(true);
    clearModalError();
    try {
      await businessPartnerFacade.updateSupplierConfig(bpId, roleId, config);
      setSupplierConfigBp(null);
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

  const openSupplierClassification = async (bp: BusinessPartnerSummaryDto) => {
    clearModalError();
    try {
      const roles = await businessPartnerFacade.getRoles(bp.id, true);
      const supplierRole = roles.find((r) => r.roleType === "Supplier");
      if (supplierRole)
        setSupplierClassificationBp({ bp, roleId: supplierRole.id });
    } catch {
      /* no action */
    }
  };

  const saveSupplierClassification = async (
    bpId: string,
    roleId: string,
    config: SupplierClassificationBody,
  ): Promise<boolean> => {
    setSaving(true);
    clearModalError();
    try {
      await businessPartnerFacade.updateSupplierClassification(
        bpId,
        roleId,
        config,
      );
      setSupplierClassificationBp(null);
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

  // ── Inline actions ─────────────────────────────────────────────────────────

  // CRITICAL-CONFIRMATIONS-BUSINESS-PARTNERS-04: antes solo capturaba el error en
  // `inlineError` sin relanzarlo — el `await` del caller (handleDisable/handleActivate en
  // MasterDataSuppliersPage.tsx) nunca veía el fallo y mostraba éxito igual. Ahora relanza para
  // que el caller decida el mensaje (message.success solo si esto no lanza).
  const disableSupplier = async (id: string) => {
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
      throw err;
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
      setInlineError(
        formatApiRequestError(err, {
          generic: "Error al procesar la operación.",
        }),
      );
      throw err;
    } finally {
      setSaving(false);
    }
  };

  const addAsCustomer = async (id: string) => {
    setSaving(true);
    setInlineError(null);
    try {
      await businessPartnerFacade.assignRole(id, {
        roleType: RoleTypeEnum.Customer,
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

  const blockSupplier = async (id: string, reason: string) => {
    setSaving(true);
    clearModalError();
    try {
      await businessPartnerFacade.blockBusinessPartner(id, { reason });
      await openSettings({ ...settingsBp!, id });
      message.success("Cliente/proveedor bloqueado correctamente.");
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

  const unblockSupplier = async (id: string) => {
    setSaving(true);
    clearModalError();
    try {
      await businessPartnerFacade.unblockBusinessPartner(id);
      await openSettings({ ...settingsBp!, id });
      message.success("Cliente/proveedor desbloqueado correctamente.");
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
    suppliers,
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
    createSupplier,
    updateSupplier,
    assignAsSupplier,
    disableSupplier,
    activateSupplier,
    addAsCustomer,
    supplierConfigBp,
    openSupplierConfig,
    closeSupplierConfig,
    saveSupplierConfig,
    supplierClassificationBp,
    openSupplierClassification,
    closeSupplierClassification,
    saveSupplierClassification,
    settingsBp,
    settingsData,
    openSettings,
    closeSettings,
    saveSettings,
    blockSupplier,
    unblockSupplier,
    saving,
    refetch: listState.refetch,
  };
}
