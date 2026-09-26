// ZH-SUPPLIER-SEARCH-REUSABLE-01 / ZH-SUPPLIER-SEARCH-SINGLE-SOURCE-02 — ÚNICA lógica remota de
// búsqueda de proveedores del frontend (consumida solo por `SupplierSearchSelect`).
// GET /master/business-partners?roles=Supplier — el backend hace ILIKE sobre razón social,
// nombre comercial y RUC. No crear helpers/endpoints paralelos.
import { businessPartnerFacade } from "../api/businessPartnerFacade";
import {
  mapBusinessPartnerDetailToSupplierPickerRow,
  mapBusinessPartnerToSupplierPickerRow,
} from "../adapters/businessPartnerCustomerAdapter";
import {
  RoleTypeEnum,
  type SupplierPickerRow,
} from "../types/businessPartner.types";

export const SUPPLIER_SEARCH_MAX_RESULTS = 30;

export interface SupplierSearchFilters {
  /** Solo proveedores con BP activo (p.ej. registrar un gasto nuevo). Default: todos. */
  activeOnly?: boolean;
}

export async function searchSuppliers(
  query: string,
  filters: SupplierSearchFilters = {},
  signal?: AbortSignal,
): Promise<SupplierPickerRow[]> {
  const rows = await businessPartnerFacade.searchBusinessPartners(
    {
      q: query,
      roles: [RoleTypeEnum.Supplier],
      take: SUPPLIER_SEARCH_MAX_RESULTS,
      ...(filters.activeOnly ? { isActive: true } : {}),
    },
    signal,
  );
  return rows.map(mapBusinessPartnerToSupplierPickerRow);
}

/** Hidrata la selección inicial cuando el consumidor solo tiene el id (formularios RHF). */
export async function getSupplierPickerRow(id: string): Promise<SupplierPickerRow> {
  return mapBusinessPartnerDetailToSupplierPickerRow(
    await businessPartnerFacade.getBusinessPartner(id),
  );
}
