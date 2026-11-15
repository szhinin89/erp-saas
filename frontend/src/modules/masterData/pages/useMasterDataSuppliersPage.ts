import { useState } from "react";
import { useCompanyScopedAsync } from "../../../hooks/useCompanyScopedAsync";
import { useDebounce } from "../../../hooks/useDebounce";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { businessPartnerFacade } from "../api/businessPartnerFacade";
import type {
  BusinessPartnerStatusFilter,
  CreateBusinessPartnerBody,
} from "../types/businessPartner.types";
import { RoleTypeEnum } from "../types/businessPartner.types";
import { formatApiRequestError } from "../../lib/apiError";

function toIsActiveParam(status: BusinessPartnerStatusFilter): boolean | undefined {
  if (status === "active") return true;
  if (status === "inactive") return false;
  return undefined;
}

/**
 * Listado de Proveedores. La edición, la configuración SRI, las retenciones y las subcategorías viven
 * en la ficha unificada (`/masterdata/business-partners/:id`); aquí solo quedan lista, alta, asignación
 * de rol y activar/desactivar.
 */
export function useMasterDataSuppliersPage() {
  const { canShow } = usePermissionsUi();
  const canView = canShow("masterdata.businesspartners.view");
  const canCreate = canShow("masterdata.businesspartners.create");
  const canUpdate = canShow("masterdata.businesspartners.update");
  const canDisable = canShow("masterdata.businesspartners.disable");

  const [search, setSearch] = useState("");
  const debouncedSearch = useDebounce(search, 300);
  const [statusFilter, setStatusFilter] =
    useState<BusinessPartnerStatusFilter>("active");
  const [page, setPage] = useState(1);
  const PAGE_SIZE = 50;

  const [saving, setSaving] = useState(false);
  const [inlineError, setInlineError] = useState<string | null>(null);

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

  const setSearchReset = (v: string) => {
    setSearch(v);
    setPage(1);
  };
  const setStatusFilterReset = (v: BusinessPartnerStatusFilter) => {
    setStatusFilter(v);
    setPage(1);
  };

  // ── Create ─────────────────────────────────────────────────────────────────

  const createSupplier = async (
    body: CreateBusinessPartnerBody,
    supplierConfig?: { refundProviderTypeCode: string },
  ): Promise<void> => {
    setSaving(true);
    try {
      const created = await businessPartnerFacade.createBusinessPartner(body);
      await businessPartnerFacade.assignRole(created.id, {
        roleType: RoleTypeEnum.Supplier,
        supplierConfig: supplierConfig
          ? {
              refundProviderTypeCode: supplierConfig.refundProviderTypeCode,
            }
          : undefined,
      });
      listState.refetch();
    } finally {
      setSaving(false);
    }
  };

  const assignAsSupplier = async (id: string): Promise<void> => {
    setSaving(true);
    try {
      await businessPartnerFacade.assignRole(id, {
        roleType: RoleTypeEnum.Supplier,
      });
      listState.refetch();
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

  return {
    canView,
    canCreate,
    canUpdate,
    canDisable,
    search,
    setSearch: setSearchReset,
    statusFilter,
    setStatusFilter: setStatusFilterReset,
    page,
    setPage,
    pageSize: PAGE_SIZE,
    totalCount,
    totalPages,
    suppliers,
    loading: listState.loading,
    listError: listState.error,
    inlineError,
    createSupplier,
    assignAsSupplier,
    disableSupplier,
    activateSupplier,
    addAsCustomer,
    saving,
    refetch: listState.refetch,
  };
}
