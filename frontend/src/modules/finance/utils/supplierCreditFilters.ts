import type { SupplierCreditListFilters, SupplierCreditSourceType } from "../api/supplierCreditService";

/** ZH-SUPPLIER-BALANCES-UX-02D-E — estado de la barra de filtros de "Saldos a favor de proveedores". */
export type SupplierCreditStatusFilter = "open" | "closed" | "all";

export interface SupplierCreditFiltersValue {
  supplierId: string | null;
  sourceType: SupplierCreditSourceType | "";
  status: SupplierCreditStatusFilter;
}

/** Default de la pantalla: solo saldos abiertos (con dinero disponible). */
export const DEFAULT_SUPPLIER_CREDIT_FILTERS: SupplierCreditFiltersValue = {
  supplierId: null,
  sourceType: "",
  status: "open",
};

/** Filtros server-side (02D-D) → parámetros del listado. */
export function toSupplierCreditListFilters(
  value: SupplierCreditFiltersValue,
): SupplierCreditListFilters {
  return {
    supplierId: value.supplierId,
    sourceType: value.sourceType || null,
    isOpen: value.status === "all" ? null : value.status === "open",
  };
}
